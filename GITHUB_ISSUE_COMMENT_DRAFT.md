# Comment to Post on WiVRn Issue #672

## Correction Comment

I need to correct the issue description - **WiVRn DOES forward headset battery to the server**. 

You can verify this by checking the battery display in `wlx-overlay-s` watch, which correctly shows the battery status from WiVRn.

## Actual Problem

The issue is that **xrizer** (the OpenXR runtime wrapper used by Steam) does not forward the battery data to games. This is tracked in [Supreeeme/xrizer#229](https://github.com/Supreeeme/xrizer/issues/229).

## Root Cause

1. **No standard OpenXR API**: There is no standard OpenXR API for battery status reporting.

2. **Monado's libmonado interface**: Monado provides battery status through the `libmonado` interface (via `xrt_device::get_battery_status()`), which WiVRn correctly implements in `server/driver/wivrn_hmd.cpp`.

3. **Steam sandbox limitation**: The `libmonado` interface is not accessible inside Steam's pressure vessel sandbox ([ValveSoftware/steam-runtime#782](https://github.com/ValveSoftware/steam-runtime/issues/782)).

4. **xrizer doesn't forward**: xrizer needs to integrate `libmonado` to pass battery status from WiVRn to games, but this hasn't been implemented yet.

## What This Means

- ✅ **WiVRn is working correctly** - it forwards battery to the server (visible in wlx-overlay-s)
- ✅ **Server exposes battery** - `wivrn_hmd::get_battery_status()` correctly implements the interface
- ❌ **xrizer doesn't forward** - games can't access battery because xrizer doesn't expose it
- ❌ **No OpenXR standard** - there's no standard way to query battery through OpenXR

## Solution

The fix needs to happen in **xrizer**, not WiVRn:
- Tracked in [Supreeeme/xrizer#229](https://github.com/Supreeeme/xrizer/issues/229)
- Requires xrizer to integrate `libmonado` to forward battery status
- May also require Steam runtime changes to allow `libmonado` access in sandbox

## Long-term Solution

Create a standard OpenXR extension (e.g., `XR_EXT_device_battery`) that would:
- Standardize battery reporting across all runtimes
- Work without runtime-specific interfaces
- Not require sandbox changes

## Recommendation

This issue should be **closed** or **updated** to reflect that WiVRn is working correctly, and the actual limitation is in xrizer/Steam runtime. The fix needs to happen upstream in xrizer.

