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

namespace WiVRnBatteryOSC;

// Mod to receive battery status from Edrakon via OSC and update Resonite's battery display
// Based on: https://github.com/resonite-modding-group/ExampleMod
public class WiVRnBatteryOSC : ResoniteMod
{
    internal const string VERSION_CONSTANT = "1.0.0";
    public override string Name => "WiVRn Battery OSC";
    public override string Author => "WiVRn Community";
    public override string Version => VERSION_CONSTANT;
    public override string Link => "https://github.com/resonite-modding-group/ExampleMod/";

    private UdpClient? udpClient;
    private CancellationTokenSource? cancellationTokenSource;
    private Task? listenerTask;
    private int oscPort = 9015; // Default Edrakon port
    private float lastBatteryLevel = -1f;
    private bool lastBatteryCharging = false;
    
    // Static instance for Harmony patch to access
    private static WiVRnBatteryOSC? instance;

    public override void OnEngineInit()
    {
        instance = this;
        Msg("WiVRn Battery OSC Mod initialized");
        
        // Start OSC listener
        StartOSCListener();
        
        // Patch VR_Manager to inject battery updates
        Harmony harmony = new("com.wivrn.BatteryOSC");
        harmony.PatchAll();
    }

    private void StartOSCListener()
    {
        try
        {
            cancellationTokenSource = new CancellationTokenSource();
            udpClient = new UdpClient(oscPort);
            Msg($"Started OSC listener on port {oscPort}");

            listenerTask = Task.Run(() => ListenForOSC(cancellationTokenSource.Token));
        }
        catch (Exception ex)
        {
            Error($"Failed to start OSC listener: {ex.Message}");
        }
    }

    private void ListenForOSC(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                IPEndPoint? remoteEndPoint = null;
                byte[] data = udpClient!.Receive(ref remoteEndPoint);
                
                // Parse OSC message
                ParseOSCMessage(data);
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.Interrupted)
            {
                // Expected when shutting down
                break;
            }
            catch (Exception ex)
            {
                Warn($"Error receiving OSC message: {ex.Message}");
            }
        }
    }

    private void ParseOSCMessage(byte[] data)
    {
        if (data.Length < 4) return;

        try
        {
            // Check if it's an OSC bundle (starts with "#bundle")
            if (data.Length >= 8 && System.Text.Encoding.ASCII.GetString(data, 0, 7) == "#bundle")
            {
                // Parse bundle - skip bundle header and timetag
                int offset = 16; // "#bundle\0" (8) + timetag (8)
                while (offset < data.Length)
                {
                    if (offset + 4 > data.Length) break;
                    
                    // Read bundle element size (big-endian int32)
                    int elementSize = ReadBigEndianInt32(data, offset);
                    offset += 4;
                    
                    if (offset + elementSize > data.Length) break;
                    
                    // Parse element as OSC message
                    byte[] element = new byte[elementSize];
                    Array.Copy(data, offset, element, 0, elementSize);
                    ParseOSCMessage(element);
                    
                    offset += elementSize;
                }
                return;
            }

            // Simple OSC message parser
            // OSC format: address string (null-padded to 4 bytes), type tag string, data
            int msgOffset = 0;
            
            // Read address string
            string? address = ReadOSCString(data, ref msgOffset);
            if (address == null) return;

            // Read type tag string
            string? typeTag = ReadOSCString(data, ref msgOffset);
            if (typeTag == null || typeTag.Length < 2 || typeTag[0] != ',') return;

            // Check if it's a battery message
            if (address == "/tracking/battery/headset" && typeTag.Contains('f'))
            {
                // Read float value (big-endian)
                if (msgOffset + 4 <= data.Length)
                {
                    float batteryLevel = ReadBigEndianFloat(data, msgOffset);
                    lastBatteryLevel = batteryLevel;
                    Msg($"Received battery level: {batteryLevel * 100:F1}%");
                }
            }
            else if (address == "/tracking/battery/headset/charging" && typeTag.Contains('i'))
            {
                // Read int32 value (big-endian)
                if (msgOffset + 4 <= data.Length)
                {
                    int charging = ReadBigEndianInt32(data, msgOffset);
                    lastBatteryCharging = charging != 0;
                    Msg($"Received battery charging: {lastBatteryCharging}");
                }
            }
        }
        catch (Exception ex)
        {
            Warn($"Error parsing OSC message: {ex.Message}");
        }
    }

    private float ReadBigEndianFloat(byte[] data, int offset)
    {
        if (BitConverter.IsLittleEndian)
        {
            byte[] bytes = new byte[4];
            Array.Copy(data, offset, bytes, 0, 4);
            Array.Reverse(bytes);
            return BitConverter.ToSingle(bytes, 0);
        }
        else
        {
            return BitConverter.ToSingle(data, offset);
        }
    }

    private int ReadBigEndianInt32(byte[] data, int offset)
    {
        if (BitConverter.IsLittleEndian)
        {
            byte[] bytes = new byte[4];
            Array.Copy(data, offset, bytes, 0, 4);
            Array.Reverse(bytes);
            return BitConverter.ToInt32(bytes, 0);
        }
        else
        {
            return BitConverter.ToInt32(data, offset);
        }
    }

    private string? ReadOSCString(byte[] data, ref int offset)
    {
        if (offset >= data.Length) return null;
        
        int start = offset;
        while (offset < data.Length && data[offset] != 0)
            offset++;
        
        if (offset >= data.Length) return null;
        
        string str = System.Text.Encoding.UTF8.GetString(data, start, offset - start);
        offset = AlignTo4Bytes(offset + 1); // Skip null terminator and align
        return str;
    }

    private int AlignTo4Bytes(int offset)
    {
        return (offset + 3) & ~3;
    }

    // Public method to get current battery state (called by Harmony patch)
    public (float level, bool charging) GetBatteryState()
    {
        return (lastBatteryLevel, lastBatteryCharging);
    }

    public void Shutdown()
    {
        cancellationTokenSource?.Cancel();
        udpClient?.Close();
        listenerTask?.Wait(1000);
        Msg("WiVRn Battery OSC Mod shutdown");
    }

    // Harmony patch to inject battery updates into VR_Manager
    // Patches HandleHeadsetState to override battery values from OSC
    [HarmonyPatch(typeof(VR_Manager), "HandleHeadsetState")]
    class VR_Manager_HandleHeadsetState_Patch
    {
        static void Postfix(VR_Manager __instance, HeadsetState state, float deltaTime)
        {
            // Get mod instance from static field
            if (instance == null) return;

            var batteryState = instance.GetBatteryState();
            float level = batteryState.level;
            bool charging = batteryState.charging;
            
            // Only update if we have valid battery data from OSC
            if (level >= 0f && level <= 1f)
            {
                // Use reflection to access private _headset field
                var headsetField = typeof(VR_Manager).GetField("_headset", 
                    BindingFlags.NonPublic | BindingFlags.Instance);
                
                if (headsetField != null)
                {
                    var headset = headsetField.GetValue(__instance) as GeneralHeadset;
                    if (headset != null)
                    {
                        // Override battery values from OSC instead of renderer state
                        headset.BatteryLevel.UpdateValue(level, deltaTime);
                        headset.BatteryCharging.UpdateState(charging);
                    }
                }
            }
        }
    }
}

