# beta.30 observable Windows gate

This branch exists only to trigger an observable pull-request run of the same Windows beta.30 gate used by main. PR runs must not modify main or publish a Release. They only reconstruct/verify the exact source archive, run preservation tests, execute real Windows Go tests including the per-user install lifecycle, run Go vet, build twice deterministically, verify PE resources, and package checksummed artifacts.
