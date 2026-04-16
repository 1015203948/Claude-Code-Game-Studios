namespace Game.Channels {
    /// <summary>
    /// Generic Tier 1 SO Channel — avoids boxing from object payload.
    /// </summary>
    /// <typeparam name="T">Event payload struct/class</typeparam>
    public class GameEvent<T> : GameEvent {
        private event Action<T> Event;

        public void Raise(T payload) {
            Event?.Invoke(payload);
        }

        public void Subscribe(Action<T> handler) {
            Event += handler;
        }

        public void Unsubscribe(Action<T> handler) {
            Event -= handler;
        }
    }
}
