"""Multi-seed robustness check: python3 seeds.py [n_seeds] [days]"""
import json, sys, statistics as st, os
import balance_sim as b
here = os.path.dirname(os.path.abspath(__file__))
cfg = json.load(open(os.path.join(here, 'balance_config.json')))
n = int(sys.argv[1]) if len(sys.argv) > 1 else 5
days = int(sys.argv[2]) if len(sys.argv) > 2 else 30
print(f"| Profile | Prestiges (median) | Best boss min / median / max | Day-7 best boss (median) |")
print("|---|---:|---:|---:|")
for name, prof in b.PROFILES.items():
    pres, best, d7 = [], [], []
    for seed in range(n):
        s = b.Sim(cfg, prof, seed); s.run_schedule(days)
        bosses = [(t, int(m.split()[1])) for t, m in s.events if m.startswith('boss')]
        best.append(max([x for _, x in bosses] or [0]))
        d7.append(max([x for t, x in bosses if t <= 7 * 86400] or [0]))
        pres.append(s.meta.prestiges)
    print(f"| {name} | {st.median(pres):g} | {min(best)} / {st.median(best):g} / {max(best)} | {st.median(d7):g} |")
