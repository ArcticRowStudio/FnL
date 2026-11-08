using Platformer.Core;
using Platformer.Model;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Platformer.UI
{
    /// <summary>
    /// Title screen, pause menu and controls page. Builds its own overlay canvas at runtime, like
    /// the rest of the HUD. The title is shown over the frozen game at scene load; Esc pauses.
    /// While any of these screens is open the game is frozen (time scale held at 0) and the
    /// player's input is off.
    /// </summary>
    public class GameMenuController : MonoBehaviour
    {
        [Header("Title")]
        public string gameTitle = "FnL";
        [Tooltip("Small line under the title. Leave empty to hide it.")]
        public string subtitle = "";
        public bool showTitleOnStart = true;
        [Tooltip("Skip the title when entering play mode in the editor, for quicker testing")]
        public bool skipTitleInEditor = false;

        [Header("Look")]
        public Color accent = new Color(1f, 0.45f, 0.1f, 1f);
        public Color panelColor = new Color(0.04f, 0.05f, 0.07f, 1f);

        /// <summary>True while the title, pause or controls screen is up.</summary>
        public static bool IsOpen => current != null && current.open;

        // State lives on the instance: domain reload is off here, and a static flag drifted out of
        // step with the screen across play sessions.
        static GameMenuController current;
        bool open;

        enum Page { None, Title, Pause, Controls, DemoEnd, BossSelect }

        static readonly string[,] ControlRows =
        {
            { "MOVE", "A / D" },
            { "CLIMB", "W / S" },
            { "JUMP", "SPACE" },
            { "SWORD", "LEFT CLICK" },
            { "AIM + SHOOT", "HOLD CTRL + LEFT CLICK" },
            { "RELOAD", "R" },
            { "WARP", "LEFT SHIFT" },
            { "GUARD / PARRY", "RIGHT CLICK  or  Q" },
            { "GRAB ROPE", "F" },
            { "INTERACT", "E" },
            { "PAUSE", "ESC" },
        };

        Page page = Page.None;
        Page controlsReturnTo = Page.Pause;
        GameObject canvasRoot, overlay, titlePage, pausePage, controlsPage, demoEndPage, bossSelectPage;
        Image overlayImage;
        Button titleFirst, pauseFirst, controlsFirst, demoEndFirst, bossSelectFirst;
        Platformer.Mechanics.DemoBossSelect bossSelect;
        InputAction menuAction;
        bool savedControl = true;
        bool closePending;
        Coroutine demoEndRoutine;

        void Awake()
        {
            current = this;
            open = false;
            closePending = false;
            demoEndRoutine = null;
            if (canvasRoot != null) Destroy(canvasRoot);   // left over from an earlier session
            bossSelect = FindAnyObjectByType<Platformer.Mechanics.DemoBossSelect>();
            if (bossSelect != null && !bossSelect.showInTitle) bossSelect = null;
            BuildUI();
            menuAction = InputSystem.actions != null ? InputSystem.actions.FindAction("Player/Menu") : null;
            // The player enables only the actions it reads itself, so Menu starts out disabled.
            menuAction?.Enable();
        }

        void Start()
        {
            bool skip = Application.isEditor && skipTitleInEditor;
            if (showTitleOnStart && !skip) Open(Page.Title);
            else ShowPage(Page.None);
        }

        void OnDestroy()
        {
            if (open) { open = false; Time.timeScale = 1f; AudioListener.pause = false; }
            if (current == this) current = null;
            if (canvasRoot != null) Destroy(canvasRoot);
        }

        void Update()
        {
            // Hand control back a frame after the click, so the click that closed the menu
            // does not also swing the sword.
            if (closePending) { closePending = false; FinishClose(); return; }

            if (MenuPressed())
            {
                switch (page)
                {
                    case Page.None: Open(Page.Pause); break;
                    case Page.Pause: Resume(); break;
                    case Page.Controls: Back(); break;
                    case Page.BossSelect: ShowPage(Page.Title); break;
                }
            }
        }

        void LateUpdate()
        {
            if (!open) return;
            // Hit-stop and dodge slow-motion restore time scale on their own timers; hold it here.
            Time.timeScale = 0f;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        bool MenuPressed()
        {
            if (menuAction != null && menuAction.WasPressedThisFrame()) return true;
            return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
        }

        #region Flow

        void Open(Page target)
        {
            if (!open)
            {
                open = true;
                var player = Simulation.GetModel<PlatformerModel>().player;
                if (player != null) { savedControl = player.controlEnabled; player.controlEnabled = false; }
                Time.timeScale = 0f;
            }
            // The title and the demo-end card keep the music going; the pause menu holds everything.
            AudioListener.pause = target == Page.Pause;
            ShowPage(target);
        }

        /// <summary>
        /// Shows the END OF DEMO card after <paramref name="delay"/> real seconds (the boss's
        /// defeat plays out first). Wired to VtolBossAI.onEscaped in the scene.
        /// </summary>
        public void ShowDemoEndAfter(float delay)
        {
            if (demoEndRoutine != null || page == Page.DemoEnd) return;
            demoEndRoutine = StartCoroutine(DemoEndRoutine(delay));
        }

        System.Collections.IEnumerator DemoEndRoutine(float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            demoEndRoutine = null;
            closePending = false;
            Open(Page.DemoEnd);
        }

        /// <summary>Opens the pause menu, unless a menu screen is already up.</summary>
        public void Pause()
        {
            if (page == Page.None && !closePending) Open(Page.Pause);
        }

        public void StartGame() => Close();
        public void Resume() => Close();

        void Close()
        {
            ShowPage(Page.None);
            closePending = true;
        }

        void FinishClose()
        {
            open = false;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            var player = Simulation.GetModel<PlatformerModel>().player;
            if (player != null) player.controlEnabled = savedControl;
        }

        public void ShowControls()
        {
            controlsReturnTo = page == Page.Title ? Page.Title : Page.Pause;
            ShowPage(Page.Controls);
        }

        public void Back() => ShowPage(controlsReturnTo);

        public void QuitToTitle()
        {
            open = false;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            Simulation.Clear();
            var scene = SceneManager.GetActiveScene();
            SceneManager.LoadScene(scene.buildIndex >= 0 ? scene.buildIndex : 0);
        }

        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void ShowPage(Page target)
        {
            page = target;
            overlay.SetActive(target != Page.None);
            titlePage.SetActive(target == Page.Title);
            pausePage.SetActive(target == Page.Pause);
            controlsPage.SetActive(target == Page.Controls);
            demoEndPage.SetActive(target == Page.DemoEnd);
            bossSelectPage.SetActive(target == Page.BossSelect);
            overlayImage.color = new Color(0f, 0f, 0f, target == Page.Title ? 0.6f : target == Page.DemoEnd ? 0.85f : 0.72f);

            Button first = target == Page.Title ? titleFirst : target == Page.Pause ? pauseFirst
                : target == Page.Controls ? controlsFirst : target == Page.DemoEnd ? demoEndFirst
                : target == Page.BossSelect ? bossSelectFirst : null;
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(first != null ? first.gameObject : null);
        }

        #endregion

        #region UI construction

        void BuildUI()
        {
            canvasRoot = new GameObject("GameMenuCanvas");
            var canvas = canvasRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasRoot.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasRoot.AddComponent<GraphicRaycaster>();

            overlay = NewRect("Overlay", canvasRoot.transform);
            Stretch(overlay);
            overlayImage = overlay.AddComponent<Image>();
            overlayImage.raycastTarget = true;   // swallow clicks meant for the game behind

            BuildTitle();
            BuildPause();
            BuildControls();
            BuildDemoEnd();
            BuildBossSelect();
        }

        void BuildDemoEnd()
        {
            demoEndPage = NewRect("DemoEnd", overlay.transform);
            Stretch(demoEndPage);

            var header = Label(demoEndPage.transform, "END OF DEMO", 96, FontStyles.Bold, Color.white);
            Place(header, new Vector2(0.5f, 0.5f), new Vector2(0f, 200f), new Vector2(1400f, 130f));
            header.characterSpacing = 10f;
            var bar = NewRect("AccentBar", demoEndPage.transform);
            bar.AddComponent<Image>().color = accent;
            Place(bar, new Vector2(0.5f, 0.5f), new Vector2(0f, 118f), new Vector2(220f, 6f));
            var sub = Label(demoEndPage.transform, "THANK YOU FOR PLAYING " + gameTitle, 30, FontStyles.Normal, new Color(1f, 1f, 1f, 0.75f));
            Place(sub, new Vector2(0.5f, 0.5f), new Vector2(0f, 70f), new Vector2(1400f, 44f));
            sub.characterSpacing = 8f;

            var list = ButtonColumn(demoEndPage.transform, new Vector2(0f, -120f));
            demoEndFirst = MenuButton(list, "QUIT TO TITLE", QuitToTitle);
            MenuButton(list, "QUIT GAME", QuitGame);
        }

        void BuildTitle()
        {
            titlePage = NewRect("Title", overlay.transform);
            Stretch(titlePage);

            var title = Label(titlePage.transform, gameTitle, 170, FontStyles.Bold, Color.white);
            Place(title, new Vector2(0.5f, 0.5f), new Vector2(0f, 230f), new Vector2(1200f, 220f));
            title.characterSpacing = 6f;

            var bar = NewRect("AccentBar", titlePage.transform);
            bar.AddComponent<Image>().color = accent;
            Place(bar, new Vector2(0.5f, 0.5f), new Vector2(0f, 110f), new Vector2(220f, 6f));

            if (!string.IsNullOrEmpty(subtitle))
            {
                var sub = Label(titlePage.transform, subtitle.ToUpperInvariant(), 26, FontStyles.Normal, new Color(1f, 1f, 1f, 0.7f));
                Place(sub, new Vector2(0.5f, 0.5f), new Vector2(0f, 70f), new Vector2(1200f, 40f));
                sub.characterSpacing = 8f;
            }

            var list = ButtonColumn(titlePage.transform, new Vector2(0f, -110f));
            titleFirst = MenuButton(list, "START", StartGame);
            if (bossSelect != null) MenuButton(list, "BOSS SELECT", () => ShowPage(Page.BossSelect));
            MenuButton(list, "CONTROLS", ShowControls);
            MenuButton(list, "QUIT", QuitGame);
        }

        void BuildBossSelect()
        {
            bossSelectPage = NewRect("BossSelect", overlay.transform);
            Stretch(bossSelectPage);
            var header = Label(bossSelectPage.transform, "BOSS SELECT", 64, FontStyles.Bold, Color.white);
            Place(header, new Vector2(0.5f, 0.5f), new Vector2(0f, 230f), new Vector2(900f, 90f));
            header.characterSpacing = 10f;
            var bar = NewRect("AccentBar", bossSelectPage.transform);
            bar.AddComponent<Image>().color = accent;
            Place(bar, new Vector2(0.5f, 0.5f), new Vector2(0f, 175f), new Vector2(140f, 5f));

            var list = ButtonColumn(bossSelectPage.transform, new Vector2(0f, -60f));
            Button first = null;
            if (bossSelect != null && bossSelect.HasQuadpod) first = first ?? MenuButton(list, "QUADPOD", () => JumpTo(bossSelect.JumpToQuadpod));
            if (bossSelect != null && bossSelect.HasLour) { var b = MenuButton(list, "LOUR", () => JumpTo(bossSelect.JumpToLour)); first = first ?? b; }
            if (bossSelect != null && bossSelect.HasVtol) { var b = MenuButton(list, "VTOL", () => JumpTo(bossSelect.JumpToVtol)); first = first ?? b; }
            var back = MenuButton(list, "BACK", () => ShowPage(Page.Title));
            bossSelectFirst = first ?? back;
        }

        void JumpTo(System.Action jump)
        {
            jump();
            Close();
        }

        void BuildPause()
        {
            pausePage = NewRect("Pause", overlay.transform);
            Stretch(pausePage);

            var header = Label(pausePage.transform, "PAUSED", 72, FontStyles.Bold, Color.white);
            Place(header, new Vector2(0.5f, 0.5f), new Vector2(0f, 230f), new Vector2(900f, 100f));
            header.characterSpacing = 10f;
            var bar = NewRect("AccentBar", pausePage.transform);
            bar.AddComponent<Image>().color = accent;
            Place(bar, new Vector2(0.5f, 0.5f), new Vector2(0f, 168f), new Vector2(140f, 5f));

            var list = ButtonColumn(pausePage.transform, new Vector2(0f, -40f));
            pauseFirst = MenuButton(list, "RESUME", Resume);
            MenuButton(list, "CONTROLS", ShowControls);
            MenuButton(list, "QUIT TO TITLE", QuitToTitle);
            MenuButton(list, "QUIT GAME", QuitGame);
        }

        void BuildControls()
        {
            controlsPage = NewRect("Controls", overlay.transform);
            Stretch(controlsPage);

            var header = Label(controlsPage.transform, "CONTROLS", 64, FontStyles.Bold, Color.white);
            Place(header, new Vector2(0.5f, 0.5f), new Vector2(0f, 400f), new Vector2(900f, 90f));
            header.characterSpacing = 10f;
            var bar = NewRect("AccentBar", controlsPage.transform);
            bar.AddComponent<Image>().color = accent;
            Place(bar, new Vector2(0.5f, 0.5f), new Vector2(0f, 345f), new Vector2(140f, 5f));

            var panel = NewRect("Panel", controlsPage.transform);
            var panelImg = panel.AddComponent<Image>();
            panelImg.color = new Color(panelColor.r, panelColor.g, panelColor.b, 0.85f);
            int rows = ControlRows.GetLength(0);
            float rowH = 50f;
            Place(panel, new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(900f, rows * rowH + 40f));

            for (int i = 0; i < rows; i++)
            {
                float y = (rows * rowH) * 0.5f - rowH * (i + 0.5f);
                var action = Label(panel.transform, ControlRows[i, 0], 28, FontStyles.Bold, new Color(1f, 1f, 1f, 0.6f));
                action.alignment = TextAlignmentOptions.MidlineRight;
                Place(action, new Vector2(0.5f, 0.5f), new Vector2(-170f, y), new Vector2(380f, rowH));
                action.characterSpacing = 4f;

                var key = Label(panel.transform, ControlRows[i, 1], 28, FontStyles.Bold, Color.white);
                key.alignment = TextAlignmentOptions.MidlineLeft;
                Place(key, new Vector2(0.5f, 0.5f), new Vector2(230f, y), new Vector2(380f, rowH));
            }

            // The column hangs from its top edge, 150 above its centre: keep it clear of the panel.
            var list = ButtonColumn(controlsPage.transform, new Vector2(0f, -470f));
            controlsFirst = MenuButton(list, "BACK", Back);
        }

        Transform ButtonColumn(Transform parent, Vector2 offset)
        {
            var col = NewRect("Buttons", parent);
            Place(col, new Vector2(0.5f, 0.5f), offset, new Vector2(420f, 300f));
            var layout = col.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 14f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandHeight = false;
            return col.transform;
        }

        Button MenuButton(Transform parent, string text, UnityEngine.Events.UnityAction onClick)
        {
            var go = NewRect(text + "_Button", parent);
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(420f, 62f);
            var img = go.AddComponent<Image>();
            img.color = Color.white;

            var btn = go.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = new Color(panelColor.r, panelColor.g, panelColor.b, 0.75f);
            colors.highlightedColor = accent;
            colors.selectedColor = accent;
            colors.pressedColor = accent * 0.8f;
            colors.disabledColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.06f;
            btn.colors = colors;
            btn.onClick.AddListener(onClick);

            var label = Label(go.transform, text, 30, FontStyles.Bold, Color.white);
            Stretch(label.gameObject);
            label.characterSpacing = 6f;
            label.raycastTarget = false;
            return btn;
        }

        static GameObject NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        static void Stretch(GameObject go)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        static void Place(Component c, Vector2 anchor, Vector2 pos, Vector2 size) => Place(c.gameObject, anchor, pos, size);

        static void Place(GameObject go, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        static TextMeshProUGUI Label(Transform parent, string text, float size, FontStyles style, Color color)
        {
            var go = NewRect("Label", parent);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.fontStyle = style;
            t.color = color;
            t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.raycastTarget = false;
            return t;
        }

        #endregion
    }
}
