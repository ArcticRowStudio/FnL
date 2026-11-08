using System.Collections.Generic;
using Platformer.Mechanics;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Platformer.UI
{
    /// <summary>
    /// Displays a military high-power Anti-Materiel Rifle (AMR .50 BMG) optical scope.
    /// Features:
    /// - Optical scope lens vignette with circular iris and peripheral darkness.
    /// - Precision Mil-Dot and Bullet Drop Compensation (BDC) tactical reticle.
    /// - Monospace digital laser rangefinder telemetry (realtime target distance, caliber, magnification).
    /// - Enemy acquisition / lock-on feedback (color shift to lethal crimson + target brackets + audio chirp).
    /// - Authentic heavy weapon recoil kick and synthesized supersonic .50 BMG gunshot audio.
    /// </summary>
    public class AimReticleUI : MonoBehaviour
    {
        [Header("Optic Styling")]
        public Color readyColor = new Color(0.2f, 0.95f, 0.65f, 0.95f);      // Tactical cyan-green
        public Color lockedColor = new Color(1.0f, 0.16f, 0.18f, 0.98f);     // Hostile locked crimson
        public Color blockedColor = new Color(1.0f, 0.55f, 0.20f, 0.90f);    // Reloading / empty amber

        [Header("Animation & Dynamics")]
        public float scopeFadeSpeed = 9f;
        public float recoilKickAmount = 28f;
        public float recoilRecoverySpeed = 140f;

        private PlayerController player;
        private Canvas canvas;
        private CanvasScaler scaler;
        private CanvasGroup canvasGroup;
        private RectTransform canvasRect;

        private GameObject root;
        private RectTransform vignetteRect;
        private Image vignetteImage;
        private Material vignetteMaterial;
        private RectTransform reticleHolder;
        private Image reticleImage;

        // Digital Rangefinder / Telemetry
        private Text distanceText;
        private Text weaponTagText;
        private Text statusTagText;

        // Target Lock-On Brackets
        private RectTransform[] lockBrackets;
        private Image[] lockBracketImages;
        private float currentBracketSize = 56f;
        private bool isLockedOn = false;

        // Recoil & Shockwave
        private float currentRecoilOffset = 0f;
        private Image shockwaveRingImage;
        private RectTransform shockwaveRingRect;
        private float shockwaveTimer = 0f;

        // Procedural Audio
        private AudioSource audioSource;
        private AudioClip gunshotClip;
        private AudioClip lockChirpClip;

        // Procedural Sprites
        private Sprite vignetteSprite;
        private Sprite reticleSprite;
        private Sprite bracketSprite;
        private Sprite ringSprite;

        private float currentAlpha = 0f;

        public void Bind(PlayerController source)
        {
            if (player != null)
            {
                player.OnSniperShot -= HandleSniperShot;
            }

            player = source;

            if (player != null)
            {
                player.OnSniperShot += HandleSniperShot;
            }
        }

        private void Awake()
        {
            GenerateProceduralSprites();
            SynthesizeProceduralAudio();
            BuildUI();
        }

        private void Start()
        {
            if (player == null)
            {
                player = FindAnyObjectByType<PlayerController>();
                if (player != null)
                {
                    player.OnSniperShot += HandleSniperShot;
                }
            }
        }

        private void OnDestroy()
        {
            if (player != null)
            {
                player.OnSniperShot -= HandleSniperShot;
            }

            if (vignetteMaterial != null)
            {
                Destroy(vignetteMaterial);
                vignetteMaterial = null;
            }
        }

        private void OnDisable()
        {
            if (root != null) root.SetActive(false);
            currentAlpha = 0f;
            if (canvasGroup != null) canvasGroup.alpha = 0f;
        }

        private void Update()
        {
            if (root == null || canvasGroup == null) return;

            bool aiming = player != null && player.IsAiming;
            float targetAlpha = aiming ? 1f : 0f;
            currentAlpha = Mathf.MoveTowards(currentAlpha, targetAlpha, Time.deltaTime * scopeFadeSpeed);
            canvasGroup.alpha = currentAlpha;

            if (currentAlpha <= 0.001f)
            {
                if (root.activeSelf) root.SetActive(false);
                isLockedOn = false;
                return;
            }

            if (!root.activeSelf) root.SetActive(true);

            Vector2 mouseScreenPos = Vector2.zero;
            if (Mouse.current != null)
            {
                mouseScreenPos = Mouse.current.position.ReadValue();
            }

            // Fallback to screen center if mouse is at origin (e.g. initial frame or unfocused editor)
            if (mouseScreenPos.sqrMagnitude < 1f)
            {
                mouseScreenPos = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            }

            // Convert screen mouse to Canvas local space (supports both ScreenSpaceOverlay and ScreenSpaceCamera)
            Camera eventCam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay) ? canvas.worldCamera : null;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, mouseScreenPos, eventCam, out Vector2 localMousePos);

            // Recoil kick recovery
            if (currentRecoilOffset > 0f)
            {
                currentRecoilOffset = Mathf.MoveTowards(currentRecoilOffset, 0f, Time.deltaTime * recoilRecoverySpeed);
            }

            Vector2 opticPos = localMousePos + new Vector2(0f, currentRecoilOffset);

            // Update procedural scope shader properties so the circular spotlight follows the scope crosshair across full screen
            if (vignetteMaterial != null)
            {
                float screenW = Mathf.Max(Screen.width, 1);
                float screenH = Mathf.Max(Screen.height, 1);
                float refH = scaler != null ? scaler.referenceResolution.y : 1080f;
                float recoilUvY = (currentRecoilOffset / refH);

                float uvX = mouseScreenPos.x / screenW;
                float uvY = (mouseScreenPos.y / screenH) + recoilUvY;

                vignetteMaterial.SetVector("_ScopeCenter", new Vector4(uvX, uvY, 0f, 0f));
                vignetteMaterial.SetFloat("_Aspect", screenW / screenH);
                vignetteMaterial.SetFloat("_ApertureRadius", 0.32f);
                vignetteMaterial.SetFloat("_SmoothWidth", 0.08f);
            }

            // Update reticle position with recoil offset
            reticleHolder.anchoredPosition = opticPos;

            // World position targeting check (100% synchronized with PlayerController ballistics)
            Vector3 mouseWorldPos = Vector3.zero;
            bool enemyFound = false;
            string targetInfo = "HOSTILE IDENTIFIED";

            if (player != null && player.TryGetAimWorldPosition(out mouseWorldPos, out EnemyController targetedEnemy))
            {
                if (targetedEnemy != null)
                {
                    enemyFound = true;
                    Health h = targetedEnemy.GetComponent<Health>();
                    if (h != null)
                    {
                        targetInfo = $"AMR LOCK: HP {h.CurrentHP}/{h.maxHP}";
                    }
                }
            }
            else
            {
                Camera mainCam = Camera.main;
                if (mainCam != null)
                {
                    float zDepth = player != null ? Mathf.Abs(mainCam.transform.position.z - player.transform.position.z) : 10f;
                    mouseWorldPos = mainCam.ScreenToWorldPoint(new Vector3(mouseScreenPos.x, mouseScreenPos.y, zDepth));
                }
                enemyFound = CheckEnemyTarget(mouseWorldPos, out targetInfo);
            }

            if (enemyFound && !isLockedOn)
            {
                // Play lock acquisition chirp
                if (audioSource != null && lockChirpClip != null)
                {
                    audioSource.PlayOneShot(lockChirpClip, 0.4f);
                }
            }
            isLockedOn = enemyFound;

            // Determine active reticle color
            Color activeColor;
            if (player != null && (player.IsReloading || player.Ammo <= 0))
            {
                activeColor = blockedColor;
            }
            else if (isLockedOn)
            {
                activeColor = lockedColor;
            }
            else
            {
                activeColor = readyColor;
            }

            reticleImage.color = activeColor;

            // Animate lock brackets
            float targetBracketSize = isLockedOn ? 38f : 56f;
            currentBracketSize = Mathf.Lerp(currentBracketSize, targetBracketSize, Time.deltaTime * 14f);
            UpdateLockBrackets(currentBracketSize, activeColor);

            // Update rangefinder & telemetry texts
            float targetDistance = player != null ? Vector2.Distance(player.transform.position, mouseWorldPos) : 0f;
            if (distanceText != null)
            {
                distanceText.color = activeColor;
                if (isLockedOn)
                {
                    distanceText.text = $"RNG: {targetDistance:F1}M [LOCK-ON]";
                }
                else
                {
                    distanceText.text = $"RNG: {targetDistance:F1}M";
                }
            }

            if (statusTagText != null)
            {
                statusTagText.color = activeColor;
                if (player != null && player.IsReloading)
                {
                    statusTagText.text = $"RELOADING ({Mathf.RoundToInt(player.ReloadProgress * 100)}%)";
                }
                else if (player != null && player.Ammo <= 0)
                {
                    statusTagText.text = "MAG EMPTY";
                }
                else if (isLockedOn)
                {
                    statusTagText.text = targetInfo;
                }
                else
                {
                    statusTagText.text = "OPTIC: 8.0x BDC";
                }
            }

            // Shockwave ring animation on firing
            if (shockwaveTimer > 0f)
            {
                shockwaveTimer -= Time.deltaTime;
                float progress = 1f - Mathf.Clamp01(shockwaveTimer / 0.22f);
                float scale = Mathf.Lerp(0.3f, 2.4f, progress);
                shockwaveRingRect.localScale = new Vector3(scale, scale, 1f);
                shockwaveRingImage.color = new Color(1f, 1f, 1f, (1f - progress) * 0.85f);
            }
            else
            {
                shockwaveRingImage.color = Color.clear;
            }
        }

        private bool CheckEnemyTarget(Vector3 worldPos, out string info)
        {
            info = "HOSTILE IDENTIFIED";
            Collider2D[] hits = Physics2D.OverlapCircleAll(worldPos, 1.2f);
            for (int i = 0; i < hits.Length; i++)
            {
                Collider2D hit = hits[i];
                if (hit == null) continue;
                if (player != null && (hit.transform == player.transform || hit.transform.IsChildOf(player.transform))) continue;

                EnemyController enemy = hit.GetComponentInParent<EnemyController>();
                if (enemy != null)
                {
                    Health h = enemy.GetComponent<Health>();
                    if (h != null)
                    {
                        info = $"AMR LOCK: HP {h.CurrentHP}/{h.maxHP}";
                    }
                    return true;
                }
            }
            return false;
        }

        private void HandleSniperShot()
        {
            // Upward recoil kick for heavy Anti-Materiel Rifle
            currentRecoilOffset = recoilKickAmount;

            // Trigger optical shockwave
            shockwaveTimer = 0.22f;
            if (shockwaveRingRect != null)
            {
                shockwaveRingRect.localScale = Vector3.one * 0.3f;
            }

            // Play synthesized heavy .50 BMG gunshot sound
            if (audioSource != null && gunshotClip != null)
            {
                audioSource.PlayOneShot(gunshotClip, 1.0f);
            }
        }

        private void UpdateLockBrackets(float size, Color color)
        {
            if (lockBrackets == null) return;
            float offset = size * 0.5f;

            // Top-Left
            lockBrackets[0].anchoredPosition = new Vector2(-offset, offset);
            lockBrackets[0].localRotation = Quaternion.Euler(0f, 0f, 0f);

            // Top-Right
            lockBrackets[1].anchoredPosition = new Vector2(offset, offset);
            lockBrackets[1].localRotation = Quaternion.Euler(0f, 0f, -90f);

            // Bottom-Right
            lockBrackets[2].anchoredPosition = new Vector2(offset, -offset);
            lockBrackets[2].localRotation = Quaternion.Euler(0f, 0f, -180f);

            // Bottom-Left
            lockBrackets[3].anchoredPosition = new Vector2(-offset, -offset);
            lockBrackets[3].localRotation = Quaternion.Euler(0f, 0f, -270f);

            for (int i = 0; i < lockBracketImages.Length; i++)
            {
                lockBracketImages[i].color = color;
            }
        }

        private void BuildUI()
        {
            root = new GameObject("AMR_ScopeCanvas");
            root.transform.SetParent(transform, false);

            canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 115;

            scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGroup = root.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasRect = root.GetComponent<RectTransform>();

            // Fullscreen Optical Scope Vignette Housing
            // Pinned fullscreen (0 to 1) so it ALWAYS covers 100% of the screen across any aspect ratio.
            // Uses procedural UI/ScopeVignette shader so the illuminated spotlight opening dynamically tracks the mouse.
            GameObject vigObj = new GameObject("ScopeVignette", typeof(RectTransform));
            vigObj.transform.SetParent(root.transform, false);
            vignetteRect = vigObj.GetComponent<RectTransform>();
            vignetteRect.anchorMin = Vector2.zero;
            vignetteRect.anchorMax = Vector2.one;
            vignetteRect.offsetMin = Vector2.zero;
            vignetteRect.offsetMax = Vector2.zero;

            vignetteImage = vigObj.AddComponent<Image>();
            
            // Assign 4x4 white sprite so Graphic initializes proper vertex UV coordinates
            vignetteImage.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), Vector2.one * 0.5f);

            // Instantiate procedural scope vignette material
            Shader scopeShader = Shader.Find("UI/ScopeVignette");
            if (scopeShader != null)
            {
                vignetteMaterial = new Material(scopeShader);
            }

            if (vignetteMaterial != null)
            {
                vignetteImage.material = vignetteMaterial;
            }
            else if (vignetteSprite != null)
            {
                vignetteImage.sprite = vignetteSprite;
            }

            vignetteImage.raycastTarget = false;
            vignetteImage.color = Color.white;

            // Reticle & Telemetry Container
            GameObject reticleObj = new GameObject("ReticleContainer", typeof(RectTransform));
            reticleObj.transform.SetParent(root.transform, false);
            reticleHolder = reticleObj.GetComponent<RectTransform>();
            reticleHolder.anchorMin = reticleHolder.anchorMax = reticleHolder.pivot = new Vector2(0.5f, 0.5f);
            reticleHolder.sizeDelta = new Vector2(256f, 256f);

            // Mil-Dot Reticle Image
            GameObject imgObj = new GameObject("ReticleImage", typeof(RectTransform));
            imgObj.transform.SetParent(reticleHolder, false);
            RectTransform imgRect = imgObj.GetComponent<RectTransform>();
            imgRect.anchorMin = imgRect.anchorMax = imgRect.pivot = new Vector2(0.5f, 0.5f);
            imgRect.sizeDelta = new Vector2(240f, 240f);

            reticleImage = imgObj.AddComponent<Image>();
            reticleImage.sprite = reticleSprite;
            reticleImage.raycastTarget = false;
            reticleImage.color = readyColor;

            // Shockwave ring (muzzle blast ring)
            GameObject shockObj = new GameObject("ShockwaveRing", typeof(RectTransform));
            shockObj.transform.SetParent(reticleHolder, false);
            shockwaveRingRect = shockObj.GetComponent<RectTransform>();
            shockwaveRingRect.anchorMin = shockwaveRingRect.anchorMax = shockwaveRingRect.pivot = new Vector2(0.5f, 0.5f);
            shockwaveRingRect.sizeDelta = new Vector2(120f, 120f);
            shockwaveRingImage = shockObj.AddComponent<Image>();
            shockwaveRingImage.sprite = ringSprite;
            shockwaveRingImage.raycastTarget = false;
            shockwaveRingImage.color = Color.clear;

            // Lock Brackets (4 corners)
            lockBrackets = new RectTransform[4];
            lockBracketImages = new Image[4];
            for (int i = 0; i < 4; i++)
            {
                GameObject bObj = new GameObject($"Bracket_{i}", typeof(RectTransform));
                bObj.transform.SetParent(reticleHolder, false);
                RectTransform bRect = bObj.GetComponent<RectTransform>();
                bRect.anchorMin = bRect.anchorMax = bRect.pivot = new Vector2(0.5f, 0.5f);
                bRect.sizeDelta = new Vector2(16f, 16f);

                Image bImg = bObj.AddComponent<Image>();
                bImg.sprite = bracketSprite;
                bImg.raycastTarget = false;
                bImg.color = readyColor;

                lockBrackets[i] = bRect;
                lockBracketImages[i] = bImg;
            }

            // Monospace Military Telemetry Font
            Font tacticalFont = Font.CreateDynamicFontFromOSFont(new string[] { "Consolas", "Courier New", "Arial" }, 14);

            // Weapon designation header
            GameObject tagObj = new GameObject("WeaponTag", typeof(RectTransform));
            tagObj.transform.SetParent(reticleHolder, false);
            RectTransform tagRect = tagObj.GetComponent<RectTransform>();
            tagRect.anchorMin = tagRect.anchorMax = tagRect.pivot = new Vector2(0.5f, 0.5f);
            tagRect.anchoredPosition = new Vector2(0f, 85f);
            tagRect.sizeDelta = new Vector2(200f, 24f);

            weaponTagText = tagObj.AddComponent<Text>();
            weaponTagText.font = tacticalFont;
            weaponTagText.fontSize = 12;
            weaponTagText.alignment = TextAnchor.MiddleCenter;
            weaponTagText.text = "[AMR .50 BMG]";
            weaponTagText.color = new Color(readyColor.r, readyColor.g, readyColor.b, 0.8f);
            weaponTagText.raycastTarget = false;

            // Distance meter readout
            GameObject distObj = new GameObject("DistanceReadout", typeof(RectTransform));
            distObj.transform.SetParent(reticleHolder, false);
            RectTransform distRect = distObj.GetComponent<RectTransform>();
            distRect.anchorMin = distRect.anchorMax = distRect.pivot = new Vector2(0.5f, 0.5f);
            distRect.anchoredPosition = new Vector2(0f, -85f);
            distRect.sizeDelta = new Vector2(200f, 24f);

            distanceText = distObj.AddComponent<Text>();
            distanceText.font = tacticalFont;
            distanceText.fontSize = 13;
            distanceText.alignment = TextAnchor.MiddleCenter;
            distanceText.text = "RNG: 00.0M";
            distanceText.color = readyColor;
            distanceText.raycastTarget = false;

            // Status / Optic Mode readout
            GameObject statusObj = new GameObject("StatusReadout", typeof(RectTransform));
            statusObj.transform.SetParent(reticleHolder, false);
            RectTransform statusRect = statusObj.GetComponent<RectTransform>();
            statusRect.anchorMin = statusRect.anchorMax = statusRect.pivot = new Vector2(0.5f, 0.5f);
            statusRect.anchoredPosition = new Vector2(0f, -105f);
            statusRect.sizeDelta = new Vector2(240f, 20f);

            statusTagText = statusObj.AddComponent<Text>();
            statusTagText.font = tacticalFont;
            statusTagText.fontSize = 11;
            statusTagText.alignment = TextAnchor.MiddleCenter;
            statusTagText.text = "OPTIC: 8.0x BDC";
            statusTagText.color = new Color(readyColor.r, readyColor.g, readyColor.b, 0.75f);
            statusTagText.raycastTarget = false;

            root.SetActive(false);
        }

        private void GenerateProceduralSprites()
        {
            // 1. Scope Vignette Texture (1024x1024 square optical lens aperture)
            int vw = 1024, vh = 1024;
            Texture2D vTex = new Texture2D(vw, vh, TextureFormat.RGBA32, false);
            vTex.wrapMode = TextureWrapMode.Clamp;
            vTex.filterMode = FilterMode.Bilinear;
            Color32[] vPixels = new Color32[vw * vh];
            float cx = vw * 0.5f;
            float cy = vh * 0.5f;

            for (int y = 0; y < vh; y++)
            {
                float dy = y - cy;
                for (int x = 0; x < vw; x++)
                {
                    float dx = x - cx;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);

                    float alpha = 0f;
                    if (dist > 300f)
                    {
                        alpha = Mathf.SmoothStep(0f, 1f, (dist - 300f) / 130f);
                    }
                    if (dist > 430f)
                    {
                        alpha = 1f;
                    }

                    // Multi-coated optical glass retainer ring reflections
                    if (Mathf.Abs(dist - 304f) < 2f)
                    {
                        float ring = 1f - (Mathf.Abs(dist - 304f) / 2f);
                        alpha = Mathf.Max(alpha, 0.28f * ring);
                    }
                    if (Mathf.Abs(dist - 425f) < 2f)
                    {
                        float ring = 1f - (Mathf.Abs(dist - 425f) / 2f);
                        alpha = Mathf.Max(alpha, 0.52f * ring);
                    }

                    byte aByte = (byte)(Mathf.Clamp01(alpha) * 255);
                    // Matte dark navy-charcoal sniper scope housing
                    vPixels[y * vw + x] = new Color32(5, 7, 11, aByte);
                }
            }
            vTex.SetPixels32(vPixels);
            vTex.Apply();
            vignetteSprite = Sprite.Create(vTex, new Rect(0, 0, vw, vh), new Vector2(0.5f, 0.5f));

            // 2. Mil-Dot AMR Reticle (256x256)
            int rw = 256, rh = 256;
            Texture2D rTex = new Texture2D(rw, rh, TextureFormat.RGBA32, false);
            Color32[] rPixels = new Color32[rw * rh];
            for (int i = 0; i < rPixels.Length; i++) rPixels[i] = new Color32(0, 0, 0, 0);

            void SetPixelAA(int x, int y, Color color)
            {
                if (x < 0 || x >= rw || y < 0 || y >= rh) return;
                int idx = y * rw + x;
                Color existing = rPixels[idx];
                Color blended = Color.Lerp(existing, color, color.a);
                blended.a = Mathf.Clamp01(existing.a + color.a);
                rPixels[idx] = blended;
            }

            void DrawH(int y, int x0, int x1, Color c)
            {
                if (x0 > x1) { int t = x0; x0 = x1; x1 = t; }
                for (int x = x0; x <= x1; x++) SetPixelAA(x, y, c);
            }

            void DrawV(int x, int y0, int y1, Color c)
            {
                if (y0 > y1) { int t = y0; y0 = y1; y1 = t; }
                for (int y = y0; y <= y1; y++) SetPixelAA(x, y, c);
            }

            void DrawCircle(int cx, int cy, int radius, Color c)
            {
                for (int y = -radius - 1; y <= radius + 1; y++)
                {
                    for (int x = -radius - 1; x <= radius + 1; x++)
                    {
                        float dist = Mathf.Sqrt(x * x + y * y);
                        float edge = Mathf.Abs(dist - radius);
                        if (edge <= 1f)
                        {
                            Color cc = c;
                            cc.a *= (1f - edge);
                            SetPixelAA(cx + x, cy + y, cc);
                        }
                    }
                }
            }

            void DrawDot(int cx, int cy, int radius, Color c)
            {
                for (int y = -radius; y <= radius; y++)
                {
                    for (int x = -radius; x <= radius; x++)
                    {
                        if (x * x + y * y <= radius * radius) SetPixelAA(cx + x, cy + y, c);
                    }
                }
            }

            int rcx = rw / 2;
            int rcy = rh / 2;
            Color white = Color.white;

            // Fine Crosshair
            DrawH(rcy, rcx - 110, rcx - 14, white);
            DrawH(rcy, rcx + 14, rcx + 110, white);
            DrawV(rcx, rcy - 110, rcy - 14, white);
            DrawV(rcx, rcy + 14, rcy + 110, white);

            // Center Ring & Dot
            DrawCircle(rcx, rcy, 6, white);
            SetPixelAA(rcx, rcy, white);

            // Outer Scope Guide Circle
            DrawCircle(rcx, rcy, 110, new Color(1f, 1f, 1f, 0.4f));

            // Mil-Dots along axes (Spacing: 25, 50, 75, 100 px)
            int[] milDist = new int[] { 25, 50, 75, 100 };
            for (int i = 0; i < milDist.Length; i++)
            {
                int d = milDist[i];
                DrawDot(rcx + d, rcy, 1, white);
                DrawDot(rcx - d, rcy, 1, white);
                DrawDot(rcx, rcy + d, 1, white);
            }

            // Bullet Drop Compensation (BDC) lines below center for heavy .50 cal trajectory
            int[] bdcDist = new int[] { 25, 50, 75, 100 };
            int[] bdcWidth = new int[] { 8, 16, 24, 34 };
            for (int i = 0; i < bdcDist.Length; i++)
            {
                int by = rcy - bdcDist[i];
                int bw = bdcWidth[i];
                DrawH(by, rcx - bw, rcx + bw, white);
            }

            rTex.SetPixels32(rPixels);
            rTex.Apply();
            reticleSprite = Sprite.Create(rTex, new Rect(0, 0, rw, rh), new Vector2(0.5f, 0.5f));

            // 3. Lock Bracket Corner (32x32)
            int bwSize = 32;
            Texture2D bTex = new Texture2D(bwSize, bwSize, TextureFormat.RGBA32, false);
            Color32[] bPixels = new Color32[bwSize * bwSize];
            for (int i = 0; i < bPixels.Length; i++) bPixels[i] = new Color32(0, 0, 0, 0);

            // Top-Left L-bracket shape
            for (int x = 0; x < 18; x++)
            {
                bPixels[(bwSize - 1) * bwSize + x] = new Color32(255, 255, 255, 255);
                bPixels[(bwSize - 2) * bwSize + x] = new Color32(255, 255, 255, 255);
            }
            for (int y = bwSize - 18; y < bwSize; y++)
            {
                bPixels[y * bwSize + 0] = new Color32(255, 255, 255, 255);
                bPixels[y * bwSize + 1] = new Color32(255, 255, 255, 255);
            }
            bTex.SetPixels32(bPixels);
            bTex.Apply();
            bracketSprite = Sprite.Create(bTex, new Rect(0, 0, bwSize, bwSize), new Vector2(0.5f, 0.5f));

            // 4. Muzzle Pressure Shockwave Ring (64x64)
            int ringSize = 64;
            Texture2D ringTex = new Texture2D(ringSize, ringSize, TextureFormat.RGBA32, false);
            Color32[] ringPixels = new Color32[ringSize * ringSize];
            Vector2 ringC = new Vector2(ringSize * 0.5f, ringSize * 0.5f);
            float rMax = ringSize * 0.46f;

            for (int y = 0; y < ringSize; y++)
            {
                for (int x = 0; x < ringSize; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), ringC);
                    float diff = Mathf.Abs(d - rMax);
                    float a = diff < 3.5f ? (1f - (diff / 3.5f)) : 0f;
                    byte aByte = (byte)(Mathf.Clamp01(a) * 255);
                    ringPixels[y * ringSize + x] = new Color32(255, 255, 255, aByte);
                }
            }
            ringTex.SetPixels32(ringPixels);
            ringTex.Apply();
            ringSprite = Sprite.Create(ringTex, new Rect(0, 0, ringSize, ringSize), new Vector2(0.5f, 0.5f));
        }

        private void SynthesizeProceduralAudio()
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f; // 2D Stereo sound

            int sampleRate = 44100;

            // 1. Synthesize Heavy Anti-Materiel Rifle .50 BMG Gunshot
            float gunshotDuration = 0.85f;
            int gunshotSamples = (int)(sampleRate * gunshotDuration);
            gunshotClip = AudioClip.Create("AMR_Gunshot_50BMG", gunshotSamples, 1, sampleRate, false);
            float[] gData = new float[gunshotSamples];

            for (int i = 0; i < gunshotSamples; i++)
            {
                float t = (float)i / sampleRate;

                // Supersonic bullet crack / sharp shockwave transient
                float snap = (Random.value * 2f - 1f) * Mathf.Exp(-t * 90f);

                // Heavy explosive chamber blast (swept sine 170Hz -> 36Hz)
                float boomFreq = Mathf.Lerp(170f, 36f, Mathf.Clamp01(t / 0.12f));
                float boom = Mathf.Sin(2f * Mathf.PI * boomFreq * t) * Mathf.Exp(-t * 18f);

                // Rolling outdoor echo & mechanical resonance
                float echoNoise = (Random.value * 2f - 1f) * Mathf.Exp(-t * 6f) * 0.22f;
                float echoLow = Mathf.Sin(2f * Mathf.PI * 52f * t) * Mathf.Exp(-t * 8f) * 0.30f;

                float sample = snap * 0.75f + boom * 0.85f + echoNoise + echoLow;
                gData[i] = Mathf.Clamp(sample, -1f, 1f);
            }
            gunshotClip.SetData(gData, 0);

            // 2. Synthesize Rangefinder Optic Lock Chirp
            float chirpDuration = 0.07f;
            int chirpSamples = (int)(sampleRate * chirpDuration);
            lockChirpClip = AudioClip.Create("AMR_OpticLock_Chirp", chirpSamples, 1, sampleRate, false);
            float[] cData = new float[chirpSamples];

            for (int i = 0; i < chirpSamples; i++)
            {
                float t = (float)i / sampleRate;
                float freq = t < 0.035f ? 1900f : 2700f;
                float env = Mathf.Sin(Mathf.Clamp01(t / chirpDuration) * Mathf.PI);
                cData[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.4f;
            }
            lockChirpClip.SetData(cData, 0);
        }
    }
}
