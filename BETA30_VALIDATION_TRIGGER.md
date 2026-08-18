# beta.30 staging validation trigger

This file records the explicit staging validation trigger for the beta.30 UI-recovery source transaction. The Windows workflow must reconstruct the exact source archive, verify SHA-256, run source/UI preservation gates, run real Windows Go tests including the per-user install lifecycle, perform deterministic A/B EXE builds, verify PE resources, and only then expand the browsable source tree on `beta30-ui-recovery`.
