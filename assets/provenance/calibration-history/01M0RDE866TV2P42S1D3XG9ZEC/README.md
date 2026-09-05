# Historical calibration context

This directory preserves the original technical context for generation run
`01M0RDE866TV2P42S1D3XG9ZEC`. It is historical evidence, not an active asset
manifest, a new generation or an asset approval.

T-074 refreshed the bgfx archive-container hash without changing its pinned
source content. Because calibration binds the complete toolchain lock bytes,
the old calibration remains bound to the old lock; its receipt is not rewritten.

Preserved raw-file SHA-256 values:

- `manifest.json`: `99225ad1c6d19851183c86c4da880fcd3e21a5a88ec7923af12f82b327f60e6c`
- `toolchain.lock.json`: `49dd75fa91e539fcf19ab3f8b01aecd81a3cf11fbff940f14051ce80ec088b42`
- Existing receipt at `assets/receipts/CAL-STONEWOOD-V1-39FAAE34C4CD/01M0RDE866TV2P42S1D3XG9ZEC.json`:
  `9d174b7b3bbfc38a01caebe1b2ba84039521efaa0bd6762298962f95fe9087b2`

These are hashes of complete stored bytes, not the canonical JSON hashes used
inside the manifest/receipt contract. To validate the historical context, use
an isolated workspace with this lock at its original root path, this manifest
at its original active path, the unchanged old receipt, original generator
sources/specification and the same generated payload bytes. Do not replace the
current repository lock to perform that check.

Portable historical manifest/receipt validation is distinct from local-run
acceptance. The isolated historical check does not contain the original
runtime run directory; a `--require-local` check correctly reports
`ASSET_GENERATION_RUN_MISSING`. No old runtime events are recreated or claimed.

The active manifest now comes from the genuine new run
`01M1RR3QZPNSGBTG44ZCYE1FVX`, generated on 2026-09-05 at 12:18:53 UTC.
The run binds the new complete lock SHA-256
`4310f9c405f261a698235c835bc56cb3d01821a44936b6146fc009e51df933cf`.
The generator's embedded sources and SDK metadata were validated by the actual
pipeline. The unchanged specification and seed produced the same three payload
hashes, totaling 2,348,418 bytes. A targeted local-artifact validation passed;
the asset remains in quarantine. No output binary is added to Git.
