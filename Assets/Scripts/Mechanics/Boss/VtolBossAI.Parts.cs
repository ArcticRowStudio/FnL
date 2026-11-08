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
    // Breakable parts: pod and duct hitboxes, pods blown off, wrecked ducts and their sputtering.
    public partial class VtolBossAI
    {
        enum Part { PodL, PodR, DuctL, DuctR }
        // Duct body hitboxes in each duct's own space (the flame is not solid).
        static readonly Vector2 LeftDuctBoxOffset = new Vector2(-0.377f, 0.031f);
        static readonly Vector2 LeftDuctBoxSize = new Vector2(1.48f, 1.96f);
        static readonly Vector2 RightDuctBoxOffset = new Vector2(0.654f, -0.023f);
        static readonly Vector2 RightDuctBoxSize = new Vector2(1.72f, 2.08f);

        // A trigger on the part itself. Being a child of the boss, it rides the boss's body as a
        // compound collider; as a trigger it never pushes anything.
        BossPartHitbox AddHitbox(Transform part, SpriteRenderer sr, Part id, Vector2 offset, Vector2 size)
        {
            if (part == null) return null;
            var box = part.gameObject.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            if (size == Vector2.zero && sr != null && sr.sprite != null)
            {
                box.offset = sr.sprite.bounds.center;
                box.size = sr.sprite.bounds.size;
            }
            else
            {
                box.offset = offset;
                box.size = size;
            }
            var hb = part.gameObject.AddComponent<BossPartHitbox>();
            hb.onHit = p => HitPart(id, p);
            return hb;
        }

        bool PartsAcceptHits => !isInvulnerable && currentState != BossState.Defeated
            && currentState != BossState.Dormant && currentState != BossState.Intro;

        int DamagedDuctCount => (_ductLDamaged ? 1 : 0) + (_ductRDamaged ? 1 : 0);

        // Damage glows redder as a part wears down, so the player can see it is going to give.
        static Color WearTint(int hp, int max) => Color.Lerp(new Color(1f, 0.5f, 0.42f), Color.white, Mathf.Clamp01((float)hp / Mathf.Max(1, max)));

        void HitPart(Part part, Vector2 point)
        {
            if (!PartsAcceptHits) return;

            ProjectileDebris.SpawnSpark(point, new Color(1f, 0.75f, 0.35f), 6, 5f);
            switch (part)
            {
                case Part.PodL:
                    if (_podLBroken) return;
                    _tintPodL = WearTint(--_podLHp, podHealth);
                    StartCoroutine(FlashPart(_podLRenderer));
                    if (_podLHp <= 0) BreakPod(true);
                    break;
                case Part.PodR:
                    if (_podRBroken) return;
                    _tintPodR = WearTint(--_podRHp, podHealth);
                    StartCoroutine(FlashPart(_podRRenderer));
                    if (_podRHp <= 0) BreakPod(false);
                    break;
                case Part.DuctL:
                    if (_ductLDamaged) return;
                    _tintDuctL = WearTint(--_ductLHp, ductHealth);
                    StartCoroutine(FlashPart(_ductLRenderer));
                    if (_ductLHp <= 0) WreckDuct(true);
                    break;
                case Part.DuctR:
                    if (_ductRDamaged) return;
                    _tintDuctR = WearTint(--_ductRHp, ductHealth);
                    StartCoroutine(FlashPart(_ductRRenderer));
                    if (_ductRHp <= 0) WreckDuct(false);
                    break;
            }
        }

        IEnumerator FlashPart(SpriteRenderer sr)
        {
            if (sr == null) yield break;
            sr.color = new Color(2f, 2f, 2f, 1f);
            yield return new WaitForSeconds(0.06f);
            if (sr != null) sr.color = _fuselageBaseColor * PartTint(sr);
        }

        // The pod blows off its mount and tumbles to the deck; its missiles are gone with it.
        void BreakPod(bool left)
        {
            Transform pod = left ? _podL : _podR;
            SpriteRenderer sr = left ? _podLRenderer : _podRRenderer;
            BossPartHitbox hb = left ? _podLHit : _podRHit;
            if (left) { _podLBroken = true; _podL = null; _podLRenderer = null; }
            else { _podRBroken = true; _podR = null; _podRRenderer = null; }
            if (pod == null) return;

            Vector3 pos = pod.position;
            CameraZoomController.TriggerShake(0.8f, 0.35f);
            ExpandingRing.Spawn(pos, new Color(1f, 0.6f, 0.2f), 3.5f, 0.3f);
            ProjectileDebris.SpawnSpark(pos, new Color(1f, 0.5f, 0.15f), 30, 10f);

            if (hb != null) { hb.acceptsHits = false; Destroy(hb); }
            foreach (var c in pod.GetComponents<Collider2D>()) Destroy(c);

            pod.SetParent(null, true);
            if (sr != null) sr.color = new Color(0.35f, 0.32f, 0.32f, 1f);
            var body = pod.gameObject.AddComponent<Rigidbody2D>();
            body.gravityScale = 2.2f;
            float outward = (left == isFacingLeft) ? -1f : 1f;  // art-left pod flies off the nose side
            body.linearVelocity = new Vector2(outward * Random.Range(1.5f, 3.5f) + _rb.linearVelocity.x, Random.Range(5f, 7f));
            body.angularVelocity = outward * Random.Range(-420f, -260f);
            var wreck = pod.gameObject.AddComponent<BossWreckage>();
            wreck.floorY = arenaFloorY;

            Rock(Random.Range(-140f, 140f));
            _rb.linearVelocity += Vector2.down * 1.5f;
            TakeDamage(partBreakDamage);
        }

        // A wrecked duct stays on (the craft needs it to fly) but loses thrust: the airframe hangs
        // toward that side, smokes, rocks harder and the engine sputters.
        void WreckDuct(bool left, bool dealDamage = true)
        {
            if (left) _ductLDamaged = true; else _ductRDamaged = true;
            Transform duct = left ? _ductL : _ductR;
            BossPartHitbox hb = left ? _ductLHit : _ductRHit;
            if (hb != null) hb.acceptsHits = false;
            // Shots at the wreck should carry on into the hull, not vanish on a dead hitbox.
            if (duct != null) foreach (var c in duct.GetComponents<Collider2D>()) c.enabled = false;
            if (left) _tintDuctL = new Color(0.62f, 0.56f, 0.55f, 1f); else _tintDuctR = new Color(0.62f, 0.56f, 0.55f, 1f);
            SetHullColor(_fuselageBaseColor);

            if (duct != null)
            {
                CameraZoomController.TriggerShake(1.0f, 0.4f);
                ExpandingRing.Spawn(duct.position, new Color(1f, 0.55f, 0.2f), 4f, 0.35f);
                ProjectileDebris.SpawnSpark(duct.position, new Color(1f, 0.45f, 0.1f), 36, 11f);
            }

            Rock(Random.Range(-200f, 200f));
            _ductVelocity += Random.Range(-260f, 260f);
            _rb.linearVelocity += Vector2.down * 3f;
            if (dealDamage) TakeDamage(partBreakDamage);
        }

        // Tilt the airframe toward the wrecked side(s), in world degrees.
        float DamageLean()
        {
            float artLean = (_ductLDamaged ? damagedDuctLean : 0f) - (_ductRDamaged ? damagedDuctLean : 0f);
            return artLean * MirrorSign;
        }

        void UpdateDamageEffects()
        {
            float dt = Time.deltaTime;
            _sputterL = Mathf.MoveTowards(_sputterL, 1f, 1.5f * dt);
            _sputterR = Mathf.MoveTowards(_sputterR, 1f, 1.5f * dt);

            if (_podLBroken && Random.value < 0.15f) SpawnSmoke(transform.TransformPoint(LeftPodJoint), 0.35f);
            if (_podRBroken && Random.value < 0.15f) SpawnSmoke(transform.TransformPoint(RightPodJoint), 0.35f);

            if (DamagedDuctCount == 0) return;

            if (_ductLDamaged) DuctSmoke(_ductL, LeftDuctBoxOffset);
            if (_ductRDamaged) DuctSmoke(_ductR, RightDuctBoxOffset);

            // Engine sputter: a hiccup on a wrecked duct drops that side and jolts the airframe.
            _sputterTimer -= dt;
            if (_sputterTimer <= 0f)
            {
                _sputterTimer = Random.Range(0.6f, 1.6f) / DamagedDuctCount;
                bool left = _ductLDamaged && (!_ductRDamaged || Random.value < 0.5f);
                if (left) _sputterL = 0.15f; else _sputterR = 0.15f;
                float artKick = left ? Random.Range(50f, 110f) : -Random.Range(50f, 110f);
                Rock(artKick * MirrorSign);
                _ductVelocity += Random.Range(-120f, 120f);
                _rb.linearVelocity += Vector2.down * Random.Range(0.8f, 1.8f);
                Transform duct = left ? _ductL : _ductR;
                if (duct != null)
                {
                    ProjectileDebris.SpawnSpark(duct.position, new Color(1f, 0.5f, 0.15f), 8, 5f);
                    for (int i = 0; i < 3; i++) SpawnSmoke(duct.position, 0.5f);
                }
            }
        }

        void DuctSmoke(Transform duct, Vector2 bodyOffset)
        {
            if (duct == null) return;
            Vector3 p = duct.TransformPoint(bodyOffset + Random.insideUnitCircle * 0.5f);
            if (Random.value < 0.55f) SpawnSmoke(p, Random.Range(0.35f, 0.6f));
            if (!_far && Random.value < 0.12f) ProjectileDebris.SpawnSpark(p, new Color(1f, 0.45f, 0.1f), 1, 2.5f);
        }
    }
}
