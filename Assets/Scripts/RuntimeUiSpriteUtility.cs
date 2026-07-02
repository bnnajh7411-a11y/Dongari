using UnityEngine;
using UnityEngine.UI;

public static class RuntimeUiSpriteUtility
{
    private static Sprite cachedWhiteSprite;

    public static Sprite GetWhiteSprite()
    {
        if (cachedWhiteSprite != null)
        {
            return cachedWhiteSprite;
        }

        Texture2D texture = Texture2D.whiteTexture;
        cachedWhiteSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            texture.width);
        cachedWhiteSprite.name = "RuntimeWhiteSprite";

        return cachedWhiteSprite;
    }
}

internal static class RuntimeGaugeUiUtility
{
    private const string BuiltinFontResourcePath = "LegacyRuntime.ttf";
    private const string FillObjectName = "Fill";
    private const float ReferenceWidth = 1920f;
    private const float ReferenceHeight = 1080f;
    private const float GaugeFillInset = 3f;

    private static Font cachedBuiltinFont;

    public static Font GetBuiltinFont()
    {
        if (cachedBuiltinFont == null)
        {
            cachedBuiltinFont = Resources.GetBuiltinResource<Font>(BuiltinFontResourcePath);
        }

        return cachedBuiltinFont;
    }

    public static Canvas FindCanvas(string canvasObjectName)
    {
        if (string.IsNullOrEmpty(canvasObjectName))
        {
            return null;
        }

        GameObject canvasObject = GameObject.Find(canvasObjectName);
        return canvasObject != null && canvasObject.TryGetComponent(out Canvas canvas)
            ? canvas
            : null;
    }

    public static Canvas GetOrCreateOverlayCanvas(
        Transform parent,
        string canvasObjectName,
        int sortingOrder,
        out bool createdCanvas)
    {
        return GetOrCreateOverlayCanvas(
            parent,
            canvasObjectName,
            canvasObjectName,
            sortingOrder,
            out createdCanvas);
    }

    public static Canvas GetOrCreateOverlayCanvas(
        Transform parent,
        string existingCanvasObjectName,
        string createdCanvasObjectName,
        int sortingOrder,
        out bool createdCanvas)
    {
        Canvas canvas = FindCanvas(existingCanvasObjectName);
        if (canvas == null
            && !string.IsNullOrEmpty(createdCanvasObjectName)
            && createdCanvasObjectName != existingCanvasObjectName)
        {
            canvas = FindCanvas(createdCanvasObjectName);
        }

        createdCanvas = false;
        if (canvas == null)
        {
            GameObject canvasObject = new GameObject(
                createdCanvasObjectName,
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler));

            if (parent != null)
            {
                canvasObject.transform.SetParent(parent, false);
            }

            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler canvasScaler = canvasObject.GetComponent<CanvasScaler>();
            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
            canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            canvasScaler.matchWidthOrHeight = 0.5f;
            createdCanvas = true;
        }

        if (canvas != null)
        {
            canvas.sortingOrder = sortingOrder;
        }

        return canvas;
    }

    public static RectTransform GetOrCreateGaugeFillRect(
        Canvas canvas,
        string gaugeObjectName,
        string labelObjectName,
        Vector2 gaugeSize,
        Vector2 gaugeAnchoredPosition,
        bool showBackgroundFrame,
        Color backgroundColor,
        Color fillColor,
        string labelText,
        Vector2 labelOffset,
        Vector2 labelSize,
        int labelFontSize,
        Color labelColor,
        out float fillBaseWidth,
        out float fillHeight)
    {
        fillBaseWidth = Mathf.Max(0f, gaugeSize.x - (GaugeFillInset * 2f));
        fillHeight = Mathf.Max(0f, gaugeSize.y - (GaugeFillInset * 2f));

        if (canvas == null)
        {
            return null;
        }

        RectTransform gaugeRectTransform = GetOrCreateRectTransform(canvas.transform, gaugeObjectName);
        gaugeRectTransform.anchorMin = new Vector2(0f, 1f);
        gaugeRectTransform.anchorMax = new Vector2(0f, 1f);
        gaugeRectTransform.pivot = new Vector2(0f, 1f);
        gaugeRectTransform.sizeDelta = gaugeSize;
        gaugeRectTransform.anchoredPosition = gaugeAnchoredPosition;

        Image backgroundImage = GetOrAddComponent<Image>(gaugeRectTransform.gameObject);
        backgroundImage.sprite = RuntimeUiSpriteUtility.GetWhiteSprite();
        backgroundImage.type = Image.Type.Simple;
        backgroundImage.color = showBackgroundFrame ? backgroundColor : Color.clear;
        backgroundImage.enabled = showBackgroundFrame;
        backgroundImage.raycastTarget = false;

        RectTransform fillRectTransform = GetOrCreateRectTransform(gaugeRectTransform, FillObjectName);
        fillRectTransform.anchorMin = new Vector2(0f, 1f);
        fillRectTransform.anchorMax = new Vector2(0f, 1f);
        fillRectTransform.pivot = new Vector2(0f, 1f);
        fillRectTransform.anchoredPosition = new Vector2(GaugeFillInset, -GaugeFillInset);
        fillRectTransform.sizeDelta = new Vector2(fillBaseWidth, fillHeight);

        Image fillImage = GetOrAddComponent<Image>(fillRectTransform.gameObject);
        fillImage.sprite = RuntimeUiSpriteUtility.GetWhiteSprite();
        fillImage.type = Image.Type.Simple;
        fillImage.color = fillColor;
        fillImage.raycastTarget = false;

        ConfigureGaugeLabel(
            gaugeRectTransform,
            labelObjectName,
            labelText,
            labelOffset,
            labelSize,
            labelFontSize,
            labelColor);

        return fillRectTransform;
    }

    private static void ConfigureGaugeLabel(
        RectTransform parent,
        string labelObjectName,
        string labelText,
        Vector2 labelOffset,
        Vector2 labelSize,
        int labelFontSize,
        Color labelColor)
    {
        RectTransform labelRectTransform = GetOrCreateRectTransform(parent, labelObjectName);
        labelRectTransform.anchorMin = new Vector2(1f, 0.5f);
        labelRectTransform.anchorMax = new Vector2(1f, 0.5f);
        labelRectTransform.pivot = new Vector2(0f, 0.5f);
        labelRectTransform.anchoredPosition = labelOffset;
        labelRectTransform.sizeDelta = labelSize;

        Text label = GetOrAddComponent<Text>(labelRectTransform.gameObject);
        label.font = GetBuiltinFont();
        label.fontSize = labelFontSize;
        label.color = labelColor;
        label.alignment = TextAnchor.MiddleLeft;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        label.raycastTarget = false;
        label.text = labelText;
    }

    private static RectTransform GetOrCreateRectTransform(Transform parent, string objectName)
    {
        Transform existingTransform = parent.Find(objectName);
        if (existingTransform != null)
        {
            return existingTransform as RectTransform ?? existingTransform.GetComponent<RectTransform>();
        }

        GameObject uiObject = new GameObject(objectName, typeof(RectTransform));
        uiObject.transform.SetParent(parent, false);
        return uiObject.GetComponent<RectTransform>();
    }

    private static T GetOrAddComponent<T>(GameObject gameObject) where T : Component
    {
        if (!gameObject.TryGetComponent(out T component))
        {
            component = gameObject.AddComponent<T>();
        }

        return component;
    }
}
