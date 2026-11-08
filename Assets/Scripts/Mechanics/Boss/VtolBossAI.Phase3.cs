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
    // Phase 3: the crash landing, the fight from the deck and the exposed cockpit.
    public partial class VtolBossAI
    {
        static readonly Vector2 CockpitCenter = new Vector2(-2.24f, 0.78f);
        static readonly Vector2 CockpitSize = new Vector2(1.1f, 0.8f);

        // The cockpit only becomes reachable once it is down on the deck in phase 3.
        void CreateCockpitWeakPoint()
        {
            var go = new GameObject("Cockpit_WeakPoint");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = CockpitCenter;
            var box = go.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = CockpitSize;
            box.enabled = false;
            _cockpitCollider = box;
            _cockpitHit = go.AddComponent<BossPartHitbox>();
            _cockpitHit.onHit = HitCockpit;

            _cockpitGlow = go.AddComponent<SpriteRenderer>();
            _cockpitGlow.sprite = VfxSprites.Glow;
            MatchHull(_cockpitGlow, (fuselageRenderer != null ? fuselageRenderer.sortingOrder : 12) + 1);
            _cockpitGlow.enabled = false;
        }

        void SetCockpitExposed(bool exposed)
        {
            if (_cockpitCollider != null) _cockpitCollider.enabled = exposed;
            if (_cockpitGlow != null) _cockpitGlow.enabled = exposed;
        }

        void HitCockpit(Vector2 point)
        {
            if (!_isPhase3 || isInvulnerable || _escaping) return;
            ProjectileDebris.SpawnSpark(point, new Color(1f, 0.3f, 0.2f), 10, 6f);
            CameraZoomController.TriggerShake(0.15f, 0.1f);
            TakeDamage(cockpitDamage);
        }

        // A red pulse over the glass so the player can see where to hit.
        void UpdateCockpitGlow()
        {
            if (_cockpitGlow == null || !_cockpitGlow.enabled) return;
            float pulse = 0.55f + 0.45f * Mathf.Sin(Time.time * 7f);
            _cockpitGlow.color = new Color(1f, 0.15f, 0.1f, 0.35f + 0.35f * pulse);
            _cockpitGlow.transform.localScale = Vector3.one * (0.9f + 0.25f * pulse);
        }

        // Phase 3: both ducts give out, it drops onto the deck nose first and skids on its belly,
        // then fights on from the ground with the cockpit low enough to reach.
        IEnumerator CrashLandingRoutine()
        {
            currentState = BossState.Crashing;
            isInvulnerable = true;
            _groundLock = false;
            if (!isPhase2) { isPhase2 = true; ApplyPhaseSprites(); }
            if (!_ductLDamaged) WreckDuct(true, false);
            if (!_ductRDamaged) WreckDuct(false, false);
            if (_healthBar != null) _healthBar.ShowStatus("PHASE 3: LAST STAND [LIFT LOST]");

            // Hit out over the topside, it limps back to the helideck on its last thrust, trailing
            // smoke, so it comes down on open deck rather than on the dome or the containers.
            float homeX = Mathf.Clamp(_rb.position.x, arenaLeft + 6f, arenaRight - 8f);
            if (Mathf.Abs(_rb.position.x - homeX) > 1.5f)
            {
                _limpingHome = true;
                _fanSpinScale = 0.7f;
                Vector2 home = new Vector2(homeX, arenaFloorY + hoverAltitude + 1f);
                float limp = 0f;
                float limpTimeout = Mathf.Abs(_rb.position.x - homeX) / Mathf.Max(1f, maxFlightSpeed) + 4f;
                while (limp < limpTimeout && Mathf.Abs(_rb.position.x - homeX) > 1.5f)
                {
                    limp += Time.deltaTime;
                    SetFacing(homeX < _rb.position.x);
                    Steer(home, maxFlightSpeed * 1.2f, flightAcceleration);
                    if (Random.value < 0.4f) SpawnSmoke(transform.position, 0.5f);
                    if (Random.value < 0.02f) Rock(Random.Range(-60f, 60f));
                    yield return null;
                }
                _limpingHome = false;
            }

            // Losing lift: it fights the stall for a moment.
            _fanSpinScale = 0.3f;
            CameraZoomController.TriggerShake(1.0f, 0.4f);
            float t = 0f;
            while (t < 0.6f)
            {
                t += Time.deltaTime;
                _rb.linearVelocity = Vector2.MoveTowards(_rb.linearVelocity, new Vector2(_rb.linearVelocity.x, 1f), 10f * Time.deltaTime);
                if (Random.value < 0.2f) Rock(Random.Range(-90f, 90f));
                if (Random.value < 0.5f) SpawnSmoke(transform.position, 0.6f);
                yield return null;
            }

            // Falling, nose dropping, still carrying its forward momentum.
            _groundY = arenaFloorY + groundedPivotHeight;
            float vx = Mathf.Clamp(_rb.linearVelocity.x, -4f, 4f) + (isFacingLeft ? -3f : 3f);
            float vy = 0f;
            _tiltOverride = NoseUp(-12f);
            while (_rb.position.y > _groundY + 0.05f)
            {
                vy = Mathf.MoveTowards(vy, -18f, 30f * Time.deltaTime);
                _rb.linearVelocity = new Vector2(vx, vy);
                if (Random.value < 0.5f) SpawnSmoke(transform.position, 0.5f);
                yield return null;
            }

            // Impact.
            Vector2 belly = new Vector2(_rb.position.x, arenaFloorY + 0.1f);
            _groundLock = true;
            HoldOnDeck();
            CameraZoomController.TriggerShake(1.6f, 0.6f);
            ExpandingRing.Spawn(belly, new Color(1f, 0.7f, 0.35f), 6f, 0.4f);
            ProjectileDebris.SpawnSpark(belly, new Color(1f, 0.6f, 0.2f), 40, 12f);
            SpawnDownwash(20);
            Rock(NoseUp(160f));
            Squash(1.25f, 0.75f);
            SpawnShockwaves(belly);

            // Skid on its belly, sparks spraying, until the deck grinds it to a halt.
            float skidFrom = _rb.position.x;
            _rb.linearVelocity = new Vector2(vx * 1.6f, 0f);
            t = 0f;
            while (t < 1.4f && Mathf.Abs(_rb.linearVelocity.x) > 0.3f)
            {
                t += Time.deltaTime;
                yield return null;
            }
            // The gouge it ploughed, glowing hot, with wreckage burning along it.
            BossDeckScar.Spawn(skidFrom, _rb.position.x, arenaFloorY, 3);

            _tiltOverride = NoseUp(-3f);
            yield return new WaitForSeconds(0.6f);

            SetCockpitExposed(true);
            isInvulnerable = false;
            ReturnToGround(0.6f);
        }

        // On the deck: height pinned, belly friction, sparks while it slides.
        void HoldOnDeck()
        {
            float dt = Time.deltaTime;
            float vx = Mathf.MoveTowards(_rb.linearVelocity.x, 0f, 6f * dt);
            _rb.position = new Vector2(_rb.position.x, _groundY);
            _rb.linearVelocity = new Vector2(vx, 0f);
            if (Mathf.Abs(vx) > 0.8f && Random.value < 0.7f)
            {
                Vector3 p = new Vector3(_rb.position.x + Random.Range(-2.5f, 2.5f), arenaFloorY + 0.1f, 0f);
                ProjectileDebris.SpawnSpark(p, new Color(1f, 0.75f, 0.3f), 2, 5f);
            }
        }

        void ReturnToGround(float cooldownScale = 1f)
        {
            currentState = BossState.Grounded;
            _stateTimer = groundAttackCooldown * cooldownScale;
            _tiltOverride = NoseUp(-3f);
            _fanSpinScale = 0.4f;
            _groundLock = true;
        }

        // Between attacks it drags itself after the player: a wrecked duct coughs out a burst and
        // shoves it across the deck.
        void UpdateGrounded()
        {
            if (!EnsurePlayer()) return;
            float dx = _player.transform.position.x - _rb.position.x;

            _lurchTimer -= Time.deltaTime;
            if (_lurchTimer <= 0f && Mathf.Abs(dx) > 5f)
            {
                _lurchTimer = Random.Range(0.9f, 1.6f);
                SetFacing(dx < 0f);
                _rb.linearVelocity = new Vector2(Mathf.Sign(dx) * lurchSpeed, 0f);
                Rock(NoseUp(-40f));
                _ductVelocity += Random.Range(-140f, 140f);
                if (Random.value < 0.5f) _sputterL = 0.2f; else _sputterR = 0.2f;
                if (_ductL != null) ProjectileDebris.SpawnSpark(_ductL.TransformPoint(LeftNozzle - LeftDuctJoint), new Color(1f, 0.5f, 0.15f), 5, 4f);
                if (_ductR != null) ProjectileDebris.SpawnSpark(_ductR.TransformPoint(RightNozzle - RightDuctJoint), new Color(1f, 0.5f, 0.15f), 5, 4f);
                CameraZoomController.TriggerShake(0.12f, 0.1f);
            }

            _stateTimer -= Time.deltaTime;
            if (_stateTimer <= 0f) DecideGroundAttack();
        }

        void DecideGroundAttack()
        {
            var pool = new List<KeyValuePair<BossState, float>>
            {
                new KeyValuePair<BossState, float>(BossState.GroundCannon, 1.0f),
                new KeyValuePair<BossState, float>(BossState.GroundBeam, 0.8f),
                new KeyValuePair<BossState, float>(BossState.LiftAttempt, 0.8f),
            };
            if (!_podLBroken || !_podRBroken) pool.Add(new KeyValuePair<BossState, float>(BossState.MissileSalvo, 0.7f));
            pool.RemoveAll(e => e.Key == _lastAttack);

            float total = 0f;
            foreach (var e in pool) total += e.Value;
            float roll = Random.value * total;
            BossState pick = pool[pool.Count - 1].Key;
            foreach (var e in pool)
            {
                roll -= e.Value;
                if (roll <= 0f) { pick = e.Key; break; }
            }
            _lastAttack = pick;

            switch (pick)
            {
                case BossState.GroundCannon: StartCoroutine(GroundCannonRoutine()); break;
                case BossState.GroundBeam: StartCoroutine(GroundBeamRoutine()); break;
                case BossState.LiftAttempt: StartCoroutine(LiftAttemptRoutine()); break;
                case BossState.MissileSalvo: StartCoroutine(GroundMissileRoutine()); break;
            }
        }

        void FaceThePlayer()
        {
            if (EnsurePlayer()) SetFacing(_player.transform.position.x < _rb.position.x);
        }

        // Bursts from the chin cannon, which is down at the player's height now.
        IEnumerator GroundCannonRoutine()
        {
            currentState = BossState.GroundCannon;
            FaceThePlayer();
            yield return StartCoroutine(TelegraphFlash(0.3f, new Color(1f, 0.8f, 0.4f)));

            Color bulletColor = new Color(1f, 0.3f, 0.15f);
            for (int burst = 0; burst < 3; burst++)
            {
                for (int r = 0; r < 6; r++)
                {
                    Vector2 muzzle = MuzzlePosition;
                    Vector2 aim = Quaternion.Euler(0f, 0f, Random.Range(-5f, 5f)) * BarrelDirection;
                    BossProjectile.Create(muzzle, aim, gunRunBulletSpeed, bulletColor);
                    ProjectileDebris.SpawnSpark(muzzle, bulletColor, 3, 4f);
                    _cannonRecoil = 0.2f;
                    Rock(NoseUp(12f));
                    CameraZoomController.TriggerShake(0.06f, 0.06f);
                    yield return new WaitForSeconds(0.08f);
                }
                yield return new WaitForSeconds(0.35f);
                FaceThePlayer();
            }
            ReturnToGround();
        }

        // A short knee-high blast down the deck, twice: jump it.
        IEnumerator GroundBeamRoutine()
        {
            currentState = BossState.GroundBeam;
            FaceThePlayer();
            if (megaBeamInstance == null) { ReturnToGround(0.5f); yield break; }

            float savedWidth = megaBeamInstance.beamWidth;
            megaBeamInstance.beamWidth = 0.8f;
            _cannonFollowsBeam = true;
            Transform muzzle = cannonMuzzle != null ? cannonMuzzle : transform;
            for (int shot = 0; shot < 2; shot++)
            {
                Vector2 dir = Quaternion.Euler(0f, 0f, isFacingLeft ? 2f : -2f) * (isFacingLeft ? Vector2.left : Vector2.right);
                yield return StartCoroutine(megaBeamInstance.FireBeamSequence(muzzle, dir, 0f, 0.35f, true));
                _rb.linearVelocity += new Vector2(isFacingLeft ? 2f : -2f, 0f);
                Rock(NoseUp(30f));
                _cannonRecoil = 0.25f;
                CameraZoomController.TriggerShake(0.4f, 0.2f);
                yield return new WaitForSeconds(0.4f);
            }
            _cannonFollowsBeam = false;
            megaBeamInstance.beamWidth = savedWidth;
            ReturnToGround();
        }

        // Whatever pods are left fire from the deck.
        IEnumerator GroundMissileRoutine()
        {
            currentState = BossState.MissileSalvo;
            FaceThePlayer();
            _podPitchTarget = 18f;
            yield return StartCoroutine(TelegraphFlash(0.3f, new Color(1f, 0.7f, 0.4f)));

            for (int i = 0; i < missileSalvoCountPhase1; i++)
            {
                bool hasL = !_podLBroken, hasR = !_podRBroken;
                if (!hasL && !hasR) break;
                bool left = hasL && hasR ? (i % 2 == 0) : hasL;
                Transform hatch = left ? leftMissileHatch : rightMissileHatch;
                Vector3 pos = hatch != null ? hatch.position : transform.position + Vector3.up * 2f;
                SpawnMissile(pos, new Vector2(isFacingLeft ? -0.3f : 0.3f, 1f).normalized);
                KickPod(left);
                Rock(NoseUp(25f));
                ProjectileDebris.SpawnSpark(pos, new Color(1f, 0.6f, 0.2f), 6, 4f);
                yield return new WaitForSeconds(missileFireInterval * 1.4f);
            }
            _podPitchTarget = 0f;
            ReturnToGround();
        }

        // It tries to take off again: spools up, heaves into the air, hangs there coughing, then the
        // ducts cut out and it slams back down, throwing shockwaves along the deck.
        IEnumerator LiftAttemptRoutine()
        {
            currentState = BossState.LiftAttempt;
            _groundLock = false;
            _tiltOverride = 0f;
            _fanSpinScale = 1.6f;

            float t = 0f;
            while (t < 0.5f)
            {
                t += Time.deltaTime;
                SpawnDownwash(3);
                CameraZoomController.TriggerShake(0.1f, 0.08f);
                yield return null;
            }

            Vector2 hang = new Vector2(_rb.position.x, _groundY + 2.6f);
            t = 0f;
            while (t < 0.9f)
            {
                t += Time.deltaTime;
                Steer(hang, 4f, 14f);
                if (Random.value < 0.08f) Rock(Random.Range(-80f, 80f));
                SpawnDownwash(1);
                yield return null;
            }
            t = 0f;
            while (t < 0.45f)
            {
                t += Time.deltaTime;
                Steer(hang, 4f, 14f);
                if (Random.value < 0.15f)
                {
                    _sputterL = _sputterR = 0.15f;
                    Rock(Random.Range(-60f, 60f));
                }
                yield return null;
            }

            // Cut out.
            _fanSpinScale = 0.2f;
            Rock(NoseUp(-30f));
            float vy = _rb.linearVelocity.y;
            while (_rb.position.y > _groundY + 0.05f)
            {
                vy = Mathf.MoveTowards(vy, -16f, 40f * Time.deltaTime);
                _rb.linearVelocity = new Vector2(_rb.linearVelocity.x * 0.98f, vy);
                yield return null;
            }

            Vector2 belly = new Vector2(_rb.position.x, arenaFloorY + 0.1f);
            _groundLock = true;
            HoldOnDeck();
            CameraZoomController.TriggerShake(1.0f, 0.4f);
            ExpandingRing.Spawn(belly, new Color(1f, 0.6f, 0.25f), 4.5f, 0.3f);
            ProjectileDebris.SpawnSpark(belly, new Color(1f, 0.6f, 0.2f), 24, 9f);
            Rock(NoseUp(120f));
            Squash(1.2f, 0.8f);
            SpawnShockwaves(belly);
            BossDeckScar.Spawn(belly.x - 0.6f, belly.x + 0.6f, arenaFloorY, 0, 0.3f);

            yield return new WaitForSeconds(0.5f);
            ReturnToGround();
        }

        void SpawnShockwaves(Vector2 belly)
        {
            Color wave = new Color(1f, 0.55f, 0.15f);
            Vector2 at = new Vector2(belly.x, arenaFloorY + 0.05f);
            BossShockwave.Create(at + Vector2.left * 3.4f, -1f, 8f, wave);
            BossShockwave.Create(at + Vector2.right * 3.4f, 1f, 8f, wave);
        }
    }
}
