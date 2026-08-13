# Assetbereiche

- `quarantine/` enthält ungeprüfte Generatorausgaben und wird vollständig ignoriert.
- `manifests/` enthält versionierte Provenienz- und Freigabemanifeste.
- `source/` enthält ausschließlich angenommene, bearbeitbare Quellen; definierte Binärformate laufen über Git LFS.
- `cooked/` entsteht reproduzierbar aus Manifesten und freigegebenen Quellen und wird ignoriert.

Vor dem ersten wichtigen Binärasset muss ein gesicherter Git-/LFS-Remote feststehen. Ohne Remote ist die lokale LFS-Kopie keine Sicherung. Ein Asset darf erst aus `quarantine/` nach `source/` wechseln, wenn `assets-check` und die im Manifest geforderten Reviews bestanden sind; dieses Gate wird mit T-003 implementiert.
