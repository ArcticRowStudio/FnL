using Platformer.View;
using UnityEngine;

namespace Platformer.Mechanics.Boss
{
    // How a beaten boss's pilot gets out.
    public enum PilotExitStyle
    {
        // The whole cockpit fires off with the pilot inside.
        EjectCapsule,
        // The pilot climbs out of a hatch and runs for it (BossPilotEscape).
        ClimbOut,
        // The pilot springs out of the hatch and bounds away in big hops, diving off the deck
        // edge (into the sea, on the topside) when there is one.
        HopAway,
    }

    // A boss's cockpit fired off with the pilot inside: explosive bolts kick it loose, then a
    // rocket burn throws it up and away from the player, tumbling, flame and smoke streaming,
    // until it is out of the shot. The capsule and the hollow left in the boss are cut by
    // Tools/cut-eject-capsules.py. A cut-scene actor: no collider.
    public class EjectionCapsule : MonoBehaviour
    {
        const float BoltTime = 0.12f;
        const float BurnTime = 1.0f;
        const float BurnAccel = 34f;
        const float Gravity = 16f;

        static readonly Color FlameCore = new Color(1f, 0.85f, 0.45f, 1f);
        static readonly Color FlameOuter = new Color(1f, 0.45f, 0.1f, 0.85f);
        static readonly Color Smoke = new Color(0.55f, 0.55f, 0.58f, 0.75f);

        Vector2 _velocity;
        float _spin;
        float _age;
        int _dir;
        float _halfHeight;
        SpriteRenderer _body, _flame, _flameCore;

        // position/rotation/scale: where the cockpit sat on the boss (scale carries its mirror).
        // awayDir: +1 right, -1 left, the way it veers off.
        public static EjectionCapsule Launch(Sprite sprite, Vector3 position, Quaternion rotation, Vector3 scale, bool flipX,
            int sortingLayerID, int sortingOrder, int awayDir)
        {
            if (sprite == null)
            {
                Debug.LogWarning("EjectionCapsule: missing capsule sprite; run Tools/cut-eject-capsules.py");
                return null;
            }
            var go = new GameObject("EjectionCapsule");
            go.transform.SetPositionAndRotation(position, rotation);
            go.transform.localScale = scale;
            var c = go.AddComponent<EjectionCapsule>();
            c._dir = awayDir >= 0 ? 1 : -1;
            c._body = go.AddComponent<SpriteRenderer>();
            c._body.sprite = sprite;
            c._body.flipX = flipX;
            c._body.sortingLayerID = sortingLayerID;
            c._body.sortingOrder = sortingOrder;
            c._halfHeight = sprite.bounds.extents.y * Mathf.Abs(scale.y);
            c._flame = c.CreateFlame("Flame", FlameOuter, sortingOrder - 1);
            c._flameCore = c.CreateFlame("FlameCore", FlameCore, sortingOrder - 1);
            c._velocity = new Vector2(c._dir * 1.2f, 3f);
            c._spin = c._dir * -60f;

            // The bolts: a flash and a ring of sparks where it breaks away.
            ExpandingRing.Spawn(position, Color.white, 3f, 0.25f);
            ProjectileDebris.SpawnSpark(position, new Color(1f, 0.8f, 0.4f), 24, 9f);
            CameraZoomController.TriggerShake(0.6f, 0.25f);
            return c;
        }

        // In world space rather than a child, so the capsule's mirror and tumble don't bend it.
        SpriteRenderer CreateFlame(string name, Color color, int order)
        {
            var sr = new GameObject(name).AddComponent<SpriteRenderer>();
            sr.sprite = VfxSprites.Glow;
            sr.color = color;
            sr.sortingLayerID = _body.sortingLayerID;
            sr.sortingOrder = order;
            sr.enabled = false;
            return sr;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            _age += dt;

            // Thrust leans away from the player and follows the tumble a little, so the burn
            // carves a curve rather than a straight line.
            Vector2 thrustDir = (new Vector2(_dir * 0.35f, 1f).normalized + (Vector2)transform.up * 0.25f).normalized;
            bool burning = _age > BoltTime && _age < BoltTime + BurnTime;
            if (burning)
            {
                _velocity += thrustDir * BurnAccel * dt;
                // The rocket catching: a hard kick and a shake on the first frame of the burn.
                if (_age - dt <= BoltTime)
                {
                    _spin = _dir * -Random.Range(260f, 360f);
                    CameraZoomController.TriggerShake(0.9f, 0.35f);
                    ExpandingRing.Spawn(transform.position, FlameOuter, 4f, 0.3f);
                }
            }
            _velocity += Vector2.down * Gravity * (burning ? 0.3f : 1f) * dt;
            _spin = Mathf.MoveTowards(_spin, _dir * -90f, 120f * dt);

            transform.position += (Vector3)(_velocity * dt);
            transform.Rotate(0f, 0f, _spin * dt);

            UpdateFlame(burning, thrustDir);
            Trail(burning);

            if ((_age > 0.6f && OffScreen()) || _age > 6f) Destroy(gameObject);
        }

        void UpdateFlame(bool burning, Vector2 thrustDir)
        {
            _flame.enabled = _flameCore.enabled = burning;
            if (!burning) return;
            float flicker = Random.Range(0.8f, 1.2f);
            float angle = Mathf.Atan2(thrustDir.y, thrustDir.x) * Mathf.Rad2Deg + 90f;
            Vector3 nozzle = transform.position - (Vector3)(thrustDir * (_halfHeight + 0.2f));
            foreach (var (sr, len, width) in new[] { (_flame, 2.6f, 0.9f), (_flameCore, 1.5f, 0.45f) })
            {
                sr.transform.position = nozzle - (Vector3)(thrustDir * len * 0.35f * flicker);
                sr.transform.rotation = Quaternion.Euler(0f, 0f, angle);
                sr.transform.localScale = new Vector3(width, len * flicker, 1f);
            }
        }

        void Trail(bool burning)
        {
            Vector3 tail = transform.position - (Vector3)(_velocity.normalized * _halfHeight);
            if (burning || Random.value < 0.5f)
                ProjectileDebris.Spawn(tail + (Vector3)(Random.insideUnitCircle * 0.25f), Smoke,
                    Random.Range(0.35f, 0.6f) * (burning ? 1f : 0.6f), -_velocity * 0.08f + Random.insideUnitCircle * 0.4f, 0.45f);
            if (burning && Random.value < 0.6f)
                ProjectileDebris.SpawnSpark(tail, FlameOuter, 2, 5f);
        }

        bool OffScreen()
        {
            var cam = Camera.main;
            if (cam == null) return false;
            Vector3 v = cam.WorldToViewportPoint(transform.position);
            return v.x < -0.2f || v.x > 1.2f || v.y > 1.25f || v.y < -0.3f;
        }

        void OnDestroy()
        {
            if (_flame != null) Destroy(_flame.gameObject);
            if (_flameCore != null) Destroy(_flameCore.gameObject);
        }
    }
}
