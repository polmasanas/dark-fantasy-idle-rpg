using System;
using System.Collections.Generic;
using System.Linq;
using DarkFantasy.Core.Config;
using DarkFantasy.Core.Events;
using DarkFantasy.Core.Items;
using DarkFantasy.Core.Session;
using DarkFantasy.Core.Util;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkFantasy.Presentation
{
    /// <summary>
    /// Inventory screen (M3-5): equipped slots with upgrade markers, inventory grid with sort and
    /// filter, item detail with stat differences, equip / upgrade / lock / salvage, filtered and
    /// automatic salvage, expansion. Nothing is ever equipped without a tap (R2-18).
    /// </summary>
    internal sealed class InventoryView
    {
        private static readonly EquipSlot[] Slots =
        {
            EquipSlot.MainHand, EquipSlot.OffHand, EquipSlot.Helmet, EquipSlot.Chest, EquipSlot.Legs, EquipSlot.Boots,
            EquipSlot.Armguards, EquipSlot.Gloves, EquipSlot.Ring1, EquipSlot.Ring2, EquipSlot.Amulet,
        };

        private static readonly string[] SortModes = { "Rarity", "Item level", "Slot" };
        private static readonly string[] FilterModes = { "All", "Upgrades", "Weapons", "Armour", "Jewellery" };
        private static readonly string[] AutoModes = { "Off", "Common", "Common + Uncommon", "Below Rare, not upgrade" };

        public readonly VisualElement Root;
        private readonly GameSession _session;
        private readonly BalanceConfig _config;
        private readonly VisualElement _equipped;
        private readonly VisualElement _grid;
        private readonly VisualElement _detail;
        private readonly Label _capacity;
        private readonly Button _expand;
        private string _sort = SortModes[0];
        private string _filter = FilterModes[0];
        private long _selected = -1;
        private bool _dirty = true;
        private float _sinceRebuild;
        private readonly VisualElement _confirm;
        private readonly Label _confirmText;
        private readonly Button _confirmYes;
        private System.Action _confirmAction = () => { };

        public bool IsOpen => Root.style.display == DisplayStyle.Flex;

        public InventoryView(GameSession session, BalanceConfig config)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _config = config ?? throw new ArgumentNullException(nameof(config));

            Root = new VisualElement();
            Root.style.position = Position.Absolute;
            Root.style.left = 0;
            Root.style.right = 0;
            Root.style.top = 0;
            Root.style.bottom = 0;
            Root.style.backgroundColor = Ui.Background;
            Root.style.paddingTop = 24;
            Root.style.paddingLeft = 16;
            Root.style.paddingRight = 16;
            Root.style.display = DisplayStyle.None;

            var top = Ui.Row(Justify.SpaceBetween);
            top.Add(Ui.Title("Equipment", 34));
            top.Add(Ui.Button("Equip upgrades", () => { _session.EquipUpgrades(); _dirty = true; }));
            top.Add(Ui.Button("Close", Close));
            Fixed(top);
            Root.Add(top);

            _equipped = Ui.Row();
            _equipped.style.flexWrap = Wrap.Wrap;
            Fixed(_equipped);
            Root.Add(_equipped);

            var bar = Ui.Row(Justify.SpaceBetween);
            bar.style.flexWrap = Wrap.Wrap;
            bar.style.marginTop = 10;
            Fixed(bar);
            _capacity = Ui.Label("", 26, Ui.Muted);
            bar.Add(_capacity);
            var sort = new DropdownField("Sort", SortModes.ToList(), 0);
            sort.labelElement.style.color = Ui.Text;
            sort.RegisterValueChangedCallback(e => { _sort = e.newValue; _dirty = true; });
            bar.Add(sort);
            var filter = new DropdownField("Show", FilterModes.ToList(), 0);
            filter.labelElement.style.color = Ui.Text;
            filter.RegisterValueChangedCallback(e => { _filter = e.newValue; _dirty = true; });
            bar.Add(filter);
            Root.Add(bar);

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            // The grid takes whatever height is left and scrolls; basis 0 stops its content height
            // from squeezing the fixed sections above and below it.
            scroll.style.flexGrow = 1;
            scroll.style.flexShrink = 1;
            scroll.style.flexBasis = 0;
            scroll.style.minHeight = 240;
            scroll.style.marginTop = 8;
            _grid = scroll.contentContainer;
            _grid.style.flexDirection = FlexDirection.Row;
            _grid.style.flexWrap = Wrap.Wrap;
            Root.Add(scroll);

            _detail = Ui.PanelBox();
            _detail.style.minHeight = 260;
            Fixed(_detail);
            Root.Add(_detail);

            var salvage = Ui.Row(Justify.SpaceBetween);
            salvage.style.flexWrap = Wrap.Wrap;
            salvage.style.paddingBottom = 16;
            Fixed(salvage);
            salvage.Add(Ui.Button("Salvage shown", SalvageShown));
            var auto = new DropdownField("Auto-salvage", AutoModes.ToList(), (int)_session.State.Inventory.AutoSalvage);
            auto.labelElement.style.color = Ui.Text;
            auto.RegisterValueChangedCallback(e =>
                _session.State.Inventory.AutoSalvage = (AutoSalvageMode)Array.IndexOf(AutoModes, e.newValue));
            salvage.Add(auto);
            _expand = Ui.Button("", Expand);
            salvage.Add(_expand);
            Root.Add(salvage);

            var (backdrop, window) = Ui.Modal(80f, 760f);
            _confirm = backdrop;
            _confirmText = Ui.Label("", 26);
            _confirmText.style.whiteSpace = WhiteSpace.Normal;
            window.Add(_confirmText);
            var buttons = Ui.Row(Justify.Center);
            buttons.style.marginTop = 16;
            _confirmYes = Ui.Button("Move", () => { });
            buttons.Add(_confirmYes);
            buttons.Add(Ui.Button("Cancel", () => _confirm.style.display = DisplayStyle.None));
            window.Add(buttons);
            Root.Add(_confirm);

            _session.Events.Subscribe<ItemDropped>(_ => _dirty = true);
            _session.Events.Subscribe<HeroSwitched>(_ => _dirty = true);
            _session.Events.Subscribe<LevelUp>(_ => _dirty = true);
        }

        public void Open()
        {
            Root.style.display = DisplayStyle.Flex;
            _dirty = true;
            Rebuild();
        }

        public void Close() => Root.style.display = DisplayStyle.None;

        /// <summary>Called every frame; rebuilds at most twice per second while open and changed.</summary>
        public void Tick(float deltaTime)
        {
            if (!IsOpen) return;
            _sinceRebuild += deltaTime;
            if (_dirty && _sinceRebuild >= 0.5f) Rebuild();
        }

        private void Rebuild()
        {
            _dirty = false;
            _sinceRebuild = 0;
            var st = _session.State;
            var inv = st.Inventory;
            var c = st.Character;

            _equipped.Clear();
            double scoreNow = _session.Score();
            foreach (var slot in Slots)
            {
                ItemInstance worn = c.Equipped.TryGetValue(slot, out var id) ? inv.Find(id) : null;
                bool upgradeWaiting = inv.Items.Any(i => !inv.WornIds.Contains(i.Id)
                                                         && ItemKeys.KindForSlot(slot) == i.Kind
                                                         && _session.Score(slot, i) > scoreNow * (1 + 1e-9));
                string text = SlotName(slot) + (upgradeWaiting ? " ▲" : "") + "\n"
                              + (worn == null ? "empty" : Short(worn));
                var cell = Ui.Button(text, () => { if (worn != null) Select(worn.Id); }, 20);
                cell.style.width = new Length(24f, LengthUnit.Percent);
                cell.style.minHeight = 90;
                Border(cell, worn == null ? Ui.Muted : RarityColor(worn.Rarity));
                _equipped.Add(cell);
            }

            var shown = Filtered().ToList();
            _capacity.text = $"Bag {_session.Inventory.UsedSpace(inv, st.Characters)}/{inv.Capacity} · Gold {NumberFormat.Short(st.Gold)} · Materials {NumberFormat.Short(st.Wallet.Materials)}";
            _expand.text = "+50 slots (" + NumberFormat.Short(_session.Inventory.ExpansionCost(inv)) + " gold)";

            _grid.Clear();
            foreach (var item in shown)
            {
                var it = item;
                string wearer = WornBy(it.Id);
                string marks = (_session.IsUpgrade(it) ? "▲ " : "") + (it.Locked ? "🔒" : "") + (wearer != null ? " " + wearer : "");
                var cell = Ui.Button(marks + "\n" + Short(it) + "\nLv " + it.ItemLevel, () => Select(it.Id), 20);
                cell.style.width = 150;
                cell.style.height = 120;
                Border(cell, RarityColor(it.Rarity));
                if (it.Id == _selected) cell.style.backgroundColor = Ui.Selected;
                _grid.Add(cell);
            }
            ShowDetail();
        }

        private IEnumerable<ItemInstance> Filtered()
        {
            var inv = _session.State.Inventory;
            // Items the active hero wears are in the equipment row; items other heroes wear stay listed (M5, D-004).
            var mine = _session.State.Character.Equipped;
            var items = inv.Items.Where(i => !mine.ContainsValue(i.Id));
            switch (_filter)
            {
                case "Upgrades": items = items.Where(_session.IsUpgrade); break;
                case "Weapons": items = items.Where(i => i.Kind == ItemKind.MainHand || i.Kind == ItemKind.OffHand); break;
                case "Armour": items = items.Where(i => i.Kind >= ItemKind.Helmet && i.Kind <= ItemKind.Gloves); break;
                case "Jewellery": items = items.Where(i => i.Kind == ItemKind.Ring || i.Kind == ItemKind.Amulet); break;
            }
            switch (_sort)
            {
                case "Item level": return items.OrderByDescending(i => i.ItemLevel).ThenByDescending(i => i.Rarity);
                case "Slot": return items.OrderBy(i => i.Kind).ThenByDescending(i => i.Rarity);
                default: return items.OrderByDescending(i => i.Rarity).ThenByDescending(i => i.ItemLevel);
            }
        }

        private void Select(long id)
        {
            _selected = id;
            _dirty = true;
            Rebuild();
        }

        private void ShowDetail()
        {
            _detail.Clear();
            var st = _session.State;
            var item = st.Inventory.Find(_selected);
            if (item == null)
            {
                _detail.Add(Ui.Label("Tap an item to see its details.", 24, Ui.Muted));
                return;
            }

            bool worn = st.Inventory.WornIds.Contains(item.Id);          // by any hero: cannot be salvaged
            bool wornByMe = st.Character.Equipped.ContainsValue(item.Id); // by the active hero
            var slot = TargetSlot(item);
            ItemInstance current = st.Character.Equipped.TryGetValue(slot, out var cid) ? st.Inventory.Find(cid) : null;

            string title = $"{item.Rarity} {_session.Items.DisplayName(item)}" + (item.Upgrade > 0 ? $" +{item.Upgrade}" : "");
            _detail.Add(Ui.Label(title, 30, RarityColor(item.Rarity), true));
            string wearer = WornBy(item.Id);
            if (wearer != null && wearer != _session.State.Character.Class.ToString())
                _detail.Add(Ui.Label("Worn by the " + wearer, 22, Ui.Muted));
            _detail.Add(Ui.Label($"Item level {item.ItemLevel} · {SlotName(slot)}" + (worn ? " · equipped" : ""), 22, Ui.Muted));

            var mine = Sum(item);
            var theirs = current == null || current.Id == item.Id ? new Dictionary<GearStat, double>() : Sum(current);
            foreach (var kv in mine)
            {
                theirs.TryGetValue(kv.Key, out var other);
                double diff = kv.Value - other;
                string delta = worn || Math.Abs(diff) < 1e-9 ? "" : (diff > 0 ? "  ▲ +" : "  ▼ ") + Format(kv.Key, diff);
                var line = Ui.Label(StatName(kv.Key) + " " + Format(kv.Key, kv.Value) + delta, 24,
                    delta.Contains("▲") ? Ui.Up : delta.Contains("▼") ? Ui.Down : Ui.Text);
                _detail.Add(line);
            }
            if (item.ClassBonus.HasValue)
                _detail.Add(Ui.Label($"+{_config.Items.ClassBonusPctDamage * 100:0}% damage ({item.ClassBonus.Value} only)", 22,
                    item.ClassBonus.Value == st.Character.Class ? Ui.Gold : Ui.Muted));

            if (!wornByMe)
            {
                double now = _session.Score();
                double with = _session.Score(slot, item);
                double pct = now > 0 ? (with / now - 1) * 100 : 0;
                _detail.Add(Ui.Label($"Power {(pct >= 0 ? "+" : "")}{pct:0.0}% if equipped", 24, pct > 0 ? Ui.Gold : Ui.Muted, true));
            }

            var buttons = Ui.Row();
            buttons.style.flexWrap = Wrap.Wrap;
            if (!wornByMe) buttons.Add(Ui.Button("Equip", () => Do(() => Equip(item))));
            var upgrades = _session.Upgrades;
            buttons.Add(item.Upgrade >= upgrades.MaxLevel(item)
                ? Ui.Button("Max upgrade", () => { })
                : Ui.Button("Upgrade (" + NumberFormat.Short(upgrades.Cost(item)) + " gold)",
                    () => Do(() => upgrades.Upgrade(item, st.Wallet))));
            buttons.Add(Ui.Button(item.Locked ? "Unlock" : "Lock",
                () => Do(() => _session.Inventory.SetLocked(st.Inventory, item.Id, !item.Locked))));
            if (!worn && !item.Locked)
                buttons.Add(Ui.Button("Salvage (+" + _session.Inventory.SalvageValue(item) + " mat.)",
                    () => Do(() => { _session.Inventory.Salvage(st.Inventory, st.Characters, st.Wallet, item.Id); _selected = -1; })));
            _detail.Add(buttons);
        }

        private Dictionary<GearStat, double> Sum(ItemInstance item)
        {
            var result = new Dictionary<GearStat, double>();
            foreach (var kv in _session.Items.Contributions(item, _session.State.Character.Class))
                result[kv.Key] = (result.TryGetValue(kv.Key, out var v) ? v : 0) + kv.Value;
            return result;
        }

        private void Equip(ItemInstance item)
        {
            var st = _session.State;
            var result = _session.Inventory.Equip(st.Inventory, st.Characters, st.Character, item.Id, TargetSlot(item));
            if (result == EquipResult.LevelTooLow)
                Debug.Log($"Needs character level {item.ItemLevel - _config.Combat.ItemLevelRequirementMargin}.");
            else if (result == EquipResult.NeedsConfirmation)
                AskToMove(item); // D-004: one wearer per item, ask before taking it from another hero
            _dirty = true;
        }

        /// <summary>The hero who wears the item, if any (M5: shown in the detail panel and the move question).</summary>
        private string WornBy(long itemId)
        {
            foreach (var h in _session.State.Heroes)
                if (h.Equipped.ContainsValue(itemId)) return h.Class.ToString();
            return null;
        }

        private void AskToMove(ItemInstance item)
        {
            var st = _session.State;
            string from = WornBy(item.Id);
            var target = st.Character;   // captured now: a switch while the dialog is open must not change who gets it
            var slot = TargetSlot(item);
            _confirmText.text = $"The {from} is wearing {_session.Items.DisplayName(item)}. Move it to the {target.Class}?";
            _confirmYes.clicked -= _confirmAction;
            _confirmAction = () =>
            {
                _session.Inventory.Equip(st.Inventory, st.Characters, target, item.Id, slot, force: true);
                _confirm.style.display = DisplayStyle.None;
                _dirty = true;
            };
            _confirmYes.clicked += _confirmAction;
            _confirm.style.display = DisplayStyle.Flex;
        }

        /// <summary>Rings: an empty ring slot first, otherwise the one where the swap scores best.</summary>
        private EquipSlot TargetSlot(ItemInstance item)
        {
            var eq = _session.State.Character.Equipped;
            foreach (var kv in eq) if (kv.Value == item.Id) return kv.Key;
            if (item.Kind != ItemKind.Ring) return Slots.First(s => ItemKeys.KindForSlot(s) == item.Kind);
            if (!eq.ContainsKey(EquipSlot.Ring1)) return EquipSlot.Ring1;
            if (!eq.ContainsKey(EquipSlot.Ring2)) return EquipSlot.Ring2;
            return _session.Score(EquipSlot.Ring1, item) >= _session.Score(EquipSlot.Ring2, item) ? EquipSlot.Ring1 : EquipSlot.Ring2;
        }

        private void SalvageShown()
        {
            var st = _session.State;
            var ids = new HashSet<long>(Filtered().Select(i => i.Id));
            _session.Inventory.SalvageWhere(st.Inventory, st.Characters, st.Wallet, i => ids.Contains(i.Id));
            _selected = -1;
            _dirty = true;
            Rebuild();
        }

        private void Expand()
        {
            _session.Inventory.Expand(_session.State.Inventory, _session.State.Wallet);
            _dirty = true;
            Rebuild();
        }

        private void Do(Action action)
        {
            action();
            _dirty = true;
            Rebuild();
        }

        private string Short(ItemInstance i) =>
            _session.Items.DisplayName(i) + (i.Upgrade > 0 ? " +" + i.Upgrade : "");

        /// <summary>UI Toolkit shrinks every child by default; sections that must keep their size opt out.</summary>
        private static void Fixed(VisualElement e) => e.style.flexShrink = 0;

        private static void Border(VisualElement e, Color c)
        {
            e.style.borderTopColor = c;
            e.style.borderBottomColor = c;
            e.style.borderLeftColor = c;
            e.style.borderRightColor = c;
            e.style.borderTopWidth = 4;
            e.style.borderBottomWidth = 4;
            e.style.borderLeftWidth = 4;
            e.style.borderRightWidth = 4;
        }

        private static Color RarityColor(Rarity r) => Ui.RarityColor(r);

        private static string SlotName(EquipSlot s)
        {
            switch (s)
            {
                case EquipSlot.MainHand: return "Main hand";
                case EquipSlot.OffHand: return "Off-hand";
                case EquipSlot.Ring1: return "Ring";
                case EquipSlot.Ring2: return "Ring";
                default: return s.ToString();
            }
        }

        private static string StatName(GearStat s)
        {
            switch (s)
            {
                case GearStat.WeaponDamage: return "Weapon damage";
                case GearStat.PctDamage: return "Damage";
                case GearStat.Primary: return "Primary attribute";
                case GearStat.CritPoints: return "Crit chance points";
                case GearStat.CritDamagePoints: return "Crit damage points";
                case GearStat.AttackSpeed: return "Attack speed";
                case GearStat.PctHp: return "HP";
                case GearStat.PctArmour: return "Armour";
                case GearStat.GoldFind: return "Gold found";
                case GearStat.DropChance: return "Item drop chance";
                case GearStat.Hp: return "HP";
                default: return s.ToString();
            }
        }

        private static string Format(GearStat s, double v)
        {
            bool pct = s == GearStat.PctDamage || s == GearStat.AttackSpeed || s == GearStat.PctHp
                       || s == GearStat.PctArmour || s == GearStat.GoldFind || s == GearStat.DropChance;
            return pct ? (v * 100).ToString("0.#") + "%" : NumberFormat.Short(v);
        }
    }
}
