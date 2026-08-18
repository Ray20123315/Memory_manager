from pathlib import Path
import hashlib

root = Path(__file__).resolve().parent.parent
parts = [root / '_source_parts' / f'part{i:02d}.bin' for i in range(5)]
missing = [str(p) for p in parts if not p.exists()]
if missing:
    raise SystemExit('missing source parts: ' + ', '.join(missing))
out = root / 'beta30-source-only.tar.gz'
with out.open('wb') as fh:
    for part in parts:
        fh.write(part.read_bytes())
actual = hashlib.sha256(out.read_bytes()).hexdigest()
expected = 'ffa22411e12572820c67457c478344f0219df9f84a1d1f2a3bfd63e6c91d2df9'
if actual != expected:
    raise SystemExit(f'source archive SHA256 mismatch: {actual} != {expected}')
print(f'BETA30_SOURCE_ARCHIVE_SHA256_OK {actual}')
