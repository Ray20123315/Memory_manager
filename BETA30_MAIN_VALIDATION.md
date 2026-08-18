# beta.30 main Windows validation

This commit starts the fail-closed Windows validation of the beta.30 UI-recovery source on `main`.

The workflow must reconstruct and SHA-256 verify the exact source archive, preserve the beta.27 Go + embedded WebView2 UI baseline, verify the immediate whole-application exit path for license rejection, run real Windows Go tests including the per-user install lifecycle, run `go vet`, reproduce the Windows EXE twice, verify restored PE resources, and verify release checksums before the browsable `beta30-source/` tree is committed back to `main`.

This commit does not authorize release publication by itself; the release step requires a later `[release-beta30]` commit after source expansion is observed.
