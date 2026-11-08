using Platformer.Core;
using Platformer.Mechanics;
using Platformer.View;
using UnityEngine;

namespace Platformer.Gameplay
{
    /// <summary>
    /// Fired when the health component on an enemy has a hitpoint value of 0.
    /// </summary>
    /// <typeparam name="EnemyDeath"></typeparam>
    public class EnemyDeath : Simulation.Event<EnemyDeath>
    {
        public EnemyController enemy;

        public override void Execute()
        {
            if (enemy == null || !enemy.gameObject.activeInHierarchy) return;
            if (enemy.DefersDeath) return;

            if (enemy._collider != null) enemy._collider.enabled = false;
            if (enemy.control != null) enemy.control.enabled = false;
            enemy.enabled = false;

            if (enemy._audio && enemy.ouch)
                enemy._audio.PlayOneShot(enemy.ouch);

            Vector3 center = enemy.transform.position + Vector3.up * 0.45f;
            ProjectileDebris.SpawnSpark(center, new Color(1f, 0.4f, 0.2f), 14, 6f);
            ExpandingRing.Spawn(center, new Color(1f, 0.9f, 0.8f, 0.8f), 1.6f, 0.22f);

            if (Application.isPlaying)
            {
                Object.Destroy(enemy.gameObject, 0.12f);
            }
            else
            {
                Object.DestroyImmediate(enemy.gameObject);
            }
        }
    }
}