"""Package apworld/shape_of_dreams into dist/shape_of_dreams.apworld (a zip with the package folder at its root)."""
import json
import sys
import zipfile
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
SRC = REPO_ROOT / "apworld" / "shape_of_dreams"
OUT = REPO_ROOT / "dist" / "shape_of_dreams.apworld"

# A zipped .apworld's manifest also needs the container format versions (what Archipelago's own "Build APWorlds"
# writes); without them Archipelago falls back to legacy loading and reports world version 0.0.0.
APWORLD_CONTAINER_VERSION = 7


def main() -> int:
    OUT.parent.mkdir(exist_ok=True)
    manifest_path = SRC / "archipelago.json"
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    manifest.setdefault("version", APWORLD_CONTAINER_VERSION)
    manifest.setdefault("compatible_version", APWORLD_CONTAINER_VERSION)
    with zipfile.ZipFile(OUT, "w", zipfile.ZIP_DEFLATED) as zf:
        for path in sorted(SRC.rglob("*")):
            if not path.is_file() or "__pycache__" in path.parts:
                continue
            arcname = path.relative_to(SRC.parent).as_posix()
            if path == manifest_path:
                zf.writestr(arcname, json.dumps(manifest, indent=2) + "\n")
            else:
                zf.write(path, arcname)
    print(f"wrote {OUT.relative_to(REPO_ROOT)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
