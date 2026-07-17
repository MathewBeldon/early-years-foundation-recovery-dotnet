# Validation baselines

`bin/migration-validate` archives each full run under `tmp/parity/validation-archives`. The first successful run creates an immutable `first-green.json` pointer there; later green runs do not replace it.

Generated browser reports, traces, screenshots and side-effect payloads are intentionally not committed. CI uploads the same evidence classes as a retained artifact. Record the CI run id or the local archive manifest in the run journal when promoting a baseline.
