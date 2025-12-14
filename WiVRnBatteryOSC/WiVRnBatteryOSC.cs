using FrooxEngine;
using HarmonyLib;
using ResoniteModLoader;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Reflection;
using Renderite.Shared;
using System.Runtime.InteropServices;

namespace WiVRnBatteryOSC;

// Mod to query battery status directly from OpenXR/WiVRn and update Resonite's battery display
// This bypasses the need for external tools like Edrakon or bash scripts
public class WiVRnBatteryOSC : ResoniteMod
{
    internal const string VERSION_CONSTANT = "0.1.0";
    public override string Name => "WiVRn Battery Direct";
    public override string Author => "troyBORG";
    public override string Version => VERSION_CONSTANT;
    public override string Link => "https://github.com/troyBORG/WiVRnBatteryOSC";

    private Task? batteryQueryTask;
    private CancellationTokenSource? cancellationTokenSource;
    private float lastBatteryLevel = -1f;
    private bool lastBatteryCharging = false;
    private bool batteryPresent = false;
    
    // Static instance for Harmony patch to access
    private static WiVRnBatteryOSC? instance;

    public override void OnEngineInit()
    {
        instance = this;
        Msg("WiVRn Battery Direct Mod initialized");
        Msg("Querying battery directly from OpenXR/WiVRn...");
        
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
            batteryQueryTask = Task.Run(() => QueryBatteryLoop(cancellationTokenSource.Token));
            Msg("Started battery query task");
        }
        catch (Exception ex)
        {
            Error($"Failed to start battery query: {ex.Message}");
        }
    }

    private void QueryBatteryLoop(CancellationToken cancellationToken)
    {
        // Query battery every 2 seconds
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // Try to get battery from VR_Manager's headset state
                // This queries the renderer which should call OpenXR
                QueryBatteryFromRenderer();
                
                // Wait 2 seconds before next query
                Thread.Sleep(2000);
            }
            catch (Exception ex)
            {
                Warn($"Error querying battery: {ex.Message}");
                Thread.Sleep(2000);
            }
        }
    }

    private void QueryBatteryFromRenderer()
    {
        try
        {
            // Get VR_Manager instance using reflection
            var vrManagerType = typeof(VR_Manager);
            var instanceField = vrManagerType.GetField("_instance", 
                BindingFlags.NonPublic | BindingFlags.Static);
            
            if (instanceField == null)
            {
                // Try alternative field names
                instanceField = vrManagerType.GetField("Instance", 
                    BindingFlags.Public | BindingFlags.Static);
            }
            
            if (instanceField == null) return;
            
            var vrManager = instanceField.GetValue(null) as VR_Manager;
            if (vrManager == null) return;
            
            // Get headset using reflection
            var headsetField = vrManagerType.GetField("_headset", 
                BindingFlags.NonPublic | BindingFlags.Instance);
            
            if (headsetField == null) return;
            
            var headset = headsetField.GetValue(vrManager) as GeneralHeadset;
            if (headset == null) return;
            
            // Try to get battery level from headset
            // The headset should have queried it from the renderer
            var batteryLevel = headset.BatteryLevel;
            var batteryCharging = headset.BatteryCharging;
            
            if (batteryLevel != null)
            {
                float level = batteryLevel.Value;
                bool charging = batteryCharging?.Held ?? false;
                
                // Only update if we got valid data (0.0 to 1.0)
                if (level >= 0f && level <= 1f)
                {
                    lastBatteryLevel = level;
                    lastBatteryCharging = charging;
                    batteryPresent = true;
                    
                    // Log occasionally (every 10 queries = ~20 seconds)
                    if (DateTime.Now.Second % 20 == 0)
                    {
                        Msg($"Battery: {level * 100:F1}%, Charging: {charging}");
                    }
                }
                else
                {
                    // Invalid data - battery not available
                    batteryPresent = false;
                }
            }
        }
        catch (Exception ex)
        {
            // Silently handle errors - battery might not be available yet
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

    // Harmony patch to ensure battery is updated correctly
    // This patches HandleHeadsetState to ensure battery data flows through
    [HarmonyPatch(typeof(VR_Manager), "HandleHeadsetState")]
    class VR_Manager_HandleHeadsetState_Patch
    {
        static void Postfix(VR_Manager __instance, HeadsetState state, float deltaTime)
        {
            // Get mod instance
            if (instance == null) return;

            var batteryState = instance.GetBatteryState();
            
            // Only update if we have valid battery data
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
