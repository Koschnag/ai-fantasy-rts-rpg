# Aufgabensteuerung

Jede autonome Arbeit beginnt mit einer versionierten Aufgabe gemäß `../schemas/task.schema.json`. `BACKLOG.md` bleibt die fachliche Übersicht; diese Dateien enthalten die maschinenlesbare Ausführungseinheit.

Erlaubte Zustände:

`draft -> ready -> running -> review -> accepted`

Zusätzlich kann eine Aufgabe aus jedem aktiven Zustand nach `blocked` oder `cancelled` wechseln. Nur `ready` darf automatisch gestartet werden. Eine KI darf ihren eigenen fachlichen Umfang oder ihre Abnahmekriterien nicht stillschweigend verändern.
