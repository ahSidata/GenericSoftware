# Requirements for Future Prompts (EnergyAutomate Battery Power Regulation)

1. **Language**
   - All code comments, log messages, summaries, and documentation must be in **English**.

2. **Device Power Limits**
   - Each battery device (`DeviceList`) can be set between **0 W and ApiSettingMaxPower / device count** (inclusive).
   - No device may receive a negative power value.
   - The sum of all device power values is limited to **ApiSettingMaxPower** (e.g., 840 W for 2 batteries).

3. **Grid Feed-in Handling**
   - If `TotalPower` (grid draw) is **negative** (i.e., feed-in/export), all device power values must be set to **0 W**.

4. **Grid Draw Regulation**
   - The algorithm aims to keep `TotalPower` as close as possible to the configured offset (`ApiSettingAvgPowerOffset`).
   - Correction power (`neededPower`) is calculated as:  
     `int delta = value.TotalPower - ApiSettingAvgPowerOffset;`
   - Only **positive `neededPower`** leads to device charging (never negative values).

5. **Adjustment Factor**
   - The setting **ApiSettingPowerAdjustmentFactor** must be used to scale the delta for power regulation.
   - Calculation example:  
     `int neededPower = Math.Max(0, Math.Min((int)(delta * ApiSettingPowerAdjustmentFactor), ApiSettingMaxPower));`
   - This ensures that only a portion of the delta is regulated per cycle, leading to smoother adjustments.

6. **Hysteresis**
   - If the deviation from the offset is within a configurable **hysteresis band** (e.g., ±10 W), all device power values must be set to **0 W** to avoid unnecessary toggling.

7. **Device Eligibility**
   - Only devices that are **not full** are eligible for power allocation.
   - Devices that are "full" (i.e., `IsBatteryFull == true`) are excluded and set to **0 W**.

8. **SoC-based Load Balancing**
   - Power is distributed among eligible devices **proportional to their SoC priority**:
     - **Lower SoC → higher priority** (gets more power allocated).
     - Priority calculation example: `priority = 1.0 - (SoC / 100.0)`
   - Allocation per device is clamped between **0 W and ApiSettingMaxPower / device count W**.

9. **Comprehensive Logging**
   - All decisions, calculations, allocations, and actions must be logged in English.
   - Logs should include device serial, SoC, allocation, changes, and reasons for setting power or skipping devices.

10. **Helper Method Conventions**
    - The method for setting device power must use the following signature:  
      `private async Task SetDevicePowerIfChanged(DeviceList device, int value, DateTimeOffset ts)`
    - The value parameter must always be clamped to 0 ... ApiSettingMaxPower / device count before being sent.

11. **General**
    - Ensure robustness against all edge cases (no eligible devices, all full, negative power, etc.).
    - Do not introduce negative device power values under any circumstances.
    - Use provided class/struct names (e.g., `DeviceList`).

