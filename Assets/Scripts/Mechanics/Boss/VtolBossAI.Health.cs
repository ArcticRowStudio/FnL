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
    // Taking damage, phase changes, invulnerability, and the ending: the escape (or a defeat).
    public partial class VtolBossAI
    {
        // Hold off EnemyDeath (scheduled by whichever weapon lands the last hit) until DefeatRoutine finishes.
        public override bool DefersDeath => !_deathSequenceDone;

        // Player weapons decrement Health directly rather than calling TakeDamage, so every hit
        // reaction has to hang off the health event.
        void HandleHealthChanged(Health h)
        {
            if (h == null || currentState == BossState.Defeated) return;

            StartCoroutine(FlashDamage());
            // Jolted by the hit.
            if (_rb != null) _rb.linearVelocity += Random.insideUnitCircle * 1.5f;
            Squash(0.94f, 1.06f);
            Rock(Random.Range(-70f, 70f));

            if (_escaping) return;

            if (!h.IsAlive)
            {
                InterruptCurrentAction();
                if (pilotEjectsOnDefeat) StartCoroutine(PilotEjectRoutine());
                else if (escapeOnDefeat) BeginEscape();
                else StartCoroutine(DefeatRoutine());
            }
            else if (!_isPhase3 && h.CurrentHP <= _maxHp * phase3Threshold)
            {
                // Checked before phase 2: a part breaking can carry it through both at once.
                _isPhase3 = true;
                _hasTriggeredPhase2 = true;
                InterruptCurrentAction();
                StartCoroutine(CrashLandingRoutine());
            }
            else if (!_hasTriggeredPhase2 && h.CurrentHP <= _maxHp * phase2Threshold)
            {
                _hasTriggeredPhase2 = true;
                InterruptCurrentAction();
                StartCoroutine(PhaseTransitionRoutine());
            }
        }

        // Stops whatever attack is running so it cannot keep firing through the transition or
        // reset currentState to Hovering underneath it.
        void InterruptCurrentAction()
        {
            StopAllCoroutines();
            if (megaBeamInstance != null) megaBeamInstance.Cancel();
            SetHullColor(_fuselageBaseColor);
            _cannonFollowsBeam = false;
            _ductOverride = 0f;
            _podPitchTarget = 0f;
        }

        public override bool CanTakeDamage(Vector2 hitPosition, Vector2 attackerPosition)
        {
            return !isInvulnerable && currentState != BossState.Defeated;
        }

        public override bool BlocksRangedHit(Vector2 hitPosition)
        {
            return isInvulnerable || currentState == BossState.Defeated;
        }

        IEnumerator PhaseTransitionRoutine()
        {
            currentState = BossState.PhaseTransition;
            isInvulnerable = true;
            _rb.linearVelocity = Vector2.zero;

            CameraZoomController.TriggerShake(1.8f, 0.4f);

            // Explosions rock it about the sky as it nearly loses control.
            for (int i = 0; i < 6; i++)
            {
                Vector3 sparkPos = transform.position + new Vector3(Random.Range(-2.5f, 2.5f), Random.Range(-1.5f, 1.5f), 0f);
                ExpandingRing.Spawn(sparkPos, Color.white, 2.5f, 0.25f);
                ProjectileDebris.SpawnSpark(sparkPos, new Color(1f, 0.4f, 0.1f), 15, 8f);
                _rb.linearVelocity = Random.insideUnitCircle * 4f + Vector2.down * 1.5f;
                Rock(Random.Range(-160f, 160f));
                Squash(1.1f, 0.9f);
                yield return new WaitForSeconds(0.2f);
            }
            _tiltOverride = 0f;

            // Swap to Overdrive Phase 2 Sprite
            isPhase2 = true;
            ApplyPhaseSprites();

            // Command all active drones to trigger Kamikaze Dive
            if (_player != null)
            {
                foreach (var drone in _activeDrones)
                {
                    if (drone != null)
                        drone.TriggerKamikazeDive(_player.transform);
                }
            }

            // Overdrive kicks in: it rears back and blasts upward.
            Squash(0.85f, 1.15f);
            ExpandingRing.Spawn(transform.position, phase2GlowColor, 6f, 0.4f);
            CameraZoomController.TriggerShake(0.8f, 0.3f);
            Vector2 surge = new Vector2(_rb.position.x, Mathf.Min(arenaCeilingY - 0.5f, _rb.position.y + 3f));
            yield return StartCoroutine(SteerFor(0.8f, () => surge, FlightSpeed * 1.5f, FlightAccel * 2f));

            isInvulnerable = false;
            ReturnToHover(0.4f); // Fast follow-up attack!
        }

        IEnumerator FlashDamage()
        {
            if (fuselageRenderer != null)
            {
                // Over-bright, like the Quadpod's flash: plain white on a white tint shows nothing.
                // Restores the cached colour so overlapping flashes cannot leave it stuck bright.
                SetHullColor(new Color(2f, 2f, 2f, 1f));
                yield return new WaitForSeconds(0.08f);
                SetHullColor(_fuselageBaseColor);
            }
        }

        public void TakeDamage(int damage)
        {
            if (isInvulnerable || currentState == BossState.Defeated) return;

            // Flash, phase change and defeat follow from HandleHealthChanged.
            for (int i = 0; i < Mathf.Max(1, damage) && _bossHealth != null && _bossHealth.IsAlive && !isInvulnerable; i++)
                _bossHealth.Decrement();
        }

        // Beaten, the pilot gets it off the deck one last time: it covers its retreat, limps out past
        // the deck edge toward the next map and shrinks into the distance behind the helipad,
        // smoking and sputtering. What it had left is saved for the ship, where it comes down.
        void BeginEscape()
        {
            _escaping = true;
            InterruptCurrentAction();
            isInvulnerable = true;
            foreach (var hb in new[] { _podLHit, _podRHit, _ductLHit, _ductRHit, _cockpitHit })
                if (hb != null) hb.acceptsHits = false;
            SetCockpitExposed(false);

            // It always leaves visibly hurt: if neither duct has given out yet, one does now.
            if (!_ductLDamaged && !_ductRDamaged) WreckDuct(Random.value < 0.5f, false);

            if (_healthBar != null) _healthBar.ShowStatus("TARGET BREAKING OFF");
            StartCoroutine(EscapeRoutine());
        }

        IEnumerator EscapeRoutine()
        {
            currentState = BossState.Escaping;

            // Off the deck on what is left of the ducts: throttle up, heave, cough, heave again.
            if (_groundLock)
            {
                _groundLock = false;
                _tiltOverride = 0f;
                _fanSpinScale = 1.6f;
                float spool = 0f;
                while (spool < 0.8f)
                {
                    spool += Time.deltaTime;
                    SpawnDownwash(3);
                    if (Random.value < 0.1f) Rock(Random.Range(-60f, 60f));
                    CameraZoomController.TriggerShake(0.15f, 0.1f);
                    yield return null;
                }
                Vector2 up = new Vector2(_rb.position.x, _groundY + 3.5f);
                float heave = 0f;
                while (heave < 1.6f)
                {
                    heave += Time.deltaTime;
                    Steer(up, 3f, 6f);
                    if (Random.value < 0.1f)
                    {
                        Rock(Random.Range(-90f, 90f));
                        _sputterL = _sputterR = 0.2f;
                        _rb.linearVelocity += Vector2.down * 1.2f;
                    }
                    SpawnDownwash(1);
                    yield return null;
                }
                _fanSpinScale = 1f;
            }

            // Covering fire: escorts thrown at the player, a last volley from whatever pods are left,
            // and a smoke screen.
            if (_player != null)
                foreach (var drone in _activeDrones)
                    if (drone != null) drone.TriggerKamikazeDive(_player.transform);
            for (int i = 0; i < 6; i++) SpawnSmoke(transform.position + (Vector3)(Random.insideUnitCircle * 2.5f), Random.Range(0.6f, 1.0f));

            // Turn for the next map (to the right) and back off with the ducts flared.
            SetFacing(false);
            _ductOverride = -25f;
            _podPitchTarget = 14f;
            int volley = 0;
            float t = 0f;
            while (t < 1.2f)
            {
                t += Time.deltaTime;
                _rb.linearVelocity = Vector2.MoveTowards(_rb.linearVelocity, new Vector2(1.5f, 2.5f), 6f * Time.deltaTime);
                if (volley < 4 && t > 0.25f + volley * 0.2f)
                {
                    bool left = (!_podLBroken && (_podRBroken || volley % 2 == 0));
                    if (!_podLBroken || !_podRBroken)
                    {
                        Transform hatch = left ? leftMissileHatch : rightMissileHatch;
                        if (hatch != null) SpawnMissile(hatch.position, new Vector2(-0.4f, 1f).normalized);
                        KickPod(left);
                    }
                    volley++;
                }
                yield return null;
            }
            _podPitchTarget = 0f;
            _ductOverride = 0f;

            // Climb out past the deck edge.
            Vector2 exit = new Vector2(arenaRight + 3f, Mathf.Min(arenaCeilingY, _rb.position.y + 3f));
            yield return StartCoroutine(FlyTo(exit, maxFlightSpeed * 1.2f, flightAcceleration * 1.2f, 1.2f, 2.5f));

            // Into the distance: shrinking, hazing over and dropping behind the helipad, losing
            // height in fits as the wrecked duct cuts out.
            if (_healthBar != null) { _healthBar.ShowStatus("TARGET ESCAPED"); _healthBar.Dismiss(2.5f); }
            var group = gameObject.GetComponent<SortingGroup>();
            if (group == null) group = gameObject.AddComponent<SortingGroup>();
            group.sortingOrder = 20;

            Vector2 start = _rb.position;
            Vector2 end = start + new Vector2(30f, 3f);
            Color haze = new Color(0.78f, 0.62f, 0.58f, 1f);
            float duration = 4.5f;
            t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / duration);
                float dip = -Mathf.Abs(Mathf.Sin(t * 2.3f)) * 0.6f * u;
                Vector2 p = Vector2.Lerp(start, end, Mathf.SmoothStep(0f, 1f, u)) + Vector2.up * dip;
                _rb.linearVelocity = (p - _rb.position) / Mathf.Max(Time.deltaTime, 0.0001f);
                _depthScale = Mathf.Lerp(1f, 0.22f, Mathf.SmoothStep(0f, 1f, u));
                SetHullColor(Color.Lerp(_fuselageBaseColor, haze, u * 0.8f));

                if (!_far && u > 0.3f)
                {
                    _far = true;
                    RemoveOffscreenIndicator();
                    group.sortingOrder = 1;   // behind the truss (2) and helipad (3)
                    CameraZoomController.ClearFocus(_col);
                }
                if (Random.value < 0.4f) SpawnSmoke(transform.position, 0.5f * _depthScale + 0.1f);
                yield return null;
            }

            VtolBossCarryOver.Save(new VtolBossCarryOver.Record
            {
                hp = _bossHealth != null ? _bossHealth.CurrentHP : 0,
                maxHp = _maxHp,
                leftDuctWrecked = _ductLDamaged,
                rightDuctWrecked = _ductRDamaged,
                leftPodBroken = _podLBroken,
                rightPodBroken = _podRBroken,
            });
            onEscaped?.Invoke();
            Destroy(gameObject);
        }

        // Beaten for good: the wreck shudders, the canopy blows and the pilot scrambles out and
        // runs for it (away from the player, off the deck edge if that way leads there), then the
        // empty gunship goes up behind it.
        IEnumerator PilotEjectRoutine()
        {
            currentState = BossState.Defeated;
            isInvulnerable = true;
            foreach (var hb in new[] { _podLHit, _podRHit, _ductLHit, _ductRHit, _cockpitHit })
                if (hb != null) hb.acceptsHits = false;
            SetCockpitExposed(false);
            if (_healthBar != null) { _healthBar.ShowStatus("PILOT EJECTED"); _healthBar.Dismiss(2.5f); }
            RemoveOffscreenIndicator();
            _fanSpinScale = 0.2f;

            // Systems failing.
            float t = 0f;
            while (t < 0.9f)
            {
                t += Time.deltaTime;
                if (_groundLock) HoldOnDeck();
                else _rb.linearVelocity = Vector2.MoveTowards(_rb.linearVelocity, Vector2.zero, 8f * Time.deltaTime);
                if (Random.value < 0.4f) SpawnSmoke(transform.position + (Vector3)(Random.insideUnitCircle * 1.5f), 0.6f);
                if (Random.value < 0.15f)
                {
                    ProjectileDebris.SpawnSpark(transform.position + (Vector3)(Random.insideUnitCircle * 2f), new Color(1f, 0.6f, 0.2f), 8, 6f);
                    CameraZoomController.TriggerShake(0.1f, 0.08f);
                }
                yield return null;
            }

            if (pilotExitStyle == PilotExitStyle.EjectCapsule)
            {
                LaunchCockpit();
            }
            else
            {
                // Canopy blows, leaving the scorched hollow, and the pilot comes out of it.
                Vector3 hatch = transform.TransformPoint(CockpitCenter);
                ExpandingRing.Spawn(hatch, Color.white, 2.5f, 0.25f);
                ProjectileDebris.SpawnSpark(hatch, new Color(1f, 0.8f, 0.4f), 18, 8f);
                CameraZoomController.TriggerShake(0.4f, 0.2f);
                var hollow = Resources.Load<Sprite>("BossPilot/Vtol_Ejected_" + (isPhase2 ? "P2" : "P1"));
                if (hollow != null && fuselageRenderer != null) fuselageRenderer.sprite = hollow;
                SpawnPilot(hatch, pilotExitStyle == PilotExitStyle.HopAway);
            }

            t = 0f;
            while (t < 1.1f)
            {
                t += Time.deltaTime;
                if (_groundLock) HoldOnDeck();
                if (Random.value < 0.3f) SpawnSmoke(transform.position, 0.6f);
                yield return null;
            }

            onEscaped?.Invoke();
            if (_groundLock) yield return StartCoroutine(GroundedDetonation());
            else yield return StartCoroutine(DefeatRoutine());
        }

        // Centre of the canopy block in hull-local units, printed by Tools/cut-eject-capsules.py.
        static readonly Vector2 CapsuleCentre = new Vector2(-1.903f, 0.825f);

        // The canopy block fires off with the pilot in it, away from the player, and the hull is
        // left with a scorched hollow where it sat.
        void LaunchCockpit()
        {
            string phase = isPhase2 ? "P2" : "P1";
            int dir = 1;
            if (EnsurePlayer())
            {
                float away = transform.position.x - _player.transform.position.x;
                if (Mathf.Abs(away) > 0.5f) dir = away > 0f ? 1 : -1;
            }
            int layer = fuselageRenderer != null ? fuselageRenderer.sortingLayerID : 0;
            int order = (fuselageRenderer != null ? fuselageRenderer.sortingOrder : 12) + 4;
            EjectionCapsule.Launch(Resources.Load<Sprite>("BossPilot/Capsule_Vtol_" + phase),
                transform.TransformPoint(CapsuleCentre), transform.rotation, transform.lossyScale, false, layer, order, dir);

            var hollow = Resources.Load<Sprite>("BossPilot/Vtol_Ejected_" + phase);
            if (hollow != null && fuselageRenderer != null) fuselageRenderer.sprite = hollow;
        }

        // Running, it goes away from the player. Hopping, it always makes for the sea past the
        // helideck's right edge (the way to the ship), bounding over the player if it has to.
        void SpawnPilot(Vector2 hatch, bool hop)
        {
            int dir = 1;
            if (!hop && EnsurePlayer())
            {
                float away = hatch.x - _player.transform.position.x;
                if (Mathf.Abs(away) > 0.5f) dir = away > 0f ? 1 : -1;
            }
            // Off the helideck's right edge if it goes that way; otherwise out of shot.
            float leapAt = dir > 0 && hatch.x < arenaRight ? arenaRight - 0.5f : float.NaN;
            int layer = fuselageRenderer != null ? fuselageRenderer.sortingLayerID : 0;
            int order = (fuselageRenderer != null ? fuselageRenderer.sortingOrder : 12) + 4;
            BossPilotEscape.Spawn(hatch + Vector2.up * 0.3f, arenaFloorY, dir, leapAt, true, layer, order, hop);
        }

        // The empty wreck on the deck: a chain of blasts along the hull, then the big one.
        IEnumerator GroundedDetonation()
        {
            for (int i = 0; i < 6; i++)
            {
                Vector3 boomPos = transform.position + new Vector3(Random.Range(-3f, 3f), Random.Range(-1f, 1.5f), 0f);
                ExpandingRing.Spawn(boomPos, Color.white, 3f, 0.3f);
                ProjectileDebris.SpawnSpark(boomPos, new Color(1f, 0.5f, 0.1f), 18, 9f);
                CameraZoomController.TriggerShake(0.3f, 0.3f);
                float wait = 0f;
                while (wait < 0.2f)
                {
                    wait += Time.deltaTime;
                    HoldOnDeck();
                    yield return null;
                }
            }

            CameraZoomController.TriggerShake(1.2f, 0.6f);
            ExpandingRing.Spawn(transform.position, Color.white, 8f, 0.6f);
            ProjectileDebris.SpawnSpark(transform.position, new Color(1f, 0.3f, 0.1f), 60, 15f);
            BossDeckScar.Spawn(_rb.position.x - 2.5f, _rb.position.x + 2.5f, arenaFloorY, 3);

            _deathSequenceDone = true;
            var ev = Schedule<EnemyDeath>();
            ev.enemy = this;
            Destroy(gameObject, 0.2f);
        }

        IEnumerator DefeatRoutine()
        {
            currentState = BossState.Defeated;
            isInvulnerable = true;
            _rb.linearVelocity = Vector2.zero;
            _rb.gravityScale = 1.2f; // Crash down!

            for (int i = 0; i < 10; i++)
            {
                Vector3 boomPos = transform.position + new Vector3(Random.Range(-3f, 3f), Random.Range(-2f, 2f), 0f);
                ExpandingRing.Spawn(boomPos, Color.white, 3.5f, 0.3f);
                ProjectileDebris.SpawnSpark(boomPos, new Color(1f, 0.5f, 0.1f), 20, 9f);
                CameraZoomController.TriggerShake(0.3f, 0.35f);

                // Spiralling down.
                float wait = 0f;
                while (wait < 0.18f)
                {
                    wait += Time.deltaTime;
                    transform.rotation *= Quaternion.Euler(0f, 0f, (isFacingLeft ? 140f : -140f) * Time.deltaTime);
                    // Fans wind down as the power dies.
                    _fanSpinScale = Mathf.MoveTowards(_fanSpinScale, 0f, 0.6f * Time.deltaTime);
                    UpdateFans();
                    yield return null;
                }
            }

            // Final Catastrophic Detonation
            CameraZoomController.TriggerShake(1.2f, 0.6f);
            ExpandingRing.Spawn(transform.position, Color.white, 8f, 0.6f);
            ProjectileDebris.SpawnSpark(transform.position, new Color(1f, 0.3f, 0.1f), 60, 15f);

            _deathSequenceDone = true;
            var ev = Schedule<EnemyDeath>();
            ev.enemy = this;

            Destroy(gameObject, 0.2f);
        }

        protected override void OnCollisionEnter2D(Collision2D collision)
        {
            if (currentState == BossState.Defeated) return;

            var player = collision.gameObject.GetComponent<PlayerController>();
            if (player != null)
            {
                if (isInvulnerable) return;

                // Player bounce on top of boss cockpit
                bool willHurtBoss = player.Bounds.min.y >= Bounds.center.y + 0.8f;
                if (willHurtBoss)
                {
                    TakeDamage(1);
                    player.Bounce(9);
                }
                else
                {
                    if (player.isParrying)
                    {
                        player.OnParrySuccess(gameObject);
                    }
                    else if (!player.isInvincible)
                    {
                        Vector2 dir = (player.transform.position - transform.position).normalized;
                        Vector2 knockback = new Vector2(Mathf.Sign(dir.x != 0 ? dir.x : 1f) * 5f, 4f);
                        player.TakeDamage(1, knockback);
                    }
                }
            }
        }
    }
}
