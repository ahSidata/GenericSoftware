# Noah Register Map & Function Documentation

Central documentation for all Noah functions, registers, and Modbus mappings for the EnergyAutomate Emulator.

---

## 📋 Table of Contents

1. [Overview](#overview)
2. [Register Ranges](#register-ranges)
3. [Time Segment Functions](#time-segment-functions)
4. [Power Functions](#power-functions)
5. [SoC Functions](#soc-functions)
6. [Input Registers (Sensor)](#input-registers-sensor)
7. [Holding Registers (Control)](#holding-registers-control)
8. [Modbus Frame Format](#modbus-frame-format)

---

## Overview

The Noah Battery Management System (BMS) uses **Modbus RTU over MQTT** for communication. The emulator (`PythonWrapper`) generates and sends Modbus payloads to the Python backend, which forwards them via MQTT to the `s/33/{deviceId}` topic.

### Device ID
- Standard: `0PVP50ZR16ST00CB`
- MQTT Send Topic: `s/33/0PVP50ZR16ST00CB`
- MQTT Receive Topic: `c_33_0PVP50ZR16ST00CB`

---

## Register Ranges

| Range | Start | End | Purpose | Commands |
|-------|-------|-----|---------|----------|
| **TIME SEGMENT** | 254 | 260 | Time segments (9 slots) | `PRESET_MULTIPLE_REGISTER` |
| **REPEAT PATTERN** | 342 | 360 | Repeat pattern per slot | `PRESET_SINGLE_REGISTER` |
| **DEFAULT POWER** | 252 | 252 | Default power | `PRESET_SINGLE_REGISTER` |
| **SMART POWER** | 310 | 312 | SmartPower settings | `PRESET_MULTIPLE_REGISTER` |
| **LOW LIMIT SOC** | 248 | 248 | Lower battery threshold | `PRESET_SINGLE_REGISTER` |

---

## Time Segment Functions

### Function: `SetNoahTimeSegment(object query)`

**Purpose:** Configures a Noah time segment with time window, power, and repeat pattern.

**Parameters:**
```csharp
var query = new DeviceNoahSetTimeSegmentQuery
{
    Type = "1",              // Slot 1-9 (Identifier)
    Enable = "1",            // 0: Disabled, 1: Enabled
    StartTime = "06:00",     // HH:MM format
    EndTime = "14:00",       // HH:MM format
    Power = "500",           // 0-800 Watts
    Mode = "0",              // 0: Load First, 1: Battery First
    Repeat = "1,2,3"         // Repeat days (1=Mon, 2=Tue, 3=Wed, etc.)
};

pythonWrapper.SetNoahTimeSegment(query);
```

### Register Map: PRESET_MULTIPLE_REGISTER (254-260)

**Modbus Function Code:** 16 (Write Multiple Registers)  
**Start Register:** 254  
**Register Count:** 7

| Register | Hex | Function | Format | Example |
|----------|-----|----------|--------|---------|
| **254** | 0xFE | Padding | Fixed `0x00FE` | `0x00FE` |
| **255** | 0xFF | Fixed Value | Fixed `0x0102` | `0x0102` |
| **256** | 0x0100 | Enable | `0` or `1` | `0x0001` |
| **257** | 0x0101 | Start Time | `(HH << 8) \| MM` | `0x0600` (06:00) |
| **258** | 0x0102 | End Time | `(HH << 8) \| MM` | `0x0E00` (14:00) |
| **259** | 0x0103 | Power (W) | `0-800` | `0x01F4` (500W) |
| **260** | 0x0104 | Type/Slot | `1-9` | `0x0001` (Slot 1) |

**Payload Example (Slot 1: 1-3, 06:00-14:00, 500W):**
```
Header: 00-01-00-07-00-2E-01-10
DeviceID: 30-50-56-50-35-30-5A-52-31-36-53-54-30-30-43-42
Data: 00-FE-01-02-00-01-06-00-0E-00-01-F4-00-01
```

### Register Map: PRESET_SINGLE_REGISTER (342+)

**Modbus Function Code:** 6 (Write Single Register)  
**Repeat Register:** `342 + ((Slot - 1) × 2)`

| Slot | Register | Hex | Purpose |
|------|----------|-----|---------|
| 1 | 342 | 0x0156 | Repeat pattern Slot 1 |
| 2 | 344 | 0x0158 | Repeat pattern Slot 2 |
| 3 | 346 | 0x015A | Repeat pattern Slot 3 |
| 4 | 348 | 0x015C | Repeat pattern Slot 4 |
| 5 | 350 | 0x015E | Repeat pattern Slot 5 |
| 6 | 352 | 0x0160 | Repeat pattern Slot 6 |
| 7 | 354 | 0x0162 | Repeat pattern Slot 7 |
| 8 | 356 | 0x0164 | Repeat pattern Slot 8 |
| 9 | 358 | 0x0166 | Repeat pattern Slot 9 |

### Repeat Pattern Encoding

**Bitmask for weekdays:**
```
Bit 0 = Monday    (Day 1)
Bit 1 = Tuesday   (Day 2)
Bit 2 = Wednesday (Day 3)
Bit 3 = Thursday  (Day 4)
Bit 4 = Friday    (Day 5)
Bit 5 = Saturday  (Day 6)
Bit 6 = Sunday    (Day 7)
```

**Examples:**
```
Repeat "1,2,3"     → Bitmask 0x07  (0000111 = Mon, Tue, Wed)
Repeat "4,5,6"     → Bitmask 0x38  (0111000 = Thu, Fri, Sat)
Repeat "1,3,5,7"   → Bitmask 0x55  (1010101 = Mon, Wed, Fri, Sun)
Repeat "1-7"       → Bitmask 0x7F  (1111111 = Daily)
```

**Payload Example (Slot 2: 4,5,6):**
```
Header: 00-01-00-07-00-24-01-06
DeviceID: 30-50-56-50-35-30-5A-52-31-36-53-54-30-30-43-42
Register: 01-58 (0x0158 = Register 344 for Slot 2)
Value: 00-38 (0x38 = 56 = Bitmask for 4,5,6)
```

**Implementation:**
```csharp
private static ushort ConvertRepeatToBitmask(string? repeatStr)
{
    if (string.IsNullOrWhiteSpace(repeatStr))
        return 0;

    ushort bitmask = 0;

    if (repeatStr.Contains(','))
    {
        var dayStrings = repeatStr.Split(',');
        foreach (var dayStr in dayStrings)
        {
            if (ushort.TryParse(dayStr.Trim(), out ushort day) && day >= 1 && day <= 7)
                bitmask |= (ushort)(1 << (day - 1));
        }
    }
    else if (ushort.TryParse(repeatStr, out ushort singleDay) && singleDay >= 1 && singleDay <= 7)
    {
        bitmask = (ushort)(1 << (singleDay - 1));
    }

    return bitmask;
}
```

---

## Power Functions

### Function: `SetSmartPower(ushort value)`

**Purpose:** Sets the SmartPower configuration.

**Parameters:**
```csharp
pythonWrapper.SetSmartPower(500);  // 0-800W
```

### Register Map: SmartPower (310-312)

**Modbus Function Code:** 16 (Write Multiple Registers)  
**Start Register:** 310  
**Register Count:** 3

| Register | Hex | Function | Format |
|----------|-----|----------|--------|
| **310** | 0x0136 | Padding | Fixed `0x0000` |
| **311** | 0x0137 | Power Value | `0-800` Watts |
| **312** | 0x0138 | Mode | Fixed `0x0001` |

**Payload Example (500W):**
```
Header: 00-01-00-07-00-2A-01-10
DeviceID: 30-50-56-50-35-30-5A-52-31-36-53-54-30-30-43-42
Data: 00-00-01-F4-00-01
```

---

### Function: `SetDefaultPower(ushort value)`

**Purpose:** Sets the default power.

**Parameters:**
```csharp
pythonWrapper.SetDefaultPower(200);  // 0-800W
```

### Register Map: DefaultPower (252)

**Modbus Function Code:** 6 (Write Single Register)  
**Register:** 252 (0x00FC)

| Register | Hex | Function | Format |
|----------|-----|----------|--------|
| **252** | 0x00FC | Default Power | `0-800` Watts |

**Payload Example (200W):**
```
Header: 00-01-00-07-00-24-01-06
DeviceID: 30-50-56-50-35-30-5A-52-31-36-53-54-30-30-43-42
Register: 00-FC (252)
Value: 00-C8 (200W)
```

---

## SoC Functions

### Function: `SetLowLimitSoC(ushort value)`

**Purpose:** Sets the lower battery State of Charge limit.

**Parameters:**
```csharp
pythonWrapper.SetLowLimitSoC(10);  // 0-100%
```

### Register Map: LowLimitSoC (248)

**Modbus Function Code:** 6 (Write Single Register)  
**Register:** 248 (0x00F8)

| Register | Hex | Function | Format | Range |
|----------|-----|----------|--------|-------|
| **248** | 0x00F8 | Low Limit SoC | Percentage | 0-100% |

**Payload Example (10%):**
```
Header: 00-01-00-07-00-24-01-06
DeviceID: 30-50-56-50-35-30-5A-52-31-36-53-54-30-30-43-42
Register: 00-F8 (248)
Value: 00-0A (10%)
```

---

## Input Registers (Sensor)

### Readable Input Registers (Emulator Response Only)

These registers are read-only and show the current system state:

| Register | Name | Unit | Type | Range | Description |
|----------|------|------|------|-------|-------------|
| 2 | Output Power | W | FLOAT | 0-10000 | Current output power |
| 7 | PV Total Power | W | FLOAT | 0-50000 | Total PV generation power |
| 8 | Priority Mode | - | ENUM | 0-2 | 0=Load First, 1=Battery First, 2=Grid First |
| 10 | Battery System State | - | ENUM | 0-2 | 0=Idle, 1=Charging, 2=Discharging |
| 11 | Charging Power | W | FLOAT | -30000-30000 | Charge/discharge power (negative=discharge) |
| 13 | Total Battery SoC | % | FLOAT | 0-100 | Total battery State of Charge |
| 41 | Battery 2 SoC | % | FLOAT | 0-100 | Battery 2 State of Charge |
| 53 | Battery 3 SoC | % | FLOAT | 0-100 | Battery 3 State of Charge |
| 65 | Battery 4 SoC | % | FLOAT | 0-100 | Battery 4 State of Charge |
| 72 | PV Energy Today | kWh | FLOAT | 0-999 | Today's PV energy |
| 74 | PV Energy Month | kWh | FLOAT | 0-999 | Monthly PV energy |
| 76 | PV Energy Year | kWh | FLOAT | 0-999 | Yearly PV energy |
| 78 | Energy Output Device | kWh | FLOAT | 0-999 | System output energy |
| 92 | PV1 Voltage | V | FLOAT | 0-600 | PV string 1 voltage |
| 95 | PV2 Voltage | V | FLOAT | 0-600 | PV string 2 voltage |
| 97 | Temperature 2 | °C | FLOAT | -40-80 | System temperature |
| 99 | Battery 1 Max Cell Voltage | V | FLOAT | 0-5 | Highest cell voltage Battery 1 |
| 100 | Battery 1 Min Cell Voltage | V | FLOAT | 0-5 | Lowest cell voltage Battery 1 |
| 109 | Output Voltage | V | FLOAT | 0-500 | Output voltage |

---

## Holding Registers (Control)

### Writable Holding Registers (Command Interface)

| Register | Name | Unit | Type | Range | Purpose |
|----------|------|------|------|-------|---------|
| 248 | Low Limit SoC | % | UINT | 0-100 | Set minimum battery threshold |
| 252 | Default Power | W | UINT | 0-800 | Standard discharge power |
| **254-260** | **Time Segment** | - | - | - | **9 time segments** |
| **310-312** | **SmartPower** | W | - | 0-800 | **SmartPower configuration** |
| **342-360** | **Repeat Pattern** | - | BITMASK | 0-127 | **Repeat pattern per slot** |

---

## Modbus Frame Format

### General MQTT Payload Structure

```
[Header (8 Bytes)][DeviceID (14 Bytes)][Padding (14 Bytes)][Register Data][CRC16 (2 Bytes)]
```

### Header Format

```
Offset  Bytes    Meaning
0-1     00-01    Transaction ID (always 0x0001)
2-3     00-07    Protocol ID (Modbus = 0x0007)
4-5     00-XX    Payload Length (after Protocol ID)
6       Function Code
```

### Function Codes

| Code | Name | Purpose |
|------|------|---------|
| 6 | Write Single Register | Write one register (e.g., DefaultPower) |
| 16 | Write Multiple Registers | Write multiple registers (e.g., TimeSegment) |
| 3 | Read Holding Registers | Read holding registers |
| 4 | Read Input Registers | Read input registers |

### CRC16 Calculation

The last 2 bytes are the CRC16 checksum (CCITT-False) of the entire payload.

```
CRC(Payload) = 2 Bytes Checksum
```

---

## Communication Flow: Set Time Segment

```
1. Client → Emulator: SetNoahTimeSegment(query)
   ├─ Extracts: Type, Enable, StartTime, EndTime, Power, Repeat

2. Emulator → Parser: BuildSetMultipleRegistersCommand(...)
   ├─ Generates: PRESET_MULTIPLE_REGISTER Payload
   ├─ Registers 254-260 with 7 values
   ├─ Calculate CRC16

3. Emulator → Python: send_msg("s/33/0PVP50ZR16ST00CB", payload)
   └─ Topic: s/33/0PVP50ZR16ST00CB
   └─ Payload: 46 Bytes (MsgLen: 46)

4. Emulator → Parser: BuildSetRegisterCommand(...)
   ├─ Generates: PRESET_SINGLE_REGISTER Payload
   ├─ Register: 342 + ((Slot-1) × 2)
   ├─ Value: Repeat Bitmask
   ├─ Calculate CRC16

5. Emulator → Python: send_msg("s/33/0PVP50ZR16ST00CB", payload)
   └─ Topic: s/33/0PVP50ZR16ST00CB
   └─ Payload: 36 Bytes (MsgLen: 36)

6. Logs (DumpFromPython):
   ├─ PRESET_MULTIPLE_REGISTER: {type, enable, startTime, endTime, power}
   └─ PRESET_SINGLE_REGISTER: {repeat → bitmask, register=0xXXXX}
```

---

## Code Implementation Overview

### PythonWrapper Main Methods

```csharp
public void SetNoahTimeSegment(object query)
{
    // Extracts query properties
    // Builds PRESET_MULTIPLE_REGISTER (254-260)
    // Sends via MQTT
    // Builds PRESET_SINGLE_REGISTER (342+Slot)
    // Sends via MQTT
    // Logs both commands
}

public void SetSmartPower(ushort value)
{
    // Builds PRESET_MULTIPLE_REGISTER (310-312)
    // Sends via MQTT
}

public void SetDefaultPower(ushort value)
{
    // Builds PRESET_SINGLE_REGISTER (252)
    // Sends via MQTT
}

public void SetLowLimitSoC(ushort value)
{
    // Builds PRESET_SINGLE_REGISTER (248)
    // Sends via MQTT
}
```

---

## Validation Rules

### Time Segment
- **Type:** 1-9 (Slot)
- **Enable:** 0 or 1
- **StartTime:** HH:MM (00:00-23:59)
- **EndTime:** HH:MM (00:00-23:59)
- **Power:** 0-800 Watts
- **Mode:** 0 (Load First) or 1 (Battery First)
- **Repeat:** Comma-separated days (1-7) or range

### SmartPower
- **Value:** 0-800 Watts

### DefaultPower
- **Value:** 0-800 Watts

### LowLimitSoC
- **Value:** 0-100 %

---

## Error Handling

```csharp
// Property extraction with null-check
string? repeatStr = GetQueryPropertyValue(query, "Repeat");

// Parsing with TryParse
if (ushort.TryParse(typeStr, out ushort slot) && slot >= 1 && slot <= 9)
{
    repeatRegister = (ushort)(342 + ((slot - 1) * 2));
}

// Logging for tracing
Logger.LogInformation(
    "[TRACE] SetNoahTimeSegment: type={Type}, repeat={Repeat} → bitmask=0x{Bitmask:X2}, register=0x{Register:X4}",
    typeStr, repeatStr, repeatBitmask, repeatRegister
);
```

---

## Trace Logs

The emulator logs show:

```
[TRACE] SetNoahTimeSegment PRESET_MULTIPLE_REGISTER: type=1, enable=1, startTime=06:00, endTime=14:00, power=500
[TRACE] SetNoahTimeSegment PRESET_SINGLE_REGISTER: repeat=1,2,3 → bitmask=0x07, register=0x0156
```

---

---

## Complete Register Reference Map

### All Registers (Known & Unknown Placeholders)

This comprehensive table lists all Noah registers from 0-360, with documented values and placeholders for undocumented registers.

| Register | Hex | Access | Function | Type | Range | Status | Description |
|----------|-----|--------|----------|------|-------|--------|-------------|
| 0 | 0x0000 | R | Reserved | - | - | 🔒 | System reserved |
| 1 | 0x0001 | R | Reserved | - | - | 🔒 | System reserved |
| 2 | 0x0002 | R | Output Power | FLOAT | 0-10000 | ✅ | Current output power (W) |
| 3 | 0x0003 | R | Reserved | - | - | ❓ | Unknown function |
| 4 | 0x0004 | R | Reserved | - | - | ❓ | Unknown function |
| 5 | 0x0005 | R | Reserved | - | - | ❓ | Unknown function |
| 6 | 0x0006 | R | Reserved | - | - | ❓ | Unknown function |
| 7 | 0x0007 | R | PV Total Power | FLOAT | 0-50000 | ✅ | Total PV generation (W) |
| 8 | 0x0008 | R | Priority Mode | ENUM | 0-2 | ✅ | Load/Battery/Grid First |
| 9 | 0x0009 | R | Reserved | - | - | ❓ | Unknown function |
| 10 | 0x000A | R | Battery System State | ENUM | 0-2 | ✅ | Idle/Charging/Discharging |
| 11 | 0x000B | R | Charging Power | FLOAT | -30000-30000 | ✅ | Charge/discharge power (W) |
| 12 | 0x000C | R | Reserved | - | - | ❓ | Unknown function |
| 13 | 0x000D | R | Total Battery SoC | FLOAT | 0-100 | ✅ | Total battery SoC (%) |
| 14-20 | 0x000E-0x0014 | R | Reserved | - | - | ❓ | Unknown functions |
| 21 | 0x0015 | R | Serial Part 1 | STRING | - | ✅ | Device serial (4 registers) |
| 22-24 | 0x0016-0x0018 | R | Serial Part 1 (cont.) | STRING | - | ✅ | Device serial continuation |
| 25 | 0x0019 | R | Serial Part 3 | STRING | - | ✅ | Device serial (4 registers) |
| 26-28 | 0x001A-0x001C | R | Serial Part 3 (cont.) | STRING | - | ✅ | Device serial continuation |
| 29-40 | 0x001D-0x0028 | R | Reserved | - | - | ❓ | Unknown functions |
| 41 | 0x0029 | R | Battery 2 SoC | FLOAT | 0-100 | ✅ | Battery 2 SoC (%) |
| 42-52 | 0x002A-0x0034 | R | Reserved | - | - | ❓ | Unknown functions |
| 53 | 0x0035 | R | Battery 3 SoC | FLOAT | 0-100 | ✅ | Battery 3 SoC (%) |
| 54-64 | 0x0036-0x0040 | R | Reserved | - | - | ❓ | Unknown functions |
| 65 | 0x0041 | R | Battery 4 SoC | FLOAT | 0-100 | ✅ | Battery 4 SoC (%) |
| 66-71 | 0x0042-0x0047 | R | Reserved | - | - | ❓ | Unknown functions |
| 72 | 0x0048 | R | PV Energy Today | FLOAT | 0-999 | ✅ | Today's PV energy (kWh) |
| 73 | 0x0049 | R | Reserved | - | - | ❓ | Unknown function |
| 74 | 0x004A | R | PV Energy Month | FLOAT | 0-999 | ✅ | Monthly PV energy (kWh) |
| 75 | 0x004B | R | Reserved | - | - | ❓ | Unknown function |
| 76 | 0x004C | R | PV Energy Year | FLOAT | 0-999 | ✅ | Yearly PV energy (kWh) |
| 77 | 0x004D | R | Reserved | - | - | ❓ | Unknown function |
| 78 | 0x004E | R | Energy Output Device | FLOAT | 0-999 | ✅ | System output energy (kWh) |
| 79-91 | 0x004F-0x005B | R | Reserved | - | - | ❓ | Unknown functions |
| 92 | 0x005C | R | PV1 Voltage | FLOAT | 0-600 | ✅ | PV string 1 voltage (V) |
| 93-94 | 0x005D-0x005E | R | Reserved | - | - | ❓ | Unknown functions |
| 95 | 0x005F | R | PV2 Voltage | FLOAT | 0-600 | ✅ | PV string 2 voltage (V) |
| 96 | 0x0060 | R | Reserved | - | - | ❓ | Unknown function |
| 97 | 0x0061 | R | Temperature 2 | FLOAT | -40-80 | ✅ | System temperature (°C) |
| 98 | 0x0062 | R | Reserved | - | - | ❓ | Unknown function |
| 99 | 0x0063 | R | Battery 1 Max Cell Voltage | FLOAT | 0-5 | ✅ | Highest cell voltage (V) |
| 100 | 0x0064 | R | Battery 1 Min Cell Voltage | FLOAT | 0-5 | ✅ | Lowest cell voltage (V) |
| 101-108 | 0x0065-0x006C | R | Reserved | - | - | ❓ | Unknown functions |
| 109 | 0x006D | R | Output Voltage | FLOAT | 0-500 | ✅ | Output voltage (V) |
| 110-247 | 0x006E-0x00F7 | R/W | Reserved | - | - | ❓ | Unknown functions |
| **248** | **0x00F8** | **W** | **Low Limit SoC** | **UINT** | **0-100** | **✅** | **Set minimum battery threshold (%)** |
| 249-251 | 0x00F9-0x00FB | - | Reserved | - | - | ❓ | Unknown functions |
| **252** | **0x00FC** | **W** | **Default Power** | **UINT** | **0-800** | **✅** | **Set default discharge power (W)** |
| 253 | 0x00FD | - | Reserved | - | - | ❓ | Unknown function |
| **254** | **0x00FE** | **W** | **Time Segment Reg 254** | **UINT** | **Fixed 0x00FE** | **✅** | **Padding (PRESET_MULTIPLE_REGISTER start)** |
| **255** | **0x00FF** | **W** | **Time Segment Reg 255** | **UINT** | **Fixed 0x0102** | **✅** | **Fixed value** |
| **256** | **0x0100** | **W** | **Time Segment Enable** | **UINT** | **0-1** | **✅** | **Enable/Disable (1 of 7)** |
| **257** | **0x0101** | **W** | **Time Segment Start** | **UINT** | **(HH<<8)\|MM** | **✅** | **Start time (2 of 7)** |
| **258** | **0x0102** | **W** | **Time Segment End** | **UINT** | **(HH<<8)\|MM** | **✅** | **End time (3 of 7)** |
| **259** | **0x0103** | **W** | **Time Segment Power** | **UINT** | **0-800** | **✅** | **Power value (4 of 7)** |
| **260** | **0x0104** | **W** | **Time Segment Type** | **UINT** | **1-9** | **✅** | **Slot identifier (5 of 7)** |
| 261-309 | 0x0105-0x0135 | R/W | Reserved | - | - | ❓ | Unknown functions |
| **310** | **0x0136** | **W** | **SmartPower Padding** | **UINT** | **Fixed 0x0000** | **✅** | **Padding (PRESET_MULTIPLE_REGISTER)** |
| **311** | **0x0137** | **W** | **SmartPower Value** | **UINT** | **0-800** | **✅** | **Power value (W)** |
| **312** | **0x0138** | **W** | **SmartPower Mode** | **UINT** | **Fixed 0x0001** | **✅** | **Mode flag** |
| 313-341 | 0x0139-0x0155 | R/W | Reserved | - | - | ❓ | Unknown functions |
| **342** | **0x0156** | **W** | **Repeat Pattern Slot 1** | **BITMASK** | **0-127** | **✅** | **Weekday repeat pattern (Slot 1)** |
| 343 | 0x0157 | R/W | Reserved | - | - | ❓ | Unknown function |
| **344** | **0x0158** | **W** | **Repeat Pattern Slot 2** | **BITMASK** | **0-127** | **✅** | **Weekday repeat pattern (Slot 2)** |
| 345 | 0x0159 | R/W | Reserved | - | - | ❓ | Unknown function |
| **346** | **0x015A** | **W** | **Repeat Pattern Slot 3** | **BITMASK** | **0-127** | **✅** | **Weekday repeat pattern (Slot 3)** |
| 347 | 0x015B | R/W | Reserved | - | - | ❓ | Unknown function |
| **348** | **0x015C** | **W** | **Repeat Pattern Slot 4** | **BITMASK** | **0-127** | **✅** | **Weekday repeat pattern (Slot 4)** |
| 349 | 0x015D | R/W | Reserved | - | - | ❓ | Unknown function |
| **350** | **0x015E** | **W** | **Repeat Pattern Slot 5** | **BITMASK** | **0-127** | **✅** | **Weekday repeat pattern (Slot 5)** |
| 351 | 0x015F | R/W | Reserved | - | - | ❓ | Unknown function |
| **352** | **0x0160** | **W** | **Repeat Pattern Slot 6** | **BITMASK** | **0-127** | **✅** | **Weekday repeat pattern (Slot 6)** |
| 353 | 0x0161 | R/W | Reserved | - | - | ❓ | Unknown function |
| **354** | **0x0162** | **W** | **Repeat Pattern Slot 7** | **BITMASK** | **0-127** | **✅** | **Weekday repeat pattern (Slot 7)** |
| 355 | 0x0163 | R/W | Reserved | - | - | ❓ | Unknown function |
| **356** | **0x0164** | **W** | **Repeat Pattern Slot 8** | **BITMASK** | **0-127** | **✅** | **Weekday repeat pattern (Slot 8)** |
| 357 | 0x0165 | R/W | Reserved | - | - | ❓ | Unknown function |
| **358** | **0x0166** | **W** | **Repeat Pattern Slot 9** | **BITMASK** | **0-127** | **✅** | **Weekday repeat pattern (Slot 9)** |
| 359-360+ | 0x0167+ | R/W | Reserved | - | - | ❓ | Future registers / Unknown |

### Legend

| Symbol | Meaning |
|--------|---------|
| **✅** | Documented & Implemented |
| **❓** | Unknown / Undocumented |
| **🔒** | System Reserved |
| **R** | Read-Only (Input Register) |
| **W** | Write-Only (Holding Register) |
| **R/W** | Read/Write (Bidirectional) |

### Register Categories

#### Input Registers (Read-Only)
- **0-1**: System Reserved
- **2, 7, 8, 10, 11, 13**: System State & Power
- **41, 53, 65**: Battery SoC (Battery 2, 3, 4)
- **72, 74, 76, 78**: Energy Measurements
- **92, 95, 97, 99, 100, 109**: Voltages & Temperature
- **21-28**: Device Serial Number

#### Holding Registers (Write-Only)
- **248**: Low Limit SoC
- **252**: Default Power
- **254-260**: Time Segment Configuration (7 registers per slot)
- **310-312**: SmartPower Configuration (3 registers)
- **342, 344, 346, 348, 350, 352, 354, 356, 358**: Repeat Pattern (one per slot)

#### Reserved/Unknown
- **3-6, 9, 12, 14-20, 29-40, 42-52, 54-64, 66-71, 73, 75, 77, 79-91, 93-94, 96, 98, 101-108, 110-247, 249-251, 253, 261-309, 313-341, 343, 345, 347, 349, 351, 353, 355, 357, 359-360+**

---

## References

- Modbus Specification: https://www.modbus.org/
- MQTT Protocol: https://mqtt.org/
- Growatt Noah Documentation: `/grobro/model/growatt_noah_registers.json`
- Python Client: `PythonGrowattClient` (MQTT Bridge)
- Emulator: `EnergyAutomate.Emulator\Python\PythonWrapper.cs`

---

**Last Updated:** 2026-05-02  
**Version:** 1.1  
**Status:** Production Ready  
**Coverage:** ~27 documented registers out of 361 total (7.5%)
