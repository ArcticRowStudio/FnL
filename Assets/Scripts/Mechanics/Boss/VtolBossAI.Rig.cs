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
    // The moving parts: building them, thrust-vectoring ducts, pods, the chin cannon, fan spin and tints.
    public partial class VtolBossAI
    {
        // Rig points on the body in its local units (art faces left, 130 PPU, pivot 0.5/0.45), as
        // printed by Tools/split-vtol-parts.py and Tools/build-vtol-fan-frames.py.
        static readonly Vector2 LeftDuctJoint = new Vector2(-3.085f, 1.117f);
        static readonly Vector2 RightDuctJoint = new Vector2(1.492f, 0.917f);
        static readonly Vector2 LeftPodJoint = new Vector2(-0.662f, 2.124f);
        static readonly Vector2 RightPodJoint = new Vector2(1.185f, 2.124f);
        static readonly Vector2 CannonJoint = new Vector2(0.592f, -1.307f);
        static readonly Vector2 LeftNozzle = new Vector2(-3.277f, 0.147f);
        static readonly Vector2 RightNozzle = new Vector2(2.531f, -0.160f);
        static readonly Vector2 CannonMuzzlePoint = new Vector2(-2.715f, -1.591f);
        static readonly Vector2 LeftPodFace = new Vector2(-1.408f, 2.424f);
        static readonly Vector2 RightPodFace = new Vector2(0.508f, 2.424f);
        static readonly Vector2 LeftFanOffset = new Vector2(-3.550f, 1.605f);
        static readonly Vector2 RightFanOffset = new Vector2(1.981f, 1.390f);

        // Builds the moving parts as children of the hull, each pivoting on its joint. Sorting
        // follows the art: the far duct behind the hull, the near duct in front of everything.
        void CreateRig()
        {
            // Leftovers from an earlier play session (the instance can be reused, see ResetRuntimeState).
            foreach (string leftover in new[] { "Duct_L", "Duct_R", "Pod_L", "Pod_R", "Cannon", "Cockpit_WeakPoint" })
            {
                Transform old = transform.Find(leftover);
                if (old != null) Destroy(old.gameObject);
            }

            int hullOrder = fuselageRenderer != null ? fuselageRenderer.sortingOrder : 12;
            _ductL = CreatePart("Duct_L", LeftDuctJoint, hullOrder - 2, out _ductLRenderer);
            _ductR = CreatePart("Duct_R", RightDuctJoint, hullOrder + 2, out _ductRRenderer);
            _podL = CreatePart("Pod_L", LeftPodJoint, hullOrder + 1, out _podLRenderer);
            _podR = CreatePart("Pod_R", RightPodJoint, hullOrder + 1, out _podRRenderer);
            _cannon = CreatePart("Cannon", CannonJoint, hullOrder + 1, out _cannonRenderer);

            _leftFan = CreateFan("Fan_L", _ductL, LeftFanOffset - LeftDuctJoint, hullOrder - 1);
            _rightFan = CreateFan("Fan_R", _ductR, RightFanOffset - RightDuctJoint, hullOrder + 3);
            _leftFanPhase = Random.value * 6f;
            _rightFanPhase = Random.value * 6f;

            // Riders: missiles leave the pod faces and shots leave the barrel, wherever those point.
            leftMissileHatch = Attach(leftMissileHatch, "LeftHatch", _podL, LeftPodFace - LeftPodJoint);
            rightMissileHatch = Attach(rightMissileHatch, "RightHatch", _podR, RightPodFace - RightPodJoint);
            cannonMuzzle = Attach(cannonMuzzle, "Muzzle", _cannon, CannonMuzzlePoint - CannonJoint);

            ApplyPhaseSprites();

            _podLHp = _podRHp = podHealth;
            _ductLHp = _ductRHp = ductHealth;
            _podLHit = AddHitbox(_podL, _podLRenderer, Part.PodL, Vector2.zero, Vector2.zero);
            _podRHit = AddHitbox(_podR, _podRRenderer, Part.PodR, Vector2.zero, Vector2.zero);
            _ductLHit = AddHitbox(_ductL, _ductLRenderer, Part.DuctL, LeftDuctBoxOffset, LeftDuctBoxSize);
            _ductRHit = AddHitbox(_ductR, _ductRRenderer, Part.DuctR, RightDuctBoxOffset, RightDuctBoxSize);
            CreateCockpitWeakPoint();
        }

        Transform CreatePart(string name, Vector2 joint, int order, out SpriteRenderer sr)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = joint;
            sr = go.AddComponent<SpriteRenderer>();
            MatchHull(sr, order);
            return go.transform;
        }

        SpriteRenderer CreateFan(string name, Transform duct, Vector2 offset, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(duct, false);
            go.transform.localPosition = offset;
            var sr = go.AddComponent<SpriteRenderer>();
            MatchHull(sr, order);
            return sr;
        }

        void MatchHull(SpriteRenderer sr, int order)
        {
            sr.sortingOrder = order;
            if (fuselageRenderer == null) return;
            sr.sortingLayerID = fuselageRenderer.sortingLayerID;
            sr.sharedMaterial = fuselageRenderer.sharedMaterial;
        }

        static Transform Attach(Transform existing, string name, Transform parent, Vector2 local)
        {
            Transform t = existing != null ? existing : new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = local;
            t.localRotation = Quaternion.identity;
            return t;
        }

        void ApplyPhaseSprites()
        {
            SetSprite(fuselageRenderer, isPhase2 ? phase2Sprite : phase1Sprite);
            SetSprite(_ductLRenderer, isPhase2 ? leftDuctPhase2 : leftDuctPhase1);
            SetSprite(_ductRRenderer, isPhase2 ? rightDuctPhase2 : rightDuctPhase1);
            SetSprite(_podLRenderer, isPhase2 ? leftPodPhase2 : leftPodPhase1);
            SetSprite(_podRRenderer, isPhase2 ? rightPodPhase2 : rightPodPhase1);
            SetSprite(_cannonRenderer, isPhase2 ? cannonPhase2 : cannonPhase1);
        }

        static void SetSprite(SpriteRenderer sr, Sprite sprite)
        {
            if (sr != null && sprite != null) sr.sprite = sprite;
        }

        // Parent is mirrored when facing right, so a local angle reads backwards on screen.
        float MirrorSign => isFacingLeft ? 1f : -1f;

        void UpdateRig()
        {
            float dt = Mathf.Max(Time.deltaTime, 0.0001f);
            Vector2 v = _rb != null ? _rb.linearVelocity : Vector2.zero;

            // Thrust vectoring. The ducts tip toward where the craft is accelerating and into its
            // cruise, and gimbal against the airframe's sway so their thrust shoves it back level.
            float want = -(_smoothedAccelX * ductPerAccel + v.x * ductPerSpeed) + _ductOverride;
            want = Mathf.Clamp(want, -ductMaxAngle, ductMaxAngle);
            want += _currentTilt * (1f - ductGimbal);
            float w = ductFrequency;
            int steps = Mathf.CeilToInt(dt / 0.005f);
            float h = dt / steps;
            for (int i = 0; i < steps; i++)
            {
                _ductVelocity += (w * w * (want - _ductAngle) - 2f * ductDamping * w * _ductVelocity) * h;
                _ductAngle += _ductVelocity * h;
            }

            // The two ducts are separate machines: a little independent hunting keeps them alive.
            float hunt = (Mathf.PerlinNoise(Time.time * 1.3f, 7.7f) - 0.5f) * 4f;
            // A wrecked duct's actuator can't hold it steady.
            float shakeL = _ductLDamaged ? (Mathf.PerlinNoise(Time.time * 6f, 1.3f) - 0.5f) * 18f : 0f;
            float shakeR = _ductRDamaged ? (Mathf.PerlinNoise(Time.time * 6f, 5.9f) - 0.5f) * 18f : 0f;
            float localL = (_ductAngle - _currentTilt + hunt + shakeL) * MirrorSign;
            float localR = (_ductAngle - _currentTilt - hunt + shakeR) * MirrorSign;
            if (_ductL != null) _ductL.localRotation = Quaternion.Euler(0f, 0f, localL);
            if (_ductR != null) _ductR.localRotation = Quaternion.Euler(0f, 0f, localR);

            UpdatePods(dt);
            UpdateCannon(dt);
        }

        // Pods elevate for a salvo and kick back along their axis on each launch.
        void UpdatePods(float dt)
        {
            const float w = 11f, z = 0.5f;
            _podPitchVelocity += (w * w * (_podPitchTarget - _podPitch) - 2f * z * w * _podPitchVelocity) * dt;
            _podPitch += _podPitchVelocity * dt;
            _podRecoilL = Mathf.MoveTowards(_podRecoilL, 0f, 0.9f * dt);
            _podRecoilR = Mathf.MoveTowards(_podRecoilR, 0f, 0.9f * dt);

            // Art space: the pods face left, so nose up is clockwise and recoil pushes toward +x.
            Quaternion pitch = Quaternion.Euler(0f, 0f, -_podPitch);
            Vector3 back = pitch * Vector3.right;
            if (_podL != null) { _podL.localRotation = pitch; _podL.localPosition = (Vector3)LeftPodJoint + back * _podRecoilL; }
            if (_podR != null) { _podR.localRotation = pitch; _podR.localPosition = (Vector3)RightPodJoint + back * _podRecoilR; }
        }

        void KickPod(bool left)
        {
            if (left) _podRecoilL = 0.16f; else _podRecoilR = 0.16f;
            _podPitchVelocity += 90f;
        }

        // The cannon swings on its ball joint to track the player (or the beam), heavy and slow.
        void UpdateCannon(float dt)
        {
            if (_cannon == null) return;

            Vector2 aimDir = Vector2.zero;
            if (_cannonFollowsBeam && megaBeamInstance != null) aimDir = megaBeamInstance.CurrentDirection;
            else if (_player != null) aimDir = (Vector2)_player.transform.position - (Vector2)_cannon.position;

            if (aimDir.sqrMagnitude > 0.0001f)
            {
                // Into the hull's art space: undo its tilt, then its mirror.
                Vector2 local = Quaternion.Euler(0f, 0f, -_currentTilt) * aimDir;
                local.x *= MirrorSign;
                // The barrel points along art -x; + turns its tip down.
                float target = Mathf.Atan2(-local.y, -local.x) * Mathf.Rad2Deg;
                target = Mathf.Clamp(target, cannonMinAngle, cannonMaxAngle);
                _cannonAngle = Mathf.MoveTowardsAngle(_cannonAngle, target, cannonTurnSpeed * (_cannonFollowsBeam ? 3f : 1f) * dt);
            }

            _cannonRecoil = Mathf.MoveTowards(_cannonRecoil, 0f, 1.6f * dt);
            Quaternion rot = Quaternion.Euler(0f, 0f, _cannonAngle);
            _cannon.localRotation = rot;
            _cannon.localPosition = (Vector3)CannonJoint + rot * Vector3.right * _cannonRecoil;
        }

        // Unit vector out of the barrel in world space.
        Vector2 BarrelDirection
        {
            get
            {
                if (_cannon == null || cannonMuzzle == null) return isFacingLeft ? Vector2.left : Vector2.right;
                return ((Vector2)cannonMuzzle.position - (Vector2)_cannon.position).normalized;
            }
        }

        // Spin follows thrust: faster while accelerating or climbing, and the fan on the side the
        // airframe is leaning toward works harder to hold the bank.
        void UpdateFans()
        {
            if (_leftFan == null || _rightFan == null) return;

            Vector2 v = _rb != null ? _rb.linearVelocity : Vector2.zero;
            float thrust = Mathf.Clamp01(0.25f + Mathf.Abs(_smoothedAccelX) * 0.03f + Mathf.Max(0f, v.y) * 0.06f + Mathf.Abs(_tiltVelocity) * 0.004f);
            if (isPhase2) thrust = Mathf.Clamp01(thrust + 0.2f);
            float fps = Mathf.Lerp(fanIdleFps, fanMaxFps, thrust) * _fanSpinScale;

            // _currentTilt > 0 dips the left side of the screen; which fan that is depends on facing.
            float leanLeftSide = Mathf.Clamp(_currentTilt / Mathf.Max(1f, maxTiltAngle), -1f, 1f);
            float artLeftLeans = isFacingLeft ? leanLeftSide : -leanLeftSide;
            float wreckL = _ductLDamaged ? 0.55f * _sputterL : 1f;
            float wreckR = _ductRDamaged ? 0.55f * _sputterR : 1f;
            _leftFanPhase += fps * (1f + 0.25f * artLeftLeans) * wreckL * Time.deltaTime;
            _rightFanPhase += fps * (1f - 0.25f * artLeftLeans) * wreckR * Time.deltaTime;

            SetFanFrame(_leftFan, isPhase2 ? leftFanFramesPhase2 : leftFanFramesPhase1, _leftFanPhase);
            SetFanFrame(_rightFan, isPhase2 ? rightFanFramesPhase2 : rightFanFramesPhase1, _rightFanPhase);
        }

        static void SetFanFrame(SpriteRenderer sr, Sprite[] frames, float phase)
        {
            if (frames == null || frames.Length == 0) return;
            sr.sprite = frames[(int)phase % frames.Length];
        }

        // Every part is its own renderer, so hull tints (flash, telegraph) go to all of them, on
        // top of each part's own damage tint.
        void SetHullColor(Color c)
        {
            foreach (var sr in new[] { fuselageRenderer, _leftFan, _rightFan, _ductLRenderer, _ductRRenderer, _podLRenderer, _podRRenderer, _cannonRenderer })
                if (sr != null) sr.color = c * PartTint(sr);
        }

        Color PartTint(SpriteRenderer sr)
        {
            if (sr == null) return Color.white;
            if (sr == _ductLRenderer || sr == _leftFan) return _tintDuctL;
            if (sr == _ductRRenderer || sr == _rightFan) return _tintDuctR;
            if (sr == _podLRenderer) return _tintPodL;
            if (sr == _podRRenderer) return _tintPodR;
            return Color.white;
        }

        Vector2 MuzzlePosition => cannonMuzzle != null ? (Vector2)cannonMuzzle.position : (Vector2)transform.position;
    }
}
