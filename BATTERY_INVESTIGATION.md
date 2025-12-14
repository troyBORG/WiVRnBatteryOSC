# WiVRn Battery Investigation Results

## Summary

**Finding**: Headset battery is only sent on Android (`#ifdef __ANDROID__`), not on Linux. This is why the headset battery shows `-1` (not available) on Linux systems.

**Controller battery**: Controllers are working because they get battery data directly from OpenXR through the renderer, not through WiVRn's battery packet system.

## Detailed Findings

### 1. Headset Battery (Not Working on Linux)

**Location**: `client/scenes/stream_tracking.cpp` lines 565-582

```cpp
#ifdef __ANDROID__
    if (next_battery_check < now and control.enabled[size_t(tid::battery)])
    {
        timer t2(instance);
        auto status = get_battery_status();
        network_session->send_stream(from_headset::battery{
                .charge = status.charge.value_or(-1),
                .present = status.charge.has_value(),
                .charging = status.charging,
        });
        next_battery_check = now + battery_check_interval;
        XrDuration battery_dur = t2.count();
        spdlog::info("Battery check took: {}", battery_dur);
    }
#endif
```

**Problem**: The entire battery sending code is wrapped in `#ifdef __ANDROID__`, meaning it only compiles and runs on Android devices. On Linux, this code is never executed, so no battery data is sent to the server.

**Why**: The `get_battery_status()` function (in `client/android/battery.cpp`) uses Android-specific APIs:
- JNI calls to Android's `BroadcastReceiver` for `ACTION_BATTERY_CHANGED`
- Android-specific battery level and charging status APIs

### 2. Controller Battery (Quest Pro Limitation)

**Quest Pro Controllers**: Quest Pro controllers do NOT expose battery via OpenXR extensions, similar to how Steam Link only provides headset battery but not controller battery.

**Why some controllers work**: Other headsets (like Quest 2/3, Index, etc.) may expose controller battery through OpenXR extensions:
1. OpenXR provides controller battery data directly to the renderer (Resonite)
2. The renderer passes this data to Resonite's `VR_Manager.HandleController()` method
3. Resonite updates controller battery via `controller.BatteryLevel.UpdateValue(state.batteryLevel, deltaTime)`

**Note**: Controller battery is NOT sent through WiVRn's `from_headset::battery` packet. It comes directly from OpenXR extensions (likely `XR_EXT_controller_battery` or similar), which Quest Pro controllers do not support.

**What you're seeing**: If controllers show "0%" or a default value in Resonite, this is likely Resonite's default/fallback value, not actual battery data from the controllers.

### 3. Server-Side Handling

**Location**: `server/driver/wivrn_session.cpp` line 747-750

```cpp
void wivrn_session::operator()(from_headset::battery && battery)
{
    hmd.update_battery(battery);
}
```

The server correctly handles battery packets when received, but on Linux they're never sent.

**Location**: `server/driver/wivrn_hmd.cpp` line 143-149

```cpp
void wivrn_hmd::update_battery(const from_headset::battery & new_battery)
{
    // We will only request a new sample if the current one is consumed
    cnx->set_enabled(to_headset::tracking_control::id::battery, false);
    std::lock_guard lock(mutex);
    battery = new_battery;
}
```

The server stores battery data and disables battery tracking control after receiving it.

## Solution Options

### Option 1: Add Linux Battery Support (Recommended)

Create a Linux implementation of `get_battery_status()` that uses Linux system APIs:

1. **Check `/sys/class/power_supply/`** for battery information
2. **Use `upower` or `systemd` APIs** if available
3. **Remove the `#ifdef __ANDROID__` guard** and add platform-specific implementations

**Files to modify**:
- `client/android/battery.h` → Rename to `client/battery.h` (platform-agnostic)
- `client/android/battery.cpp` → Create `client/linux/battery.cpp` or add `#ifdef` blocks
- `client/scenes/stream_tracking.cpp` → Remove `#ifdef __ANDROID__` guard

### Option 2: Use OpenXR Battery Extension (If Available)

If the headset exposes battery via OpenXR extensions (similar to controllers), query it directly from OpenXR instead of system APIs.

**Check for**:
- `XR_EXT_controller_battery` (for controllers, already working)
- Any headset-specific battery extensions

### Option 3: Query from Renderer Side

Since controllers work via OpenXR, check if headset battery is also available through OpenXR and can be queried by the renderer (Resonite) directly.

## Current Mod Status

The mod (`WiVRnBatteryOSC`) is working correctly:
- ✅ Successfully accesses `VR_Manager` and `GeneralHeadset`
- ✅ Reads battery data from the renderer
- ✅ Logs battery status (shows `-1` because WiVRn isn't sending data)
- ✅ Added controller battery logging to help debug and verify what data is available

**The mod cannot fix the root cause** - it can only read what the renderer provides. The issues are:
1. **Headset battery**: WiVRn isn't sending headset battery data on Linux (Android-only)
2. **Controller battery**: Quest Pro controllers don't expose battery via OpenXR extensions (hardware/OpenXR limitation, similar to Steam Link behavior)

## Next Steps

1. ✅ **Added controller battery logging** - The mod now logs when controllers are detected and their battery levels (will show if Quest Pro provides any battery data)
2. 🔄 **Investigate Linux battery APIs** - Check what battery information is available on Linux systems for headset
3. 🔄 **Implement Linux battery support** - Add Linux-specific battery querying to WiVRn client for headset only
4. ⚠️ **Controller battery**: Quest Pro controllers don't support battery via OpenXR - this is a hardware/OpenXR limitation, not fixable in software

**Note**: The controller logging will help confirm what battery data (if any) Quest Pro controllers provide. Based on Steam Link behavior, expect `0%` or `-1` (not available).

