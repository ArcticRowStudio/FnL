using System.Collections;
using Platformer.View;
using UnityEngine;

namespace Platformer.Mechanics.Boss
{
    // The pilot both bosses share: the tiger in the flight helmet. A beaten boss hands it a hatch
    // position; it pops out, drops to the deck, glances back at the wreck and gets away on a
    // procedural cut-out rig - running, or bounding off in big hops and diving off the deck edge. The art is one 3/4 standing pose split into parts by
    // Tools/split-pilot-parts.py (drawn facing right; running left mirrors it). A cut-scene
    // actor only: no collider, nothing hits it.
    public class BossPilotEscape : MonoBehaviour
    {
        const string SpritePath = "BossPilot/Pilot_";
        // Joint offsets from the pelvis, as printed by Tools/split-pilot-parts.py.
        static readonly Vector2 ArmFrontJoint = new Vector2(-0.420f, 0.544f);
        static readonly Vector2 ArmBackJoint = new Vector2(0.328f, 0.668f);
        static readonly Vector2 LegFrontJoint = new Vector2(-0.191f, -0.010f);
        static readonly Vector2 LegBackJoint = new Vector2(0.187f, -0.010f);
        const float PelvisHeight = 1.213f;   // pelvis above the claw tips
        const float LegLength = 1.2f;

        [Header("Run")]
        public float runSpeed = 7.5f;
        public float runAcceleration = 16f;
        [Tooltip("Ground covered per full stride (both feet)")]
        public float strideLength = 2.4f;
        public float legSwing = 24f;
        public float armSwing = 16f;
        public float footLift = 0.14f;
        public float bodyBounce = 0.1f;
        public float runLean = 7f;
        [Tooltip("Gives up and fades out after this long if it is still on screen")]
        public float maxRunTime = 6f;

        [Header("Hop")]
        public float hopSpeed = 7f;
        [Tooltip("Upward speed of each hop; with the hop gravity this sets the height and hang time")]
        public float hopJump = 10f;
        public float hopGravity = 26f;
        [Tooltip("Crouch before each hop, seconds")]
        public float hopCrouch = 0.12f;

        float _groundY;
        int _dir;
        float _exitX = float.NaN;
        bool _leapAtExit;
        bool _hop;
        Vector2 _velocity;
        float _cycle;
        Transform _visual, _body, _armF, _armB, _legF, _legB;
        SpriteRenderer[] _renderers;

        // hatch: where its feet start. runDir: +1 right, -1 left. exitX: where it leaves, either
        // leaping off (a deck edge) or ducking out of sight (a gap past a wall) - NaN to just run
        // out of shot. hop: bound away in jumps instead of running, after a bigger launch out of
        // the hatch. The parts sort from baseOrder up to baseOrder + 5.
        public static BossPilotEscape Spawn(Vector2 hatch, float groundY, int runDir, float exitX, bool leapAtExit, int sortingLayerID, int baseOrder, bool hop = false)
        {
            var go = new GameObject("BossPilot");
            go.transform.position = hatch;
            var pilot = go.AddComponent<BossPilotEscape>();
            pilot._groundY = groundY;
            pilot._dir = runDir >= 0 ? 1 : -1;
            pilot._exitX = exitX;
            pilot._leapAtExit = leapAtExit;
            pilot._hop = hop;
            if (!pilot.Build(sortingLayerID, baseOrder))
            {
                Destroy(go);
                return null;
            }
            pilot.StartCoroutine(pilot.Escape());
            return pilot;
        }

        bool Build(int sortingLayerID, int baseOrder)
        {
            _visual = new GameObject("Visual").transform;
            _visual.SetParent(transform, false);
            _body = new GameObject("Body").transform;
            _body.SetParent(_visual, false);
            _body.localPosition = new Vector3(0f, PelvisHeight, 0f);

            // Back to front: far arm, far leg, torso, near leg, near arm, then the shoulder dome
            // over the near arm's top.
            _armB = Part("arm_back", ArmBackJoint, 0);
            _legB = Part("leg_back", LegBackJoint, 1);
            Part("torso", Vector2.zero, 2);
            _legF = Part("leg_front", LegFrontJoint, 3);
            _armF = Part("arm_front", ArmFrontJoint, 4);
            Part("pauldron", Vector2.zero, 5);

            _renderers = GetComponentsInChildren<SpriteRenderer>();
            foreach (var r in _renderers)
            {
                if (r.sprite == null)
                {
                    Debug.LogWarning("BossPilotEscape: missing sprite under Resources/" + SpritePath + "*; run Tools/split-pilot-parts.py");
                    return false;
                }
                r.sortingLayerID = sortingLayerID;
                r.sortingOrder += baseOrder;
            }
            Face(_dir);
            return true;

            Transform Part(string name, Vector2 joint, int order)
            {
                var t = new GameObject(name).transform;
                t.SetParent(_body, false);
                t.localPosition = joint;
                var sr = t.gameObject.AddComponent<SpriteRenderer>();
                sr.sprite = Resources.Load<Sprite>(SpritePath + name);
                sr.sortingOrder = order;
                return t;
            }
        }

        void Face(int dir)
        {
            _visual.localScale = new Vector3(dir, 1f, 1f);
        }

        IEnumerator Escape()
        {
            // Out of the hatch: a scramble up and over, arms flung up, legs kicking. Hopping, it
            // springs clear in one big jump.
            _velocity = _hop ? new Vector2(_dir * 3f, 13f) : new Vector2(_dir * 2.2f, 8.5f);
            float t = 0f;
            while (_velocity.y > 0f || transform.position.y > _groundY)
            {
                t += Time.deltaTime;
                _velocity.y -= 26f * Time.deltaTime;
                Move();
                float kick = Mathf.Sin(t * 14f);
                // Arms flung up and back: swung forward, the near arm would cross the helmet.
                Pose(legF: 30f + 10f * kick, legB: -18f - 10f * kick, armF: -110f, armB: -90f, lean: -12f, bob: 0f);
                yield return null;
            }

            // Thump onto the deck.
            yield return Land(8, 0.15f, 0.35f);

            // A glance back at the wreck, then away.
            Face(-_dir);
            Pose(0f, 0f, 0f, 0f, 0f, 0f);
            yield return new WaitForSeconds(0.4f);
            Face(_dir);

            if (_hop)
            {
                yield return HopAway();
                yield break;
            }

            // Run.
            t = 0f;
            float lastSin = 0f;
            while (t < maxRunTime)
            {
                t += Time.deltaTime;
                _velocity.x = Mathf.MoveTowards(_velocity.x, _dir * runSpeed, runAcceleration * Time.deltaTime);
                float before = transform.position.x;
                Move();
                // Cadence from the ground covered, so the feet don't skate.
                _cycle += Mathf.Abs(transform.position.x - before) / strideLength * Mathf.PI * 2f;
                float effort = Mathf.Clamp01(Mathf.Abs(_velocity.x) / runSpeed);
                float s = Mathf.Sin(_cycle);
                float c = Mathf.Cos(_cycle);
                float swing = legSwing * s * effort;
                // Hips drop as the legs open, so the feet stay down; a hop between footfalls.
                float drop = LegLength * (1f - Mathf.Cos(Mathf.Abs(swing) * Mathf.Deg2Rad));
                float bob = -drop + bodyBounce * Mathf.Abs(c) * effort;
                Pose(swing, -swing, -armSwing * s * effort, armSwing * s * effort, -runLean * effort, bob);
                _legF.localPosition = LegFrontJoint + Vector2.up * (footLift * Mathf.Max(0f, c) * effort);
                _legB.localPosition = LegBackJoint + Vector2.up * (footLift * Mathf.Max(0f, -c) * effort);

                if (Mathf.Sign(s) != Mathf.Sign(lastSin) && effort > 0.5f)
                    ProjectileDebris.Spawn(transform.position, new Color(0.75f, 0.74f, 0.7f, 0.6f), Random.Range(0.1f, 0.2f),
                        new Vector2(-_dir * Random.Range(0.5f, 1.5f), Random.Range(0.3f, 1f)), 1.6f);
                lastSin = s;

                if (!float.IsNaN(_exitX) && (transform.position.x - _exitX) * _dir >= 0f)
                {
                    if (_leapAtExit) yield return Leap();
                    else yield return FadeOut(0.25f);
                    yield break;
                }
                if (t > 0.6f && OffScreen()) { Destroy(gameObject); yield break; }
                yield return null;
            }
            yield return FadeOut(0.4f);
        }

        // Squat into the deck with a puff of dust; a hard landing shakes the camera.
        IEnumerator Land(int dust, float shake, float duration)
        {
            transform.position = new Vector3(transform.position.x, _groundY, 0f);
            _velocity = Vector2.zero;
            for (int i = 0; i < dust; i++)
                ProjectileDebris.Spawn(transform.position + Vector3.right * Random.Range(-0.6f, 0.6f), new Color(0.75f, 0.74f, 0.7f, 0.8f),
                    Random.Range(0.15f, 0.3f), new Vector2(Random.Range(-2.5f, 2.5f), Random.Range(0.5f, 2f)), 1.2f);
            if (shake > 0f) CameraZoomController.TriggerShake(shake, 0.12f);
            yield return Crouch(duration, 1f);
        }

        // A squat and back up, deepest halfway through.
        IEnumerator Crouch(float duration, float depth)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float squat = Mathf.Sin(Mathf.Clamp01(t / duration) * Mathf.PI) * depth;
                _body.localScale = new Vector3(1f + 0.08f * squat, 1f - 0.12f * squat, 1f);
                Pose(legF: 8f, legB: -8f, armF: 20f * squat, armB: -15f * squat, lean: -10f * squat, bob: -0.2f * squat);
                yield return null;
            }
            _body.localScale = Vector3.one;
        }

        // Bounds away: crouch, spring, sail with the legs tucked and the arms flung up, land and go
        // again. The hop that would carry it over the exit becomes the dive off the edge.
        IEnumerator HopAway()
        {
            float hangTime = 2f * hopJump / Mathf.Max(0.01f, hopGravity);
            float reach = hopSpeed * hangTime;
            float elapsed = 0f;
            int hop = 0;
            while (elapsed < maxRunTime)
            {
                // Coiling for the jump.
                yield return Crouch(hopCrouch, 0.8f);
                elapsed += hopCrouch;

                if (!float.IsNaN(_exitX) && (transform.position.x + _dir * reach - _exitX) * _dir >= 0f)
                {
                    if (_leapAtExit) yield return Leap();
                    else yield return FadeOut(0.25f);
                    yield break;
                }

                _velocity = new Vector2(_dir * hopSpeed, hopJump);
                float t = 0f;
                // Alternate the arm swing each hop so it doesn't look stamped out.
                float flail = hop % 2 == 0 ? 1f : -1f;
                while (_velocity.y > 0f || transform.position.y > _groundY)
                {
                    t += Time.deltaTime;
                    _velocity.y -= hopGravity * Time.deltaTime;
                    Move();
                    float rise = Mathf.Clamp(_velocity.y / hopJump, -1f, 1f);
                    // Legs kick forward on the way up and reach for the deck on the way down.
                    float legF = Mathf.Lerp(-10f, 38f, (rise + 1f) * 0.5f);
                    float legB = Mathf.Lerp(20f, -28f, (rise + 1f) * 0.5f);
                    // Arms drive back on take-off and come forward for the landing.
                    float u = (rise + 1f) * 0.5f;
                    float swing = 10f * Mathf.Sin(t * 18f) * flail;
                    Pose(legF, legB, Mathf.Lerp(25f, -70f, u) + swing, Mathf.Lerp(20f, -55f, u) - swing, -14f * rise - 4f, 0f);
                    yield return null;
                }
                elapsed += t;
                hop++;
                yield return Land(4, 0f, 0.14f);
                if (OffScreen()) { Destroy(gameObject); yield break; }
            }
            yield return FadeOut(0.4f);
        }

        // Off the edge in a long dive, gone below the deck.
        IEnumerator Leap()
        {
            _velocity = new Vector2(_dir * runSpeed * 1.1f, 9f);
            float t = 0f;
            while (t < 3f)
            {
                t += Time.deltaTime;
                _velocity.y -= 22f * Time.deltaTime;
                Move();
                // A dive: arms swept back, legs trailing, pitching head first.
                Pose(legF: -15f, legB: -35f, armF: -100f, armB: -80f, lean: Mathf.Lerp(-10f, -70f, t / 1.2f), bob: 0f);
                if (t > 0.5f && OffScreen()) break;
                yield return null;
            }
            Destroy(gameObject);
        }

        IEnumerator FadeOut(float seconds)
        {
            float t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                foreach (var r in _renderers) r.color = new Color(1f, 1f, 1f, 1f - t / seconds);
                yield return null;
            }
            Destroy(gameObject);
        }

        void Move()
        {
            transform.position += (Vector3)(_velocity * Time.deltaTime);
        }

        // Angles in degrees, positive swings a limb forward (toward the way it faces).
        void Pose(float legF, float legB, float armF, float armB, float lean, float bob)
        {
            _legF.localRotation = Quaternion.Euler(0f, 0f, legF);
            _legB.localRotation = Quaternion.Euler(0f, 0f, legB);
            _armF.localRotation = Quaternion.Euler(0f, 0f, armF);
            _armB.localRotation = Quaternion.Euler(0f, 0f, armB);
            _body.localRotation = Quaternion.Euler(0f, 0f, lean);
            _body.localPosition = new Vector3(0f, PelvisHeight + bob, 0f);
        }

        bool OffScreen()
        {
            var cam = Camera.main;
            if (cam == null) return false;
            Vector3 v = cam.WorldToViewportPoint(transform.position + Vector3.up * PelvisHeight);
            return v.x < -0.15f || v.x > 1.15f || v.y < -0.3f || v.y > 1.3f;
        }
    }
}
