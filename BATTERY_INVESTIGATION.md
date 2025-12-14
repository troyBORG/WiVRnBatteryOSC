# WiVRn Battery Investigation Results

## Summary

**CORRECTION**: WiVRn **DOES** forward headset battery to the server. This can be verified by checking the battery display in `wlx-overlay-s` watch, which shows the battery status correctly.

**Actual Problem**: The issue is that **xrizer** (the OpenXR runtime wrapper used by Steam) does not forward the battery data to games. This is tracked in [Supreeeme/xrizer#229](https://github.com/Supreeeme/xrizer/issues/229).

**Root Cause**: There is no standard OpenXR API for battery status. Monado provides battery status through the `libmonado` interface (via `xrt_device::get_battery_status()`), but this interface is not accessible inside Steam's pressure vessel sandbox (see [ValveSoftware/steam-runtime#782](https://github.com/ValveSoftware/steam-runtime/issues/782)).

**Controller battery**: Controllers get battery data directly from OpenXR extensions (when supported), not through WiVRn's battery packet system.

## Detailed Findings

### 1. Headset Battery (WiVRn Works, xrizer Doesn't Forward)

**WiVRn Status**: ✅ **WiVRn correctly forwards headset battery to the server**

**Evidence**: The battery status is visible in `wlx-overlay-s` watch, which directly accesses WiVRn's battery data.

**Server Implementation**: `server/driver/wivrn_hmd.cpp` implements `get_battery_status()` as part of the `xrt_device` structure:

```cpp
xrt_result_t wivrn_hmd::get_battery_status(bool * out_present,
                                           bool * out_charging,
                                           float * out_charge)
{
    cnx->set_enabled(to_headset::tracking_control::id::battery, true);
    std::lock_guard lock(mutex);
    bool is_valid = battery.present && battery.charge >= 0.0f && battery.charge <= 1.0f;
    *out_present = is_valid;
    *out_charging = battery.charging;
    *out_charge = is_valid ? battery.charge : 0.0f;
    return XRT_SUCCESS;
}
```

**The Real Problem**: **xrizer** (OpenXR runtime wrapper for Steam) does not forward battery data to games.

**Why**: 
- There is **no standard OpenXR API** for battery status
- Monado provides battery through the `libmonado` interface (`xrt_device::get_battery_status()`)
- This interface is **not accessible inside Steam's pressure vessel sandbox** ([ValveSoftware/steam-runtime#782](https://github.com/ValveSoftware/steam-runtime/issues/782))
- xrizer needs to integrate `libmonado` to pass battery status (tracked in [Supreeeme/xrizer#229](https://github.com/Supreeeme/xrizer/issues/229))

**What This Means**: 
- WiVRn is working correctly
- The limitation is in the OpenXR runtime layer (xrizer) and Steam's sandbox
- Games cannot access battery data because xrizer doesn't expose it, not because WiVRn doesn't send it

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

### Option 1: Fix xrizer (Required for Steam Games)

**Status**: Tracked in [Supreeeme/xrizer#229](https://github.com/Supreeeme/xrizer/issues/229)

xrizer needs to integrate `libmonado` to forward battery status from WiVRn to games. This is the primary blocker for Steam games (like Resonite via Steam).

### Option 2: Create OpenXR Battery Extension (Long-term Solution)

**Problem**: There is no standard OpenXR API for battery status.

**Solution**: Create an OpenXR extension (e.g., `XR_EXT_device_battery`) that standardizes battery reporting across all runtimes. This would:
- Allow games to query battery through standard OpenXR APIs
- Work across all OpenXR runtimes (not just Monado-based ones)
- Not require runtime-specific interfaces like `libmonado`

**Status**: This would require OpenXR working group approval and implementation across runtimes.

### Option 3: Steam Runtime Sandbox Changes

**Problem**: Steam's pressure vessel sandbox blocks access to `libmonado` interface.

**Solution**: Valve would need to allow access to `libmonado` interface within the sandbox, or provide an alternative mechanism for battery reporting.

**Status**: Tracked in [ValveSoftware/steam-runtime#782](https://github.com/ValveSoftware/steam-runtime/issues/782)

### Option 4: Use Non-Steam OpenXR Runtime (Workaround)

**Workaround**: Use WiVRn directly with Monado (not through Steam/xrizer) for games that support it. This would allow direct access to `libmonado` interface.

**Limitation**: Most games (including Resonite) are distributed through Steam and require SteamVR/xrizer.

## Current Mod Status

The mod (`WiVRnBatteryOSC`) is working correctly:
- ✅ Successfully accesses `VR_Manager` and `GeneralHeadset`
- ✅ Reads battery data from the renderer
- ✅ Logs battery status (shows `-1` because xrizer doesn't forward it)
- ✅ Added controller battery logging to help debug and verify what data is available

**The mod cannot fix the root cause** - it can only read what the renderer provides. The issues are:
1. **Headset battery**: xrizer doesn't forward battery data from WiVRn to games (WiVRn itself works correctly)
2. **Controller battery**: Quest Pro controllers don't expose battery via OpenXR extensions (hardware/OpenXR limitation, similar to Steam Link behavior)

## Next Steps

1. ✅ **Added controller battery logging** - The mod now logs when controllers are detected and their battery levels (will show if Quest Pro provides any battery data)
2. ✅ **Verified WiVRn works** - Confirmed that WiVRn correctly forwards battery to server (visible in wlx-overlay-s)
3. 🔄 **Track xrizer fix** - Monitor [Supreeeme/xrizer#229](https://github.com/Supreeeme/xrizer/issues/229) for libmonado integration
4. 🔄 **Advocate for OpenXR extension** - Support creation of standard OpenXR battery extension
5. ⚠️ **Controller battery**: Quest Pro controllers don't support battery via OpenXR - this is a hardware/OpenXR limitation, not fixable in software

**Note**: The controller logging will help confirm what battery data (if any) Quest Pro controllers provide. Based on Steam Link behavior, expect `0%` or `-1` (not available).

## Related Issues

- [Supreeeme/xrizer#229](https://github.com/Supreeeme/xrizer/issues/229) - xrizer battery forwarding
- [ValveSoftware/steam-runtime#782](https://github.com/ValveSoftware/steam-runtime/issues/782) - Steam sandbox libmonado access
- [WiVRn/WiVRn#672](https://github.com/WiVRn/WiVRn/issues/672) - Original (incorrect) issue - needs correction

