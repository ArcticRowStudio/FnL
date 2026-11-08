using Platformer.Core;
using Platformer.Mechanics;
using Platformer.Model;
using UnityEngine;
using static Platformer.Core.Simulation;

namespace Platformer.Gameplay
{

    /// <summary>
    /// Fired when a Player collides with an Enemy.
    /// </summary>
    /// <typeparam name="EnemyCollision"></typeparam>
    public class PlayerEnemyCollision : Simulation.Event<PlayerEnemyCollision>
    {
        public EnemyController enemy;
        public PlayerController player;

        PlatformerModel model = Simulation.GetModel<PlatformerModel>();

        public override void Execute()
        {
            // A stomp is the player's feet coming down on the enemy's upper half. Comparing
            // the player's midpoint against the enemy's top only worked while the two were
            // about the same height: the player stands 1.82 units tall against a 0.45 unit
            // enemy, so that midpoint clears the enemy's head even walking into one head on,
            // and every side on bump read as a kill.
            var willHurtEnemy = player.Bounds.min.y >= enemy.Bounds.center.y;

            if (willHurtEnemy)
            {
                var enemyHealth = enemy.GetComponent<Health>();
                if (enemyHealth != null)
                {
                    enemyHealth.Decrement();
                    if (!enemyHealth.IsAlive)
                    {
                        Schedule<EnemyDeath>().enemy = enemy;
                        player.Bounce(2);
                    }
                    else
                    {
                        enemy.OnStomped();
                        player.Bounce(7);
                    }
                }
                else
                {
                    Schedule<EnemyDeath>().enemy = enemy;
                    player.Bounce(2);
                }
            }
            else
            {
                if (player.isParrying)
                {
                    player.OnParrySuccess(enemy.gameObject);
                }
                else if (player.isGuarding)
                {
                    player.OnGuardBlock(enemy.transform.position);
                }
                else if (!player.isInvincible)
                {
                    Vector2 dir = (player.transform.position - enemy.transform.position).normalized;
                    Vector2 knockback = new Vector2(Mathf.Sign(dir.x != 0 ? dir.x : 1f) * 3.5f, 4.5f);
                    player.TakeDamage(1, knockback);
                }
            }
        }
    }
}