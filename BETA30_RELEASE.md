# Memory Manager v0.9.0-beta.30 Release Gate

This prerelease is built from the user-provided beta.27 Go + embedded WebView2 UI Canonical now stored in `beta30-source/` with validation in `tests_beta30/`.

Release requirements:

- Preserve the beta.27 UI architecture and behavior; the legacy beta.28/beta.29 WPF tree is not the beta.30 UI Canonical.
- Clicking the license/welcome `不同意` action must immediately request `license {accepted:false}` without waiting for Theme/Accent persistence.
- The backend sets the application to exiting state, destroys the main window, and completes through the existing `WM_DESTROY -> PostQuitMessage` path so no tray/background process remains.
- Windows `go test ./...` includes the real per-user install lifecycle test.
- Windows `go vet`, deterministic A/B Go build, PE resource verification, source packaging, and SHA-256 verification must all pass before publication.
- Release assets must include `MemoryManager_v0.9.0-beta.30.exe`, `MemoryManager_v0.9.0-beta.30-source.tar.gz`, and `SHA256SUMS.txt`.
- The release remains unsigned unless a trusted Code Signing credential is configured; do not represent an unsigned build as signed.
