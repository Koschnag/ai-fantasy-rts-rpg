# Drittanbieterhinweise

Diese Datei dokumentiert Drittanbieterkomponenten des privaten
Entwicklungsstandes. Sie lizenziert **nicht** den Projektcode oder die
Projektassets; deren Projektlizenz ist weiterhin offen.

## Native Komponenten (T-010, linux-x64)

Gepinnt in `toolchain.lock.json` (`nativeComponents`, Kohorte
`2026-08-23-cohort-1`); Lizenzen wurden am gepinnten Stand geprüft.
Quellarchive liegen hashgeprüft im lokalen Cache außerhalb von Git.

| Komponente | Revision | Lizenz | Zweck |
|---|---|---|---|
| SDL3 | Tag `release-3.4.14`, Commit `147a8ee32dbf9ac02f3794964490687b6bbda1bc` | zlib | Fenster, Ereignisse, Eingabe |
| bgfx | Commit `35a98dd6453cf25dc75c68e233abb400836d5920` | BSD-2-Clause | Renderabstraktion (OpenGL-3.3-Core-Pflichtpfad) |
| bx | Commit `9e3fadf6f11380031486be704d2ff46ca143664f` | BSD-2-Clause | bgfx-Grundbibliothek |
| bimg | Commit `371d90098b1fd017cd00205979d5ef74b8c3ed62` | BSD-2-Clause | bgfx-Bildbibliothek |

Austauschstrategie: Pin-Austausch mit Neubau gemäß
`scripts/native-build-linux-x64.sh`; die Abweichung von Release-Tags bei
bgfx/bx/bimg ist in `toolchain.lock.json` dokumentiert.

### zlib-Lizenz (SDL3)

Copyright (C) 1997-2026 Sam Lantinga <slouken@libsdl.org>

This software is provided 'as-is', without any express or implied
warranty. In no event will the authors be held liable for any damages
arising from the use of this software.

Permission is granted to anyone to use this software for any purpose,
including commercial applications, and to alter it and redistribute it
freely, subject to the following restrictions:

1. The origin of this software must not be misrepresented; you must not
   claim that you wrote the original software. If you use this software
   in a product, an acknowledgment in the product documentation would be
   appreciated but is not required.
2. Altered source versions must be plainly marked as such, and must not be
   misrepresented as being the original software.
3. This notice may not be removed or altered from any source distribution.

### BSD-2-Clause-Lizenz (bgfx, bx, bimg)

Copyright 2010-2026 Branimir Karadzic

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice,
   this list of conditions and the following disclaimer.
2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED ``AS IS'' AND ANY EXPRESS OR IMPLIED WARRANTIES,
INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND
FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE
REGENTS OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS;
OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY,
WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR
OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF
ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

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
