**Prompt Start:**
Please provide your response in English, formatted as Markdown formated text and file in workbench area.
**Prompt End:**


# TibberRTMAdjustment3 Decision Logic Documentation

This document outlines the decision-making flow and operational modes of the TibberRTMAdjustment3 energy management system.

## Decision Hierarchy

### 1. Auto Mode Check (Highest Priority)
- **IF** `ApiSettingAutoMode = true`
  - **THEN** Call `TibberRTMAdjustment3AutoMode(value)` and exit
  - **ELSE** Continue to next check

### 2. Inactivity Check
- **IF** No PV power (effectiveSolarPower <= 0) AND battery empty
  - **THEN** No action, exit
  - **ELSE** Continue to next check

### 3. Mode Determination
- **IF** `TibberRTMAdjustment3IsExtensionModeActive()` returns true
  - **THEN** Enter Extension Mode (step 4)
  - **ELSE** Enter Normal Mode (step 5)

## 4. Extension Mode Logic

Extension mode is active when `ApiSettingExtentionMode` is true and current time is outside the exclusion period (default: 07:00-18:00).

### Priority Sequence:
1. **Power Price Check**
   - **IF** `IsCheapRestrictionMode` (cheap electricity prices)
     - **THEN** `TibberRTMDefaultBatteryPriorityAsync()` (charge battery)
   - **IF** `IsExpensiveRestrictionMode` (expensive electricity prices)
     - **THEN** `TibberRTMAdjustment3AutoMode()` (activate energy saving mode)

2. **Morning Price Forecast**
   - **IF** `TibberRTMAdjustment3IsMorningApproaching()` AND `TibberRTMAdjustment3ShouldPreserveBatteryForMorning()`
     - **THEN** `TibberRTMDefaultLoadPriorityAvgAsync()` (conserve battery, feed with average power only)

3. **Battery Level Based Decisions**
   - **IF** Battery level > `MEDIUM_BATTERY_THRESHOLD` (50%)
     - **THEN** `TibberRTMDefaultLoadPriorityMaxAsync()` (maximize load operation)
   - **ELSE IF** Battery level > `CRITICAL_BATTERY_THRESHOLD` (20%)
     - **THEN** `TibberRTMAdjustment3AutoMode()` (activate energy saving mode)
   - **ELSE** 
     - **THEN** `TibberRTMDefaultLoadPriorityAvgAsync()` (run with average load)

## 5. Normal Mode Logic

Normal mode is the default operational state when Extension Mode is not active.

### Priority Sequence:

1. **Expensive Price Check**
   - **IF** `IsExpensiveRestrictionMode` (expensive electricity prices)
     - **THEN** `TibberRTMAdjustment3AutoMode()` (activate energy saving mode)
     - **ELSE** Continue to next check

2. **Direct Solar Usage Optimization**
   - **IF** `!IsCheapRestrictionMode` (Not in cheap price mode)
     - **THEN** `TibberRTMDefaultLoadPrioritySolarInputAsync()` (prioritize direct solar usage)
     - **ELSE** Continue to next check

3. **Battery Charging Prioritization**
   - **IF** Any of the following conditions are true:
     - Battery is full (`isBatteryFull`)
     - Battery charging window is active (`isBatteryChargingWindowActive`)
     - Low prices (`isLowPrices` = `IsCheapRestrictionMode || IsBelowAvgPrice`)
     - Low battery level (< 30%) and cloudy weather
     - Battery level below high threshold (< 80%) and good weather
     - **THEN** `TibberRTMDefaultBatteryPriorityAsync()` (prioritize battery charging)
     - **ELSE** Continue to default fallback

4. **Default Fallback**
   - When no other condition applies
     - **THEN** `TibberRTMAdjustment3AutoMode()` (optimal distribution based on measurements)

## Auto Mode Operation

The Auto Mode (`TibberRTMAdjustment3AutoMode`) performs the following:

1. Clears time segments for Noah devices if needed
2. Calls `TibberRTMAdjustment3SetPower()` to adjust power distribution

### Power Adjustment Logic (`TibberRTMAdjustment3SetPower`)

1. **Maximum Power Limit Check**
   - **IF** `Math.Abs(value.TotalPower) > ApiSettingMaxPower`
     - **IF** TotalPower is negative: Set all devices to 0W
     - **IF** TotalPower is positive: Distribute maximum power evenly among devices

2. **Hysteresis Range Check**
   - **IF** TotalPower is within hysteresis range (between lower and upper limits)
     - **THEN** Perform load balancing without changing total power

3. **Power Adjustment Based on Delta**
   - **IF** Delta is positive: Increase total power (capped at max)
     - Distribute with high SoC prioritization
   - **IF** Delta is negative: Decrease total power (minimum 0)
     - Distribute with low SoC prioritization
   - **IF** Delta is zero: Perform load balancing

## Forecast and Condition Evaluation

### Morning Preservation Check
The system evaluates whether to preserve battery for the morning based on:
- **Price Forecast**: Checks if tomorrow morning (6AM-12PM) has any expensive pricing periods
- **Weather Forecast**: Checks if tomorrow has forecasted poor weather (cloudy/rainy)

### Extension Mode Activation
Extension mode is active when:
- `ApiSettingExtentionMode` is true AND
- Current time is outside the exclusion period (outside of `ApiSettingExtentionExclusionFrom` to `ApiSettingExtentionExclusionUntil`)

## Battery Thresholds

| Threshold | Value | Description |
|-----------|-------|-------------|
| `CRITICAL_BATTERY_THRESHOLD` | 20% | Minimum safe level for battery operation |
| `LOW_BATTERY_THRESHOLD` | 30% | Low battery level requiring attention |
| `MEDIUM_BATTERY_THRESHOLD` | 50% | Medium battery level for normal operation |
| `HIGH_BATTERY_THRESHOLD` | 80% | High battery level for optimal flexibility |

## Solar Power Thresholds

| Threshold | Value | Description |
|-----------|-------|-------------|
| `HIGH_SOLAR_RATIO` | 0.75 | High solar production ratio |
| `VERY_HIGH_SOLAR_RATIO` | 0.8 | Very high solar production ratio |

## Configuration Parameters

### Extension Mode Parameters
| Parameter | Type | Description |
|-----------|------|-------------|
| `ApiSettingExtentionMode` | bool | Enables/disables Extension Mode |
| `ApiSettingExtentionExclusionFrom` | TimeSpan | Start of exclusion period |
| `ApiSettingExtentionExclusionUntil` | TimeSpan | End of exclusion period |

### Power Adjustment Parameters
| Parameter | Type | Description |
|-----------|------|-------------|
| `ApiSettingAvgPower` | int | Target average power |
| `ApiSettingAvgPowerOffset` | int | Offset for the target power |
| `ApiSettingAvgPowerHysteresis` | int | Hysteresis range for power adjustments |
| `ApiSettingMaxPower` | int | Maximum allowed power |
| `ApiSettingPowerAdjustmentFactor` | int | Adjustment factor in percentage |
| `ApiSettingPowerAdjustmentWaitCycles` | int | Number of cycles to wait before adjustment |

