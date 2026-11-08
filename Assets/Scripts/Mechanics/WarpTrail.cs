using UnityEngine;
using System.Collections;
using Platformer.View;

namespace Platformer.Mechanics
{
    /// <summary>
    /// Creates the visual afterimages, directional dash poses, and shockwaves for a warp.
    /// </summary>
    public class WarpTrail : MonoBehaviour
    {
        [Header("Effect prefabs")]
        /// <summary>
        /// Played where the warp started. Leave empty for afterimages only.
        /// </summary>
        public SpriteSequenceEffect departEffect;

        /// <summary>
        /// Played where the warp ended.
        /// </summary>
        public SpriteSequenceEffect arriveEffect;

        /// <summary>
        /// The source art shows a whole left to right warp, so the two halves are spawned at
        /// their own ends and turned to face the direction travelled. That way a warp upwards
        /// or diagonally reads correctly, which a single baked in sequence could not do.
        /// </summary>
        public bool rotateEffectsToWarpDirection = true;

        [Header("Afterimages")]
        /// <summary>
        /// Number of afterimages left at the departure point, and again on arrival.
        /// </summary>
        public int ghostsPerEnd = 3;

        /// <summary>
        /// How far each cluster of afterimages fans out along the warp, in world units.
        /// </summary>
        public float clusterSpread = 0.45f;

        /// <summary>
        /// Delay before the arrival afterimages appear, so the two ends read as separate.
        /// </summary>
        public float arrivalDelay = 0.04f;

        public float ghostLifetime = 0.24f;
        public Color trailColor = new Color(0.35f, 0.85f, 1f, 0.72f);

        private SpriteRenderer playerSpriteRenderer;
        private Animator playerAnimator;
        private PlayerController playerController;
        private Sprite[] horizontalDashSprites;
        private Sprite[] verticalDashSprites;
        private Coroutine poseOverrideCoroutine;
        private Sprite activeDashSprite;
        private bool playerHiddenInWarp;

        private const float PixelsPerUnit = 210.24156f;
        private static readonly Vector2 StandardPivot = new Vector2(0.5f, 0.5416667f);

        void Awake()
        {
            playerSpriteRenderer = GetComponent<SpriteRenderer>();
            playerAnimator = GetComponent<Animator>();
            playerController = GetComponent<PlayerController>();
            LoadWarpDashSprites();
        }

        void LateUpdate()
        {
            if (playerSpriteRenderer == null) return;

            // Override Animator in LateUpdate so the 4-frame dash animation is 100% visible every frame
            if (activeDashSprite != null)
            {
                playerSpriteRenderer.sprite = activeDashSprite;
            }

            if (playerHiddenInWarp)
            {
                Color c = playerSpriteRenderer.color;
                c.a = 0f;
                playerSpriteRenderer.color = c;
            }
        }

        private void LoadWarpDashSprites()
        {
            horizontalDashSprites = new Sprite[4];
            verticalDashSprites = new Sprite[4];
            for (int i = 0; i < 4; i++)
            {
                horizontalDashSprites[i] = LoadSpriteFromResource($"WarpSprites/Warp_Dash_0{i + 1}");
                verticalDashSprites[i] = LoadSpriteFromResource($"WarpSprites/Vertical_Dash_0{i + 1}");
            }
        }

        private static Sprite LoadSpriteFromResource(string resourcePath)
        {
            Texture2D tex = Resources.Load<Texture2D>(resourcePath);
            if (tex == null) return null;
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            return Sprite.Create(
                tex,
                new Rect(0f, 0f, tex.width, tex.height),
                StandardPivot,
                PixelsPerUnit);
        }

        private Sprite[] GetPoseSequence(Vector3 direction)
        {
            bool isVertical = Mathf.Abs(direction.y) > Mathf.Abs(direction.x) * 1.1f && direction.y > 0.1f;
            Sprite[] seq = isVertical ? verticalDashSprites : horizontalDashSprites;
            if (seq != null && seq.Length == 4 && seq[0] != null) return seq;
            return null;
        }

        /// <summary>
        /// Leaves a burst of directional dash afterimages along the warp path, plays the
        /// portal depart/arrive effects, and briefly displays the directional dash pose on the player.
        /// </summary>
        public void SpawnWarpTrail(Vector3 from, Vector3 to)
        {
            if (playerSpriteRenderer == null) return;

            var delta = to - from;
            float dist = delta.magnitude;
            var direction = dist > 0.001f ? delta / dist : Vector3.right;
            bool isVertical = Mathf.Abs(direction.y) > Mathf.Abs(direction.x) * 1.1f;

            // Keep vertical rift upright for horizontal warps; rotate 90 degrees for vertical warps
            Vector2 riftFacing = (rotateEffectsToWarpDirection && isVertical)
                ? new Vector2(0f, Mathf.Sign(direction.y))
                : Vector2.right;

            float portalOffset = Mathf.Min(0.55f, dist * 0.25f);
            Vector3 entryPortalPos = from + direction * portalOffset;
            Vector3 exitPortalPos = to - direction * portalOffset;

            Sprite[] dashSeq = GetPoseSequence(direction);

            // 1. Entry portal opens IN FRONT of the character
            var depEffect = SpriteSequenceEffect.Spawn(departEffect, entryPortalPos, riftFacing);
            if (depEffect != null)
            {
                var depSr = depEffect.GetComponent<SpriteRenderer>();
                if (depSr != null)
                {
                    depSr.sortingLayerID = playerSpriteRenderer.sortingLayerID;
                    depSr.sortingOrder = playerSpriteRenderer.sortingOrder + 1;
                }
            }
            ExpandingRing.Spawn(entryPortalPos, new Color(0.45f, 0.75f, 1f, 0.85f), 1.35f, 0.18f);

            // Play the 3-phase portal entry -> dimensional vanish -> portal exit sequence
            if (dashSeq != null)
            {
                if (poseOverrideCoroutine != null)
                {
                    StopCoroutine(poseOverrideCoroutine);
                    RestorePlayerVisibility();
                }
                poseOverrideCoroutine = StartCoroutine(PlayWarpDashPoseRoutine(dashSeq, from, entryPortalPos, exitPortalPos, to, direction, riftFacing));
            }
            else
            {
                SpawnCluster(from, direction);
                StartCoroutine(SpawnArrivalCluster(exitPortalPos, -direction, riftFacing, null));
            }
        }

        private void RestorePlayerVisibility()
        {
            playerHiddenInWarp = false;
            if (playerSpriteRenderer != null)
            {
                Color c = playerSpriteRenderer.color;
                c.a = 1f;
                playerSpriteRenderer.color = c;
            }
        }

        private IEnumerator PlayWarpDashPoseRoutine(
            Sprite[] dashSeq,
            Vector3 from,
            Vector3 entryPortalPos,
            Vector3 exitPortalPos,
            Vector3 to,
            Vector3 direction,
            Vector2 riftFacing)
        {
            const float enterDuration = 0.085f;
            const float vanishDuration = 0.055f;
            const float exitDuration = 0.130f;
            const float totalDuration = enterDuration + vanishDuration + exitDuration;
            float elapsed = 0f;
            bool isVerticalUp = Mathf.Abs(direction.y) > Mathf.Abs(direction.x) * 1.1f && direction.y > 0.1f;

            bool spawnedGhost0 = false;
            bool spawnedGhost1 = false;
            bool spawnedArrivalPortal = false;
            bool spawnedGhost2 = false;

            playerHiddenInWarp = false;
            activeDashSprite = dashSeq[0];
            if (playerSpriteRenderer != null)
            {
                playerSpriteRenderer.sprite = activeDashSprite;
            }

            while (elapsed < totalDuration)
            {
                elapsed += Time.deltaTime;

                if (elapsed < enterDuration)
                {
                    // Phase 1 (0.00s - 0.085s): Character dashes forward INTO the portal in front
                    playerHiddenInWarp = false;
                    activeDashSprite = elapsed < 0.035f ? dashSeq[0] : dashSeq[1];

                    if (!spawnedGhost0 && elapsed >= 0.015f)
                    {
                        spawnedGhost0 = true;
                        SpawnGhostWithSprite(from, dashSeq[0], ghostLifetime * 0.85f, new Color(0.55f, 0.85f, 1f, 0.75f), Vector3.one);
                    }
                    if (!spawnedGhost1 && elapsed >= 0.055f)
                    {
                        spawnedGhost1 = true;
                        SpawnGhostWithSprite(Vector3.Lerp(from, entryPortalPos, 0.6f), dashSeq[1], ghostLifetime * 0.9f, trailColor, Vector3.one);
                    }
                }
                else if (elapsed < enterDuration + vanishDuration)
                {
                    // Phase 2 (0.085s - 0.140s): Character vanishes inside the dimensional rift
                    playerHiddenInWarp = true;

                    // Spawn the arrival portal BEHIND the destination right before the character bursts out
                    if (!spawnedArrivalPortal && elapsed >= enterDuration + vanishDuration * 0.45f)
                    {
                        spawnedArrivalPortal = true;
                        var arrEffect = SpriteSequenceEffect.Spawn(arriveEffect, exitPortalPos, riftFacing);
                        if (arrEffect != null && playerSpriteRenderer != null)
                        {
                            var arrSr = arrEffect.GetComponent<SpriteRenderer>();
                            if (arrSr != null)
                            {
                                arrSr.sortingLayerID = playerSpriteRenderer.sortingLayerID;
                                arrSr.sortingOrder = playerSpriteRenderer.sortingOrder - 1;
                            }
                        }
                        ExpandingRing.Spawn(exitPortalPos, new Color(0.6f, 0.9f, 1f, 0.85f), 1.5f, 0.2f);
                    }
                }
                else
                {
                    // Phase 3 (0.140s - 0.270s): Character bursts OUT in front of the arrival portal
                    if (playerHiddenInWarp)
                    {
                        RestorePlayerVisibility();
                    }

                    float exitElapsed = elapsed - (enterDuration + vanishDuration);
                    // For vertical upward warp, keep the head-tilted-up surging pose (dashSeq[2]) all the way to the top!
                    activeDashSprite = isVerticalUp
                        ? dashSeq[2]
                        : (exitElapsed < 0.075f ? dashSeq[2] : dashSeq[3]);

                    if (!spawnedGhost2)
                    {
                        spawnedGhost2 = true;
                        SpawnGhostWithSprite(exitPortalPos, dashSeq[2], ghostLifetime * 0.95f, new Color(0.45f, 0.92f, 1f, 0.82f), Vector3.one);
                    }
                }

                yield return null;
            }

            if (isVerticalUp && playerController != null)
            {
                // Vertical upward warp: hold the head-raised surging pose (dashSeq[2])
                // until upward velocity finishes at the apex of the rise
                float maxHold = 0.35f;
                float holdElapsed = 0f;
                while (holdElapsed < maxHold && !playerController.IsGrounded && playerController.velocity.y > 0.05f)
                {
                    holdElapsed += Time.deltaTime;
                    activeDashSprite = dashSeq[2];
                    yield return null;
                }
            }
            else if (dashSeq.Length >= 4)
            {
                // Horizontal / diagonal warp: linger on the final dash follow-through frame (dashSeq[3])
                // so the finishing pose stays clearly visible just like the vertical warp
                float maxHold = 0.25f;
                float holdElapsed = 0f;
                while (holdElapsed < maxHold)
                {
                    holdElapsed += Time.deltaTime;
                    activeDashSprite = dashSeq[3];
                    yield return null;
                }
            }

            RestorePlayerVisibility();
            activeDashSprite = null;
            poseOverrideCoroutine = null;
        }

        private void SpawnCluster(Vector3 anchor, Vector3 fanDirection)
        {
            var count = Mathf.Max(1, ghostsPerEnd);
            for (var i = 0; i < count; i++)
            {
                var t = count == 1 ? 0f : i / (float)(count - 1);
                SpawnGhostWithSprite(
                    anchor + fanDirection * (clusterSpread * t),
                    playerSpriteRenderer.sprite,
                    ghostLifetime * Mathf.Lerp(1f, 0.55f, t),
                    trailColor,
                    Vector3.one);
            }
        }

        private IEnumerator SpawnArrivalCluster(Vector3 anchor, Vector3 fanDirection, Vector2 facing, Sprite[] dashSeq)
        {
            if (arrivalDelay > 0f) yield return new WaitForSeconds(arrivalDelay);

            SpriteSequenceEffect.Spawn(arriveEffect, anchor, facing);
            ExpandingRing.Spawn(anchor, new Color(0.6f, 0.9f, 1f, 0.85f), 1.5f, 0.2f);

            if (playerSpriteRenderer == null) yield break;
            if (dashSeq != null)
            {
                SpawnGhostWithSprite(anchor + fanDirection * 0.18f, dashSeq[2], ghostLifetime * 0.85f, trailColor, Vector3.one);
            }
            else
            {
                SpawnCluster(anchor, fanDirection);
            }
        }

        private void SpawnGhostWithSprite(Vector3 position, Sprite sprite, float lifetime, Color color, Vector3 scaleMultiplier)
        {
            if (sprite == null) return;

            GameObject ghost = new GameObject("WarpGhost");
            ghost.transform.position = position;
            ghost.transform.localScale = Vector3.Scale(transform.localScale, scaleMultiplier);

            SpriteRenderer sr = ghost.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.flipX = playerSpriteRenderer.flipX;
            sr.color = color;

            sr.sortingLayerID = playerSpriteRenderer.sortingLayerID;
            sr.sortingOrder = playerSpriteRenderer.sortingOrder - 1;

            StartCoroutine(FadeGhost(sr, lifetime));
        }

        private IEnumerator FadeGhost(SpriteRenderer sr, float lifetime)
        {
            float elapsed = 0f;
            Color startColor = sr.color;

            while (elapsed < lifetime)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Lerp(startColor.a, 0f, elapsed / lifetime);
                sr.color = new Color(startColor.r, startColor.g, startColor.b, alpha);
                yield return null;
            }

            Destroy(sr.gameObject);
        }
    }
}
