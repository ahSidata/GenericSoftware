### Entscheidungslogik von TibberRTMAdjustment3

#### Prüfreihenfolge (von höchster zu niedrigster Priorität)

1. **Automodus-Prüfung**
   - Wenn `ApiSettingAutoMode = true`: Rufe `TibberRTMAdjustment3AutoMode(value)` auf und beende.
   - Sonst: Fahre mit Schritt 2 fort.

2. **Inaktivitätsprüfung**
   - Wenn kein PV-Strom verfügbar UND alle Batterien leer: Keine Aktion, beende.
   - Sonst: Fahre mit Schritt 3 fort.

3. **Betriebsmodus-Ermittlung**
   - Wenn `ApiSettingExtentionMode = true` UND Uhrzeit außerhalb 07:00-18:00 Uhr: 
     → ExtensionMode aktiv (Schritt 4)
   - Sonst: Normaler Modus aktiv (Schritt 5)

4. **ExtensionMode-Entscheidungen**
   - Strompreisabhängige Prüfung (höchste Priorität):
     - Wenn `IsCheapRestrictionMode`: Batterie laden
     - Wenn `IsExpensiveRestrictionMode`: Energiesparmodus aktivieren
   - Wenn keine Preisrestriktion aktiv → Batteriekapazitätsabhängige Prüfung:
     - Hoher Akkustand (>50%): Maximal-Lastbetrieb
     - Mittlerer Akkustand (20-50%): Energiesparmodus
     - Niedriger Akkustand (<20%): Durchschnittslastbetrieb
   - Nach jeder Aktion: Methode beenden.

5. **Normaler Modus-Entscheidungen**
   - **Preisabhängige Prüfung (höchste Priorität):**
     - Wenn `IsExpensiveRestrictionMode`: Energiesparmodus aktivieren (AutoMode)
     - **Aktion:** `TibberRTMAdjustment3AutoMode(value)`

   - **Akkuerhaltung bei vollem Akku (zweithöchste Priorität):**
     - Wenn `CurrentState.IsGrowattBatteryFull`: Akku-Ladezustand erhalten
       - Bei hoher Solarleistung: Maximaler Verbrauch für optimale Nutzung
         - **Bedingung:** `effectiveSolarPower > ApiSettingMaxPower * 0.75`
         - **Aktion:** `TibberRTMDefaultLoadPriorityMaxAsync(value, GrowattGetDeviceNoahSnList())`
       - Standardfall: Nur Überschuss einspeisen
         - **Aktion:** `TibberRTMDefaultBatteryPriorityAsync(value)`
          
   - **Standard-Situationen am Tag bei nicht vollem Akku basierend auf Fakten:**

     1. **Wenn Akku leer ist:**
        - **Bedingung:** `Sonder Konstellationen`
          - `Akkustand leer + normale Preise: !CurrentState.IsBelowAvgPrice && CurrentState.IsBatteryEmpty`
        - **Aktion:** `TibberRTMDefaultLoadPriorityAvgAsync(value)`
        - **Beschreibung:** Es wird nur die Grundlast eingespeist um keinen Akkustrom zu verschwenden.

     2. **Wenn Akku geladen werden soll (mehrere Bedingungen möglich):**
        - **Bedingungen:** Einer der folgenden Fälle muss zutreffen:
          - `Schlechte Wetterprognose (wenig Sonne erwartet): CurrentState.IsCloudy && GetExpectedSolarProductionForNextHours(6) < ApiSettingAvgPower * 2`
          - `Akkustand niedrig + günstige Preise: GetBatteryLevel() < 30 && CurrentState.IsBelowAvgPrice`
          - `Akkustand niedrig + schlechte Wetterprognose: GetBatteryLevel() < 80 && (CurrentState.IsCloudy || GetExpectedSolarProductionForNextHours(4) < ApiSettingAvgPower * 2)`
          - `Akkustand < 80% mit guter Wetterprognose: GetBatteryLevel() < 80 && !CurrentState.IsCloudy`
        - **Aktion:** `TibberRTMDefaultBatteryPriorityAsync(value)`
        - **Beschreibung:** Batterie wird priorisiert geladen.

     3. **Wenn direkte Solarstromnutzung optimal ist:**
        - **Bedingung:** `Nicht billig !IsCheapMode, nur solarstrom verwenden keine batterie`
        - **Aktion:** `TibberRTMDefaultLoadPrioritySolarInputAsync(value)`
        - **Beschreibung:** Last priorisiert, um die Solarenergie direkt zu nutzen.

     4. **Wenn sonst keine Bedinung greift:**
        - **Aktion:** `TibberRTMAdjustment3AutoMode(value)`
        - **Beschreibung:** Optimale Verteilung basierend auf aktuellen Messwerten.

---

### **Reserveberechnung basierend auf einem Reorg-Lauf**
- **Beschreibung:** Die Reserve wird dynamisch auf Grundlage eines Reorg-Laufs berechnet, um die Akkukapazität optimal an die zukünftigen Anforderungen anzupassen.
- **Aktion:** Rufe `CalculateReserveBasedOnReorg()` auf, um die Reserve zu berechnen und anzupassen.
- **Details:**
  - Der Reorg-Lauf analysiert historische Daten und zukünftige Anforderungen.
  - Die berechnete Reserve überschreibt `ApiSettingExtentionReserveThreshold` und `ApiSettingExtentionMinimalReserve`, falls erforderlich.

---

### **Zusätzliche Parameter für den ExtensionMode**
1. **`ApiSettingExtentionMode`**
   - **Typ:** `bool`
   - **Standardwert:** `true`
   - **Beschreibung:** Aktiviert oder deaktiviert den ExtensionMode. Wenn `true`, wird der ExtensionMode verwendet.

2. **`ApiSettingExtentionExclusionFrom`**
   - **Typ:** `TimeSpan`
   - **Standardwert:** `new TimeSpan(7, 0, 0)` (07:00 Uhr)
   - **Beschreibung:** Gibt die Startzeit des Ausschlusszeitraums an, in dem der ExtensionMode nicht aktiv ist.

3. **`ApiSettingExtentionExclusionUntil`**
   - **Typ:** `TimeSpan`
   - **Standardwert:** `new TimeSpan(18, 0, 0)` (18:00 Uhr)
   - **Beschreibung:** Gibt die Endzeit des Ausschlusszeitraums an, in dem der ExtensionMode nicht aktiv ist.

4. **`ApiSettingExtentionAvgPower`**
   - **Typ:** `int`
   - **Standardwert:** `300`
   - **Beschreibung:** Gibt die Menge an Strom (in Watt) an, die im ExtensionMode vom Extension-Akku eingespeist wird.

5. **`ApiSettingExtentionReserveThreshold`**
   - **Typ:** `int`
   - **Standardwert:** `50` (%)
   - **Beschreibung:** Schwellenwert der Akkukapazität in Prozent, ab dem eine Reserve für teure Preiszeiten eingehalten werden soll.
   
6. **`ApiSettingExtentionMinimalReserve`**
   - **Typ:** `int`
   - **Standardwert:** `20` (%)
   - **Beschreibung:** Minimale Akkukapazität in Prozent, die als absolute Reserve beibehalten werden soll.

---

### **Zusammenfassung der Hauptfaktoren**
1. **Zeit (ExtensionMode oder Normaler Modus):**
   - **ExtensionMode:** Aktiv, wenn `ApiSettingExtentionMode == true` und die aktuelle Zeit außerhalb des Ausschlusszeitraums liegt.
   - **Normaler Modus:** Aktiv, wenn der ExtensionMode nicht aktiv ist.

2. **Solarleistung (hoch, mittel, niedrig):**
   - Hohe Solarleistung: Priorisiere Batterieladung.
   - Mittlere Solarleistung: Abhängig vom Akkustand.
   - Niedrige Solarleistung: Abhängig von Akkustand und Strompreisen.

3. **Akkustand (voll, leer, teilweise geladen):**
   - Voll: Fokus auf Lastpriorisierung.
   - Leer: Fokus auf Batterieladung oder Lastreduktion.

4. **Strompreise (günstig, teuer):**
   - Günstig: Priorisiere Batterieladung.
   - Teuer: Aktiviere Energiesparmodus.
