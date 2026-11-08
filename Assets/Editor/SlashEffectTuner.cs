using System.Collections.Generic;
using Platformer.Mechanics;
using Platformer.View;
using UnityEditor;
using UnityEngine;

namespace Platformer.EditorTools
{
    /// <summary>
    /// Previews the horizontal slash arc against the attack animation, and tunes it.
    ///
    /// The effect cannot be judged in the Scene view: both the size multiplier and the sweep
    /// rotation are applied in OnEnable and Update, so an instance dropped into the scene sits
    /// at frame zero, unscaled and unturned. In Play mode it is over in a third of a second and
    /// deletes itself. This window plays the two clips against each other on the same timeline,
    /// with the same placement maths the runtime uses, and can be scrubbed frame by frame.
    ///
    /// Nothing is written until Apply is pressed.
    /// </summary>
    public class SlashEffectTuner : EditorWindow
    {
        const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";
        const string SlashPrefabPath = "Assets/Prefabs/Vfx/SlashEffect.prefab";
        const string SlashClipPath = "Assets/Character/Animations/Pixel/Juve_HorizontalSlash.anim";

        struct Key
        {
            public float Time;
            public Sprite Sprite;
        }

        // Live values. Edited here, pushed to the prefabs only on Apply.
        float sizeMultiplier;
        float startDegrees;
        float sweepDegrees;
        float framesPerSecond;
        Vector2 slashEffectOffset;
        float slashEffectDelay;

        Sprite[] effectFrames;
        List<Key> playerKeys = new List<Key>();
        float clipLength;
        Vector2 colliderOffset;
        Vector2 colliderSize;

        bool faceRight = true;
        bool playing = true;
        bool showCollider = true;
        bool showHitbox = true;
        float time;
        double lastTick;
        float zoom = 300f;
        Vector2 pan;
        string status;
        bool loaded;

        [MenuItem("Tools/Pixel art/Slash effect tuner")]
        static void Open()
        {
            SlashEffectTuner w = GetWindow<SlashEffectTuner>("Slash Tuner");
            w.minSize = new Vector2(560f, 620f);
            w.Reload();
        }

        void OnEnable()
        {
            lastTick = EditorApplication.timeSinceStartup;
            EditorApplication.update += Tick;
            if (!loaded) Reload();
        }

        void OnDisable()
        {
            EditorApplication.update -= Tick;
        }

        float EffectDuration =>
            effectFrames == null || effectFrames.Length == 0 || framesPerSecond <= 0f
                ? 0f
                : effectFrames.Length / framesPerSecond;

        float Timeline => Mathf.Max(clipLength, slashEffectDelay + EffectDuration);

        void Tick()
        {
            double now = EditorApplication.timeSinceStartup;
            float dt = (float)(now - lastTick);
            lastTick = now;
            if (!playing || !loaded) return;

            float total = Timeline;
            if (total <= 0f) return;
            // A beat of dead air at the end, so the last frame is readable before it loops.
            time = Mathf.Repeat(time + dt, total + 0.25f);
            Repaint();
        }

        void Reload()
        {
            loaded = false;
            status = null;
            playerKeys.Clear();

            GameObject slashGo = AssetDatabase.LoadAssetAtPath<GameObject>(SlashPrefabPath);
            GameObject playerGo = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(SlashClipPath);

            if (slashGo == null) { status = "Not found: " + SlashPrefabPath; return; }
            if (playerGo == null) { status = "Not found: " + PlayerPrefabPath; return; }

            SpriteSequenceEffect fx = slashGo.GetComponent<SpriteSequenceEffect>();
            PlayerController pc = playerGo.GetComponent<PlayerController>();
            BoxCollider2D box = playerGo.GetComponent<BoxCollider2D>();
            if (fx == null) { status = "SlashEffect.prefab has no SpriteSequenceEffect."; return; }
            if (pc == null) { status = "Player.prefab has no PlayerController."; return; }

            effectFrames = fx.frames;
            sizeMultiplier = fx.sizeMultiplier;
            startDegrees = fx.startDegrees;
            sweepDegrees = fx.sweepDegrees;
            framesPerSecond = fx.framesPerSecond;
            slashEffectOffset = pc.slashEffectOffset;
            slashEffectDelay = pc.slashEffectDelay;

            colliderOffset = box != null ? box.offset : Vector2.zero;
            colliderSize = box != null ? box.size : Vector2.one;

            if (clip != null)
            {
                clipLength = clip.length;
                foreach (EditorCurveBinding b in AnimationUtility.GetObjectReferenceCurveBindings(clip))
                {
                    ObjectReferenceKeyframe[] keys = AnimationUtility.GetObjectReferenceCurve(clip, b);
                    foreach (ObjectReferenceKeyframe k in keys)
                        playerKeys.Add(new Key { Time = k.time, Sprite = k.value as Sprite });
                    break;
                }
            }
            if (playerKeys.Count == 0) status = "No sprite keys in " + SlashClipPath + " - showing the effect alone.";

            loaded = true;
        }

        Sprite PlayerSpriteAt(float t)
        {
            Sprite result = null;
            for (int i = 0; i < playerKeys.Count; i++)
                if (playerKeys[i].Time <= t) result = playerKeys[i].Sprite;
            // Past the end of the swing the character is back to its first pose.
            if (t > clipLength && playerKeys.Count > 0) result = playerKeys[0].Sprite;
            return result;
        }

        int EffectFrameAt(float t)
        {
            if (effectFrames == null || effectFrames.Length == 0) return -1;
            float elapsed = t - slashEffectDelay;
            if (elapsed < 0f) return -1;
            int i = Mathf.FloorToInt(elapsed * framesPerSecond);
            return i >= effectFrames.Length ? -1 : i;
        }

        void OnGUI()
        {
            if (!loaded)
            {
                EditorGUILayout.HelpBox(status ?? "Not loaded.", MessageType.Error);
                if (GUILayout.Button("Reload")) Reload();
                return;
            }

            DrawToolbar();
            Rect view = GUILayoutUtility.GetRect(position.width, Mathf.Max(240f, position.height - 330f));
            DrawPreview(view);
            DrawControls();
        }

        void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            playing = GUILayout.Toggle(playing, playing ? "Pause" : "Play", EditorStyles.toolbarButton, GUILayout.Width(55f));
            if (GUILayout.Button("Restart", EditorStyles.toolbarButton, GUILayout.Width(60f))) time = 0f;
            faceRight = !GUILayout.Toggle(!faceRight, faceRight ? "Facing right" : "Facing left",
                EditorStyles.toolbarButton, GUILayout.Width(90f));
            GUILayout.Space(10f);
            showCollider = GUILayout.Toggle(showCollider, "Collider", EditorStyles.toolbarButton, GUILayout.Width(60f));
            showHitbox = GUILayout.Toggle(showHitbox, "Hit box", EditorStyles.toolbarButton, GUILayout.Width(60f));
            GUILayout.FlexibleSpace();
            zoom = GUILayout.HorizontalSlider(zoom, 120f, 700f, GUILayout.Width(90f));
            if (GUILayout.Button("Reload", EditorStyles.toolbarButton, GUILayout.Width(55f))) Reload();
            EditorGUILayout.EndHorizontal();
        }

        void DrawPreview(Rect view)
        {
            EditorGUI.DrawRect(view, new Color(0.13f, 0.13f, 0.16f));

            Vector2 origin = new Vector2(view.center.x + pan.x, view.center.y + view.height * 0.28f + pan.y);
            HandlePan(view);

            GUI.BeginClip(view);
            Vector2 local = origin - new Vector2(view.x, view.y);

            // Ground line at the bottom of the collider, which is where the feet sit.
            float groundY = local.y - (colliderOffset.y - colliderSize.y * 0.5f) * zoom;
            EditorGUI.DrawRect(new Rect(0f, groundY, view.width, 1f), new Color(1f, 1f, 1f, 0.18f));

            Sprite player = PlayerSpriteAt(time);
            if (player != null) DrawSprite(player, Vector2.zero, local, 0f, 1f, !faceRight);

            int fi = EffectFrameAt(time);
            if (fi >= 0 && effectFrames[fi] != null)
            {
                float sign = faceRight ? 1f : -1f;
                Vector2 at = colliderOffset + new Vector2(sign * slashEffectOffset.x, slashEffectOffset.y);
                float dur = EffectDuration;
                float u = dur <= 0f ? 0f : Mathf.Clamp01((time - slashEffectDelay) / dur);
                float angle = sign * (startDegrees + sweepDegrees * u);
                DrawSprite(effectFrames[fi], at, local, angle, sizeMultiplier, !faceRight);
                DrawCross(at, local, new Color(1f, 0.85f, 0.2f, 0.9f));
            }

            if (showCollider)
                DrawWireRect(colliderOffset, colliderSize, local, new Color(0.3f, 1f, 0.4f, 0.65f));

            if (showHitbox)
            {
                float facing = faceRight ? 1f : -1f;
                Vector2 hs = new Vector2(colliderSize.x * 1.9f, colliderSize.y * 0.9f);
                Vector2 hc = colliderOffset + new Vector2(facing * (colliderSize.x * 0.5f + hs.x * 0.5f), 0f);
                DrawWireRect(hc, hs, local, new Color(1f, 0.35f, 0.3f, 0.65f));
            }

            GUI.EndClip();

            string label = string.Format("t = {0:F3}s   player frame {1}   effect frame {2}",
                time, PlayerFrameIndex(time), fi < 0 ? "-" : fi.ToString());
            GUI.Label(new Rect(view.x + 8f, view.yMax - 20f, 420f, 18f), label, EditorStyles.miniLabel);
        }

        int PlayerFrameIndex(float t)
        {
            int idx = -1;
            for (int i = 0; i < playerKeys.Count; i++)
                if (playerKeys[i].Time <= t) idx = i;
            return idx;
        }

        void HandlePan(Rect view)
        {
            Event e = Event.current;
            if (e.type == EventType.MouseDrag && view.Contains(e.mousePosition) && e.button != 1)
            {
                pan += e.delta;
                e.Use();
                Repaint();
            }
        }

        /// <summary>
        /// Places a sprite by its pivot and applies the same transform the runtime builds:
        /// the renderer's flip happens in local space, then the sweep rotation, then the
        /// uniform size multiplier. Screen y runs the other way from world y, so the rotation
        /// is negated going in.
        /// </summary>
        void DrawSprite(Sprite s, Vector2 pivotWorld, Vector2 origin, float angleDeg, float scale, bool flip)
        {
            if (s == null || s.texture == null) return;

            Vector2 pivotScreen = new Vector2(origin.x + pivotWorld.x * zoom, origin.y - pivotWorld.y * zoom);
            float ppu = s.pixelsPerUnit;
            Rect r = s.textureRect;
            Vector2 piv = s.pivot;

            Rect dest = new Rect(
                pivotScreen.x - piv.x / ppu * zoom,
                pivotScreen.y - (r.height - piv.y) / ppu * zoom,
                r.width / ppu * zoom,
                r.height / ppu * zoom);

            Rect uv = new Rect(r.x / s.texture.width, r.y / s.texture.height,
                               r.width / s.texture.width, r.height / s.texture.height);

            Matrix4x4 old = GUI.matrix;
            GUI.matrix = old
                * Matrix4x4.TRS(pivotScreen, Quaternion.Euler(0f, 0f, -angleDeg),
                                new Vector3(flip ? -scale : scale, scale, 1f))
                * Matrix4x4.TRS(-pivotScreen, Quaternion.identity, Vector3.one);
            GUI.DrawTextureWithTexCoords(dest, s.texture, uv, true);
            GUI.matrix = old;
        }

        void DrawWireRect(Vector2 centreWorld, Vector2 sizeWorld, Vector2 origin, Color c)
        {
            float x = origin.x + (centreWorld.x - sizeWorld.x * 0.5f) * zoom;
            float y = origin.y - (centreWorld.y + sizeWorld.y * 0.5f) * zoom;
            float w = sizeWorld.x * zoom;
            float h = sizeWorld.y * zoom;
            EditorGUI.DrawRect(new Rect(x, y, w, 1f), c);
            EditorGUI.DrawRect(new Rect(x, y + h, w, 1f), c);
            EditorGUI.DrawRect(new Rect(x, y, 1f, h), c);
            EditorGUI.DrawRect(new Rect(x + w, y, 1f, h + 1f), c);
        }

        void DrawCross(Vector2 world, Vector2 origin, Color c)
        {
            float x = origin.x + world.x * zoom;
            float y = origin.y - world.y * zoom;
            EditorGUI.DrawRect(new Rect(x - 6f, y, 13f, 1f), c);
            EditorGUI.DrawRect(new Rect(x, y - 6f, 1f, 13f), c);
        }

        void DrawControls()
        {
            EditorGUILayout.Space(4f);
            float total = Timeline;
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Time", GUILayout.Width(70f));
            time = EditorGUILayout.Slider(time, 0f, Mathf.Max(0.01f, total + 0.25f));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("SlashEffect.prefab", EditorStyles.boldLabel);
            sizeMultiplier = EditorGUILayout.Slider("Size multiplier", sizeMultiplier, 0.05f, 2f);
            startDegrees = EditorGUILayout.Slider("Start degrees", startDegrees, -180f, 180f);
            sweepDegrees = EditorGUILayout.Slider("Sweep degrees", sweepDegrees, -270f, 270f);
            framesPerSecond = EditorGUILayout.Slider("Frames per second", framesPerSecond, 4f, 90f);

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Player.prefab", EditorStyles.boldLabel);
            slashEffectOffset = EditorGUILayout.Vector2Field("Slash effect offset", slashEffectOffset);
            slashEffectDelay = EditorGUILayout.Slider("Slash effect delay", slashEffectDelay, 0f, 0.6f);

            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField(
                string.Format("effect {0} frames, {1:F3}s   swing {2:F3}s   timeline {3:F3}s",
                    effectFrames == null ? 0 : effectFrames.Length, EffectDuration, clipLength, total),
                EditorStyles.miniLabel);

            if (!string.IsNullOrEmpty(status)) EditorGUILayout.HelpBox(status, MessageType.Info);

            EditorGUILayout.Space(4f);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Revert")) Reload();
            GUI.backgroundColor = new Color(0.6f, 1f, 0.6f);
            if (GUILayout.Button("Apply to prefabs")) Apply();
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
        }

        void Apply()
        {
            GameObject slashGo = AssetDatabase.LoadAssetAtPath<GameObject>(SlashPrefabPath);
            GameObject playerGo = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (slashGo == null || playerGo == null) { status = "Prefab missing, nothing written."; return; }

            SerializedObject fx = new SerializedObject(slashGo.GetComponent<SpriteSequenceEffect>());
            fx.FindProperty("sizeMultiplier").floatValue = sizeMultiplier;
            fx.FindProperty("startDegrees").floatValue = startDegrees;
            fx.FindProperty("sweepDegrees").floatValue = sweepDegrees;
            fx.FindProperty("framesPerSecond").floatValue = framesPerSecond;
            fx.ApplyModifiedProperties();

            SerializedObject pc = new SerializedObject(playerGo.GetComponent<PlayerController>());
            pc.FindProperty("slashEffectOffset").vector2Value = slashEffectOffset;
            pc.FindProperty("slashEffectDelay").floatValue = slashEffectDelay;
            pc.ApplyModifiedProperties();

            PrefabUtility.SavePrefabAsset(slashGo);
            PrefabUtility.SavePrefabAsset(playerGo);
            AssetDatabase.SaveAssets();

            status = string.Format("Applied at {0:HH:mm:ss}.", System.DateTime.Now);
            Debug.Log("Slash tuner: wrote SlashEffect.prefab and Player.prefab.");
        }
    }
}
