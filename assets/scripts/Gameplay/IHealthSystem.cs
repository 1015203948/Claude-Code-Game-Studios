namespace Game.Gameplay
{
    /// <summary>
    /// Abstraction over HealthSystem.ApplyDamage for testability.
    /// WeaponSystem consumes this interface; production uses HealthSystemAdapter.
    /// </summary>
    public interface IHealthSystem
    {
        void ApplyDamage(string instanceId, float amount, DamageType damageType);
    }

    /// <summary>
    /// Production adapter that forwards to HealthSystem.Instance.
    /// </summary>
    public class HealthSystemAdapter : IHealthSystem
    {
        public void ApplyDamage(string instanceId, float amount, DamageType damageType)
        {
            HealthSystem.Instance?.ApplyDamage(instanceId, amount, damageType);
        }
    }
}
