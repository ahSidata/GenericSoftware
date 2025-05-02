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
- **IF** No PV power available AND all batteries empty
  - **THEN** No action, exit
  - **ELSE** Continue to next check

### 3. Mode Determination
- **IF** `ApiSettingExtentionMode = true` AND current time is outside Exclusion period
  - **THEN** Enter Extension Mode (step 4)
  - **ELSE** Enter Normal Mode (step 5)

## 4. Extension Mode Logic

Extension mode is active when configured and outside the exclusion time window (default: 07:00-18:00).

### Priority Sequence:
1. **Power Price Check**
   - **IF** `IsExpensiveRestrictionMode or not below avg price` 
     - **THEN** Activate AutoMode

2. **Morning Price Forecast**
   - **IF** High-price phase expected next morning
     - **THEN** Conserve battery, feed with average power only
     - **ACTION:** `TibberRTMDefaultLoadPriorityAsync()`
     - **RESULT:** Battery is preserved for high-price periods

## 5. Normal Mode Logic

Normal mode is the default operational state when Extension Mode is not active.

### Priority Sequence:

1. **Direct Solar Usage Optimization**
   - **IF** `!IsCheapMode` (Not in cheap price mode)
     - **ACTION:** `TibberRTMDefaultLoadPrioritySolarInputAsync(value)`
     - **RESULT:** Load prioritized to use solar energy directly without battery

2. **Battery Charging Prioritization**
   - **IF** Any of the following conditions are true:
     - Poor weather forecast (little sun expected)
     - Low battery level + cheap prices
     - Low battery level + poor weather forecast
     - Battery level < 80% with good weather forecast
     - **ACTION:** `TibberRTMDefaultBatteryPriorityAsync(value)`
     - **RESULT:** Battery charging is prioritized

5. **Default Fallback**
   - When no other condition applies
     - **ACTION:** `AutoMode`
     - **RESULT:** Optimal distribution based on current measurements

## Reserve Calculation

The system dynamically calculates battery reserves based on a reorganization run to optimally adjust capacity for future requirements.

- **Process:** Call `CalculateReserveBasedOnReorg()`
- **Effect:** May override `ApiSettingExtentionReserveThreshold` and `ApiSettingExtentionMinimalReserve`
- **Analysis Method:** Uses historical data and future requirements

## Configuration Parameters

### Extension Mode Parameters
| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `ApiSettingExtentionMode` | bool | `true` | Enables/disables Extension Mode |
| `ApiSettingExtentionExclusionFrom` | TimeSpan | `07:00` | Start of exclusion period |
| `ApiSettingExtentionExclusionUntil` | TimeSpan | `18:00` | End of exclusion period |

