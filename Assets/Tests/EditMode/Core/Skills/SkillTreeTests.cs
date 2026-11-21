using DarkFantasy.Core.Characters;
using DarkFantasy.Core.Items;
using DarkFantasy.Core.Skills;
using DarkFantasy.Core.Stats;
using NUnit.Framework;

namespace DarkFantasy.Core.Tests.Skills
{
    public class SkillTreeTests
    {
        private static SkillTree Tree() => new SkillTree(TestConfig.Load());

        private static CharacterState Hero(int level, ClassId cls = ClassId.Fighter)
        {
            var h = CharacterState.Create(cls);
            h.Level = level;
            return h;
        }

        [Test]
        public void Earned_Level24_Is4_Level25_Is5()
        {
            Assert.AreEqual(4, Tree().Earned(Hero(24)));
            Assert.AreEqual(5, Tree().Earned(Hero(25)));
        }

        [Test]
        public void NewHero_HasEmptyLoadoutOfConfiguredSize()
        {
            var h = Hero(1);
            Assert.AreEqual(TestConfig.Load().Combat.ActiveSkillSlots, h.Loadout.Length);
            Assert.IsEmpty(h.SkillRanks);
            Assert.IsFalse(h.AutoSpendSkills);
        }

        [Test]
        public void AddRank_NoPoints_Refused()
        {
            var h = Hero(4);
            Assert.AreEqual(SkillResult.NoPoints, Tree().AddRank(h, "fighter_heavy_swing"));
            Assert.IsEmpty(h.SkillRanks);
        }

        [Test]
        public void AddRank_UnknownOrOtherClass_Refused()
        {
            var h = Hero(50);
            Assert.AreEqual(SkillResult.UnknownSkill, Tree().AddRank(h, "nope"));
            Assert.AreEqual(SkillResult.UnknownSkill, Tree().AddRank(h, "archer_aimed_shot"));
        }

        [Test]
        public void Tier2_LockedUntil5Spent()
        {
            var t = Tree();
            var h = Hero(50);
            Assert.IsFalse(t.TierOpen(h, 2));
            Assert.AreEqual(SkillResult.TierLocked, t.AddRank(h, "fighter_rend"));
            for (int i = 0; i < 3; i++) Assert.AreEqual(SkillResult.Ok, t.AddRank(h, "fighter_heavy_swing"));
            for (int i = 0; i < 2; i++) Assert.AreEqual(SkillResult.Ok, t.AddRank(h, "fighter_toughness"));
            Assert.IsTrue(t.TierOpen(h, 2));
            Assert.AreEqual(SkillResult.Ok, t.AddRank(h, "fighter_rend"));
        }

        [Test]
        public void MaxRank_Refused()
        {
            var t = Tree();
            var h = Hero(50);
            for (int i = 0; i < 3; i++) t.AddRank(h, "fighter_heavy_swing");
            Assert.AreEqual(SkillResult.MaxRank, t.AddRank(h, "fighter_heavy_swing"));
            Assert.AreEqual(3, h.SkillRanks["fighter_heavy_swing"]);
        }

        [Test]
        public void Unspent_IsEarnedMinusRanks()
        {
            var t = Tree();
            var h = Hero(30); // 6 points
            t.AddRank(h, "fighter_heavy_swing");
            t.AddRank(h, "fighter_toughness");
            Assert.AreEqual(2, t.Spent(h));
            Assert.AreEqual(4, t.Unspent(h));
        }

        [Test]
        public void Slot_PassiveRefused()
        {
            var t = Tree();
            var h = Hero(10);
            t.AddRank(h, "fighter_toughness");
            Assert.AreEqual(SkillResult.NotActive, t.Slot(h, "fighter_toughness", 0));
        }

        [Test]
        public void Slot_Unlearned_Refused()
        {
            Assert.AreEqual(SkillResult.NotLearned, Tree().Slot(Hero(10), "fighter_heavy_swing", 0));
        }

        [Test]
        public void Slot_AlreadySlotted_Moves()
        {
            var t = Tree();
            var h = Hero(10);
            t.AddRank(h, "fighter_heavy_swing");
            Assert.AreEqual(SkillResult.Ok, t.Slot(h, "fighter_heavy_swing", 0));
            Assert.AreEqual(SkillResult.Ok, t.Slot(h, "fighter_heavy_swing", 2));
            Assert.IsNull(h.Loadout[0]);
            Assert.AreEqual("fighter_heavy_swing", h.Loadout[2]);
        }

        [Test]
        public void Swap_And_Unslot()
        {
            var t = Tree();
            var h = Hero(60);
            for (int i = 0; i < 3; i++) t.AddRank(h, "fighter_heavy_swing");
            for (int i = 0; i < 2; i++) t.AddRank(h, "fighter_toughness");
            t.AddRank(h, "fighter_rend");
            t.Slot(h, "fighter_heavy_swing", 0);
            t.Slot(h, "fighter_rend", 1);
            t.Swap(h, 0, 1);
            Assert.AreEqual("fighter_rend", h.Loadout[0]);
            Assert.AreEqual("fighter_heavy_swing", h.Loadout[1]);
            t.Unslot(h, 0);
            Assert.IsNull(h.Loadout[0]);
        }

        [Test]
        public void Respec_ClearsRanksAndLoadout_ChargesGold()
        {
            var t = Tree();
            var h = Hero(20);
            t.AddRank(h, "fighter_heavy_swing");
            t.Slot(h, "fighter_heavy_swing", 0);
            double cost = t.RespecCost(h);
            var r = TestConfig.Load().SkillRules;
            Assert.AreEqual(r.RespecBase * System.Math.Pow(r.RespecGrowth, 19), cost, 1e-9);
            var w = new Wallet { Gold = cost + 5 };
            Assert.AreEqual(SkillResult.Ok, t.Respec(h, w));
            Assert.IsEmpty(h.SkillRanks);
            Assert.IsNull(h.Loadout[0]);
            Assert.AreEqual(5, w.Gold, 1e-9);
        }

        [Test]
        public void Respec_NotEnoughGold_ChangesNothing()
        {
            var t = Tree();
            var h = Hero(20);
            t.AddRank(h, "fighter_heavy_swing");
            var w = new Wallet { Gold = t.RespecCost(h) - 1 };
            double before = w.Gold;
            Assert.AreEqual(SkillResult.NotEnoughGold, t.Respec(h, w));
            Assert.AreEqual(1, h.SkillRanks["fighter_heavy_swing"]);
            Assert.AreEqual(before, w.Gold);
        }

        [Test]
        public void AutoSpend_FollowsSuggestedOrder_AndSlotsActives()
        {
            var t = Tree();
            var h = Hero(30); // 6 points
            var order = TestConfig.Load().Skills.SuggestedOrder[ClassId.Fighter];
            Assert.AreEqual(6, t.AutoSpend(h));
            Assert.AreEqual(0, t.Unspent(h));
            var expected = new System.Collections.Generic.Dictionary<string, int>();
            for (int i = 0; i < 6; i++)
            {
                expected.TryGetValue(order[i], out int r);
                expected[order[i]] = r + 1;
            }
            CollectionAssert.AreEquivalent(expected, h.SkillRanks);
            Assert.AreEqual("fighter_heavy_swing", h.Loadout[0]);
            Assert.AreEqual("fighter_rend", h.Loadout[1]);
        }

        [Test]
        public void AutoSpend_FullTree_StopsAtMax()
        {
            var t = Tree();
            var h = Hero(400); // 80 points, tree holds 52
            Assert.AreEqual(52, t.AutoSpend(h));
            Assert.AreEqual(28, t.Unspent(h));
            Assert.AreEqual(0, t.AutoSpend(h));
        }

        [Test]
        public void Sanitize_DropsUnknownIds_AndRefunds()
        {
            var t = Tree();
            var h = Hero(30);
            t.AddRank(h, "fighter_heavy_swing");
            h.SkillRanks["gone_skill"] = 2;
            h.Loadout[1] = "gone_skill";
            t.Sanitize(h);
            Assert.IsFalse(h.SkillRanks.ContainsKey("gone_skill"));
            Assert.IsNull(h.Loadout[1]);
            Assert.AreEqual(5, t.Unspent(h));
        }

        [Test]
        public void Sanitize_SpentAboveEarned_ResetsTree()
        {
            var t = Tree();
            var h = Hero(9); // 1 point
            h.SkillRanks["fighter_heavy_swing"] = 3;
            h.Loadout[0] = "fighter_heavy_swing";
            t.Sanitize(h);
            Assert.IsEmpty(h.SkillRanks);
            Assert.IsNull(h.Loadout[0]);
        }

        [Test]
        public void Sanitize_RankAboveMax_ResetsTree()
        {
            var t = Tree();
            var h = Hero(200);
            h.SkillRanks["fighter_heavy_swing"] = 9;
            t.Sanitize(h);
            Assert.IsEmpty(h.SkillRanks);
        }
    }
}
