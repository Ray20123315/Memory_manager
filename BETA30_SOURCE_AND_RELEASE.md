# Memory Manager beta.30 UI Recovery

`v0.9.0-beta.30` is rebuilt from the user-supplied, verified `v0.9.0-beta.27` Go + embedded WebView2 source. The beta.27 UI architecture is the canonical UI baseline; the later WPF beta.29 interface is not used as the beta.30 UI baseline.

## Key behavior

- License **Disagree** immediately requests whole-application exit; it does not wait for Theme/Accent persistence and does not minimize to tray.
- Original WebView2 UI, animations, three languages, Theme/Accent, floating ball, Game Memory Reserve, incident tools and installer behavior are preserved.
- Notifications and beginner Log are added inside the original UI architecture.
- GitHub Releases polling supports update notifications without claiming true server push.

## Verification

The `Build beta.30 Go UI Recovery` Windows workflow reconstructs the exact source archive, verifies its SHA-256, runs source preservation tests and real Windows Go tests (including a real per-user install lifecycle in a temporary LocalAppData), runs `go vet`, performs deterministic A/B Windows builds, restores the beta.27 icon/manifest resource payload, verifies PE resources, and publishes SHA-256 checksums.

The staging workflow also commits `beta30-source/` and `tests_beta30/` back to `beta30-ui-recovery` after the Windows gate passes, so the source is directly browsable on GitHub before promotion to `main`.
