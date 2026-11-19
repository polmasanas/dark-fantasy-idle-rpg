using System;
using System.Collections.Generic;

namespace DarkFantasy.Core.Events
{
    /// <summary>
    /// Synchronous, typed publish/subscribe (TECHNICAL_ARCHITECTURE section 8). Game systems
    /// publish; the presentation layer listens. Handlers run in subscription order.
    /// </summary>
    public sealed class EventBus
    {
        private readonly Dictionary<Type, List<Delegate>> _handlers = new Dictionary<Type, List<Delegate>>();

        public void Subscribe<T>(Action<T> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            if (!_handlers.TryGetValue(typeof(T), out var list))
                _handlers[typeof(T)] = list = new List<Delegate>();
            list.Add(handler);
        }

        public void Unsubscribe<T>(Action<T> handler)
        {
            if (handler != null && _handlers.TryGetValue(typeof(T), out var list))
                list.Remove(handler);
        }

        public void Publish<T>(T e)
        {
            if (!_handlers.TryGetValue(typeof(T), out var list)) return;
            // Copy so handlers may subscribe or unsubscribe while being notified.
            foreach (var d in list.ToArray())
                ((Action<T>)d)(e);
        }
    }
}
