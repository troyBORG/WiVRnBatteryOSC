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
- ✅ Automatic background polling (every 2 seconds)
- ✅ No external dependencies
- ✅ Harmony patch to ensure battery data flows through
- ✅ Simple installation - just drop the DLL in rml_mods

## Building

```bash
cd WiVRnBatteryOSC
dotnet build
```

The DLL will be automatically copied to your Resonite `rml_mods` folder if `CopyToMods` is enabled in the project file.

## Version History

### v0.1.0
- Direct battery query from OpenXR/VR_Manager
- Removed OSC dependency
- No external tools required
- Automatic background polling

### v0.0.1
- Initial release with OSC support (deprecated)

## Credits

- [ResoniteModLoader](https://github.com/resonite-modding-group/ResoniteModLoader) - Mod framework
- [WiVRn](https://github.com/meumeu/WiVRn) - OpenXR runtime for Quest headsets
- [HarmonyLib](https://github.com/pardeike/Harmony) - Runtime patching
