# Akzeptierte Umsetzung T-004 – Run-Provenienz, Evidenz und Retention

- Aufgabe: `T-004` (Epic E-001)
- Status: akzeptiert durch separaten Reviewlauf
- Freigabe: 2026-08-22 durch Projektleitung (`draft -> ready`, siehe `releaseNote` in der Aufgabendatei)
- Implementierungslauf: `01M0NCFAVJ308TVZ3XY7J8SY58` (Akteur `t004-implementer`)
- Abnahmeläufe: Recon `01M0N4NR7ZNF551246BGC6Z4D2`, Implementierung `01M0NCFAVJ308TVZ3XY7J8SY58`, unabhängiger Review `01M0QMA3NAPRXX1KBVH8XFRA6J`
- Fertigstellungslauf: `01M0QNHR7BVCXS9YMQVG1ZXMSP` (unabhängige Review-Wiederholung mit Gates, Adversarial-Probes und Kriterien-Crosschecks, 2026-08-23)
- Basis-Commit: `0ae20474dcc12e8bf967b0c37506c1a4877ee311`

## Ergebnis

Das Rift Harness hält seit T-004 für jeden neuen Lauf eine vollständige
Start-Provenienz fest (Task-, Git-, Prompt-, Modell-, Toolchain- und
Konfigurationshashes mit expliziter Vollständigkeitskennzeichnung; Git wird ohne
Unterprozess aus `.git/HEAD` gelesen), erzwingt Trace-/Span-/Kriteriums-Hüllen
für Retrieval-, Tool- und Evidenzereignisse mit Auflösung gegen die gebundene
Aufgabe, schreibt ein byte-deterministisches RAG-Buildmanifest, das Index,
Konfiguration und alle Quellen per SHA-256 bindet, und stellt eine bewachte
Retention bereit: read-only Plan mit Planhash, Ausführung nur nach Confirm-Hash,
abgelaufener Frist, gültigem Abschluss und akzeptiertem bereinigten
History-Bericht, transaktional und mit Purge-Nachweis in
`.ai/runtime/retention-log.jsonl`. Prompts werden ausschließlich als Hash
geführt; Rohprompts, Rohmodellantworten und verborgene Begründungen werden nie
gespeichert.

## Kriterien

- AC-T004-01 ACCEPT — erweitertes Manifest, Hashes mit/ohne Git-Commit, konsistente Vollständigkeitskennzeichnung; Manipulation wird erkannt.
- AC-T004-02 ACCEPT — Hüllenpflicht strikt; fremde/fehlerhafte IDs abgelehnt; je Span-Kombination höchstens eine Evidenz; Retrieval-Ereignisse werden gegen die Retrieval-Kette verifiziert.
- AC-T004-03 ACCEPT — Build-Manifest byte-deterministisch (keine Zeitstempel); bindet Index, Konfiguration und Quellen; Änderungen oder Fehlen invalidieren `verify`.
- AC-T004-04 ACCEPT — Retention read-first; Löschung nur bei Fristablauf + gültigem Abschluss + History-Beweis; transaktional, Symlink-fail-closed, Purge-Log.
- AC-T004-05 ACCEPT — Secrets redigiert, Prompts nur als Hash, geschlossene Feldmengen, Path-Traversal abgelehnt.

## Evidenz (unabhängiger Reviewlauf)

- Gates im Review neu ausgeführt: lint PASS, build 0 Warnungen, test 129/129
  (davon 9 neue T-004-Tests in `tests/RiftHarness.Tests/RunProvenanceTests.fs`),
  security PASS, `rag-build` mit Manifest, `verify` valid über 25 Runs fehlerfrei.
- Quell-Crosschecks je Kriterium im Reviewlauf-Artefakt `ac-crosscheck.txt`.
- Gate-Logs als hashadressierte Artefakte an die Evidenzereignisse gebunden.
- Im Fertigstellungslauf `01M0QNHR7BVCXS9YMQVG1ZXMSP` erneut bestätigt: alle
  Gates grün (inklusive `verify` über 26 Runs), Adversarial-Probes gegen
  Hüllenpflicht, fremde Kriterien, Evidenz-Duplikat-Spans, manipulierte
  Provenienz, gefälschte Retrieval-Verweise und falsche Retention-Hashes wurden
  ausnahmslos abgewiesen; die Probe-Workspaces wurden danach entfernt.

## Nachträgliche Vertragsverfeinerung

Die während der Produktionsprobe ergänzte Zusatzregel „Evidenz nur mit früherer
Trace-Aktivität“ wurde auf den AC-Text zurückgebaut: Autogenerierte Trace-IDs
hätten sonst unheilbar ungültige Läufe erzeugt. Strikte Bestandteile bleiben
Kriteriumsauflösung, Hüllenpflicht, Evidenz-Duplikatsperre und
Retrieval-Querverweis. Dokumentiert im `reviewNote` der Aufgabendatei.

## Restpunkte

- Gates nur auf Linux-x64 ausgeführt; macOS-/Windows-Nachweise bleiben G-PLATFORM.
- Die Abnahme deckt die technische Ebene ab; produktbezogene oder lizenzielle
  Entscheidungen bleiben der Projektleitung vorbehalten.
