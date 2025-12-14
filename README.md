# WiVRn Battery Direct Mod

A [ResoniteModLoader](https://github.com/resonite-modding-group/ResoniteModLoader) mod that queries battery status directly from OpenXR/WiVRn and updates Resonite's battery display.

## Overview

Resonite queries battery from OpenXR, but OpenXR doesn't expose battery via a standard API. WiVRn implements battery via internal functions that may not be accessible to Resonite's renderer. This mod bypasses the normal OpenXR query path by:

1. Querying battery directly from Resonite's `VR_Manager`/renderer
2. Using Harmony to patch `HandleHeadsetState` to ensure battery data flows through
3. Updating Resonite's battery display automatically

## Installation

1. Install [ResoniteModLoader](https://github.com/resonite-modding-group/ResoniteModLoader)
2. Place `WiVRnBatteryOSC.dll` into your `rml_mods` folder:
   - Linux: `~/.local/share/Steam/steamapps/common/Resonite/rml_mods/`
   - Windows: `C:\Program Files (x86)\Steam\steamapps\common\Resonite\rml_mods\`
3. Start Resonite - battery will be queried automatically

## Requirements

- **WiVRn** OpenXR runtime
- **Resonite** with ResoniteModLoader
- **No additional tools required** - the mod handles everything

## How It Works

1. Mod queries battery from `VR_Manager`'s headset state (which comes from OpenXR renderer)
2. Background task polls battery every 2 seconds
3. Harmony patch ensures battery data flows through to the display
4. Battery level and charging status are updated automatically

## Features

- ✅ Direct battery query from OpenXR/VR_Manager
- ✅ **External service support** - Reads battery from `/tmp/wivrn-battery.json` (bypasses xrizer!)
- ✅ Automatic background polling (configurable interval, default 2 seconds)
- ✅ Automatic fallback - tries external service, then renderer query
- ✅ Controller battery logging (for debugging)
- ✅ Headset battery logging (for debugging)
- ✅ Configurable debug logging
- ✅ No external dependencies (for mod itself)
- ✅ Harmony patch to ensure battery data flows through
- ✅ Simple installation - just drop the DLL in rml_mods

## Configuration

The mod includes in-game configuration options (accessible via Resonite's mod settings):

- **ShowDebugInfo** (default: `true`) - Enable detailed battery logging
- **UpdateInterval** (default: `2.0` seconds) - How often to query battery status
- **ForceUpdate** (default: `false`) - Force update battery values even if renderer provides them
- **UseExternalService** (default: `true`) - Try to read battery from external service file (`/tmp/wivrn-battery.json`)

## Building

```bash
cd WiVRnBatteryOSC
dotnet build
```

The DLL will be automatically copied to your Resonite `rml_mods` folder if `CopyToMods` is enabled in the project file.

## Known Limitations

### Headset Battery (Steam/xrizer)
**Issue**: Headset battery shows `-1` (not available) in games running through Steam.

**Root Cause**: **WiVRn correctly forwards battery to the server** (visible in `wlx-overlay-s` watch), but **xrizer** (the OpenXR runtime wrapper for Steam) does not forward this data to games.

**Why**: There is no standard OpenXR API for battery status. Monado provides it through the `libmonado` interface (`xrt_device::get_battery_status()`), but this interface is not accessible inside Steam's pressure vessel sandbox.

**Status**: This is an xrizer/Steam limitation, not a WiVRn or mod limitation. The mod correctly reads what the renderer provides, but xrizer doesn't expose battery data to games.

**Solution**: 
- **Workaround Available**: Use the external battery service (see `EXTERNAL_SERVICE_README.md`) - this bypasses xrizer entirely!
- **Long-term**: Tracked in [Supreeeme/xrizer#229](https://github.com/Supreeeme/xrizer/issues/229) - requires xrizer to integrate `libmonado` to forward battery status. See `BATTERY_INVESTIGATION.md` for details.

### Controller Battery (Quest Pro)
**Issue**: Quest Pro controllers don't expose battery via OpenXR extensions.

**Root Cause**: Quest Pro controllers don't support the OpenXR battery extension (`XR_EXT_controller_battery`), similar to how Steam Link only provides headset battery but not controller battery.

**Status**: This is a hardware/OpenXR limitation, not fixable in software.

**Note**: The mod includes controller battery logging to help verify what data (if any) is available from your controllers.

## Version History

### v0.2.0
- **Added external service support**: Mod can now read battery from `/tmp/wivrn-battery.json`
- **Bypasses xrizer limitation**: External C++ service queries WiVRn/Monado directly
- **Automatic fallback**: Tries external service first, falls back to renderer query
- **New config option**: `UseExternalService` to enable/disable external service reading
- See `EXTERNAL_SERVICE_README.md` for details on the external service

### v0.1.2
- Added controller battery logging
- Added headset battery logging from renderer
- Improved debug output with raw battery values
- Added configuration options (ShowDebugInfo, UpdateInterval, ForceUpdate)
- Created `BATTERY_INVESTIGATION.md` documenting findings

### v0.1.0
- Direct battery query from OpenXR/VR_Manager
- Removed OSC dependency
- No external tools required
- Automatic background polling

### v0.0.1
- Initial release with OSC support (deprecated)

## Documentation

- **[BATTERY_INVESTIGATION.md](BATTERY_INVESTIGATION.md)** - Detailed investigation of battery data flow, Linux/Android differences, and Quest Pro limitations
- **[EXTERNAL_SERVICE_README.md](EXTERNAL_SERVICE_README.md)** - How to use the external battery service to bypass xrizer limitation
- **[WORKAROUND_PROPOSAL.md](WORKAROUND_PROPOSAL.md)** - Architecture and implementation details of the external service workaround

## Credits

- [ResoniteModLoader](https://github.com/resonite-modding-group/ResoniteModLoader) - Mod framework
- [WiVRn](https://github.com/meumeu/WiVRn) - OpenXR runtime for Quest headsets
- [HarmonyLib](https://github.com/pardeike/Harmony) - Runtime patching
