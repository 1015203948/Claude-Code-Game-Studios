using System;

namespace Game.Channels {
    /// <summary>
    /// Base class for all Tier 1 ScriptableObject Channels.
    /// Memory: ~200B per instance — negligible.
    /// Zero reflection in Raise() — directly invokes C# event.
    /// </summary>
    public abstract class GameEvent {
        private event Action<object> Event;

        public void Raise(object payload) {
            Event?.Invoke(payload);
        }

        public void Subscribe(Action<object> handler) {
            Event += handler;
        }

        public void Unsubscribe(Action<object> handler) {
            Event -= handler;
        }
    }
}
