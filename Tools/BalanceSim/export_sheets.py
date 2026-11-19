#!/usr/bin/env python3
"""
Exports balance_config.json into the rows of the Google Sheets "Config" tab (D-031).

Prints JSON: {"rows": [[section, key, value, note], ...], "cells": {"section.key": "Config!$C$n"}}
Only scalar numbers go to Config. List values (rarity tables) live on the "Rarity" tab.
The Sheets workbook references Config cells by address, so keep the row order stable:
new keys are appended at the end of their section, never inserted in the middle.

Usage: python3 export_sheets.py [--config balance_config.json]
"""
import argparse
import json
import os

HEADER_ROWS = 3  # title, blank, column headers


def flatten(cfg):
    for section, body in cfg.items():
        if section.startswith("_") or not isinstance(body, dict):
            continue
        for key, val in body.items():
            if key.startswith("_"):
                continue
            if isinstance(val, dict):
                for sub, v in val.items():
                    if isinstance(v, dict):
                        for k2, v2 in v.items():
                            if isinstance(v2, (int, float)) and not isinstance(v2, bool):
                                yield section, f"{key}.{sub}.{k2}", v2
                    elif isinstance(v, (int, float)) and not isinstance(v, bool):
                        yield section, f"{key}.{sub}", v
            elif isinstance(val, (int, float)) and not isinstance(val, bool):
                yield section, key, val


def main():
    here = os.path.dirname(os.path.abspath(__file__))
    ap = argparse.ArgumentParser()
    ap.add_argument("--config", default=os.path.join(here, "balance_config.json"))
    args = ap.parse_args()
    cfg = json.load(open(args.config))
    rows, cells = [], {}
    for i, (section, key, val) in enumerate(flatten(cfg)):
        rows.append([section, key, val])
        cells[f"{section}.{key}"] = f"Config!$C${HEADER_ROWS + 1 + i}"
    print(json.dumps({"rows": rows, "cells": cells}))


if __name__ == "__main__":
    main()
