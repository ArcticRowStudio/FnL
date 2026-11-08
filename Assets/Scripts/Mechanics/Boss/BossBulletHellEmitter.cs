using System.Collections;
using Platformer.View;
using UnityEngine;

namespace Platformer.Mechanics.Boss
{
    /// <summary>
    /// Bullet hell emission patterns for the Quadpod Boss.
    /// Handles Gatling spreads, Cluster mortars, Wall curtain fire, EMP rings, and Spirals.
    /// </summary>
    public static class BossBulletHellEmitter
    {
        /// <summary>
        /// Fires a fan of bullets angled towards the target direction.
        /// </summary>
        public static void FireGatlingFan(Vector2 origin, Vector2 targetDir, int count, float spreadAngle, float speed, Color color)
        {
            if (count <= 0) return;
            float baseAngle = Mathf.Atan2(targetDir.y, targetDir.x) * Mathf.Rad2Deg;
            float startAngle = baseAngle - (spreadAngle * 0.5f);
            float step = count > 1 ? spreadAngle / (count - 1) : 0f;

            for (int i = 0; i < count; i++)
            {
                float angle = (startAngle + (step * i)) * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                BossProjectile.Create(origin, dir, speed, color, isParriable: true);
            }

            // Muzzle flash
            ExpandingRing.Spawn(origin, color, 0.7f, 0.12f);
            ProjectileDebris.SpawnSpark(origin, color, 4, 3f);
        }

        /// <summary>
        /// Fires an arcing cluster mortar that explodes into radial fragments upon impact.
        /// </summary>
        public static BossProjectile FireClusterMortar(Vector2 origin, Vector2 targetPos, float timeToTarget, Color color, int fragments = 8, float fragmentSpeed = 6.5f)
        {
            float gravity = Mathf.Abs(Physics2D.gravity.y * 1.2f);
            float dx = targetPos.x - origin.x;
            float dy = targetPos.y - origin.y;

            float vx = dx / timeToTarget;
            float vy = (dy + (0.5f * gravity * timeToTarget * timeToTarget)) / timeToTarget;

            Vector2 velocity = new Vector2(vx, vy);
            float speed = velocity.magnitude;
            Vector2 dir = velocity.normalized;

            var shell = BossProjectile.Create(origin, dir, speed, color, isParriable: false, useGravity: true, gravityScale: 1.2f);
            shell.ConfigureAsCluster(fragments, fragmentSpeed);

            // Launch FX
            ExpandingRing.Spawn(origin, color, 1.1f, 0.18f);
            ProjectileDebris.SpawnSpark(origin, color, 6, 5f);
            return shell;
        }

        /// <summary>
        /// Fires downward falling curtain bullets spaced across a horizontal line below the boss.
        /// </summary>
        public static void FireWallCurtain(Vector2 origin, float arenaWidth, int count, float fallSpeed, Color color, float safeGapX)
        {
            float left = origin.x - (arenaWidth * 0.5f);
            float step = arenaWidth / Mathf.Max(1, count - 1);

            for (int i = 0; i < count; i++)
            {
                float posX = left + (step * i);

                // Leave a safe gap around safeGapX so player has a dodging lane!
                if (Mathf.Abs(posX - safeGapX) < 1.2f)
                {
                    continue;
                }

                Vector2 spawnPos = new Vector2(posX, origin.y);
                Vector2 dir = Vector2.down;
                BossProjectile.Create(spawnPos, dir, fallSpeed, color, isParriable: true);
            }

            ExpandingRing.Spawn(origin, color, 1.4f, 0.2f);
        }

        /// <summary>
        /// 360-degree radial ring of electric orbs from the top antenna.
        /// </summary>
        public static void FireArcRing(Vector2 origin, int count, float speed, Color color, float angleOffset = 0f)
        {
            if (count <= 0) return;
            float step = 360f / count;

            for (int i = 0; i < count; i++)
            {
                float angle = (angleOffset + (step * i)) * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                BossProjectile.Create(origin, dir, speed, color, isParriable: true);
            }

            ExpandingRing.Spawn(origin, color, 2.0f, 0.22f);
            ProjectileDebris.SpawnSpark(origin, color, 12, 6f);
        }

        /// <summary>
        /// Coroutine for emitting an expanding spiral bullet stream.
        /// </summary>
        public static IEnumerator FireSpiralRoutine(Vector2 origin, int totalBullets, float duration, float rotations, float speed, Color color)
        {
            float delay = duration / Mathf.Max(1, totalBullets);
            float totalAngle = rotations * 360f;
            float angleStep = totalAngle / Mathf.Max(1, totalBullets);

            for (int i = 0; i < totalBullets; i++)
            {
                float angle = (i * angleStep) * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                BossProjectile.Create(origin, dir, speed, color, isParriable: true);

                if (i % 4 == 0)
                {
                    ProjectileDebris.SpawnSpark(origin, color, 2, 2f);
                }

                yield return new WaitForSeconds(delay);
            }
        }
    }
}
