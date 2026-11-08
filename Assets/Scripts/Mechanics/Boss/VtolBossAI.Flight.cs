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
    // Flight: picking spots, accelerating steering, facing, squash and the self-balancing attitude.
    public partial class VtolBossAI
    {
        void UpdateHoverMovement()
        {
            if (!EnsurePlayer()) return;

            if (_roamLeg)
            {
                // Nose into the leg so it reads as travelling, not drifting.
                float vx = _rb.linearVelocity.x;
                if (Mathf.Abs(vx) > 1.5f) SetFacing(vx < 0f);
                if (Mathf.Abs(_hoverTarget.x - _rb.position.x) < 2.5f || Time.time >= _roamLegEnd)
                {
                    _roamLeg = false;
                    _nextRepositionTime = Time.time + Random.Range(roamLoiterMin, roamLoiterMax);
                }
            }
            else
            {
                // Turn only once the player is clearly on the other side, or the boss flickers while
                // crossing overhead.
                float dx = _player.transform.position.x - _rb.position.x;
                if (Mathf.Abs(dx) > 1.2f) SetFacing(dx < 0f);

                // Chasing: re-aim as soon as the player has moved on from the spot it is heading for.
                bool retarget = Chasing && Mathf.Abs(_hoverTarget.x - _player.transform.position.x) > attackRange;
                if (Time.time >= _nextRepositionTime || retarget) PickHoverTarget();
            }

            float bob = Mathf.Sin(Time.time * hoverBobFrequency) * hoverBobAmplitude;
            float speed = _roamLeg || Chasing ? Mathf.Max(FlightSpeed, roamCruiseSpeed * (1f - 0.12f * DamagedDuctCount)) : FlightSpeed;
            Steer(_hoverTarget + Vector2.up * bob, speed, FlightAccel);
        }

        // The player has got away across the topside: it closes in at cruise speed.
        bool Chasing => !_roamLeg && EnsurePlayer() && Mathf.Abs(_player.transform.position.x - _rb.position.x) > attackRange;

        // Between attacks the boss keeps relocating instead of tracking a fixed offset: usually to a
        // firing spot off to one side of the player, sometimes across to the far side, which sends
        // it sweeping over the player's head and turning round. Now and then it makes a fast pass
        // over the player to a spot well out on the other side, and wheels back from there.
        void PickHoverTarget()
        {
            if (!EnsurePlayer()) return;
            if (!Chasing && TryStartRoamLeg()) return;

            Vector2 pos = _rb.position;
            float px = _player.transform.position.x;
            float side = Mathf.Sign(pos.x - px);
            if (side == 0f) side = 1f;
            if (Random.value < (isPhase2 ? 0.45f : 0.3f)) side = -side;

            float x = px + side * Random.Range(4.5f, 8f);
            if (x < RoamLeft + 3f || x > RoamRight - 3f) x = px - side * Random.Range(4.5f, 8f);
            x = NearestHoverX(Mathf.Clamp(x, RoamLeft + 3f, RoamRight - 3f));

            // Wrecked ducts can't hold it as high.
            float y = Mathf.Max(arenaFloorY + hoverAltitude, _player.transform.position.y + 2.5f) + Random.Range(-0.8f, 2.6f) - 0.7f * DamagedDuctCount;
            y = Mathf.Clamp(y, arenaFloorY + 3f, arenaCeilingY - 0.5f);

            _hoverTarget = new Vector2(x, Mathf.Max(y, FloorAt(x)));
            _nextRepositionTime = Time.time + Random.Range(repositionIntervalMin, repositionIntervalMax) * (isPhase2 ? 0.7f : 1f);
        }

        // A pass: cruise over the player's head to a hoverable spot well out on the far side. Measured
        // from the player, so it never drags the boss off to another part of the map.
        bool TryStartRoamLeg()
        {
            if (!roamTopside || Random.value >= roamLegChance) return false;
            // At least one ordinary relocation in between, so legs don't chain back to back.
            if (Time.time - _lastRoamLegTime < repositionIntervalMax * 2f) return false;

            float from = _rb.position.x;
            float px = _player.transform.position.x;
            float across = px >= from ? 1f : -1f;
            for (int i = 0; i < 12; i++)
            {
                float x = px + across * Random.Range(roamLegMinDistance, roamLegMaxDistance);
                if (x < roamLeft + 3f || x > roamRight - 3f) x = px - across * Random.Range(roamLegMinDistance, roamLegMaxDistance);
                if (x < roamLeft + 3f || x > roamRight - 3f || !CanHoverAt(x)) continue;

                float y = Mathf.Max(arenaFloorY + hoverAltitude + Random.Range(0f, 2.5f), FloorAt(x));
                _hoverTarget = new Vector2(x, y);
                _roamLeg = true;
                _lastRoamLegTime = Time.time;
                _roamLegEnd = Time.time + Mathf.Abs(x - from) / Mathf.Max(1f, roamCruiseSpeed) + 6f;
                return true;
            }
            return false;
        }

        float RoamLeft => roamTopside ? roamLeft : arenaLeft;
        float RoamRight => roamTopside ? roamRight : arenaRight;

        // Lowest pivot height that keeps the hull clear of the deck and of the art standing on it.
        float FloorAt(float x)
        {
            float floor = arenaFloorY + 2.3f;
            if (roamObstacles == null) return floor;
            foreach (var o in roamObstacles)
            {
                if (o.backdrop) continue;
                if (x < o.left - obstaclePadding || x > o.right + obstaclePadding) continue;
                float top = o.top;
                if (o.dome)
                {
                    // The crown's highest point within the hull's reach of x.
                    float centre = (o.left + o.right) * 0.5f;
                    float halfWidth = Mathf.Max(0.01f, (o.right - o.left) * 0.5f);
                    float nearest = Mathf.Clamp(centre, x - obstaclePadding, x + obstaclePadding);
                    float u = Mathf.Clamp01(Mathf.Abs(nearest - centre) / halfWidth);
                    top = arenaFloorY + (o.top - arenaFloorY) * Mathf.Sqrt(1f - u * u);
                }
                floor = Mathf.Max(floor, top + obstacleClearance);
            }
            return floor;
        }

        float MaxFloorBetween(float a, float b)
        {
            float lo = Mathf.Min(a, b), hi = Mathf.Max(a, b);
            float floor = FloorAt(hi);
            for (float x = lo; x < hi; x += 0.5f) floor = Mathf.Max(floor, FloorAt(x));
            return floor;
        }

        bool CanHoverAt(float x) => FloorAt(x) <= roamHoverCeilingY;

        // The closest x to the wanted one that it may hover over (not the dome's crown).
        float NearestHoverX(float x)
        {
            if (CanHoverAt(x)) return x;
            for (float d = 1f; d < RoamRight - RoamLeft; d += 1f)
            {
                if (x - d >= RoamLeft + 3f && CanHoverAt(x - d)) return x - d;
                if (x + d <= RoamRight - 3f && CanHoverAt(x + d)) return x + d;
            }
            return x;
        }

        // How far from x it can go toward dir with the hull still below limitY.
        float ClearExtent(float x, float dir, float maxDistance, float limitY)
        {
            float d = 0f;
            while (d < maxDistance)
            {
                float next = x + dir * (d + 0.5f);
                if (next < RoamLeft + 2f || next > RoamRight - 2f || FloorAt(next) > limitY) break;
                d += 0.5f;
            }
            return d;
        }

        // A stretch attackSpan either side of the player, slid to stay inside the roam bounds.
        void AttackWindow(out float left, out float right)
        {
            float px = EnsurePlayer() ? _player.transform.position.x : _rb.position.x;
            left = px - attackSpan;
            right = px + attackSpan;
            if (left < RoamLeft) { right += RoamLeft - left; left = RoamLeft; }
            if (right > RoamRight) { left -= right - RoamRight; right = RoamRight; }
            left = Mathf.Max(left, RoamLeft);
        }

        // Raises the target so the boss climbs over deck art instead of through it: never below the
        // floor at the target, nor below the highest floor just ahead, and while it is still under
        // that, it holds back and climbs before pressing on.
        Vector2 AvoidObstacles(Vector2 target)
        {
            if (_groundLock || _escaping || currentState == BossState.Intro) return target;
            if (currentState == BossState.Crashing && !_limpingHome) return target;

            Vector2 pos = _rb.position;
            float ahead = Mathf.Clamp(target.x - pos.x, -obstacleLookahead, obstacleLookahead);
            float need = MaxFloorBetween(pos.x, pos.x + ahead);
            target.y = Mathf.Max(target.y, FloorAt(target.x), need);
            if (pos.y < need - 0.3f) target.x = pos.x + ahead * 0.15f;
            return target;
        }

        float FlightSpeed => maxFlightSpeed * (isPhase2 ? 1.3f : 1f) * (1f - 0.12f * DamagedDuctCount);
        float FlightAccel => flightAcceleration * (isPhase2 ? 1.3f : 1f);

        // Accelerates toward the target instead of easing onto it, so the boss carries momentum,
        // overshoots a little and banks into turns.
        void Steer(Vector2 target, float maxSpeed, float accel)
        {
            target = AvoidObstacles(target);
            Vector2 desired = Vector2.ClampMagnitude((target - _rb.position) * 2.2f, maxSpeed);
            _rb.linearVelocity = Vector2.MoveTowards(_rb.linearVelocity, desired, accel * Time.deltaTime);
        }

        IEnumerator FlyTo(Vector2 target, float maxSpeed, float accel, float arriveDistance, float timeout)
        {
            float t = 0f;
            while (t < timeout && Vector2.Distance(_rb.position, target) > arriveDistance)
            {
                t += Time.deltaTime;
                Steer(target, maxSpeed, accel);
                yield return null;
            }
        }

        // Keeps flying toward a moving target for a fixed time; attacks use it to fire on the move.
        IEnumerator SteerFor(float seconds, System.Func<Vector2> target, float maxSpeed, float accel)
        {
            float t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                Steer(target(), maxSpeed, accel);
                yield return null;
            }
        }

        void ReturnToHover(float cooldownScale = 1f)
        {
            _tiltOverride = 0f;
            currentState = BossState.Hovering;
            _stateTimer = (isPhase2 ? attackCooldownPhase2 : attackCooldownPhase1) * cooldownScale;
            _nextRepositionTime = 0f;
        }

        void SetFacing(bool faceLeft)
        {
            isFacingLeft = faceLeft;
        }

        // The art faces left; facing right mirrors the whole rig through a negative X scale.
        // _scaleMul carries squash and stretch on top and relaxes back to 1 by itself.
        void ApplyFacing()
        {
            _scaleMul = Vector2.MoveTowards(_scaleMul, Vector2.one, 2.2f * Time.deltaTime);
            float sx = Mathf.Abs(_originalScale.x) * _scaleMul.x * _depthScale;
            transform.localScale = new Vector3(isFacingLeft ? sx : -sx, _originalScale.y * _scaleMul.y * _depthScale, _originalScale.z);
        }

        void Squash(float x, float y)
        {
            _scaleMul = new Vector2(x, y);
        }

        // Self-balancing attitude. The airframe leans the way it is accelerating (and a little into
        // its cruise speed), then a spring-damper drives the actual angle toward that lean. Being
        // underdamped, it overshoots: braking flares it back past level, and it rocks a couple of
        // times before it settles, as a hovering craft correcting itself does. Launches, recoil and
        // hits kick the angle through Rock(); attacks add a held pitch through _tiltOverride.
        void ApplyBankedTilt()
        {
            float dt = Mathf.Max(Time.deltaTime, 0.0001f);
            Vector2 v = _rb != null ? _rb.linearVelocity : Vector2.zero;
            float accelX = (v.x - _prevVelocity.x) / dt;
            _prevVelocity = v;
            _smoothedAccelX = Mathf.Lerp(_smoothedAccelX, accelX, Mathf.Clamp01(dt * 10f));

            float lean = -(_smoothedAccelX * leanPerAccel + v.x * leanPerSpeed);
            float target = Mathf.Clamp(lean, -maxTiltAngle, maxTiltAngle);
            float gust = (Mathf.PerlinNoise(Time.time * 0.7f, 3.1f) - 0.5f) * 2f * hoverWobble * (1f + 1.5f * DamagedDuctCount) * (_groundLock ? 0.15f : 1f);
            target += gust + _tiltOverride + DamageLean();

            // Substeps keep the spring stable through frame hitches.
            float w = attitudeFrequency;
            int steps = Mathf.CeilToInt(dt / 0.005f);
            float h = dt / steps;
            for (int i = 0; i < steps; i++)
            {
                // Lost thrust on one side leaves the balance looser, so it rocks for longer.
                float damping = attitudeDamping * (DamagedDuctCount > 0 ? 0.55f : 1f);
                _tiltVelocity += (w * w * (target - _currentTilt) - 2f * damping * w * _tiltVelocity) * h;
                _currentTilt += _tiltVelocity * h;
            }
            _currentTilt = Mathf.Clamp(_currentTilt, -maxTiltAngle - 20f, maxTiltAngle + 20f);

            if (_rb != null) _rb.rotation = _currentTilt;
            transform.rotation = Quaternion.Euler(0f, 0f, _currentTilt);
        }

        // Knocks the airframe's angle, in degrees per second; the balance then rights it.
        void Rock(float degreesPerSecond)
        {
            _tiltVelocity += degreesPerSecond;
        }

        // Positive pitches the nose up whichever way the boss faces.
        float NoseUp(float degrees) => isFacingLeft ? -degrees : degrees;

        // Select the boss to see the bounds in the Scene view: helideck arena (orange), roam stretch
        // up to roamHoverCeilingY (cyan), each obstacle (red) and the floor it keeps above (yellow).
        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.6f, 0.1f);
            DrawGizmoRect(arenaLeft, arenaRight, arenaFloorY, arenaCeilingY);
            if (roamTopside)
            {
                Gizmos.color = new Color(0.2f, 0.9f, 1f);
                DrawGizmoRect(roamLeft, roamRight, arenaFloorY, roamHoverCeilingY);
            }
            if (roamObstacles != null)
            {
                foreach (var o in roamObstacles)
                {
                    Gizmos.color = o.backdrop ? new Color(1f, 0.25f, 0.2f, 0.25f) : new Color(1f, 0.25f, 0.2f);
                    DrawGizmoRect(o.left, o.right, arenaFloorY, o.top);
                }
            }
            Gizmos.color = Color.yellow;
            float left = RoamLeft, right = RoamRight;
            Vector3 prev = new Vector3(left, FloorAt(left), 0f);
            for (float x = left + 0.5f; x <= right; x += 0.5f)
            {
                Vector3 next = new Vector3(x, FloorAt(x), 0f);
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }

        static void DrawGizmoRect(float left, float right, float bottom, float top)
        {
            Gizmos.DrawLine(new Vector3(left, bottom), new Vector3(right, bottom));
            Gizmos.DrawLine(new Vector3(right, bottom), new Vector3(right, top));
            Gizmos.DrawLine(new Vector3(right, top), new Vector3(left, top));
            Gizmos.DrawLine(new Vector3(left, top), new Vector3(left, bottom));
        }

        void ClampArenaBounds()
        {
            Vector2 p = _rb.position;
            // Down on the deck in phase 3 it stays on the helideck; in the air it has the topside.
            float left = _groundLock ? arenaLeft : RoamLeft;
            float right = _groundLock ? arenaRight : RoamRight;
            // Over and near tall art the ceiling lifts so it can climb across (the dome's crown).
            float ceiling = Mathf.Max(arenaCeilingY, MaxFloorBetween(p.x - obstacleLookahead, p.x + obstacleLookahead) + 2f);
            // The collider reaches 2.0 below the pivot; keep it clear of the deck.
            float clampedX = Mathf.Clamp(p.x, left + 2f, right - 2f);
            float clampedY = Mathf.Clamp(p.y, arenaFloorY + 2.3f, ceiling + 0.5f);

            if (clampedX != p.x || clampedY != p.y)
            {
                _rb.position = new Vector2(clampedX, clampedY);
            }
        }
    }
}
