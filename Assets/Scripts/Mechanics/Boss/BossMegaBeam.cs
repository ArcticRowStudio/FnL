using System.Collections;
using Platformer.Core;
using Platformer.Gameplay;
using Platformer.Mechanics;
using UnityEngine;

namespace Platformer.Mechanics.Boss
{
    /// <summary>
    /// Mega Particle Beam Cannon attack for the VTOL Boss.
    /// Features aiming telegraph line, screen-shaking particle laser beam,
    /// and floor sweep dynamics.
    /// </summary>
    public class BossMegaBeam : MonoBehaviour
    {
        [Header("Beam Parameters")]
        public float maxBeamLength = 25f;
        public float beamWidth = 1.4f;
        public int damagePerTick = 1;
        public float tickInterval = 0.2f;
        public LayerMask hitLayers;

        [Header("Components")]
        public LineRenderer telegraphLine;
        public LineRenderer activeBeamLine;
        public BoxCollider2D beamCollider;

        private bool _isFiring = false;

        /// <summary>Where the beam (or its warning line) is pointing right now, for the cannon to follow.</summary>
        public Vector2 CurrentDirection { get; private set; } = Vector2.left;
        private float _lastDamageTime = 0f;
        private Transform _target;
        private Color _beamColor = new Color(0.2f, 0.85f, 1f, 1f);

        void Awake()
        {
            SetupLinesIfNeeded();
        }

        void SetupLinesIfNeeded()
        {
            if (telegraphLine == null)
            {
                var tObj = new GameObject("TelegraphLine");
                tObj.transform.SetParent(transform, false);
                telegraphLine = tObj.AddComponent<LineRenderer>();
                telegraphLine.useWorldSpace = true;
                telegraphLine.startWidth = 0.08f;
                telegraphLine.endWidth = 0.08f;
                telegraphLine.material = new Material(Shader.Find("Sprites/Default"));
                telegraphLine.startColor = new Color(1f, 0.2f, 0.2f, 0.7f);
                telegraphLine.endColor = new Color(1f, 0.2f, 0.2f, 0.7f);
                telegraphLine.sortingOrder = 25;
                telegraphLine.enabled = false;
            }

            if (activeBeamLine == null)
            {
                var bObj = new GameObject("ActiveBeamLine");
                bObj.transform.SetParent(transform, false);
                activeBeamLine = bObj.AddComponent<LineRenderer>();
                activeBeamLine.useWorldSpace = true;
                activeBeamLine.startWidth = beamWidth;
                activeBeamLine.endWidth = beamWidth * 1.2f;
                activeBeamLine.material = new Material(Shader.Find("Sprites/Default"));
                activeBeamLine.startColor = Color.white;
                activeBeamLine.endColor = _beamColor;
                activeBeamLine.sortingOrder = 26;
                activeBeamLine.enabled = false;
            }

            if (beamCollider == null)
            {
                beamCollider = gameObject.AddComponent<BoxCollider2D>();
                beamCollider.isTrigger = true;
                beamCollider.enabled = false;
            }

            // Own kinematic body: the trigger is moved every frame, and must not fold into the
            // boss's Rigidbody2D as part of its hitbox.
            if (GetComponent<Rigidbody2D>() == null)
            {
                var body = gameObject.AddComponent<Rigidbody2D>();
                body.bodyType = RigidbodyType2D.Kinematic;
            }
        }

        /// <summary>
        /// Shuts the beam off mid-sequence. The sequence runs as a coroutine on the boss, so when the
        /// boss stops its coroutines the lines and trigger would otherwise stay on.
        /// </summary>
        public void Cancel()
        {
            _isFiring = false;
            if (telegraphLine != null) telegraphLine.enabled = false;
            if (activeBeamLine != null) activeBeamLine.enabled = false;
            if (beamCollider != null) beamCollider.enabled = false;
        }

        public IEnumerator FireBeamSequence(Transform muzzle, Vector2 initialDirection, float sweepAngle, float duration, bool isPhase2)
        {
            SetupLinesIfNeeded();
            _beamColor = isPhase2 ? new Color(1f, 0.3f, 0.15f, 1f) : new Color(0.2f, 0.85f, 1f, 1f);
            activeBeamLine.endColor = _beamColor;

            // 1. Telegraph Phase (1.2s warning track)
            telegraphLine.enabled = true;
            float prepTime = 1.1f;
            float elapsed = 0f;

            while (elapsed < prepTime)
            {
                elapsed += Time.deltaTime;
                Vector3 origin = muzzle.position;
                Vector3 target = origin + (Vector3)initialDirection * maxBeamLength;

                telegraphLine.SetPosition(0, origin);
                telegraphLine.SetPosition(1, target);
                CurrentDirection = initialDirection;

                // Flickering warning
                float alpha = Mathf.PingPong(elapsed * 12f, 1f) * 0.7f + 0.3f;
                telegraphLine.startColor = new Color(1f, 0.1f, 0.1f, alpha);
                telegraphLine.endColor = new Color(1f, 0.1f, 0.1f, alpha);

                // Small charge sparks at muzzle
                if (Random.value < 0.35f)
                {
                    ProjectileDebris.SpawnSpark(origin, _beamColor, 3, 2.5f);
                }

                yield return null;
            }

            telegraphLine.enabled = false;

            // 2. Beam Firing Phase with Sweep
            _isFiring = true;
            activeBeamLine.enabled = true;
            beamCollider.enabled = true;

            float fireElapsed = 0f;
            float currentAngle = Mathf.Atan2(initialDirection.y, initialDirection.x) * Mathf.Rad2Deg;
            float targetAngle = currentAngle + sweepAngle;

            CameraZoomController.TriggerShake(duration, 0.35f);

            while (fireElapsed < duration)
            {
                fireElapsed += Time.deltaTime;
                float t = fireElapsed / duration;
                float angle = Mathf.Lerp(currentAngle, targetAngle, Mathf.SmoothStep(0f, 1f, t));
                Vector2 dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
                CurrentDirection = dir;

                Vector3 origin = muzzle.position;
                Vector3 endPoint = origin + (Vector3)dir * maxBeamLength;

                // Raycast against ground for impact flare
                RaycastHit2D hit = Physics2D.Raycast(origin, dir, maxBeamLength, LayerMask.GetMask("Ground", "Default", "Platforms"));
                if (hit.collider != null && !hit.collider.isTrigger)
                {
                    endPoint = hit.point;
                    ProjectileDebris.SpawnSpark(endPoint, _beamColor, 4, 6f);
                }

                activeBeamLine.SetPosition(0, origin);
                activeBeamLine.SetPosition(1, endPoint);

                // Update BoxCollider2D trigger
                float actualLength = Vector2.Distance(origin, endPoint);
                beamCollider.size = new Vector2(actualLength, beamWidth);
                beamCollider.offset = new Vector2(actualLength * 0.5f, 0f);
                transform.position = origin;
                transform.rotation = Quaternion.Euler(0, 0, angle);

                // Beam pulsing width
                float pulse = Mathf.Sin(fireElapsed * 30f) * 0.2f + 1f;
                activeBeamLine.startWidth = beamWidth * pulse;
                activeBeamLine.endWidth = beamWidth * 1.2f * pulse;

                yield return null;
            }

            // 3. Beam Fadeout
            activeBeamLine.enabled = false;
            beamCollider.enabled = false;
            _isFiring = false;
        }

        void OnTriggerStay2D(Collider2D other)
        {
            if (!_isFiring) return;

            if (Time.time - _lastDamageTime >= tickInterval)
            {
                var player = other.GetComponent<PlayerController>();
                if (player != null)
                {
                    if (player.isParrying)
                    {
                        // Throttled like damage, or a parry held in the beam succeeds every physics step.
                        _lastDamageTime = Time.time;
                        player.OnParrySuccess(gameObject);
                    }
                    else if (!player.isInvincible)
                    {
                        _lastDamageTime = Time.time;
                        Vector2 knockback = new Vector2(Mathf.Sign(transform.right.x) * 4f, 2f);
                        player.TakeDamage(damagePerTick, knockback);
                    }
                }
            }
        }
    }
}
