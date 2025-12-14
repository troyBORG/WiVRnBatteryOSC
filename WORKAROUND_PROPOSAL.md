# Battery Workaround Proposal

## The Problem

- WiVRn correctly forwards battery to server ✅
- xrizer doesn't forward battery to games ❌
- Mod can't access libmonado from inside Steam's sandbox ❌

## The Solution: External Battery Service

Create a small C++ service that:
1. Runs **outside** Steam's sandbox
2. Links against Monado/libmonado directly
3. Queries WiVRn's `xrt_device::get_battery_status()` 
4. Exposes battery via simple IPC (Unix socket or HTTP)
5. C# mod queries this service

This bypasses xrizer entirely!

## Implementation Plan

### 1. C++ Battery Service (`wivrn-battery-service`)

**Location**: `tools/wivrn-battery-service/`

**Features**:
- Links against Monado's `libxrt-device` 
- Finds WiVRn's HMD device via Monado
- Calls `get_battery_status()` directly
- Exposes via Unix socket: `/tmp/wivrn-battery.sock`
- Simple JSON protocol: `{"charge": 0.85, "charging": false, "present": true}`

**Dependencies**:
- Monado development libraries
- WiVRn server running

### 2. C# Mod Updates

**Changes**:
- Add HTTP client or Unix socket client
- Query `http://localhost:8765/battery` or Unix socket
- Fallback to renderer query if service unavailable
- Auto-start service if not running (optional)

**Benefits**:
- Works even with xrizer limitation
- No sandbox restrictions
- Simple, maintainable solution

## Alternative: File-Based IPC

If Unix sockets are problematic:
- Service writes to `/tmp/wivrn-battery.json` every second
- Mod reads file (no sandbox restrictions on reading `/tmp`)
- Even simpler, no network code needed

## Status

**Proposed** - Ready to implement if you want this workaround!

