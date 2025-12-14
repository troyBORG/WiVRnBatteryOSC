# [Feature Request]: Headset battery reporting in game

## Feature description

Be able to see your headset battery percentage in-game (like when you open the stats overlay). This would allow players to check their headset battery level without having to remove the headset or check the headset's UI.

## Reason for the feature

When playing in VR, I'd like to know what my headset battery level is without having to check the headset's UI. Many VR games and applications already have battery percentage and charging status available through OpenXR, but WiVRn currently doesn't send this data from the headset to the desktop/server, so games can't display it.

This would be especially useful for:
- Long play sessions where you want to monitor battery
- Knowing when to take a break to charge
- Avoiding unexpected shutdowns mid-game

## How games query battery data

Games like Resonite and Steam Link query headset battery through OpenXR's device interface:

1. **OpenXR application calls device's `get_battery_status()` function**: When a game wants to check battery (e.g., when opening a stats overlay), it calls the HMD device's `get_battery_status()` function pointer, which is part of the `xrt_device` structure.

2. **WiVRn server's `get_battery_status()` is called**: WiVRn's `wivrn_hmd::get_battery_status()` function (in `server/driver/wivrn_hmd.cpp`) is invoked. This function:
   - Requests battery data from the headset by calling `cnx->set_enabled(to_headset::tracking_control::id::battery, true)`
   - Returns the battery data it has stored: `out_present`, `out_charging`, and `out_charge` (0.0-1.0)

3. **The problem**: On Linux, the WiVRn client never sends battery data, so the server always has invalid data (`charge = -1`), which gets returned as `present = false` and `charge = 0.0`.

## Proposed implementation

Send the battery data from the headset back to the desktop/server so that games can access it through OpenXR. The headset already has access to battery information (battery percentage and charging status), and the WiVRn server already has infrastructure to receive and expose this data - it just needs to be sent from the client.

**Current state**:
- ✅ Server-side: `wivrn_hmd::get_battery_status()` correctly exposes battery through OpenXR's `xrt_device` interface
- ✅ Server-side: `wivrn_hmd::update_battery()` correctly receives and stores battery packets
- ✅ Android client: Battery data is sent from headset to server (in `client/scenes/stream_tracking.cpp`)
- ❌ Linux client: Battery sending code is wrapped in `#ifdef __ANDROID__`, so no data is sent

**What needs to be done**:
- Implement Linux battery querying (using `/sys/class/power_supply/` or `upower`) similar to Android's `get_battery_status()` in `client/android/battery.cpp`
- Remove or conditionally compile the `#ifdef __ANDROID__` guard in `client/scenes/stream_tracking.cpp` to allow Linux battery data to be sent

## Additional context

- Controller battery already works in many games because it comes directly from OpenXR extensions
- The server-side code already handles battery packets correctly when received
- This would benefit all Linux users of WiVRn, regardless of which headset they're using
