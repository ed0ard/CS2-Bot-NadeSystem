#!/usr/bin/env python3
"""
convert_lineups.py
==================
Converts NadeLauncher lineup files into NadeSystem-compatible JSON.

Source format  (NadeLauncher):
  <NadeLauncherPlugin>/data/lineups/<mapname>.json
  → single JSON array, each entry has full lineup data

Output format  (NadeSystem):
  <NadeSystemPlugin>/grenades/<mapname>_<grenadeType>.json
  → JSON array of stripped-down records, one file per (map, grenadeType) pair

Fields kept:
  id, mapName, grenadeType, projectilePosition, projectileVelocity, landingPosition

Usage:
  python convert_lineups.py [NadeLauncherDir] [NadeSystemDir]

  Defaults:
    NadeLauncherDir  = ./NadeLauncher
    NadeSystemDir = ./NadeSystem

  Batch example (convert all maps at once):
    python convert_lineups.py ./addons/counterstrikesharp/plugins/NadeLauncher \
                              ./addons/counterstrikesharp/plugins/NadeSystem
"""

import json
import os
import sys
from collections import defaultdict


REQUIRED_FIELDS = {
    "id",
    "mapName",
    "grenadeType",
    "projectilePosition",
    "projectileVelocity",
    "landingPosition",
}

KNOWN_TYPES = {"smoke", "flash", "he", "molotov", "incgrenade", "decoy"}


def strip_entry(entry: dict) -> dict:
    """Keep only the fields NadeSystem needs."""
    result = {
        "id":                 entry["id"],
        "mapName":            entry["mapName"],
        "grenadeType":        entry["grenadeType"].lower(),
        "projectilePosition": entry["projectilePosition"],
        "projectileVelocity": entry["projectileVelocity"],
        "landingPosition":    entry["landingPosition"],
    }
    # Keep description if present (used by NadeSystem for TeamTag parsing)
    if "description" in entry:
        result["description"] = entry["description"]
    return result


def validate_entry(entry: dict, filename: str, index: int) -> bool:
    missing = REQUIRED_FIELDS - set(entry.keys())
    if missing:
        print(f"  WARNING [{filename}][{index}] !goto {entry.get('id', 'N/A')}: missing fields {missing} — skipped")
        return False

    # Validate Vec3 sub-objects
    for vec_field in ("projectilePosition", "projectileVelocity", "landingPosition"):
        v = entry.get(vec_field)
        if not isinstance(v, dict) or not all(k in v for k in ("x", "y", "z")):
            print(f"  WARNING [{filename}][{index}] !goto {entry.get('id', 'N/A')}: invalid {vec_field} — skipped")
            return False

    gtype = entry["grenadeType"].lower()
    if gtype not in KNOWN_TYPES:
        print(f"  WARNING [{filename}][{index}]: unknown grenadeType '{gtype}' — kept anyway")

    return True


def convert_file(input_path: str, output_dir: str) -> int:
    """Convert one NadeLauncher JSON file. Returns number of entries written."""
    fname = os.path.basename(input_path)

    try:
        with open(input_path, encoding="utf-8") as f:
            data = json.load(f)
    except Exception as e:
        print(f"  ERROR reading {fname}: {e}")
        return 0

    if not isinstance(data, list):
        print(f"  ERROR {fname}: expected a JSON array at root")
        return 0

    # Group valid entries by (mapName, grenadeType)
    groups: dict[tuple, list] = defaultdict(list)
    for i, entry in enumerate(data):
        if not validate_entry(entry, fname, i):
            continue
        key = (entry["mapName"], entry["grenadeType"].lower())
        groups[key].append(strip_entry(entry))

    os.makedirs(output_dir, exist_ok=True)
    written = 0

    for (map_name, gtype), items in sorted(groups.items()):
        out_filename = f"{map_name}_{gtype}.json"
        out_path     = os.path.join(output_dir, out_filename)

        # Merge with existing file if present (allows incremental updates)
        existing = []
        if os.path.exists(out_path):
            try:
                with open(out_path, encoding="utf-8") as f:
                    existing = json.load(f)
                existing_ids = {e["id"] for e in existing}
            except Exception:
                existing_ids = set()
        else:
            existing_ids = set()

        new_items = [it for it in items if it["id"] not in existing_ids]
        all_items = existing + new_items

        with open(out_path, "w", encoding="utf-8") as f:
            json.dump(all_items, f, indent=2, ensure_ascii=False)

        print(f"  → {out_filename}  "
              f"(total {len(all_items)}, +{len(new_items)} new)")
        written += len(new_items)

    return written


def main():
    nade_dir   = sys.argv[1] if len(sys.argv) > 1 else "./NadeLauncher"
    grenade_dir = sys.argv[2] if len(sys.argv) > 2 else "./NadeSystem"

    lineups_dir = os.path.join(nade_dir, "data", "lineups")
    output_dir  = os.path.join(grenade_dir, "grenades")

    if not os.path.isdir(lineups_dir):
        print(f"ERROR: NadeLauncher lineups directory not found:\n  {lineups_dir}")
        print("\nExpected structure:")
        print("  <NadeLauncherDir>/")
        print("    data/")
        print("      lineups/")
        print("        de_dust2.json")
        print("        de_mirage.json")
        print("        ...")
        sys.exit(1)

    json_files = [f for f in os.listdir(lineups_dir) if f.endswith(".json")]
    if not json_files:
        print(f"No .json files found in {lineups_dir}")
        sys.exit(0)

    print(f"Source : {lineups_dir}")
    print(f"Output : {output_dir}")
    print(f"Files  : {len(json_files)}\n")

    total = 0
    for fname in sorted(json_files):
        print(f"[{fname}]")
        count = convert_file(os.path.join(lineups_dir, fname), output_dir)
        total += count

    print(f"\nDone. {total} new grenade entries written to {output_dir}")


if __name__ == "__main__":
    main()
