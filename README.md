# Navimow Desktop Controller

Inoffizielle Windows-Desktop-App für Segway Navimow.

Die App bietet Login, Token-Verwaltung, Geräteauswahl, Statusanzeige, Steuerbefehle, Kartenansicht und eine möglichst vollständige Visualisierung der von API und MQTT gelieferten Daten.

## Funktionen

- OAuth-Login über die Navimow Smart-Home Login-Seite
- Token-Austausch, Token-Refresh und Token-Löschung
- Geräte abrufen und steuern
- REST-Statusabfrage
- MQTT-over-WebSocket für Live-Updates
- REST-Fallback, wenn MQTT aktuell nicht verfügbar ist
- Kartenansicht für Positionsdaten
- Strukturansichten für Gerät, Status, MQTT-Event, MQTT-Attribute und MQTT-Info
- Raw-JSON- und Log-Ansicht

## Build

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\build.ps1
```

Die erzeugte Datei liegt danach unter `dist\NavimowDesktopController.exe`.

## Nutzung

1. `Login bei Navimow` anklicken.
2. Nach dem Login die komplette Redirect-URL aus dem Browser kopieren.
3. In `Authorization Code` einfügen.
4. `Token abrufen` klicken.
5. `Geräte abrufen` klicken.
6. Gerät wählen und Status/MQTT nutzen.

## Lokale Daten

- Sessiondaten werden lokal unter `%LOCALAPPDATA%\NavimowDesktopController\session.json` gespeichert.
- Fehler beim App-Start werden lokal unter `%LOCALAPPDATA%\NavimowDesktopController\startup-error.log` protokolliert.
- Diese Dateien gehören nicht in ein Git-Repository.

## GitHub-Hinweise

- `dist/` ist in `.gitignore` ausgeschlossen und sollte besser über GitHub Releases verteilt werden.
- Vor dem Veröffentlichen keine persönlichen Tokens oder Logs hochladen.
- Wenn du eine Release-EXE anbieten willst, nutze am besten ein GitHub Release statt eines normalen Commits.

## Herkunft und Hinweise

- Dieses Projekt ist inoffiziell und steht in keiner offiziellen Verbindung zu Segway/Navimow.
- Die App nutzt die offizielle Navimow-SDK-REST-API und MQTT für Echtzeit-Updates: [segwaynavimow/navimow-sdk](https://github.com/segwaynavimow/navimow-sdk)
- Die Login- und API-Werte orientieren sich an den öffentlich einsehbaren Navimow-Endpunkten.
- Teile der Logik wurden durch die vorhandene `ioBroker.navimow`-Integration und öffentliche Navimow-Integrationen inspiriert.
- Das offizielle Python-SDK übernimmt den OAuth2-Login nicht direkt, daher setzt diese App den Login-, Token-Refresh- und MQTT-Teil selbst um.

## Lizenz

MIT. Siehe [LICENSE](LICENSE).
