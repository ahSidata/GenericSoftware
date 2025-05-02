**Prompt Start:**
Please provide your response in English, formatted as Markdown formated text and file in workbench area.
**Prompt End:**


# Decision Logic of TibberRTMAdjustment3

## Check Sequence (from highest to lowest priority)

1. **Auto Mode Check**
   - If `ApiSettingAutoMode = true`: Call `TibberRTMAdjustment3AutoMode(value)` and exit.
   - Otherwise: Continue with step 2.

2. **Inactivity Check**
   - If no PV power available AND all batteries empty: No action, exit.
   - Otherwise: Continue with step 3.

3. **Operating Mode Determination**
   - If `ApiSettingExtentionMode = true` AND time not within Exclusion From-To:
     → Extension Mode active (Step 4)
   - Otherwise: Normal Mode active (Step 5)

4. **Extension Mode Decisions**
   - Power price-dependent check (highest priority):
     - If `IsExpensiveRestrictionMode`: Activate AutoMode
   - Check if a high-price phase is expected the next morning,
     - If yes, then conserve battery and only feed with average.
       - **Action:** `TibberRTMDefaultLoadPriorityAsync()`
       - **Description:** Battery is preserved.
   - After each action: End method.

5. **Normal Mode Decisions**
   - **Price-dependent check (highest priority):**
     - If `IsExpensiveRestrictionMode`: Activate energy saving mode (AutoMode)
     - **Action:** `AutoMode(value)`

   - **Battery maintenance with full battery (second highest priority):**
     - If `CurrentState.IsGrowattBatteryFull`: Maintain battery charge level
       - With high solar output: Maximum consumption for optimal usage
         - **Condition:** `greater than 75 of ApiSettingMaxPower`
         - **Action:** `TibberRTMDefaultLoadPriorityMaxAsync(value, GrowattGetDeviceNoahSnList())`
       - Standard case: Only feed excess
         - **Action:** `TibberRTMDefaultBatteryPriorityAsync(value)`
          
   - **Standard situations during the day with not full battery based on facts:**

     1. **When battery is empty:**
        - **Condition:** `Special constellations`
          - `Battery empty + normal prices: !CurrentState.IsBelowAvgPrice && CurrentState.IsBatteryEmpty`
        - **Action:** `TibberRTMDefaultLoadPriorityAvgAsync(value)`
        - **Description:** Only the base load is fed to avoid wasting battery power.

     2. **When battery should be charged (multiple conditions possible):**
        - **Conditions:** One of the following cases must apply:
          - `Poor weather forecast (little sun expected)`
          - `Low battery level + cheap prices`
          - `Low battery level + poor weather forecast`
          - `Battery level < 80% with good weather forecast`
        - **Action:** `TibberRTMDefaultBatteryPriorityAsync(value)`
        - **Description:** Battery charging is prioritized.

     3. **When direct solar power usage is optimal:**
        - **Condition:** `Not cheap !IsCheapMode, use only solar power, no battery`
        - **Action:** `TibberRTMDefaultLoadPrioritySolarInputAsync(value)`
        - **Description:** Load prioritized to use solar energy directly.

     4. **When no other condition applies:**
        - **Action:** `TibberRTMAdjustment3AutoMode(value)`
        - **Description:** Optimal distribution based on current measurements.

---

## **Reserve Calculation Based on a Reorg Run**
- **Description:** The reserve is dynamically calculated based on a reorg run to optimally adjust battery capacity to future requirements.
- **Action:** Call `CalculateReserveBasedOnReorg()` to calculate and adjust the reserve.
- **Details:**
  - The reorg run analyzes historical data and future requirements.
  - The calculated reserve overwrites `ApiSettingExtentionReserveThreshold` and `ApiSettingExtentionMinimalReserve` if necessary.

---

## **Additional Parameters for Extension Mode**
1. **`ApiSettingExtentionMode`**
   - **Type:** `bool`
   - **Default value:** `true`
   - **Description:** Enables or disables Extension Mode. If `true`, Extension Mode is used.

2. **`ApiSettingExtentionExclusionFrom`**
   - **Type:** `TimeSpan`
   - **Default value:** `new TimeSpan(7, 0, 0)` (07:00)
   - **Description:** Specifies the start time of the exclusion period during which Extension Mode is not active.

3. **`ApiSettingExtentionExclusionUntil`**
   - **Type:** `TimeSpan`
   - **Default value:** `new TimeSpan(18, 0, 0)` (18:00)
   - **Description:** Specifies the end time of the exclusion period during which Extension Mode is not active.

4. **`ApiSettingExtentionAvgPower`**
   - **Type:** `int`
   - **Default value:** `300`
   - **Description:** Specifies the amount of power (in watts) fed from the extension battery in Extension Mode.

5. **`ApiSettingExtentionReserveThreshold`**
   - **Type:** `int`
   - **Default value:** `50` (%)
   - **Description:** Threshold of battery capacity in percent from which a reserve should be maintained for expensive price periods.
   
6. **`ApiSettingExtentionMinimalReserve`**
   - **Type:** `int`
   - **Default value:** `20` (%)
   - **Description:** Minimum battery capacity in percent that should be maintained as an absolute reserve.

---

## **Summary of Main Factors**
1. **Time (Extension Mode or Normal Mode):**
   - **Extension Mode:** Active when `ApiSettingExtentionMode == true` and the current time is outside the exclusion period.
   - **Normal Mode:** Active when Extension Mode is not active.

2. **Solar Power (high, medium, low):**
   - High solar power: Prioritize battery charging.
   - Medium solar power: Dependent on battery level.
   - Low solar power: Dependent on battery level and electricity prices.

3. **Battery Level (full, empty, partially charged):**
   - Full: Focus on load prioritization.
   - Empty: Focus on battery charging or load reduction.

4. **Electricity Prices (cheap, expensive):**
   - Cheap: Prioritize battery charging.
   - Expensive: Activate energy saving mode.


