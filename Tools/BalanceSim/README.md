# Balance Simulation

Reference implementation of the formulas in `Docs/GAME_BALANCE.md`. Python 3.10+, standard library only.

```bash
python3 balance_sim.py            # 30-day run, all profiles → results/REPORT.md + CSV time series
python3 balance_sim.py --days 7   # shorter run
python3 seeds.py 7 30             # robustness across 7 seeds → markdown table
```

- `balance_config.json` is the single source of balance numbers. The Unity `BalanceConfig` will be imported from it.
- `export_sheets.py` prints the rows for the Google Sheets **Config** tab (keep row order stable: the workbook references cells by address).
- Profiles (active, casual, offline_focused, farmer, optimiser, never_prestige) are defined in `PROFILES` in `balance_sim.py`.
- Change a number → re-run both scripts → update `Docs/GAME_BALANCE.md` §9 → log the change in `Docs/DECISIONS.md`.
