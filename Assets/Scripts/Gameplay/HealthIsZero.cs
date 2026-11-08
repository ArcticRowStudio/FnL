using Platformer.Core;
using Platformer.Mechanics;
using Platformer.Model;
using static Platformer.Core.Simulation;

namespace Platformer.Gameplay
{
    /// <summary>
    /// Fired when an entity's health reaches 0.
    /// Triggers PlayerDeath if health belongs to player, or EnemyDeath if it belongs to an enemy.
    /// </summary>
    /// <typeparam name="HealthIsZero"></typeparam>
    public class HealthIsZero : Simulation.Event<HealthIsZero>
    {
        public Health health;

        public override void Execute()
        {
            if (health == null) return;

            var player = health.GetComponent<PlayerController>();
            if (player != null)
            {
                Schedule<PlayerDeath>();
                return;
            }

            var enemy = health.GetComponent<EnemyController>();
            if (enemy != null)
            {
                var ev = Schedule<EnemyDeath>();
                ev.enemy = enemy;
            }
        }
    }
}