# Retrospective calibration bindings

Calibration specs are optional, immutable evidence bindings for the existing
`research import-git-history` command. They do not change the raw Git import,
authorize a task, promote a manifest, or convert retrospective evidence into a
prospective run.

R-001 is assembled with a full local Git object database:

```bash
./scripts/rift.sh harness research import-git-history \
  --task T-037 \
  --base c22f4b267a45bedca769c5d77739bf0d91873143 \
  --head bbedc09f85e885bfe6b9f618aac42c7e61eced02 \
  --calibration-spec docs/research/calibrations/R-001-T-037.json \
  --output .ai/runtime/research/imports/R-001-T-037.json
```

The verifier fails closed if a commit, tree, ordered range, blob object ID,
blob SHA-256, manifest status, reconciliation receipt, or historical audit
does not match. A shallow clone or missing object therefore cannot produce a
validated calibration artifact.

The primary range ends with T-037 in `review`. Its `taskOutcome` and every
unobserved duration, usage, cost, intervention, model, provider, identity, and
actor value remain the literal `unknown`. Acceptance is observed only in the
separate later lifecycle binding at commit
`2506d71211fd539d3781ce8c52cf30f0c757724a`; it is never retrodicted into the
review-state head. Historical role separation is recorded as
`not-publicly-proven`, without a role or personhood claim.
