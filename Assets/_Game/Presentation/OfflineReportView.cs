using System;
using System.Linq;
using DarkFantasy.Core.Offline;
using DarkFantasy.Core.Session;
using DarkFantasy.Core.Util;
using UnityEngine.UIElements;

namespace DarkFantasy.Presentation
{
    /// <summary>"While you were away" panel (M4-1): what happened offline, and Collect for the held loot.</summary>
    internal sealed class OfflineReportView
    {
        public readonly VisualElement Root;
        private readonly GameSession _session;
        private readonly Action<CollectReport> _collected;
        private readonly Label _title;
        private readonly Label _body;

        public bool IsOpen => Root.style.display == DisplayStyle.Flex;

        public OfflineReportView(GameSession session, Action<CollectReport> collected)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _collected = collected;
            // Owner, CD playtest: a pop-up window over the dimmed game, not a full page.
            var (backdrop, window) = Ui.Modal();
            Root = backdrop;
            _title = Ui.Title("While you were away", 34, Ui.Gold);
            _title.style.flexShrink = 0;
            window.Add(_title);
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexShrink = 1;
            scroll.style.marginTop = 12;
            scroll.style.marginBottom = 16;
            scroll.style.alignSelf = Align.Stretch;
            _body = Ui.Label("", 26);
            _body.style.whiteSpace = WhiteSpace.Normal;
            scroll.Add(_body);
            window.Add(scroll);
            var collect = Ui.Button("Collect", Collect, 30);
            collect.style.flexShrink = 0; // always reachable, even with a long report
            collect.style.minWidth = 240;
            window.Add(collect);
        }

        /// <param name="report">Null when only loot from an earlier absence is waiting.</param>
        public void Show(OfflineReport report, OfflineCredit credit, double efficiency, string extra = null)
        {
            var st = _session.State;
            var text = "";
            if (report != null)
            {
                text += $"Away: {Ui.Duration(credit.CountedSeconds)}  ->  {Ui.Duration(report.CreditedSeconds)} of fighting ({efficiency:P0})\n";
                switch (credit.Adjustment)
                {
                    case OfflineAdjustment.Capped: text += "(time away is capped)\n"; break;
                    case OfflineAdjustment.ClampedToUptime: text += "(the device clock jumped: only real time counts)\n"; break;
                    case OfflineAdjustment.ClockRolledBack: text += "(the device clock moved back: no time counted)\n"; break;
                }
                text += $"\nEnemies defeated: {report.Kills}\n";
                if (report.BossesDefeated + report.BossesFailed > 0)
                    text += $"Bosses: {report.BossesDefeated} defeated, {report.BossesFailed} failed\n";
                if (report.LevelsGained > 0) text += $"Levels gained: {report.LevelsGained}\n";
                text += $"Gold: +{NumberFormat.Short(report.Gold)}\n";
                text += $"Materials: +{NumberFormat.Short(report.Materials)}" +
                        (report.AutoSalvaged > 0 ? $" ({report.AutoSalvaged} items auto-salvaged)\n" : "\n");
            }
            int free = st.Inventory.Capacity - (st.Inventory.Items.Count - st.Inventory.WornIds.Count);
            text += $"\nItems waiting: {st.PendingOfflineLoot.Count}   (bag space: {Math.Max(0, free)})";
            var byRarity = st.PendingOfflineLoot.GroupBy(i => i.Rarity).OrderByDescending(g => g.Key)
                .Select(g => $"{g.Key} {g.Count()}");
            if (st.PendingOfflineLoot.Count > 0) text += "\n" + string.Join(" · ", byRarity);
            int upgrades = st.PendingOfflineLoot.Count(_session.IsUpgrade);
            if (upgrades > 0) text += $"\n▲ {upgrades} would be upgrades (equip them in the Bag)";
            if (st.PendingOfflineLoot.Count > free) text += "\nItems that do not fit are salvaged into materials.";
            if (!string.IsNullOrEmpty(extra)) text += "\n\n" + extra;
            _body.text = text;
            Root.style.display = DisplayStyle.Flex;
        }

        private void Collect()
        {
            var result = _session.Collect();
            Root.style.display = DisplayStyle.None;
            _collected?.Invoke(result);
        }
    }
}
