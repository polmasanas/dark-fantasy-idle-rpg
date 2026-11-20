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
            Root = Ui.Overlay();
            _title = Ui.Label("While you were away", 36, Ui.Gold, true);
            Root.Add(_title);
            var box = Ui.PanelBox();
            box.style.marginTop = 16;
            _body = Ui.Label("", 28);
            _body.style.whiteSpace = WhiteSpace.Normal;
            box.Add(_body);
            Root.Add(box);
            Root.Add(Ui.Button("Collect", Collect, 32));
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
