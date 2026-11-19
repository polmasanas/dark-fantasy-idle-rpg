using System;
using System.Collections.Generic;
using DarkFantasy.Core.Formulas;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace DarkFantasy.Core.Tests.Formulas
{
    /// <summary>
    /// C# formulas must equal the Python reference (Tools/BalanceSim, decision D-031).
    /// Regenerate with: python3 Tools/BalanceSim/balance_sim.py --golden Tools/BalanceSim/results/golden_values.json
    /// </summary>
    public class FormulaGoldenTests
    {
        private const double RelTol = 1e-9;

        [Test]
        public void Golden_AllMatchPython()
        {
            var f = new GameFormulas(TestConfig.Load());
            var rows = JArray.Parse(TestPaths.ReadRepoFile("Tools/BalanceSim/results/golden_values.json"));
            var failures = new List<string>();
            foreach (JObject row in rows)
            {
                string fn = (string)row["fn"];
                var a = (JArray)row["args"];
                double expected = (double)row["value"];
                double actual = Evaluate(f, fn, a);
                if (!Close(expected, actual))
                    failures.Add($"{fn}({string.Join(", ", a)}) expected {expected:R} got {actual:R}");
            }
            Assert.IsEmpty(failures, string.Join("\n", failures));
            Assert.That(rows.Count, Is.GreaterThan(400));
        }

        [Test]
        public void Formulas_StayFinite_AtLevel5000()
        {
            var f = new GameFormulas(TestConfig.Load());
            Assert.IsFalse(double.IsInfinity(f.EnemyHp(5000, true)) || double.IsNaN(f.EnemyHp(5000, true)));
            Assert.IsFalse(double.IsInfinity(f.XpToNext(5000)) || double.IsNaN(f.XpToNext(5000)));
            Assert.IsFalse(double.IsInfinity(f.GoldDrop(5000, true)));
        }

        private static double Evaluate(GameFormulas f, string fn, JArray a)
        {
            switch (fn)
            {
                case "KillsToAdvance": return f.KillsToAdvance((int)a[0]);
                case "BossLevel": return f.BossLevel((int)a[0]);
                case "SubzoneLevel": return f.SubzoneLevel((int)a[0], (int)a[1]);
                case "DustForRun": return f.DustForRun((int)a[0]);
                case "DustForPrestige": return f.DustForPrestige((int)a[0], (int)a[1]);
                case "EnemyHp": return f.EnemyHp((int)a[0], (bool)a[1]);
                case "EnemyAtk": return f.EnemyAtk((int)a[0], (bool)a[1]);
                case "GoldDrop": return f.GoldDrop((int)a[0], (bool)a[1]);
                case "EnemyXp": return f.EnemyXp((int)a[0], (int)a[1], (bool)a[2]);
                case "XpToNext": return f.XpToNext((int)a[0]);
                case "ArmourK": return f.ArmourK((int)a[0]);
                case "UpgradeCost": return f.UpgradeCost((int)a[0], (int)a[1]);
                case "ArtifactCost": return f.ArtifactCost((string)a[0], (int)a[1]);
                case "ArtifactMult": return f.ArtifactMult((string)a[0], (int)a[1]);
                default: throw new ArgumentException("Unknown golden function " + fn);
            }
        }

        private static bool Close(double expected, double actual)
        {
            if (double.IsInfinity(expected) || double.IsInfinity(actual)) return expected.Equals(actual);
            double scale = Math.Max(Math.Abs(expected), 1e-12);
            return Math.Abs(expected - actual) / scale <= RelTol;
        }
    }
}
