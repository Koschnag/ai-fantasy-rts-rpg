# Security-Baseline

Project Riftward nutzt zunächst einen kleinen, lokalen und FOSS-basierten Sicherheitsgate. Er ist eine überprüfbare Mindestkontrolle für die Vorproduktion, keine Aussage über vollständige Produktsicherheit.

## Ausführen

```sh
./scripts/security.sh
```

Der Gate:

- enumeriert mit Git alle versionierten und unversionierten, aber nicht ignorierten Dateien; Build-, Runtime-, Cook- und Quarantänepfade sowie Binärdateien werden nicht als Text gelesen;
- meldet typische Private-Key-, Bearer-, Provider-Token- und Credential-Zuweisungsmuster, ohne den gefundenen Wert auszugeben;
- prüft sämtliche einbezogenen JSON-Dateien syntaktisch mit `jq`, darunter `toolchain.lock.json` und das Asset-Manifestschema;
- führt einen Locked Restore mit `NuGetAuditMode=all` aus und behandelt Audit-/Restorefehler als Gatefehler;
- ruft `git lfs fsck --pointers` auf. In einem Repository ohne ersten Commit ist ausschließlich der Fall „kein `HEAD` und keine vorhandene LFS-Datei“ als nicht anwendbar erlaubt.

Die Erkennungslogik kann mit rein künstlichen Dateien außerhalb des Repositorys geprüft werden:

```sh
./scripts/security.sh --self-test
```

## Kontrollierte Testplatzhalter

Eine Credential-Zuweisung wird nur dann als Testplatzhalter akzeptiert, wenn alle Bedingungen gelten:

1. der Pfad liegt unter `tests/fixtures/security/`,
2. dieselbe Zeile enthält `security-gate: allow-test-placeholder`,
3. der Wert enthält den eindeutigen Präfix `RIFTWARD_TEST_ONLY_`.

Private-Key-Header lassen sich mit diesem Marker nicht unterdrücken. Zusätzlich kennt der Gate genau eine bereits vorhandene eingebettete Redaction-Testsignatur: unter `tests/` muss dieselbe Quellzeile sowohl einen RSA-Private-Key-Header als auch den wörtlichen künstlichen Body `private-key-material` enthalten. Diese eng definierte Ausnahme ist kein allgemeines Allowlisting und darf nicht für Produktionskonfiguration, echte Tokenformate oder fremde Beispieldaten verwendet werden.

## Grenzen

Der Gate ist heuristisch. Er findet keine beliebig kodierten, aufgeteilten oder unbekannten Secrets und folgt bewusst keinen Symlinks. Binärdateien benötigen separate Provenienz-, Malware- und Assetprüfungen. Die JSON-Prüfung bestätigt nur Syntax, nicht die semantische Gültigkeit gegen JSON Schema. NuGetAudit erfasst NuGet-Pakete und ersetzt weder einen Review nativer SDL/bgfx-Artefakte noch SBOM-, Lizenz-, SAST-, DAST-, Fuzzing- oder Threat-Model-Prüfungen.

Ein grüner Lauf ist daher nur Evidenz für diesen dokumentierten Baseline-Umfang. Vor ausführbarem Fremdinput, Netzwerkfunktionen oder Releasepaketen werden eigene READY-Aufgaben für Threat Model, native Supply Chain, Sandbox-/Parsergrenzen und plattformspezifische Releaseprüfung benötigt.

## Umgang mit einem Fund

Echte Zugangsdaten werden nicht in Issues, Logs oder Harness-Payloads kopiert. Sie werden beim Anbieter widerrufen beziehungsweise rotiert, anschließend aus Arbeitsbaum und gegebenenfalls Historie entfernt und nur durch eine referenzierte lokale/CI-Secretquelle ersetzt. Der Abschlussnachweis nennt Regel, betroffenen Pfad und Rotation, aber niemals den Secretwert.

## Zentraler Aufgabenbefehl

Der Baseline-Gate ist im zentralen Aufgabenvertrag verdrahtet:

```sh
./scripts/rift.sh security
```

`security` ist nicht Teil des Sammelarms nicht verfügbarer Gates und reicht Argumente an `scripts/security.sh` weiter. Der Gate wird nicht allein deshalb als vollständiger Release-`check` behandelt: dessen übrige Sicherheits-/Lizenznachweise bleiben wie oben beschrieben offen.
