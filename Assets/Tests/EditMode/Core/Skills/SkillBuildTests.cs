using System.Collections.Generic;
using System.Linq;
using DarkFantasy.Core.Characters;
using DarkFantasy.Core.Items;
using DarkFantasy.Core.Skills;
using DarkFantasy.Core.Stats;
using NUnit.Framework;

namespace DarkFantasy.Core.Tests.Skills
{
    public class SkillBuildTests
    {
        private static CharacterState Hero(int level, ClassId cls, params (string id, int rank)[] ranks)
        {
            var h = CharacterState.Create(cls);
            h.Level = level;
            foreach (var r in ranks) h.SkillRanks[r.id] = r.rank;
            return h;
        }

        private static StatInputs Inputs(CharacterState h) =>
            new CharacterStatsBuilder(TestConfig.Load()).Build(h, new List<ItemInstance>());

        [Test]
        public void Toughness_Rank2_Adds12PctHp()
        {
            var plain = Inputs(Hero(30, ClassId.Fighter));
            var tough = Inputs(Hero(30, ClassId.Fighter, ("fighter_toughness", 2)));
            Assert.AreEqual(plain.PctHp + 0.12, tough.PctHp, 1e-12);
        }

        [Test]
        public void KeenEye_AddsCritChance_CapRespected()
        {
            var c = TestConfig.Load();
            var calc = new StatCalculator(c);
            var plain = calc.Compute(Inputs(Hero(30, ClassId.Archer)));
            var keen = calc.Compute(Inputs(Hero(30, ClassId.Archer, ("archer_keen_eye", 5))));
            Assert.AreEqual(System.Math.Min(c.Player.CritChanceCap, plain.CritChance + 0.10), keen.CritChance, 1e-12);
        }

        [Test]
        public void GlassCannon_AddsDamage_RemovesArmour()
        {
            var plain = Inputs(Hero(80, ClassId.Mage));
            var gc = Inputs(Hero(80, ClassId.Mage, ("mage_glass_cannon", 1)));
            Assert.AreEqual(plain.PctDamage + 0.08, gc.PctDamage, 1e-12);
            Assert.AreEqual(plain.PctArmour - 0.04, gc.PctArmour, 1e-12);
        }

        [Test]
        public void NoRanks_StatsIdenticalToM5()
        {
            var withSkills = TestConfig.Load();
            var without = TestConfig.LoadWithoutSkills();
            var h = Hero(30, ClassId.Fighter);
            var a = new StatCalculator(withSkills).Compute(new CharacterStatsBuilder(withSkills).Build(h, new List<ItemInstance>()));
            var b = new StatCalculator(without).Compute(new CharacterStatsBuilder(without).Build(h, new List<ItemInstance>()));
            Assert.AreEqual(b.HitDamage, a.HitDamage);
            Assert.AreEqual(b.MaxHp, a.MaxHp);
            Assert.AreEqual(b.Armour, a.Armour);
            Assert.AreEqual(b.AttackInterval, a.AttackInterval);
            Assert.AreEqual(b.CritChance, a.CritChance);
            Assert.AreEqual(b.CritDamage, a.CritDamage);
            Assert.AreEqual(b.CounterChance, a.CounterChance);
        }

        [Test]
        public void Kit_ActivesInSlotOrder_WithRankCooldown()
        {
            var h = Hero(60, ClassId.Fighter, ("fighter_heavy_swing", 3), ("fighter_toughness", 2), ("fighter_rend", 1));
            h.Loadout[0] = "fighter_rend";
            h.Loadout[2] = "fighter_heavy_swing";
            var kit = new SkillBuild(TestConfig.Load()).Kit(h);
            Assert.AreEqual(2, kit.Actives.Count);
            Assert.AreEqual("fighter_rend", kit.Actives[0].Node.Id);
            Assert.AreEqual(0, kit.Actives[0].Slot);
            Assert.AreEqual(10.0, kit.Actives[0].Cooldown, 1e-12);
            Assert.AreEqual("fighter_heavy_swing", kit.Actives[1].Node.Id);
            Assert.AreEqual(3, kit.Actives[1].Rank);
            Assert.AreEqual(8.0 * (1 - 0.05 * 2), kit.Actives[1].Cooldown, 1e-12);
        }

        [Test]
        public void Kit_UnslottedActives_NotIncluded()
        {
            var h = Hero(10, ClassId.Fighter, ("fighter_heavy_swing", 1));
            Assert.IsEmpty(new SkillBuild(TestConfig.Load()).Kit(h).Actives);
        }

        [Test]
        public void CooldownReduction_ClampedAtHalf()
        {
            var c = TestConfig.Load();
            var h = Hero(200, ClassId.Archer, ("archer_aimed_shot", 1), ("archer_wind_step", 5));
            h.Loadout[0] = "archer_aimed_shot";
            var kit = new SkillBuild(c).Kit(h, extraCooldownReduction: 0.9);
            Assert.AreEqual(7.0 * (1 - c.SkillRules.CooldownReductionCap), kit.Actives[0].Cooldown, 1e-12);
            Assert.Greater(kit.Actives[0].Cooldown, 0);
        }

        [Test]
        public void WindStep_Rank5_Cuts20Pct()
        {
            var h = Hero(200, ClassId.Archer, ("archer_aimed_shot", 1), ("archer_wind_step", 5));
            h.Loadout[0] = "archer_aimed_shot";
            Assert.AreEqual(7.0 * 0.8, new SkillBuild(TestConfig.Load()).Kit(h).Actives[0].Cooldown, 1e-12);
        }

        [Test]
        public void Dodge_ClampedAtCap()
        {
            var c = TestConfig.Load();
            var h = Hero(200, ClassId.Archer, ("archer_evasive", 5));
            var kit = new SkillBuild(c).Kit(h, extraDodge: 0.9);
            Assert.AreEqual(c.SkillRules.HeroDodgeCap, kit.Dodge, 1e-12);
            Assert.AreEqual(0.15, new SkillBuild(c).Kit(h).Dodge, 1e-12);
        }

        [Test]
        public void Kit_Passives_FromRanks()
        {
            var h = Hero(200, ClassId.Fighter, ("fighter_second_wind", 2), ("fighter_bloodlust", 3), ("fighter_unbreakable", 1),
                ("fighter_stand_firm", 4));
            var kit = new SkillBuild(TestConfig.Load()).Kit(h);
            Assert.AreEqual(0.20, kit.HealBelowPct, 1e-12);
            Assert.AreEqual(0.3, kit.HealBelowAt, 1e-12);
            Assert.AreEqual(0.03, kit.Lifesteal, 1e-12);
            Assert.AreEqual(0.05, kit.CheatDeathHealPct, 1e-12);
            Assert.AreEqual(0.20, kit.BossDamageTakenCut, 1e-12);
            Assert.IsTrue(kit.HasPassives);
        }

        [Test]
        public void Kit_ConditionalDamage_GroupedByNode()
        {
            var h = Hero(200, ClassId.Archer, ("archer_hunters_mark", 2));
            var kit = new SkillBuild(TestConfig.Load()).Kit(h);
            Assert.AreEqual(2, kit.ConditionalDamage.Count);
            Assert.IsTrue(kit.ConditionalDamage.All(b => b.NodeId == "archer_hunters_mark" && System.Math.Abs(b.Value - 0.16) < 1e-12));
        }

        [Test]
        public void EmptyKit_HasNoPassives()
        {
            var kit = new SkillBuild(TestConfig.Load()).Kit(Hero(30, ClassId.Mage));
            Assert.IsEmpty(kit.Actives);
            Assert.IsFalse(kit.HasPassives);
            Assert.IsFalse(SkillKit.None.HasPassives);
        }
    }
}
