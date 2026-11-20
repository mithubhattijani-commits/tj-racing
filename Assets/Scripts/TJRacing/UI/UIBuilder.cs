// TJ Racing 3D — programmatic UI builder (mockup palette, no binary assets).
// All HUD/menu/results UI is built in code so the .unity scenes stay tiny and the
// whole project remains text-only. Palette from the mockups:
//   navy bg #0E0E2A, pink #FF2E88, cyan #19C2FF, gold #FFC93C, green #22C55E,
//   red brake #D92B2B, panel black 80% alpha, white text.
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

namespace TJRacing
{
    public static class UIBuilder
    {
        public static readonly Color Navy = new Color(0.055f, 0.055f, 0.16f);
        public static readonly Color Pink = new Color(1f, 0.18f, 0.53f);
        public static readonly Color Cyan = new Color(0.10f, 0.76f, 1f);
        public static readonly Color Gold = new Color(1f, 0.79f, 0.24f);
        public static readonly Color Green = new Color(0.13f, 0.77f, 0.37f);
        public static readonly Color Red = new Color(0.85f, 0.17f, 0.17f);
        public static readonly Color Panel = new Color(0f, 0f, 0f, 0.8f);
        public static readonly Color PanelSoft = new Color(0f, 0f, 0f, 0.55f);

        // Default font: Arial.ttf was removed as a built-in resource in
        // Unity 2022.3 — Resources.GetBuiltinResource<Font>("Arial.ttf")
        // throws ArgumentException in the Editor (and aborts whatever
        // UI was being built). The built-in font is now
        // "LegacyRuntime.ttf" (the name the Arial error itself names);
        // cached once.
        private static Font _defaultFont;
        public static Font DefaultFont
        {
            get
            {
                if (_defaultFont == null)
                    _defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return _defaultFont;
            }
        }

        public static Canvas MakeCanvas(GameObject parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        public static RectTransform MakePanel(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax,
            Color color)
        {
            var go = new GameObject(name);
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            var img = go.AddComponent<Image>();
            img.color = color;
            return rt;
        }

        public static Text Label(Transform parent, string name, string text, int fontSize,
            Color color, TextAnchor align, FontStyle style = FontStyle.Normal)
        {
            var go = new GameObject(name);
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.text = text;
            t.font = DefaultFont;
            t.fontSize = fontSize;
            t.color = color;
            t.alignment = align;
            t.fontStyle = style;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        public static Button MakeButton(Transform parent, string name, string label,
            int fontSize, Color bg, UnityAction onClick)
        {
            var go = new GameObject(name);
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = bg;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.pressedColor = bg * 0.75f;
            btn.colors = colors;
            if (onClick != null) btn.onClick.AddListener(() =>
            {
                // Every tap button clicks (menu, results, pause, splash
                // START). Hold buttons pass null onClick — no click.
                AudioManager.Instance?.PlayClick();
                onClick.Invoke();
            });
            var txt = new GameObject("Label");
            txt.transform.SetParent(go.transform, false);
            var trt = txt.AddComponent<RectTransform>();
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
            var t = txt.AddComponent<Text>();
            t.text = label;
            t.font = DefaultFont;
            t.fontSize = fontSize;
            t.color = Color.white;
            t.alignment = TextAnchor.MiddleCenter;
            t.fontStyle = FontStyle.Bold;
            return btn;
        }

        /// <summary>Hold-to-press button (steer / brake / nitro): down/up events.</summary>
        public static Button MakeHoldButton(Transform parent, string name, string label,
            int fontSize, Color bg, UnityAction<bool> onHold)
        {
            var btn = MakeButton(parent, name, label, fontSize, bg, null);
            var trigger = btn.gameObject.AddComponent<HoldButtonTrigger>();
            trigger.OnHold = onHold;
            return btn;
        }

        public static Slider MakeSlider(Transform parent, string name, float min, float max, float value)
        {
            var go = new GameObject(name);
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            var slider = go.AddComponent<Slider>();
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = value;
            var bg = new GameObject("Background");
            bg.transform.SetParent(go.transform, false);
            var brt = bg.AddComponent<RectTransform>();
            brt.anchorMin = new Vector2(0f, 0.4f); brt.anchorMax = new Vector2(1f, 0.6f);
            brt.offsetMin = Vector2.zero; brt.offsetMax = Vector2.zero;
            var bimg = bg.AddComponent<Image>();
            bimg.color = new Color(1f, 1f, 1f, 0.25f);
            slider.targetGraphic = bimg;
            var fillArea = new GameObject("Fill Area");
            fillArea.transform.SetParent(go.transform, false);
            var fart = fillArea.AddComponent<RectTransform>();
            fart.anchorMin = new Vector2(0f, 0.4f); fart.anchorMax = new Vector2(1f, 0.6f);
            fart.offsetMin = Vector2.zero; fart.offsetMax = Vector2.zero;
            var fill = new GameObject("Fill");
            fill.transform.SetParent(fillArea.transform, false);
            var frt = fill.AddComponent<RectTransform>();
            frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one;
            var fimg = fill.AddComponent<Image>();
            fimg.color = Cyan;
            slider.fillRect = frt;
            var handleArea = new GameObject("Handle Slide Area");
            handleArea.transform.SetParent(go.transform, false);
            var hart = handleArea.AddComponent<RectTransform>();
            hart.anchorMin = Vector2.zero; hart.anchorMax = Vector2.one;
            hart.offsetMin = new Vector2(10f, 0f); hart.offsetMax = new Vector2(-10f, 0f);
            var handle = new GameObject("Handle");
            handle.transform.SetParent(handleArea.transform, false);
            var hrt = handle.AddComponent<RectTransform>();
            hrt.sizeDelta = new Vector2(44f, 44f);
            var himg = handle.AddComponent<Image>();
            himg.color = Color.white;
            slider.handleRect = hrt;
            return slider;
        }

        public static void Place(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
        }
    }

    /// <summary>Pointer-down/up forwarder for hold buttons (no EventTrigger setup in code).</summary>
    public class HoldButtonTrigger : MonoBehaviour, UnityEngine.EventSystems.IPointerDownHandler,
        UnityEngine.EventSystems.IPointerUpHandler, UnityEngine.EventSystems.IPointerExitHandler
    {
        public UnityAction<bool> OnHold;

        public void OnPointerDown(UnityEngine.EventSystems.PointerEventData e) { OnHold?.Invoke(true); }
        public void OnPointerUp(UnityEngine.EventSystems.PointerEventData e) { OnHold?.Invoke(false); }
        public void OnPointerExit(UnityEngine.EventSystems.PointerEventData e) { OnHold?.Invoke(false); }
    }
}
