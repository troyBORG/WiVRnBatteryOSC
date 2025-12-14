# External Battery Service

## Overview

This is a **C++ service/script** that runs **outside Steam's sandbox** and queries WiVRn/Monado directly for battery status. The RML mod reads the data from a file.

## Architecture

```
┌─────────────────────────────────┐
│  C++ Battery Service            │
│  (runs outside Steam sandbox)    │
│  - Links against Monado         │
│  - Queries WiVRn HMD device     │
│  - Writes to /tmp/wivrn-        │
│    battery.json                 │
└──────────────┬──────────────────┘
               │ writes JSON file
               ▼
┌─────────────────────────────────┐
│  /tmp/wivrn-battery.json        │
│  {"charge": 0.85,               │
│   "charging": false,             │
│   "present": true}               │
└──────────────┬──────────────────┘
               │ reads file
               ▼
┌─────────────────────────────────┐
│  RML Mod (inside game)          │
│  - Reads /tmp/wivrn-battery.json│
│  - Updates Resonite battery     │
└─────────────────────────────────┘
```

## Implementation

The service needs to be built as part of WiVRn and link against:
- Monado's `libxrt-device`
- WiVRn's server libraries

It will:
1. Connect to Monado's IPC server
2. Enumerate devices to find WiVRn HMD
3. Call `get_battery_status()` on the HMD device
4. Write JSON to `/tmp/wivrn-battery.json` every second

## Usage

1. Build the service as part of WiVRn
2. Run the service before starting the game:
   ```bash
   wivrn-battery-service
   ```
3. Start your game - the mod will automatically read the file

## Status

**Ready to implement** - The mod already supports reading from this file!

