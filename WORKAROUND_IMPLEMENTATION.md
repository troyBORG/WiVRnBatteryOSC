# Battery Workaround Implementation

## Approach: File-Based IPC

Since we can't access libmonado from inside Steam's sandbox, we'll create a workaround:

1. **External C++ service** (runs outside sandbox) queries WiVRn/Monado
2. **Writes battery to file**: `/tmp/wivrn-battery.json`
3. **C# mod reads file** (reading `/tmp` is usually allowed in sandbox)

## Implementation Steps

### Step 1: Create C++ Battery Service

**File**: `tools/wivrn-battery-service/main.cpp`

This service will:
- Link against Monado's `libxrt-device`
- Find WiVRn HMD via Monado's device enumeration
- Call `get_battery_status()` 
- Write JSON to `/tmp/wivrn-battery.json` every second

### Step 2: Update C# Mod

**Add file reader** to `WiVRnBatteryOSC.cs`:
- Check for `/tmp/wivrn-battery.json`
- Parse JSON: `{"charge": 0.85, "charging": false, "present": true}`
- Use this data if available, fallback to renderer query

### Step 3: Build & Deploy

- Build C++ service as part of WiVRn or standalone
- User runs service before starting game
- Mod automatically picks it up

## Alternative: Query wlx-overlay-s

If wlx-overlay-s exposes battery via API, we could query it instead of creating a new service.

## Status

**Ready to implement** - This will bypass xrizer entirely and work immediately!

