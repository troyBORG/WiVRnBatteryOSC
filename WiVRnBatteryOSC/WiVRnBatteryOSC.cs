using FrooxEngine;
using HarmonyLib;
using ResoniteModLoader;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Reflection;
using Renderite.Shared;

namespace WiVRnBatteryOSC;

// Mod to query battery status directly from OpenXR/WiVRn and update Resonite's battery display
public class WiVRnBatteryOSC : ResoniteMod
{
    internal const string VERSION_CONSTANT = "0.1.2";
    public override string Name => "WiVRn Battery Direct";
    public override string Author => "troyBORG";
    public override string Version => VERSION_CONSTANT;
    public override string Link => "https://github.com/troyBORG/WiVRnBatteryOSC";

    // Configuration
    [AutoRegisterConfigKey]
    public static ModConfigurationKey<bool> ShowDebugInfo = new("ShowDebugInfo", "Show debug information in logs", () => true);
    
    [AutoRegisterConfigKey]
    public static ModConfigurationKey<float> UpdateInterval = new("UpdateInterval", "Battery query interval in seconds", () => 2.0f);
    
    [AutoRegisterConfigKey]
    public static ModConfigurationKey<bool> ForceUpdate = new("ForceUpdate", "Force update battery values even if renderer provides them", () => false);

    private Task? batteryQueryTask;
    private CancellationTokenSource? cancellationTokenSource;
    private float lastBatteryLevel = -1f;
    private bool lastBatteryCharging = false;
    private bool batteryPresent = false;
    private int queryCount = 0;
    private string lastStatusMessage = "Initializing...";
    private string lastError = "";
    private DateTime lastUpdateTime = DateTime.MinValue;
    
    // Static instance for Harmony patch to access
    private static WiVRnBatteryOSC? instance;

    public override void OnEngineInit()
    {
        instance = this;
        Msg("WiVRn Battery Direct Mod initialized");
        Msg($"Version: {VERSION_CONSTANT}");
        Msg("Querying battery directly from OpenXR/WiVRn...");
        
        // Load configuration
        var config = GetConfiguration();
        Msg($"Configuration - ShowDebugInfo: {config.GetValue(ShowDebugInfo)}, UpdateInterval: {config.GetValue(UpdateInterval)}s");
        
        // Start battery query task
        StartBatteryQuery();
        
        // Patch VR_Manager to inject battery updates
        Harmony harmony = new("com.wivrn.BatteryDirect");
        harmony.PatchAll();
    }


    private void StartBatteryQuery()
    {
        try
        {
            cancellationTokenSource = new CancellationTokenSource();
            var interval = (int)(GetConfiguration().GetValue(UpdateInterval) * 1000);
            batteryQueryTask = Task.Run(() => QueryBatteryLoop(cancellationTokenSource.Token, interval));
            Msg("Started battery query task");
        }
        catch (Exception ex)
        {
            Error($"Failed to start battery query: {ex.Message}");
        }
    }

    private void QueryBatteryLoop(CancellationToken cancellationToken, int intervalMs)
    {
        // Wait a bit for VR_Manager to initialize
        Thread.Sleep(5000);
        
        // Query battery at specified interval
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                queryCount++;
                QueryBatteryFromRenderer();
                
                // Wait before next query
                Thread.Sleep(intervalMs);
            }
            catch (Exception ex)
            {
                lastError = ex.Message;
                if (GetConfiguration().GetValue(ShowDebugInfo))
                {
                    Warn($"Error querying battery (query #{queryCount}): {ex.Message}");
                }
                Thread.Sleep(intervalMs);
            }
        }
    }

    private void QueryBatteryFromRenderer()
    {
        try
        {
            // Get Engine.Current to access InputInterface
            var engineType = typeof(Engine);
            var currentProperty = engineType.GetProperty("Current", 
                BindingFlags.Public | BindingFlags.Static);
            
            if (currentProperty == null)
            {
                lastStatusMessage = "Engine.Current property not found";
                if (GetConfiguration().GetValue(ShowDebugInfo) && queryCount % 10 == 0)
                    Warn(lastStatusMessage);
                return;
            }
            
            var engine = currentProperty.GetValue(null) as Engine;
            if (engine == null)
            {
                lastStatusMessage = "Engine.Current is null";
                if (GetConfiguration().GetValue(ShowDebugInfo) && queryCount % 10 == 0)
                    Warn(lastStatusMessage);
                return;
            }
            
            // Get InputInterface from Engine
            var inputInterface = engine.InputInterface;
            if (inputInterface == null)
            {
                lastStatusMessage = "InputInterface is null";
                if (GetConfiguration().GetValue(ShowDebugInfo) && queryCount % 10 == 0)
                    Warn(lastStatusMessage);
                return;
            }
            
            // Get VR_Manager from InputInterface using reflection
            var inputInterfaceType = typeof(InputInterface);
            var vrManagerField = inputInterfaceType.GetField("_vrManager", 
                BindingFlags.NonPublic | BindingFlags.Instance);
            
            if (vrManagerField == null)
            {
                lastStatusMessage = "InputInterface._vrManager field not found";
                if (GetConfiguration().GetValue(ShowDebugInfo) && queryCount % 10 == 0)
                    Warn(lastStatusMessage);
                return;
            }
            
            var vrManager = vrManagerField.GetValue(inputInterface) as VR_Manager;
            if (vrManager == null)
            {
                lastStatusMessage = "VR_Manager is null";
                if (GetConfiguration().GetValue(ShowDebugInfo) && queryCount % 10 == 0)
                    Warn(lastStatusMessage);
                return;
            }
            
            // Get headset using reflection
            var vrManagerType = typeof(VR_Manager);
            var headsetField = vrManagerType.GetField("_headset", 
                BindingFlags.NonPublic | BindingFlags.Instance);
            
            if (headsetField == null)
            {
                lastStatusMessage = "_headset field not found";
                if (GetConfiguration().GetValue(ShowDebugInfo) && queryCount % 10 == 0)
                    Warn(lastStatusMessage);
                return;
            }
            
            var headset = headsetField.GetValue(vrManager) as GeneralHeadset;
            if (headset == null)
            {
                lastStatusMessage = "Headset is null";
                if (GetConfiguration().GetValue(ShowDebugInfo) && queryCount % 10 == 0)
                    Warn(lastStatusMessage);
                return;
            }
            
            // Try to get battery level from headset
            var batteryLevel = headset.BatteryLevel;
            var batteryCharging = headset.BatteryCharging;
            
            if (batteryLevel == null)
            {
                lastStatusMessage = "BatteryLevel is null";
                if (GetConfiguration().GetValue(ShowDebugInfo) && queryCount % 10 == 0)
                    Warn(lastStatusMessage);
                return;
            }
            
            float level = batteryLevel.Value;
            bool charging = batteryCharging?.Held ?? false;
            
            // Log what we're getting
            if (GetConfiguration().GetValue(ShowDebugInfo) && queryCount % 5 == 0)
            {
                Msg($"Query #{queryCount}: BatteryLevel={level}, Charging={charging}, BatteryLevel type={batteryLevel.GetType().Name}");
            }
            
            // Check if we got valid data
            if (level >= 0f && level <= 1f)
            {
                lastBatteryLevel = level;
                lastBatteryCharging = charging;
                batteryPresent = true;
                lastUpdateTime = DateTime.Now;
                lastError = "";
                lastStatusMessage = $"Battery: {level * 100:F1}%, Charging: {charging}";
                
                if (GetConfiguration().GetValue(ShowDebugInfo) && queryCount % 10 == 0)
                {
                    Msg($"✓ {lastStatusMessage}");
                }
            }
            else
            {
                // Invalid data - battery not available
                batteryPresent = false;
                lastStatusMessage = $"Invalid battery level: {level} (expected 0.0-1.0)";
                if (GetConfiguration().GetValue(ShowDebugInfo) && queryCount % 10 == 0)
                {
                    Warn($"✗ {lastStatusMessage}");
                }
            }
            
            // Try to force update by calling UpdateValue directly
            // This might help if the renderer isn't updating it
            var config = GetConfiguration();
            if ((batteryPresent && config.GetValue(ForceUpdate)) || (batteryPresent && queryCount % 5 == 0))
            {
                try
                {
                    batteryLevel.UpdateValue(lastBatteryLevel, 0.016f); // ~60fps delta
                    batteryCharging?.UpdateState(lastBatteryCharging);
                }
                catch (Exception ex)
                {
                    if (config.GetValue(ShowDebugInfo) && queryCount % 10 == 0)
                        Warn($"Failed to force update: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            lastError = ex.Message;
            lastStatusMessage = $"Exception: {ex.Message}";
            if (GetConfiguration().GetValue(ShowDebugInfo) && queryCount % 10 == 0)
                Warn($"Exception in QueryBatteryFromRenderer: {ex.Message}\n{ex.StackTrace}");
            batteryPresent = false;
        }
    }

    // Public method to get current battery state (called by Harmony patch)
    public (float level, bool charging, bool present) GetBatteryState()
    {
        return (lastBatteryLevel, lastBatteryCharging, batteryPresent);
    }

    public void Shutdown()
    {
        cancellationTokenSource?.Cancel();
        batteryQueryTask?.Wait(1000);
        Msg("WiVRn Battery Direct Mod shutdown");
    }

    // Harmony patch to log what the renderer is providing and potentially inject battery data
    // This patches HandleHeadsetState to see what battery data comes from the renderer
    [HarmonyPatch(typeof(VR_Manager), "HandleHeadsetState")]
    class VR_Manager_HandleHeadsetState_Patch
    {
        static void Postfix(VR_Manager __instance, HeadsetState state, float deltaTime)
        {
            // Get mod instance
            if (instance == null) return;

            // Log what the renderer is providing (every 20 calls to avoid spam)
            // Note: The renderer is providing -1, which means WiVRn isn't sending battery data
            // This is logged in the main query loop, so we don't need to log here
            
            // The renderer is providing -1, which means WiVRn isn't sending battery data
            // We can't inject fake data, but we can log that we see the issue
            // The real fix needs to be in WiVRn server or the renderer querying battery
            
            var batteryState = instance.GetBatteryState();
            
            // Only update if we have valid battery data from our query
            // But since we're also getting -1, this won't help until WiVRn provides data
            if (batteryState.present && batteryState.level >= 0f && batteryState.level <= 1f)
            {
                try
                {
                    // Use reflection to access private _headset field
                    var headsetField = typeof(VR_Manager).GetField("_headset", 
                        BindingFlags.NonPublic | BindingFlags.Instance);
                    
                    if (headsetField != null)
                    {
                        var headset = headsetField.GetValue(__instance) as GeneralHeadset;
                        if (headset != null)
                        {
                            // Update battery values
                            headset.BatteryLevel.UpdateValue(batteryState.level, deltaTime);
                            headset.BatteryCharging.UpdateState(batteryState.charging);
                        }
                    }
                }
                catch
                {
                    // Silently handle errors
                }
            }
        }
    }
}
