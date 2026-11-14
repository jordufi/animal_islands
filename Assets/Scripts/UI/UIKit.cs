using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// Small helper to build uGUI layouts from code (menus, HUD, card choice). Placeholder look:
// flat panels and legacy text, so the art pass can later replace it without touching game logic.
public static class UIKit
{
    static Font font;
    public static Font Font => font != null ? font : (font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));

    public static readonly Color PanelColor = new Color(0.05f, 0.07f, 0.1f, 0.85f);
    public static readonly Color Accent = new Color(1f, 0.72f, 0.2f);
    public static readonly Color ButtonColor = new Color(0.16f, 0.2f, 0.27f, 1f);

    public static Canvas CreateCanvas(string name, int sortingOrder)
    {
        var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.layer = 5;
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        return canvas;
    }

    public static void EnsureEventSystem()
    {
        if (Object.FindAnyObjectByType<EventSystem>() != null) return;
        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
    }

    public static RectTransform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = 5;
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
        return rt;
    }

    // Rect anchored at a single point (pivot = anchor) with a fixed size.
    public static RectTransform Anchored(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
    {
        var rt = Rect(name, parent, anchor, anchor, Vector2.zero, Vector2.zero);
        rt.pivot = anchor;
        rt.sizeDelta = size;
        rt.anchoredPosition = position;
        return rt;
    }

    public static RectTransform Stretch(string name, Transform parent) => Rect(name, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

    public static Image Image(RectTransform rt, Color color, bool raycast = false)
    {
        var img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = raycast;
        return img;
    }

    public static Text Text(RectTransform rt, string text, int size, TextAnchor anchor = TextAnchor.MiddleCenter, Color? color = null, bool shadow = true)
    {
        var t = rt.gameObject.AddComponent<Text>();
        t.font = Font;
        t.text = text;
        t.fontSize = size;
        t.alignment = anchor;
        t.color = color ?? Color.white;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        if (shadow) rt.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(2, -2);
        return t;
    }

    public static Text Label(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, string text, int fontSize, TextAnchor align = TextAnchor.MiddleCenter, Color? color = null)
        => Text(Anchored(name, parent, anchor, position, size), text, fontSize, align, color);

    public static Button Button(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, string label, UnityAction onClick, int fontSize = 32)
    {
        var rt = Anchored(name, parent, anchor, position, size);
        var img = Image(rt, Color.white, true);
        var button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = img;
        var colors = button.colors;
        colors.normalColor = ButtonColor;
        colors.highlightedColor = new Color(0.85f, 0.55f, 0.12f);
        colors.selectedColor = new Color(0.92f, 0.6f, 0.12f);
        colors.pressedColor = Accent;
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        if (onClick != null) button.onClick.AddListener(onClick);
        Text(Stretch("Label", rt), label, fontSize);
        return button;
    }

    public static Slider Slider(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, float min, float max, float value, UnityAction<float> onChanged)
    {
        var rt = Anchored(name, parent, anchor, position, size);
        var bg = Image(Stretch("Background", rt), new Color(0.12f, 0.14f, 0.18f), true);
        var fillArea = Rect("Fill Area", rt, Vector2.zero, Vector2.one, new Vector2(6, 6), new Vector2(-6, -6));
        var fill = Image(Stretch("Fill", fillArea), Accent);
        var handleArea = Rect("Handle Area", rt, Vector2.zero, Vector2.one, new Vector2(10, 0), new Vector2(-10, 0));
        var handleRt = Rect("Handle", handleArea, new Vector2(0, 0), new Vector2(0, 1), new Vector2(-10, -6), new Vector2(10, 6));
        var handle = Image(handleRt, Color.white, true);
        var slider = rt.gameObject.AddComponent<Slider>();
        slider.fillRect = (RectTransform)fill.transform;
        slider.handleRect = handleRt;
        slider.targetGraphic = handle;
        slider.minValue = min;
        slider.maxValue = max;
        slider.value = value;
        var colors = slider.colors;
        colors.selectedColor = Accent;
        colors.highlightedColor = Accent;
        slider.colors = colors;
        if (onChanged != null) slider.onValueChanged.AddListener(onChanged);
        bg.raycastTarget = true;
        return slider;
    }

    // Horizontal alpha ramp, for soft shading behind menus.
    public static Sprite GradientSprite(float alphaLeft, float alphaRight)
    {
        var tex = new Texture2D(256, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int x = 0; x < 256; x++) tex.SetPixel(x, 0, new Color(1, 1, 1, Mathf.Lerp(alphaLeft, alphaRight, Mathf.SmoothStep(0f, 1f, x / 255f))));
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 256, 1), new Vector2(0.5f, 0.5f));
    }

    public static void Select(Selectable s)
    {
        if (s == null || EventSystem.current == null) return;
        EventSystem.current.SetSelectedGameObject(null);
        EventSystem.current.SetSelectedGameObject(s.gameObject);
    }

    // Explicit vertical navigation between selectables (gamepad friendly).
    public static void ChainVertical(params Selectable[] items)
    {
        for (int i = 0; i < items.Length; i++)
        {
            var nav = new Navigation { mode = Navigation.Mode.Explicit };
            nav.selectOnUp = items[(i - 1 + items.Length) % items.Length];
            nav.selectOnDown = items[(i + 1) % items.Length];
            items[i].navigation = nav;
        }
    }

    public static void ChainHorizontal(params Selectable[] items)
    {
        for (int i = 0; i < items.Length; i++)
        {
            var nav = new Navigation { mode = Navigation.Mode.Explicit };
            nav.selectOnLeft = items[(i - 1 + items.Length) % items.Length];
            nav.selectOnRight = items[(i + 1) % items.Length];
            items[i].navigation = nav;
        }
    }
}
