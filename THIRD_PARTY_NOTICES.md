# Drittanbieterhinweise

Diese Datei dokumentiert Drittanbieterkomponenten des privaten
Entwicklungsstandes. Sie lizenziert **nicht** den Projektcode oder die
Projektassets; deren Projektlizenz ist weiterhin offen.

## JSON-Schema-Paketgraph des RiftHarness

| Paket | Version | Upstream-Revision laut NuGet-Paket | Lizenz |
|---|---:|---|---|
| JsonSchema.Net | 8.0.5 | [`json-everything` `3520d7ac`](https://github.com/json-everything/json-everything/tree/3520d7ac43e5c6c9b91abeac5af992eb82ffbf63) | MIT |
| JsonPointer.Net | 6.0.1 | [`json-everything` `694b3e47`](https://github.com/json-everything/json-everything/tree/694b3e47897b6bca03ec8285acb426822c129308) | MIT |
| Json.More.Net | 2.2.0 | [`json-everything` `f53d4d27`](https://github.com/json-everything/json-everything/tree/f53d4d27eeed6a73d377bf4188175f0b8a46f856) | MIT |
| Humanizer.Core | 3.0.1 | [`Humanizer` `6e54d378`](https://github.com/Humanizr/Humanizer/tree/6e54d3786f4c4fe2cf665fa41d74a6e79bf9a85f) | MIT |

Die exakten Paket-Content-Hashes sind Bestandteil von
`tools/RiftHarness/packages.lock.json` und
`tests/RiftHarness.Tests/packages.lock.json`. `JsonSchema.Net`,
`JsonPointer.Net` und `Json.More.Net` nennen Greg Dennis als Autor;
`Humanizer.Core` nennt Claire Novotny und Mehdi Khalili. Die mitgelieferten
LICENSE-Dateien der drei `json-everything`-Pakete und das Copyrightfeld der
Humanizer-Paketmetadaten nennen die .NET Foundation und Contributors.

### MIT License

Copyright (c) .NET Foundation and Contributors

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
