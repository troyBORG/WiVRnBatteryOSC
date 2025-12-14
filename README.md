# WiVRn Battery OSC Mod

A [ResoniteModLoader](https://github.com/resonite-modding-group/ResoniteModLoader) mod that receives battery status from Edrakon via OSC and updates Resonite's battery display.

## Purpose

Resonite queries battery from OpenXR, but OpenXR doesn't expose battery via a standard API. WiVRn implements battery via internal functions that may not be accessible to Resonite's renderer. This mod bypasses the normal OpenXR query path by:

1. Listening to OSC messages from Edrakon on port 9015
2. Receiving battery data via `/tracking/battery/headset` messages
3. Updating Resonite's `VR_Manager` battery state directly

## Installation

1. Install [ResoniteModLoader](https://github.com/resonite-modding-group/ResoniteModLoader).
2. Place `WiVRnBatteryOSC.dll` into your `rml_mods` folder:
   - Windows: `C:\Program Files (x86)\Steam\steamapps\common\Resonite\rml_mods`
   - Linux: `~/.local/share/Steam/steamapps/common/Resonite/rml_mods`
3. Start Resonite. Check logs to verify the mod loaded.

## Requirements

- **Edrakon** must be running and sending battery data via OSC
- OSC messages must be sent to `127.0.0.1:9015`
- Battery messages must use format:
  - `/tracking/battery/headset` - float (0.0 to 1.0)
  - `/tracking/battery/headset/charging` - int (1 = charging, 0 = not)

## How It Works

1. Mod starts OSC listener on port 9015
2. Receives OSC messages from Edrakon
3. Parses battery level and charging status
4. Uses Harmony to patch `VR_Manager.HandleHeadsetState()`
5. Overrides battery values from renderer state with OSC values

## Building

```bash
cd WiVRnBatteryOSC
dotnet build
```

The DLL will be automatically copied to your Resonite `rml_mods` folder if `CopyToMods` is enabled.

## Status

- ✅ OSC listener implemented
- ✅ Harmony patch to update battery state
- ⚠️ Requires Edrakon to actually send battery data (currently placeholder)

## Related

- [Edrakon](https://github.com/YourEdrakonRepo) - Sends battery data via OSC
- [WiVRn](https://github.com/meumeu/WiVRn) - OpenXR runtime for Quest headsets
