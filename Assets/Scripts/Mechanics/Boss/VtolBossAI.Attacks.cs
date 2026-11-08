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
    // Phase 1 and 2 attacks, flown on the move.
    public partial class VtolBossAI
    {
        // longRange: the player is out of reach of the close attacks, so only the homing missiles
        // and the drones, which find the player by themselves, are on offer.
        void DecideNextAttack(bool longRange)
        {
            _activeDrones.RemoveAll(d => d == null);

            var pool = new List<KeyValuePair<BossState, float>>();
            if (!longRange)
            {
                // A swoop needs open deck either side of the player, not a crane or the tower.
                if (SwoopRoom(out _, out _)) pool.Add(new KeyValuePair<BossState, float>(BossState.Swoop, 1.1f));
                pool.Add(new KeyValuePair<BossState, float>(BossState.GunRun, 1.0f));
                pool.Add(new KeyValuePair<BossState, float>(BossState.MegaBeamStrike, 0.7f));
                if (isPhase2) pool.Add(new KeyValuePair<BossState, float>(BossState.CarpetBombing, 1.0f));
            }
            if (!_podLBroken || !_podRBroken) pool.Add(new KeyValuePair<BossState, float>(BossState.MissileSalvo, 1.0f));
            if (_activeDrones.Count == 0) pool.Add(new KeyValuePair<BossState, float>(BossState.DeployDrones, 0.8f));

            // Never the same attack twice running, unless it is the only one on offer.
            if (pool.Count > 1) pool.RemoveAll(e => e.Key == _lastAttack);
            if (pool.Count == 0) { _stateTimer = 0.5f; return; }

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
                case BossState.MissileSalvo: StartCoroutine(MissileSalvoRoutine()); break;
                case BossState.Swoop: StartCoroutine(SwoopRoutine()); break;
                case BossState.GunRun: StartCoroutine(GunRunRoutine()); break;
                case BossState.MegaBeamStrike: StartCoroutine(MegaBeamRoutine()); break;
                case BossState.DeployDrones: StartCoroutine(DeployDronesRoutine()); break;
                case BossState.CarpetBombing: StartCoroutine(CarpetBombingRoutine()); break;
            }
        }

        // Climbs and drifts across while the pods empty, kicked down and back by every launch.
        IEnumerator MissileSalvoRoutine()
        {
            currentState = BossState.MissileSalvo;

            AttackWindow(out float left, out float right);
            float drift = _rb.position.x < (left + right) * 0.5f ? 1f : -1f;
            float altitude = Mathf.Min(arenaCeilingY - 0.5f, arenaFloorY + hoverAltitude + 2f);

            Squash(0.9f, 1.1f);
            _podPitchTarget = 14f;
            yield return StartCoroutine(TelegraphFlash(0.3f, new Color(1f, 0.7f, 0.4f)));

            int count = isPhase2 ? missileSalvoCountPhase2 : missileSalvoCountPhase1;
            for (int i = 0; i < count; i++)
            {
                // A pod can be blown off mid-salvo: fire from what is left, or stop.
                bool hasL = !_podLBroken, hasR = !_podRBroken;
                if (!hasL && !hasR) break;
                if (!(hasL && hasR) && i % 2 == 1) continue;  // one pod fires half as many

                if (EnsurePlayer()) SetFacing(_player.transform.position.x < _rb.position.x);

                bool useLeftHatch = hasL && hasR ? (i % 2 == 0) : hasL;
                Vector3 launchPos = useLeftHatch
                    ? (leftMissileHatch != null ? leftMissileHatch.position : transform.position + new Vector3(-1.2f, 1.8f, 0f))
                    : (rightMissileHatch != null ? rightMissileHatch.position : transform.position + new Vector3(1.2f, 1.8f, 0f));

                float launchAngleX = useLeftHatch ? Random.Range(-2.5f, -0.5f) : Random.Range(0.5f, 2.5f);
                Vector2 launchDir = new Vector2(isFacingLeft ? launchAngleX : -launchAngleX, Random.Range(6f, 9f)).normalized;

                SpawnMissile(launchPos, launchDir);
                KickPod(useLeftHatch);

                _rb.linearVelocity += new Vector2(isFacingLeft ? 0.8f : -0.8f, -1.2f);
                Squash(1.06f, 0.94f);
                Rock(NoseUp(45f));
                CameraZoomController.TriggerShake(0.12f, 0.1f);
                ProjectileDebris.SpawnSpark(launchPos, new Color(1f, 0.6f, 0.2f), 6, 4f);

                float x = Mathf.Clamp(_rb.position.x + drift * 3f, RoamLeft + 3f, RoamRight - 3f);
                yield return StartCoroutine(SteerFor(missileFireInterval, () => new Vector2(x, altitude), FlightSpeed * 0.55f, FlightAccel));
            }

            _podPitchTarget = 0f;
            yield return StartCoroutine(SteerFor(0.4f, () => _rb.position, FlightSpeed, FlightAccel));
            ReturnToHover();
        }

        void SpawnMissile(Vector3 pos, Vector2 dir)
        {
            GameObject mObj;
            if (homingMissilePrefab != null)
            {
                mObj = Instantiate(homingMissilePrefab, pos, Quaternion.identity);
            }
            else
            {
                mObj = new GameObject("BossHomingMissile");
                mObj.transform.position = pos;
                var sr = mObj.AddComponent<SpriteRenderer>();
                sr.sortingOrder = 16;
                var col = mObj.AddComponent<CircleCollider2D>();
                col.isTrigger = true;
                col.radius = 0.3f;
                mObj.AddComponent<Rigidbody2D>();
                mObj.AddComponent<BossHomingMissile>();
            }

            var missile = mObj.GetComponent<BossHomingMissile>();
            if (missile != null && _player != null)
            {
                missile.Init(dir, _player.transform);
            }
        }

        // Open deck either side of the player under the swoop's wind-up height: at least 5 one way
        // to wind up from and 3 the other to climb out.
        bool SwoopRoom(out float roomL, out float roomR)
        {
            roomL = roomR = 0f;
            if (!EnsurePlayer()) return false;
            float px = _player.transform.position.x;
            float limit = arenaFloorY + hoverAltitude + 3.5f;
            if (FloorAt(px) > limit) return false;
            roomL = ClearExtent(px, -1f, 9f, limit);
            roomR = ClearExtent(px, 1f, 9f, limit);
            return Mathf.Max(roomL, roomR) >= 5f && Mathf.Min(roomL, roomR) >= 3f;
        }

        // Pulls up and back, marks the deck where it will strike, then dives through the player's
        // spot in a U and climbs out on the far side. Low enough to hit a standing player (dodge
        // or move), and low enough to be stomped on the way through.
        IEnumerator SwoopRoutine()
        {
            currentState = BossState.Swoop;
            int passes = isPhase2 ? 2 : 1;

            for (int pass = 0; pass < passes; pass++)
            {
                if (!EnsurePlayer()) break;

                // The player may have walked under a crane or the tower since the first pass.
                if (!SwoopRoom(out float roomL, out float roomR)) break;
                float px = _player.transform.position.x;
                float side = Mathf.Sign(_rb.position.x - px);
                if (side == 0f) side = 1f;
                // Not enough room behind it: wind up on the other side instead.
                if ((side < 0f ? roomL : roomR) < 5f) side = -side;

                Vector2 windup = new Vector2(
                    px + side * Mathf.Min(9f, side < 0f ? roomL : roomR),
                    Mathf.Min(arenaCeilingY - 0.5f, arenaFloorY + hoverAltitude + 3.5f));
                yield return StartCoroutine(FlyTo(windup, FlightSpeed * 1.3f, FlightAccel * 1.5f, 0.8f, 1.4f));

                // Wind-up: nose high, braking hard, the strike point flickering on the deck.
                px = _player.transform.position.x;
                SetFacing(px < _rb.position.x);
                _tiltOverride = NoseUp(14f);
                Squash(1.12f, 0.88f);
                float telegraph = isPhase2 ? 0.4f : 0.6f;
                StartCoroutine(TelegraphFlash(telegraph, new Color(1f, 0.35f, 0.3f)));
                float tw = 0f;
                while (tw < telegraph)
                {
                    tw += Time.deltaTime;
                    _rb.linearVelocity = Vector2.MoveTowards(_rb.linearVelocity, Vector2.zero, 30f * Time.deltaTime);
                    if (Random.value < 0.5f)
                        ProjectileDebris.SpawnSpark(new Vector3(px + Random.Range(-1.2f, 1.2f), arenaFloorY + 0.2f, 0f), new Color(1f, 0.25f, 0.2f), 1, 2f);
                    yield return null;
                }

                // Dive: a quadratic Bezier through the low point at its middle.
                Vector2 start = _rb.position;
                Vector2 low = new Vector2(px, arenaFloorY + swoopLowClearance);
                Vector2 exit = new Vector2(Mathf.Clamp(px + (px - start.x), px - roomL, px + roomR), start.y);
                Vector2 control = 2f * low - 0.5f * (start + exit);
                SetFacing(exit.x < start.x);
                _tiltOverride = NoseUp(-10f);
                Squash(0.88f, 1.12f);
                CameraZoomController.TriggerShake(0.25f, 0.2f);

                float duration = swoopDuration * (isPhase2 ? 0.8f : 1f);
                float t = 0f;
                bool bottomed = false;
                while (t < duration)
                {
                    t += Time.deltaTime;
                    float u = Mathf.Clamp01(t / duration);
                    Vector2 onCurve = (1 - u) * (1 - u) * start + 2 * (1 - u) * u * control + u * u * exit;
                    Vector2 tangent = (2 * (1 - u) * (control - start) + 2 * u * (exit - control)) / duration;
                    // Follow the curve's speed, correcting any drift from collisions.
                    _rb.linearVelocity = tangent + (onCurve - _rb.position) * 8f;

                    if (u > 0.5f && !bottomed)
                    {
                        bottomed = true;
                        _ductVelocity += isFacingLeft ? -160f : 160f;
                        _tiltOverride = NoseUp(8f);
                        CameraZoomController.TriggerShake(0.5f, 0.25f);
                        SpawnDownwash(10);
                    }
                    if (_ductR != null) ProjectileDebris.SpawnSpark(_ductR.TransformPoint(RightNozzle - RightDuctJoint), new Color(1f, 0.7f, 0.3f), 2, 3f);
                    yield return null;
                }

                _tiltOverride = 0f;
                yield return StartCoroutine(SteerFor(0.3f, () => _rb.position + Vector2.up, FlightSpeed, FlightAccel));
            }

            ReturnToHover();
        }

        // Lines up at one end of the deck and strafes the full length, the chin cannon raking the
        // player the whole way.
        IEnumerator GunRunRoutine()
        {
            currentState = BossState.GunRun;
            if (!EnsurePlayer()) { ReturnToHover(0.5f); yield break; }

            AttackWindow(out float left, out float right);
            bool startLeft = _rb.position.x < (left + right) * 0.5f;
            float startX = startLeft ? left + 3f : right - 3f;
            float endX = startLeft ? right - 3f : left + 3f;
            float y = Mathf.Clamp(Mathf.Max(arenaFloorY + hoverAltitude + 1.2f, _player.transform.position.y + 3.5f), arenaFloorY + 3f, arenaCeilingY - 1f);
            // The run is flown level, so it holds above the tallest art along it.
            y = Mathf.Max(y, MaxFloorBetween(startX, endX));

            yield return StartCoroutine(FlyTo(new Vector2(startX, y), FlightSpeed * 1.4f, FlightAccel * 1.6f, 1.0f, 1.8f));

            float dir = Mathf.Sign(endX - startX);
            SetFacing(dir < 0f);

            // Spin-up: sparks at the muzzle, nose dipped toward the deck.
            _tiltOverride = NoseUp(-6f);
            float spin = 0f;
            while (spin < 0.4f)
            {
                spin += Time.deltaTime;
                _rb.linearVelocity = Vector2.MoveTowards(_rb.linearVelocity, Vector2.zero, 25f * Time.deltaTime);
                if (Random.value < 0.6f) ProjectileDebris.SpawnSpark(MuzzlePosition, new Color(1f, 0.85f, 0.4f), 2, 2f);
                yield return null;
            }

            Color bulletColor = isPhase2 ? new Color(1f, 0.3f, 0.15f) : new Color(1f, 0.85f, 0.25f);
            float speed = gunRunSpeed * (isPhase2 ? 1.2f : 1f);
            float fireTimer = 0f;
            float elapsed = 0f;
            while (elapsed < 4f && (endX - _rb.position.x) * dir > 0f)
            {
                elapsed += Time.deltaTime;
                float targetY = y + Mathf.Sin(elapsed * 5f) * 0.4f;
                float vx = Mathf.MoveTowards(_rb.linearVelocity.x, dir * speed, FlightAccel * 1.5f * Time.deltaTime);
                _rb.linearVelocity = new Vector2(vx, (targetY - _rb.position.y) * 4f);

                fireTimer -= Time.deltaTime;
                if (fireTimer <= 0f && _player != null)
                {
                    fireTimer = gunRunFireInterval * (isPhase2 ? 0.75f : 1f);

                    Vector2 muzzle = MuzzlePosition;
                    // Straight out of the barrel, which is already swinging after the player.
                    Vector2 aim = Quaternion.Euler(0f, 0f, Random.Range(-4f, 4f)) * BarrelDirection;
                    _cannonRecoil = 0.18f;

                    BossProjectile.Create(muzzle, aim, gunRunBulletSpeed, bulletColor);
                    ProjectileDebris.SpawnSpark(muzzle, bulletColor, 3, 4f);
                    _rb.linearVelocity -= new Vector2(dir * 0.6f, 0f);
                    Squash(1.03f, 0.97f);
                    Rock(NoseUp(18f));
                    CameraZoomController.TriggerShake(0.06f, 0.06f);
                }
                yield return null;
            }

            _tiltOverride = 0f;
            yield return StartCoroutine(SteerFor(0.35f, () => _rb.position, FlightSpeed, FlightAccel * 1.5f));
            ReturnToHover();
        }

        // Phase 2: climbs to the ceiling and sweeps the deck end to end dropping bombs, every
        // third one a cluster shell.
        IEnumerator CarpetBombingRoutine()
        {
            currentState = BossState.CarpetBombing;

            AttackWindow(out float left, out float right);
            bool startLeft = _rb.position.x < (left + right) * 0.5f;
            float startX = startLeft ? left + 2.5f : right - 2.5f;
            float endX = startLeft ? right - 2.5f : left + 2.5f;
            float y = Mathf.Max(arenaCeilingY - 0.8f, MaxFloorBetween(startX, endX));

            yield return StartCoroutine(FlyTo(new Vector2(startX, y), FlightSpeed * 1.4f, FlightAccel * 1.6f, 1.0f, 1.8f));

            float dir = Mathf.Sign(endX - startX);
            SetFacing(dir < 0f);
            Squash(1.1f, 0.9f);
            yield return StartCoroutine(TelegraphFlash(0.35f, new Color(1f, 0.55f, 0.2f)));

            Color bombColor = new Color(1f, 0.55f, 0.15f);
            float dropTimer = 0f;
            int dropped = 0;
            float elapsed = 0f;
            while (elapsed < 4f && (endX - _rb.position.x) * dir > 0f)
            {
                elapsed += Time.deltaTime;
                float vx = Mathf.MoveTowards(_rb.linearVelocity.x, dir * 8f, FlightAccel * Time.deltaTime);
                _rb.linearVelocity = new Vector2(vx, (y - _rb.position.y) * 4f);

                dropTimer -= Time.deltaTime;
                if (dropTimer <= 0f)
                {
                    dropTimer = bombInterval;
                    Vector2 bay = droneBay != null ? (Vector2)droneBay.position : (Vector2)transform.position + Vector2.down * 1.5f;
                    var bomb = BossProjectile.Create(bay, new Vector2(dir * 0.6f, -1f), 3f, bombColor, true, true, 1.6f);
                    if (++dropped % 3 == 0) bomb.ConfigureAsCluster(5, 5f);
                    ProjectileDebris.SpawnSpark(bay, bombColor, 3, 2f);
                }
                yield return null;
            }

            yield return StartCoroutine(SteerFor(0.35f, () => _rb.position, FlightSpeed, FlightAccel));
            ReturnToHover();
        }

        // Climbs to a vantage point, then the beam's recoil shoves the boss backward and up for as
        // long as it fires.
        IEnumerator MegaBeamRoutine()
        {
            currentState = BossState.MegaBeamStrike;

            Vector2 vantage = new Vector2(
                Mathf.Clamp(_rb.position.x, RoamLeft + 5f, RoamRight - 5f),
                Mathf.Min(arenaCeilingY - 0.5f, arenaFloorY + hoverAltitude + 1.8f));
            yield return StartCoroutine(FlyTo(vantage, FlightSpeed * 1.2f, FlightAccel * 1.4f, 0.6f, 1.2f));

            if (EnsurePlayer()) SetFacing(_player.transform.position.x < _rb.position.x);
            _tiltOverride = NoseUp(-5f);

            Transform muzzle = cannonMuzzle != null ? cannonMuzzle : transform;
            Vector2 fireDir = _player != null
                ? ((Vector2)_player.transform.position - (Vector2)muzzle.position).normalized
                : (isFacingLeft ? Vector2.left + Vector2.down * 0.4f : Vector2.right + Vector2.down * 0.4f).normalized;

            float sweepAngle = isFacingLeft ? -35f : 35f;
            float duration = isPhase2 ? 3.5f : 2.6f;

            if (megaBeamInstance != null)
            {
                _cannonFollowsBeam = true;
                bool firing = true;
                IEnumerator Fire()
                {
                    yield return megaBeamInstance.FireBeamSequence(muzzle, fireDir, sweepAngle, duration, isPhase2);
                    firing = false;
                }
                StartCoroutine(Fire());

                // The charge (first 1.1 s) holds still; once the beam is live it pushes back.
                float t = 0f;
                Vector2 recoil = new Vector2(isFacingLeft ? 1.3f : -1.3f, 0.35f);
                while (firing)
                {
                    t += Time.deltaTime;
                    Vector2 want = t < 1.1f ? Vector2.zero : recoil;
                    _rb.linearVelocity = Vector2.MoveTowards(_rb.linearVelocity, want, 12f * Time.deltaTime);
                    if (t >= 1.1f && Random.value < 0.3f) Squash(1.02f, 0.98f);
                    if (t >= 1.1f) _cannonRecoil = Mathf.Max(_cannonRecoil, 0.1f + Random.value * 0.05f);
                    // Ducts lean into the recoil to hold position.
                    _ductOverride = t >= 1.1f ? (isFacingLeft ? 18f : -18f) : 0f;
                    yield return null;
                }
                _cannonFollowsBeam = false;
                _ductOverride = 0f;
            }
            else
            {
                yield return new WaitForSeconds(duration);
            }

            _tiltOverride = 0f;
            yield return StartCoroutine(SteerFor(0.5f, () => _rb.position, FlightSpeed, FlightAccel));
            ReturnToHover();
        }

        // Pulls up into a climbing turn and drops the escorts out of the bay on the way.
        IEnumerator DeployDronesRoutine()
        {
            currentState = BossState.DeployDrones;

            AttackWindow(out float left, out float right);
            float drift = _rb.position.x < (left + right) * 0.5f ? 1f : -1f;
            Vector2 climb = new Vector2(
                Mathf.Clamp(_rb.position.x + drift * 4f, RoamLeft + 3f, RoamRight - 3f),
                Mathf.Min(arenaCeilingY - 0.5f, _rb.position.y + 2f));

            for (int i = 0; i < 2; i++)
            {
                Vector3 spawnPoint = droneBay != null ? droneBay.position : transform.position + Vector3.down * 1.2f;
                Vector2 formationOffset = (i == 0) ? new Vector2(-4.0f, 1.8f) : new Vector2(4.0f, 1.8f);

                GameObject dObj;
                if (supportDronePrefab != null)
                {
                    dObj = Instantiate(supportDronePrefab, spawnPoint, Quaternion.identity);
                }
                else
                {
                    dObj = new GameObject($"SupportDrone_{i}");
                    dObj.transform.position = spawnPoint;
                    var sr = dObj.AddComponent<SpriteRenderer>();
                    sr.sortingOrder = 14;
                    var col = dObj.AddComponent<CircleCollider2D>();
                    col.isTrigger = true;
                    col.radius = 0.5f;
                    dObj.AddComponent<Rigidbody2D>();
                    dObj.AddComponent<BossSupportDrone>();
                }

                var drone = dObj.GetComponent<BossSupportDrone>();
                if (drone != null)
                {
                    drone.Init(transform, formationOffset, isPhase2);
                    _activeDrones.Add(drone);
                }

                ProjectileDebris.SpawnSpark(spawnPoint, new Color(0.2f, 0.8f, 1f), 8, 3f);
                Squash(0.94f, 1.06f);
                yield return StartCoroutine(SteerFor(0.4f, () => climb, FlightSpeed * 0.7f, FlightAccel));
            }

            yield return StartCoroutine(SteerFor(0.4f, () => climb, FlightSpeed * 0.7f, FlightAccel));
            ReturnToHover();
        }
    }
}
