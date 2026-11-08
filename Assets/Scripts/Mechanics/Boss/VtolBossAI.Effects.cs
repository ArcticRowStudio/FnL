using System.Collections;
using System.Collections.Generic;
using Platformer.Core;
using Platformer.Gameplay;
using Platformer.Model;
using Platformer.View;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using static Platformer.Core.Simulation;

namespace Platformer.Mechanics.Boss
{
    // Effects: engine exhaust, rotor wash, smoke, attack telegraphs.
    public partial class VtolBossAI
    {
        void UpdateEngineEffects()
        {
            if (currentState == BossState.Dormant || _far) return;

            float speed = _rb != null ? _rb.linearVelocity.magnitude : 0f;
            Color flame = isPhase2 ? new Color(1f, 0.45f, 0.15f) : new Color(0.35f, 0.85f, 1f);
            if (Random.value < 0.55f + speed * 0.04f)
            {
                SpawnExhaust(_ductL, LeftNozzle - LeftDuctJoint, flame, speed);
                SpawnExhaust(_ductR, RightNozzle - RightDuctJoint, flame, speed);
            }

            if (!_groundLock && _rb != null && _rb.position.y - arenaFloorY < 5.5f && Random.value < 0.5f) SpawnDownwash(1);

            // Overdrive: the airframe is coming apart.
            if (isPhase2 && Random.value < 0.12f)
            {
                Vector3 p = transform.TransformPoint(new Vector3(Random.Range(-2.5f, 2.5f), Random.Range(-0.8f, 1.2f), 0f));
                ProjectileDebris.SpawnSpark(p, new Color(0.3f, 0.3f, 0.32f, 0.8f), 2, 1.5f, 0.2f, 0.4f);
            }
        }

        // Exhaust leaves along the duct's axis, so it swings with the thrust.
        void SpawnExhaust(Transform duct, Vector2 nozzleLocal, Color color, float speed)
        {
            if (duct == null) return;
            Vector3 nozzle = duct.TransformPoint(nozzleLocal);
            Vector3 axis = (nozzle - duct.position).normalized;
            Vector3 p = nozzle + axis * Random.Range(0.1f, 0.6f);
            ProjectileDebris.SpawnSpark(p, color, 1, 2.5f + speed * 0.25f, 0.1f, 0.22f);
        }

        // Rotor wash: standing under the boss shoves the player out sideways, harder the closer and
        // the lower it is, and harder still while it strains to lift off.
        void UpdateDownwashPush()
        {
            if (_player == null || _rb == null || _groundLock || _far) return;
            if (currentState == BossState.Dormant || currentState == BossState.Defeated) return;

            Vector2 feet = new Vector2(_player.transform.position.x, _player.Bounds.min.y);
            float dx = feet.x - _rb.position.x;
            float dy = _rb.position.y - feet.y;
            float reach = downwashReach * _depthScale;
            if (dy < 0.5f || dy > downwashDepth || Mathf.Abs(dx) > reach) return;

            float closeness = (1f - Mathf.Abs(dx) / reach) * (1f - dy / downwashDepth);
            bool straining = currentState == BossState.LiftAttempt || currentState == BossState.Intro || currentState == BossState.Escaping;
            float push = Mathf.Sign(dx == 0f ? 1f : dx) * downwashStrength * Mathf.Clamp01(0.35f + closeness) * (straining ? 1.6f : 1f);
            _player.Push(push);

            if (Random.value < 0.35f)
                ProjectileDebris.Spawn(feet, new Color(0.8f, 0.82f, 0.85f, 0.6f), Random.Range(0.12f, 0.22f),
                    new Vector2(Mathf.Sign(push) * Random.Range(2f, 4f), Random.Range(0.3f, 1.2f)), 0.7f);
        }

        // Rotor wash kicking dust off the deck under the boss.
        void SpawnDownwash(int count)
        {
            for (int i = 0; i < count; i++)
            {
                Vector3 p = new Vector3(transform.position.x + Random.Range(-3.2f, 3.2f), arenaFloorY + 0.15f, 0f);
                ProjectileDebris.SpawnSpark(p, new Color(0.8f, 0.82f, 0.85f, 0.6f), 1, 3.5f, 0.12f, 0.28f);
            }
        }

        // Flickers the hull toward a warning colour while the boss winds up an attack.
        IEnumerator TelegraphFlash(float duration, Color tint)
        {
            if (fuselageRenderer == null) yield break;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float pulse = Mathf.PingPong(t * 10f, 1f);
                SetHullColor(Color.Lerp(_fuselageBaseColor, tint, pulse));
                yield return null;
            }
            SetHullColor(_fuselageBaseColor);
        }

        static float EaseOutBack(float t, float overshoot)
        {
            float u = t - 1f;
            return 1f + (overshoot + 1f) * u * u * u + overshoot * u * u;
        }

        // Dark puff that drifts up and thins out.
        void SpawnSmoke(Vector3 pos, float size)
        {
            ProjectileDebris.Spawn(
                pos + (Vector3)(Random.insideUnitCircle * 0.2f),
                new Color(0.16f, 0.16f, 0.18f, 0.75f),
                size,
                new Vector2(Random.Range(-0.5f, 0.5f), Random.Range(1.2f, 2.4f)),
                size * 0.6f,
                _far ? 1 : (fuselageRenderer != null ? fuselageRenderer.sortingOrder : 12) + 4);
        }
    }
}
