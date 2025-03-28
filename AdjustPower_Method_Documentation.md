
# AdjustPower Methode - Detaillierte Dokumentation

## Zweck:
Die Methode `AdjustPower` passt den aktuellen Leistungswert an, um die Energieverbrauchseffizienz basierend auf einer Zielvorgabe zu optimieren. Dabei wird eine adaptive Hysterese verwendet, um nur bei ausreichend großer Abweichung zwischen aktuellem und Zielverbrauch zu reagieren. Diese Anpassung erfolgt schrittweise unter Berücksichtigung der Anzahl von Geräten, die die Leistung teilen müssen.

## Parameter:
- **RealTimeMeasurementExtention value**: Ein Objekt, das die aktuelle Leistungsanforderung speichert. Der `RequestedPowerValue` wird in diesem Objekt gesetzt, wenn der Leistungswert angepasst wird.

## Logik:

### 1. Berechnung des Unterschieds zwischen aktuellem Verbrauch und Zielverbrauch:
- **currentConsumption**: Der durchschnittliche Energieverbrauch, abgerufen aus `ApiServiceInfo.AvgPowerLoad`.
- **targetConsumption**: Der Zielwert für den Verbrauch, abgerufen aus `ApiServiceInfo.SettingOffsetAvg`.
- **Berechneter Unterschied (`difference`)**: Der Unterschied zwischen dem aktuellen Verbrauch und dem Zielverbrauch wird als `currentConsumption - targetConsumption` berechnet.

### 2. Abholen des letzten gesetzten Power-Werts:
Der Wert des zuletzt angeforderten oder gesetzten Power-Werts wird anhand von `ApiServiceInfo.GetLastRequestedPowerValueItem()` ermittelt:
- Falls keine vorherige Power-Werteinheit vorhanden ist, wird der Wert von `ApiServiceInfo.GetNoahCurrentPowerValueSum()` verwendet.
- Andernfalls wird der Wert von `ApiServiceInfo.GetLastCommitedPowerValue()` (sofern vorhanden) genutzt.

### 3. Adaptive Hysterese:
- **Berechnung der dynamischen Hysterese**: Die Hysterese wird basierend auf dem aktuellen Unterschied (`difference`) und einer Konstante `SettingAvgPowerHysteresis` berechnet:
  ```csharp
  dynamicHysteresis = ApiServiceInfo.SettingAvgPowerHysteresis * Math.Max(1, Math.Abs(difference) / 5);
  ```
  Diese Hysterese sorgt dafür, dass Anpassungen nur dann vorgenommen werden, wenn die Abweichung groß genug ist.

### 4. Überprüfung der Abweichung und Anpassung des Power-Werts:
- Wenn der Unterschied (`Math.Abs(difference)`) größer als die berechnete Hysterese ist, wird eine Anpassung vorgenommen.

### 5. Berechnung des Anpassungsfaktors:
- Ein logarithmischer Anpassungsfaktor (`adjustmentFactor`) wird verwendet, um eine sanfte Anpassung zu gewährleisten. Der Faktor wird berechnet als:
  ```csharp
  adjustmentFactor = Math.Log10(Math.Abs(difference) + 1) * (timeSinceLastAdjustment / 10);
  ```
  Dieser Faktor sorgt dafür, dass bei größeren Unterschieden eine größere Anpassung erfolgt.

- **Begrenzung des Anpassungsfaktors**: Der Anpassungsfaktor wird mit den Grenzen 0.1 und 10 begrenzt, um extrem große oder kleine Anpassungen zu vermeiden.

### 6. Berechnung des neuen Power-Werts:
- Der neue Power-Wert wird basierend auf dem letzten gesetzten Wert und dem berechneten Anpassungsschritt berechnet:
  ```csharp
  newPowerValue = lastCommitedPowerValue + (difference > 0 ? -adjustmentStep : adjustmentStep);
  ```

### 7. Begrenzung auf maximal zulässige Leistung:
- Wenn der berechnete neue Power-Wert den maximalen zulässigen Wert (`SettingMaxPower`) überschreitet, wird der Power-Wert auf den maximalen Wert begrenzt.
- Falls dies geschieht, wird dies nur einmal protokolliert, um unnötige Logs zu vermeiden.

### 8. Sicherstellen, dass der neue Power-Wert durch die Anzahl der Geräte teilbar ist:
- **Berechnung der Anzahl der Geräte**: Die Anzahl der Geräte wird mit `ApiServiceInfo.GetDeviceCount()` ermittelt.
- **Teilbarkeit des Power-Werts durch die Geräteanzahl**: Der neue Power-Wert wird so angepasst, dass er ohne Rest durch die Anzahl der Geräte teilbar ist. Wenn ein Rest vorhanden ist (`newPowerValue % deviceCount`), wird dieser Rest abgezogen, um sicherzustellen, dass der Wert teilbar ist.
  
  **Wichtig**: Diese Anpassung stellt sicher, dass die angeforderte Leistung gleichmäßig auf alle Geräte verteilt wird.

### 9. Setzen des neuen angeforderten Power-Werts:
- Der neue Power-Wert wird nur dann als angeforderter Wert gesetzt, wenn er sich von dem zuletzt angeforderten Wert unterscheidet. Dies wird überprüft durch:
  ```csharp
  if (ApiServiceInfo.NewPowerValue != ApiServiceInfo.GetLastRequestedPowerValue())
  ```

- Der neue Power-Wert wird sowohl in `ApiServiceInfo.NewPowerValue` gespeichert als auch im `value.RequestedPowerValue` gesetzt.

### 10. Logging der Änderung:
- Die Änderung des Power-Werts wird mit den entsprechenden Werten in den Logs protokolliert, einschließlich des alten und neuen Werts sowie des Zielverbrauchs (`Offset`).

### 11. Aktualisierung der letzten Anpassungszeit:
- Der Zeitpunkt der letzten Anpassung (`ApiServiceInfo.SettingAvgPowerlastAdjustmentTime`) wird auf die aktuelle Zeit gesetzt.
- Der Unterschied (`ApiServiceInfo.SettingAvgPowerLastDifference`) wird ebenfalls auf den berechneten Unterschied gesetzt, um ihn für die nächste Anpassung zu verwenden.

## Zusammenfassung:
Die Methode `AdjustPower` sorgt dafür, dass der Power-Wert dynamisch angepasst wird, um den Energieverbrauch effizienter zu gestalten, während sie gleichzeitig eine adaptive Hysterese anwendet, um zu vermeiden, dass kleine Änderungen unnötig das System belasten. Zudem wird der Power-Wert so angepasst, dass er durch die Anzahl der Geräte teilbar ist, und es wird sichergestellt, dass der Wert nicht über den maximal zulässigen Wert hinausgeht. Alle Änderungen werden protokolliert und die Anpassung erfolgt schrittweise, um das System stabil zu halten.
