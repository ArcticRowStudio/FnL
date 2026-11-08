using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Platformer.Mechanics.Boss
{
    /// <summary>
    /// The boss's name across the middle of the screen as it arrives: a dark band opens out, the
    /// title slides in over a thin accent rule, holds, then everything fades. Builds its own
    /// overlay canvas and removes it when done.
    /// </summary>
    public class BossNameCard : MonoBehaviour
    {
        RectTransform band;
        Image bandImage, ruleTop, ruleBottom;
        Text title, subtitle;
        Color accent;

        public static void Show(string titleText, string subtitleText, Color accentColor, float hold = 2.2f)
        {
            var go = new GameObject("BossNameCard");
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(960f, 540f);

            var card = go.AddComponent<BossNameCard>();
            card.accent = accentColor;
            card.Build(titleText, subtitleText);
            card.StartCoroutine(card.Play(hold));
        }

        void Build(string titleText, string subtitleText)
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            band = NewRect("Band", transform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 70f));
            band.anchoredPosition = new Vector2(0f, 40f);
            bandImage = band.gameObject.AddComponent<Image>();
            bandImage.color = new Color(0.02f, 0.03f, 0.05f, 0f);

            ruleTop = NewRule("RuleTop", 35f);
            ruleBottom = NewRule("RuleBottom", -35f);

            title = NewText("Title", font, titleText, 34, new Vector2(0f, 8f));
            subtitle = NewText("Subtitle", font, subtitleText, 13, new Vector2(0f, -20f));
        }

        Image NewRule(string name, float y)
        {
            var rt = NewRect(name, band, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 2f));
            rt.anchoredPosition = new Vector2(0f, y);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = accent;
            return img;
        }

        Text NewText(string name, Font font, string text, int size, Vector2 pos)
        {
            var rt = NewRect(name, band, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(900f, size + 10f));
            rt.anchoredPosition = pos;
            var t = rt.gameObject.AddComponent<Text>();
            t.font = font;
            t.text = text;
            t.fontSize = size;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = new Color(1f, 1f, 1f, 0f);
            return t;
        }

        static RectTransform NewRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.sizeDelta = size;
            return rt;
        }

        IEnumerator Play(float hold)
        {
            // Open: the band fades up and the accent rules draw outward from the centre.
            float t = 0f;
            while (t < 0.35f)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.SmoothStep(0f, 1f, t / 0.35f);
                bandImage.color = new Color(0.02f, 0.03f, 0.05f, 0.72f * u);
                SetRuleWidth(620f * u);
                yield return null;
            }

            // Title slides in from the right, subtitle follows.
            t = 0f;
            while (t < 0.4f)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.SmoothStep(0f, 1f, t / 0.4f);
                title.color = new Color(1f, 1f, 1f, u);
                title.rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(80f, 0f, u), 8f);
                subtitle.color = new Color(accent.r, accent.g, accent.b, Mathf.Clamp01(u * 1.4f - 0.4f));
                yield return null;
            }

            yield return new WaitForSecondsRealtime(hold);

            t = 0f;
            while (t < 0.45f)
            {
                t += Time.unscaledDeltaTime;
                float a = 1f - t / 0.45f;
                bandImage.color = new Color(0.02f, 0.03f, 0.05f, 0.72f * a);
                title.color = new Color(1f, 1f, 1f, a);
                subtitle.color = new Color(accent.r, accent.g, accent.b, a);
                ruleTop.color = ruleBottom.color = new Color(accent.r, accent.g, accent.b, a);
                yield return null;
            }
            Destroy(gameObject);
        }

        void SetRuleWidth(float w)
        {
            ruleTop.rectTransform.sizeDelta = new Vector2(w, 2f);
            ruleBottom.rectTransform.sizeDelta = new Vector2(w, 2f);
        }
    }
}
