using System;
using System.Collections.Generic;
using DarkFantasy.Core.Session;
using DarkFantasy.Core.Stats;
using UnityEngine.UIElements;

namespace DarkFantasy.Presentation
{
    /// <summary>
    /// Three hero buttons in the top bar (M5-3, M5-4): the active hero is highlighted, the Archer and
    /// Mage show as locked until the region 1 boss falls, and every button is greyed out while a boss
    /// fight is running. Tapping asks the session to switch.
    /// </summary>
    internal sealed class HeroBarView
    {
        public readonly VisualElement Root;
        private readonly GameSession _session;
        private readonly Dictionary<ClassId, Button> _buttons = new Dictionary<ClassId, Button>();
        private float _pulse;

        public HeroBarView(GameSession session, Action<ClassId> onTap)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            Root = Ui.Row(Justify.SpaceBetween);
            Root.style.marginTop = 8;
            foreach (ClassId cls in Enum.GetValues(typeof(ClassId)))
            {
                var hero = cls;
                var b = Ui.Button("", () => onTap(hero), 22);
                b.AddToClassList("hero-button");
                b.style.flexGrow = 1;
                b.style.marginLeft = 3;
                b.style.marginRight = 3;
                _buttons[cls] = b;
                Root.Add(b);
            }
            Refresh(0);
        }

        /// <summary>Briefly highlights the newly unlocked heroes.</summary>
        public void Pulse() => _pulse = 3f;

        public void Refresh(float dt)
        {
            var st = _session.State;
            if (_pulse > 0) _pulse -= dt;
            foreach (var kv in _buttons)
            {
                var hero = st.Heroes[(int)kv.Key];
                bool active = (int)kv.Key == st.ActiveHero;
                bool locked = kv.Key != ClassId.Fighter && !st.Account.HeroesUnlocked;
                kv.Value.text = locked ? kv.Key + " · locked" : $"{kv.Key} · {hero.Level}";
                kv.Value.EnableInClassList("hero-button--active", active);
                kv.Value.EnableInClassList("hero-button--locked", locked);
                kv.Value.EnableInClassList("hero-button--new", _pulse > 0 && kv.Key != ClassId.Fighter && ((int)(_pulse * 4) % 2 == 0));
                kv.Value.SetEnabled(!locked && !(_session.InBossFight && !active));
                kv.Value.tooltip = locked ? "Beat the region 1 boss to unlock" : _session.InBossFight ? "Not during a boss fight" : "";
            }
        }
    }
}
