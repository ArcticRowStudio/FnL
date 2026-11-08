using Platformer.Core;
using Platformer.Gameplay;
using Platformer.View;
using UnityEngine;
using static Platformer.Core.Simulation;

namespace Platformer.Mechanics
{
    /// <summary>
    /// A high-caliber Anti-Materiel Rifle (.50 BMG) round fired by an ally sniper positioned
    /// in the FOREGROUND (front plane/viewer's side), shooting directly into the BACKGROUND (depth plane)
    /// onto the designated target coordinate under the scope crosshair.
    /// Features:
    /// - True foreground-to-background (front-to-back depth) penetration: starts large in the foreground
    ///   and rushes away into the scene depth directly onto the targeted aim point.
    /// - High-energy supersonic shockwave & vapor rings expanding as it pierces through the depth.
    /// - 100% reliable hit resolution at any distance across the entire screen.
    /// - Heavy kinetic impact: expanding shockwave, spark debris, and enemy destruction.
    /// </summary>
    public class IncomingRound : MonoBehaviour
    {
        [Header("Ballistics & Depth")]
        public float travelTime = 0.10f;       // Rapid supersonic entry (~0.10s from front to back)
        public float entryScale = 5.0f;        // Large size in foreground right at viewer/lens plane
        public float impactScale = 0.25f;      // Compact size at target depth
        public float impactRadius = 0.1f;      // How far off a target still counts; the shooter sets it
        public float bossDamageFraction = 0f;  // TEMP testing: share of a boss's max HP per hit (0 = 1 HP); the shooter sets it

        private Vector3 targetPosition;
        private EnemyController priorityTarget;
        private Collider2D priorityPart;
        private float elapsed = 0f;
        private bool launched = false;
        private SpriteRenderer spriteRenderer;
        private GameObject haloObject;
        private SpriteRenderer haloRenderer;

        /// <summary>
        /// Launches the sniper round from the foreground into the depth towards the target.
        /// </summary>
        public void Launch(Vector3 target, EnemyController priorityEnemy = null, Collider2D aimedPart = null)
        {
            targetPosition = target;
            priorityTarget = priorityEnemy;
            priorityPart = aimedPart;

            transform.position = targetPosition;
            transform.localScale = Vector3.one * entryScale;

            BuildVisuals();

            // Initial foreground supersonic shockwave ring when the round punches through the near plane
            ExpandingRing.Spawn(targetPosition, new Color(1f, 0.95f, 0.8f, 0.85f), 1.6f, 0.15f);

            // Subtle muzzle blast recoil impulse through camera
            CameraZoomController.TriggerShake(0.18f, 0.08f);

            launched = true;
        }

        private void BuildVisuals()
        {
            spriteRenderer = gameObject.GetComponent<SpriteRenderer>();
            if (spriteRenderer == null) spriteRenderer = gameObject.AddComponent<SpriteRenderer>();

            spriteRenderer.sprite = VfxSprites.Glow;
            spriteRenderer.color = new Color(1f, 1f, 1f, 1f);       // White-hot tungsten slug core
            spriteRenderer.sortingOrder = 131;                      // Front layer

            // Outer supersonic shockwave aura / vapor cone
            haloObject = new GameObject("SupersonicAura");
            haloObject.transform.SetParent(transform, false);
            haloRenderer = haloObject.AddComponent<SpriteRenderer>();
            haloRenderer.sprite = VfxSprites.Ring;
            haloRenderer.color = new Color(1f, 0.8f, 0.3f, 0.85f);
            haloRenderer.sortingOrder = 130;
            haloObject.transform.localScale = Vector3.one * 1.8f;
        }

        private void Update()
        {
            if (!launched) return;

            elapsed += Time.deltaTime;
            float t = travelTime > 0f ? Mathf.Clamp01(elapsed / travelTime) : 1f;

            // Squared easing accelerates the round as it recedes from foreground into depth
            float currentScale = Mathf.Lerp(entryScale, impactScale, t * t);
            transform.localScale = new Vector3(currentScale, currentScale, 1f);

            // Color transition from white-hot foreground to high-energy supersonic amber
            if (spriteRenderer != null)
            {
                spriteRenderer.color = Color.Lerp(new Color(1f, 1f, 1f, 1f), new Color(1f, 0.55f, 0.15f, 0.95f), t);
            }
            if (haloRenderer != null)
            {
                haloRenderer.color = Color.Lerp(new Color(1f, 0.85f, 0.4f, 0.9f), new Color(1f, 0.3f, 0.05f, 0.6f), t);
            }

            if (t >= 1f)
            {
                Impact();
            }
        }

        private void Impact()
        {
            // A part aimed at directly (a boss's pod or duct, a drone, a missile) takes the round,
            // provided it is still there: it may have been blown off during the flight.
            if (priorityPart != null && priorityPart.enabled && priorityPart.gameObject.activeInHierarchy)
            {
                var part = priorityPart.GetComponent<IPlayerHittable>();
                if (part != null)
                {
                    Vector2 partPoint = priorityPart.bounds.ClosestPoint(targetPosition);
                    CameraZoomController.TriggerShake(0.38f, 0.16f);
                    ProjectileDebris.SpawnSpark(partPoint, new Color(1f, 0.25f, 0.1f), 18, 9f, 0.20f, 0.38f);
                    ProjectileDebris.SpawnSpark(partPoint, new Color(1f, 0.9f, 0.4f), 12, 7f, 0.16f, 0.30f);
                    ExpandingRing.Spawn(partPoint, new Color(1f, 0.85f, 0.45f, 0.95f), 3.4f, 0.22f);
                    part.OnPlayerHit(partPoint);
                    Destroy(gameObject);
                    return;
                }
            }

            EnemyController enemy = priorityTarget != null ? priorityTarget : FindEnemyAtImpact();
            if (enemy != null)
            {
                Vector3 hitPoint = enemy.transform.position;
                var col = enemy.GetComponentInChildren<Collider2D>();
                if (col != null) hitPoint = col.bounds.ClosestPoint(targetPosition);

                if (enemy.BlocksRangedHit(hitPoint))
                {
                    enemy.OnBlockedHit(hitPoint, targetPosition);
                    Destroy(gameObject);
                    return;
                }

                // Heavy kinetic impact screen shake
                CameraZoomController.TriggerShake(0.38f, 0.16f);

                // Heavy armor-piercing kinetic impact sparks
                ProjectileDebris.SpawnSpark(hitPoint, new Color(1f, 0.25f, 0.1f), 18, 9f, 0.20f, 0.38f);
                ProjectileDebris.SpawnSpark(hitPoint, new Color(1f, 0.9f, 0.4f), 12, 7f, 0.16f, 0.30f);

                // High-pressure shockwave ring
                ExpandingRing.Spawn(hitPoint, new Color(1f, 0.85f, 0.45f, 0.95f), 3.4f, 0.22f);

                var enemyHealth = enemy.GetComponent<Health>();
                if (enemyHealth != null)
                {
                    // TEMP testing boost on bosses. It stops once the boss turns invulnerable (a phase
                    // change), so one round never skips a phase.
                    int hits = 1;
                    bool boss = enemy is Boss.VtolBossAI || enemy is Boss.QuadpodBossAI;
                    if (boss && bossDamageFraction > 0f)
                        hits = Mathf.Max(1, Mathf.CeilToInt(enemyHealth.maxHP * bossDamageFraction));
                    for (int i = 0; i < hits && enemyHealth.IsAlive; i++)
                    {
                        if (i > 0 && BossInvulnerable(enemy)) break;
                        enemyHealth.Decrement();
                    }
                    if (!enemyHealth.IsAlive)
                    {
                        Schedule<EnemyDeath>().enemy = enemy;
                    }
                }
                else
                {
                    Schedule<EnemyDeath>().enemy = enemy;
                }
            }
            else if (FindHittableAtImpact(out var hittable, out var hittablePoint))
            {
                CameraZoomController.TriggerShake(0.38f, 0.16f);
                ProjectileDebris.SpawnSpark(hittablePoint, new Color(1f, 0.25f, 0.1f), 18, 9f, 0.20f, 0.38f);
                ExpandingRing.Spawn(hittablePoint, new Color(1f, 0.85f, 0.45f, 0.95f), 3.4f, 0.22f);
                hittable.OnPlayerHit(hittablePoint);
            }
            else
            {
                // Terrain impact
                CameraZoomController.TriggerShake(0.22f, 0.10f);
                ProjectileDebris.SpawnSpark(targetPosition, new Color(0.95f, 0.85f, 0.65f), 10, 6f, 0.15f, 0.25f);
                ExpandingRing.Spawn(targetPosition, new Color(0.9f, 0.85f, 0.75f, 0.6f), 2.0f, 0.18f);
            }

            Destroy(gameObject);
        }

        private static bool BossInvulnerable(EnemyController enemy)
        {
            if (enemy is Boss.VtolBossAI vtol) return vtol.isInvulnerable;
            if (enemy is Boss.QuadpodBossAI quadpod) return quadpod.isInvulnerable;
            return false;
        }

        // Enemies take priority; this only runs when the round found none, so a drone escorting a
        // boss never soaks up a shot aimed at the boss.
        private bool FindHittableAtImpact(out IPlayerHittable closest, out Vector2 point)
        {
            closest = null;
            point = targetPosition;
            float closestDistance = float.PositiveInfinity;

            var hits = Physics2D.OverlapCircleAll(targetPosition, impactRadius);
            for (int i = 0; i < hits.Length; i++)
            {
                var hit = hits[i];
                if (hit == null) continue;

                var hittable = hit.GetComponentInParent<IPlayerHittable>();
                if (hittable == null) continue;

                float distance = ((Vector2)targetPosition - (Vector2)hit.bounds.center).sqrMagnitude;
                if (distance < closestDistance)
                {
                    closest = hittable;
                    point = hit.bounds.center;
                    closestDistance = distance;
                }
            }

            return closest != null;
        }

        private EnemyController FindEnemyAtImpact()
        {
            var hits = Physics2D.OverlapCircleAll(targetPosition, impactRadius);
            EnemyController closest = null;
            float closestDistance = float.PositiveInfinity;

            for (int i = 0; i < hits.Length; i++)
            {
                var hit = hits[i];
                if (hit == null) continue;

                var enemy = hit.GetComponentInParent<EnemyController>();
                if (enemy == null) continue;

                float distance = ((Vector2)targetPosition - (Vector2)enemy.transform.position).sqrMagnitude;
                if (distance < closestDistance)
                {
                    closest = enemy;
                    closestDistance = distance;
                }
            }

            return closest;
        }
    }
}
