using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DarkFantasy.Core.Config;
using DarkFantasy.Core.Look;
using DarkFantasy.Core.Session;
using DarkFantasy.Core.Skills;
using DarkFantasy.Core.Util;
using UnityEngine.UIElements;

namespace DarkFantasy.Presentation
{
    /// <summary>
    /// The active hero's Common skill tree (M6, spec §7): loadout in priority order, four tiers of nodes with
    /// ranks, auto-spend and respec. Draws only: every change goes through <see cref="GameSession"/>.
    /// </summary>
    internal sealed class SkillsView
    {
        public readonly VisualElement Root;
        private readonly GameSession _session;
        private readonly BalanceConfig _config;
        private readonly LookConfig _look;
        private readonly Label _title;
        private readonly VisualElement _loadout;
        private readonly VisualElement _tree;
        private readonly Label _detail;
        private readonly Toggle _autoSpend;
        private readonly Button _respec;
        private string _selected;
        private bool _confirmRespec;

        public SkillsView(GameSession session, BalanceConfig config, LookConfig look)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _config = config;
            _look = look;
            Root = Ui.Overlay();

            var top = Ui.Row(Justify.SpaceBetween);
            top.style.flexShrink = 0;
            _title = Ui.Title("", 34);
            top.Add(_title);
            top.Add(Ui.Button("Done", Close));
            Root.Add(top);

            Root.Add(Ui.Label("Loadout (slot 1 fires first)", 24, Ui.Muted));
            _loadout = Ui.Column();
            _loadout.style.flexShrink = 0;
            Root.Add(_loadout);

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            scroll.style.flexShrink = 1;
            scroll.style.flexBasis = 0;
            scroll.style.marginTop = 8;
            _tree = Ui.Column();
            scroll.Add(_tree);
            Root.Add(scroll);

            _detail = Ui.Label("Tap a skill to see what it does.", 24, Ui.Text);
            _detail.style.whiteSpace = WhiteSpace.Normal;
            _detail.style.minHeight = 90;
            _detail.style.flexShrink = 0;
            Root.Add(_detail);

            var bottom = Ui.Row(Justify.SpaceBetween);
            bottom.style.flexShrink = 0;
            _autoSpend = new Toggle("Spend points automatically");
            _autoSpend.style.fontSize = 24;
            _autoSpend.labelElement.style.color = Ui.Text;
            _autoSpend.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue == _session.State.Character.AutoSpendSkills) return;
                _session.SetAutoSpendSkills(evt.newValue);
                Refresh();
            });
            bottom.Add(_autoSpend);
            _respec = Ui.Button("", OnRespec, 22);
            bottom.Add(_respec);
            Root.Add(bottom);
        }

        public void Open()
        {
            _selected = null;
            _confirmRespec = false;
            Refresh();
            Root.style.display = DisplayStyle.Flex;
        }

        private void Close() => Root.style.display = DisplayStyle.None;

        private SkillTree Tree => _session.Skills;

        private string Name(string id) => _look.Skills.TryGetValue(id, out var s) ? s.Name : id;

        private void Refresh()
        {
            var hero = _session.State.Character;
            int unspent = Tree.Unspent(hero);
            _title.text = $"Skills · {Capitalise(hero.Class.ToString())} · {unspent} point{(unspent == 1 ? "" : "s")}";
            _autoSpend.SetValueWithoutNotify(hero.AutoSpendSkills);
            _respec.text = _confirmRespec ? $"Confirm reset · {NumberFormat.Short(Tree.RespecCost(hero))} g"
                : $"Reset tree · {NumberFormat.Short(Tree.RespecCost(hero))} g";
            _respec.SetEnabled(Tree.Spent(hero) > 0);

            _loadout.Clear();
            for (int slot = 0; slot < hero.Loadout.Length; slot++)
            {
                var row = Ui.Row(Justify.SpaceBetween);
                row.style.marginTop = 4;
                string id = hero.Loadout[slot];
                row.Add(Ui.Label($"{slot + 1}  " + (id == null ? "—" : Name(id)), 26, id == null ? Ui.Muted : Ui.Gold, true));
                var controls = Ui.Row();
                int s = slot;
                if (slot > 0) controls.Add(Ui.Button("Up", () => { _session.SwapSlots(s, s - 1); Refresh(); }, 22));
                if (slot < hero.Loadout.Length - 1) controls.Add(Ui.Button("Down", () => { _session.SwapSlots(s, s + 1); Refresh(); }, 22));
                if (id != null) controls.Add(Ui.Button("Remove", () => { _session.UnslotSkill(s); Refresh(); }, 22));
                row.Add(controls);
                _loadout.Add(row);
            }

            _tree.Clear();
            var nodes = _config.Skills.For(hero.Class).ToList();
            for (int tier = 1; tier <= 4; tier++)
            {
                bool open = Tree.TierOpen(hero, tier);
                var header = Ui.Label(open ? $"Tier {tier}" : $"Tier {tier} · opens at {(tier - 1) * _config.SkillRules.TierPoints} points spent",
                    26, open ? Ui.Text : Ui.Muted, true);
                header.style.marginTop = 10;
                _tree.Add(header);
                foreach (var node in nodes.Where(n => n.Tier == tier))
                    _tree.Add(NodeRow(node, open));
            }

            if (_selected != null) ShowDetail(_config.Skills.Find(_selected));
        }

        private VisualElement NodeRow(SkillNode node, bool tierOpen)
        {
            var hero = _session.State.Character;
            int rank = Tree.Rank(hero, node.Id);
            var row = Ui.Row(Justify.SpaceBetween);
            row.style.marginTop = 4;
            if (node.Id == _selected) row.style.backgroundColor = Ui.Selected;

            string kind = node.Kind == SkillKind.Active ? "active" : "passive";
            var name = Ui.Button($"{Name(node.Id)}  {rank}/{node.MaxRank}  ({kind})", () => { _selected = node.Id; Refresh(); }, 24);
            name.style.flexGrow = 1;
            name.style.unityTextAlign = UnityEngine.TextAnchor.MiddleLeft;
            if (!tierOpen) name.style.color = Ui.Muted;
            row.Add(name);

            var controls = Ui.Row();
            var plus = Ui.Button("+1", () =>
            {
                _selected = node.Id;
                var r = _session.AddSkillRank(node.Id);
                if (r != SkillResult.Ok) _detail.text = Refusal(r);
                else Refresh();
            }, 22);
            plus.SetEnabled(Tree.CanAddRank(hero, node.Id) == SkillResult.Ok);
            controls.Add(plus);
            if (node.Kind == SkillKind.Active && rank > 0)
            {
                int current = Array.IndexOf(hero.Loadout, node.Id);
                for (int slot = 0; slot < hero.Loadout.Length; slot++)
                {
                    int s = slot;
                    var b = Ui.Button((s + 1).ToString(CultureInfo.InvariantCulture), () => { _session.SlotSkill(node.Id, s); Refresh(); }, 22);
                    b.SetEnabled(current != s);
                    controls.Add(b);
                }
            }
            row.Add(controls);
            return row;
        }

        private void ShowDetail(SkillNode node)
        {
            if (node == null) return;
            var hero = _session.State.Character;
            int rank = Tree.Rank(hero, node.Id);
            string text = _look.Skills.TryGetValue(node.Id, out var s) ? s.Description : node.Id;
            if (rank > 0) text += "\nNow: " + SkillText.Values(node, rank);
            if (rank < node.MaxRank) text += "\n" + (rank == 0 ? "Rank 1: " : "Next: ") + SkillText.Values(node, rank + 1);
            if (node.Kind == SkillKind.Active)
                text += "\n" + (rank > 0 ? "Put it in slot 1, 2 or 3 to use it." : "Learn it, then put it in a slot.");
            _detail.text = text;
        }

        private void OnRespec()
        {
            if (!_confirmRespec)
            {
                _confirmRespec = true;
                Refresh();
                return;
            }
            _confirmRespec = false;
            var r = _session.RespecSkills();
            Refresh();
            if (r == SkillResult.NotEnoughGold) _detail.text = "Not enough gold to reset the tree.";
            else if (r == SkillResult.Ok) _detail.text = "Tree reset: every point is back to spend.";
        }

        private static string Refusal(SkillResult r)
        {
            switch (r)
            {
                case SkillResult.NoPoints: return "No points left. You earn one every 5 levels.";
                case SkillResult.TierLocked: return "Spend more points in lower tiers to open this one.";
                case SkillResult.MaxRank: return "Already at its highest rank.";
                default: return "Can't do that right now.";
            }
        }

        private static string Capitalise(string s) =>
            string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);
    }

    /// <summary>Short value summaries of a skill at a rank, read from skills_config.json.</summary>
    internal static class SkillText
    {
        public static string Values(SkillNode node, int rank)
        {
            var parts = new List<string>();
            foreach (var e in node.Effects)
            {
                switch (e.Kind)
                {
                    case EffectKind.Hit:
                        string hit = Pct(e.At("percent", rank)) + " damage" + (e.Hits > 1 ? $" × {e.Hits}" : "");
                        if (e.Element != DarkFantasy.Core.Combat.Element.Physical) hit += " " + e.Element;
                        parts.Add(hit);
                        break;
                    case EffectKind.Dot:
                        parts.Add($"{Pct(e.At("percent", rank))} {e.Element} damage over {e.At("duration", rank):0.#} s");
                        break;
                    case EffectKind.Buff:
                        parts.Add($"+{Pct(e.At("value", rank))} damage for {e.At("duration", rank):0.#} s");
                        break;
                    case EffectKind.EnemySlow:
                        parts.Add($"enemy attacks {Pct(e.At("value", rank))} slower for {e.At("duration", rank):0.#} s");
                        break;
                    default:
                        parts.Add((e.At("value", rank) >= 0 ? "+" : "") + Pct(e.At("value", rank)));
                        break;
                }
            }
            if (node.Kind == SkillKind.Active)
                parts.Add($"cooldown {node.CooldownS * Math.Max(0, 1 - node.RankCdr * (rank - 1)):0.#} s");
            return string.Join(", ", parts);
        }

        private static string Pct(double v) => (v * 100).ToString("0.#", CultureInfo.InvariantCulture) + "%";
    }
}
