using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class IntroCutsceneController : MonoBehaviour
{
    public const string CutsceneSceneName = "IntroCutscene";

    private const string EventSystemObjectName = "CutsceneEventSystem";
    private const string CanvasObjectName = "CutsceneCanvas";
    private const string SkipButtonObjectName = "SkipButton";
    private const string SkipButtonLabelObjectName = "Label";

    private static string pendingNextSceneName;

    [SerializeField] private string fallbackNextSceneName = "Zoo";
    [SerializeField] private string skipButtonLabel = "START";
    [SerializeField] private Color skipButtonColor = new Color(0.18f, 0.19f, 0.22f, 0.64f);
    [SerializeField] private Color skipButtonTextColor = new Color(0.97f, 0.98f, 1f, 1f);

    private Font builtinFont;
    private Canvas cutsceneCanvas;
    private Button skipButton;
    private bool isLoadingNextScene;

    public static void SetPendingNextScene(string sceneName)
    {
        pendingNextSceneName = sceneName;
    }

    private void Awake()
    {
        EnsureEventSystem();
        cutsceneCanvas = EnsureCanvas();
        skipButton = EnsureSkipButton(cutsceneCanvas.transform);
    }

    private void Update()
    {
        if (isLoadingNextScene)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Return)
            || Input.GetKeyDown(KeyCode.KeypadEnter)
            || Input.GetKeyDown(KeyCode.Space))
        {
            LoadNextScene();
        }
    }

    private void LoadNextScene()
    {
        if (isLoadingNextScene)
        {
            return;
        }

        string nextSceneName = string.IsNullOrWhiteSpace(pendingNextSceneName)
            ? fallbackNextSceneName
            : pendingNextSceneName;

        pendingNextSceneName = null;

        if (string.IsNullOrWhiteSpace(nextSceneName))
        {
            Debug.LogError("IntroCutsceneController does not have a destination scene configured.", this);
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(nextSceneName))
        {
            Debug.LogError($"Scene '{nextSceneName}' is not available in Build Settings.", this);
            return;
        }

        isLoadingNextScene = true;
        SceneFadeTransition.LoadScene(nextSceneName);
    }

    private void EnsureEventSystem()
    {
        if (EventSystem.current != null)
        {
            return;
        }

        GameObject eventSystemObject = new GameObject(EventSystemObjectName);
        eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<StandaloneInputModule>();
    }

    private Canvas EnsureCanvas()
    {
        GameObject canvasObject = GameObject.Find(CanvasObjectName);

        if (canvasObject != null && canvasObject.TryGetComponent(out Canvas existingCanvas))
        {
            return existingCanvas;
        }

        canvasObject = new GameObject(CanvasObjectName);

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 300;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        canvasObject.AddComponent<GraphicRaycaster>();

        return canvas;
    }

    private Button EnsureSkipButton(Transform parent)
    {
        GameObject existingButtonObject = GameObject.Find(SkipButtonObjectName);

        if (existingButtonObject != null && existingButtonObject.TryGetComponent(out Button existingButton))
        {
            existingButton.onClick.RemoveAllListeners();
            existingButton.onClick.AddListener(HandleSkipButtonPressed);
            return existingButton;
        }

        GameObject buttonObject = new GameObject(SkipButtonObjectName);
        buttonObject.transform.SetParent(parent, false);

        RectTransform rectTransform = buttonObject.AddComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(1f, 1f);
        rectTransform.anchorMax = new Vector2(1f, 1f);
        rectTransform.pivot = new Vector2(1f, 1f);
        rectTransform.anchoredPosition = new Vector2(-48f, -48f);
        rectTransform.sizeDelta = new Vector2(180f, 64f);

        Image buttonImage = buttonObject.AddComponent<Image>();
        buttonImage.color = skipButtonColor;

        Outline buttonOutline = buttonObject.AddComponent<Outline>();
        buttonOutline.effectColor = new Color(1f, 1f, 1f, 0.12f);
        buttonOutline.effectDistance = new Vector2(1.5f, -1.5f);

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = buttonImage;
        button.transition = Selectable.Transition.ColorTint;
        button.colors = CreateButtonColors(skipButtonColor);

        Navigation navigation = button.navigation;
        navigation.mode = Navigation.Mode.None;
        button.navigation = navigation;

        button.onClick.AddListener(HandleSkipButtonPressed);

        GameObject labelObject = new GameObject(SkipButtonLabelObjectName);
        labelObject.transform.SetParent(buttonObject.transform, false);

        RectTransform labelRectTransform = labelObject.AddComponent<RectTransform>();
        labelRectTransform.anchorMin = Vector2.zero;
        labelRectTransform.anchorMax = Vector2.one;
        labelRectTransform.pivot = new Vector2(0.5f, 0.5f);
        labelRectTransform.offsetMin = Vector2.zero;
        labelRectTransform.offsetMax = Vector2.zero;

        Text labelText = labelObject.AddComponent<Text>();
        labelText.font = GetBuiltinFont();
        labelText.fontSize = 26;
        labelText.alignment = TextAnchor.MiddleCenter;
        labelText.color = skipButtonTextColor;
        labelText.text = skipButtonLabel;
        labelText.raycastTarget = false;

        return button;
    }

    private void HandleSkipButtonPressed()
    {
        if (skipButton != null)
        {
            skipButton.interactable = false;
        }

        LoadNextScene();
    }

    private ColorBlock CreateButtonColors(Color normalColor)
    {
        ColorBlock colors = ColorBlock.defaultColorBlock;

        colors.normalColor = normalColor;
        colors.highlightedColor = new Color(
            Mathf.Lerp(normalColor.r, 1f, 0.12f),
            Mathf.Lerp(normalColor.g, 1f, 0.12f),
            Mathf.Lerp(normalColor.b, 1f, 0.12f),
            normalColor.a);

        colors.pressedColor = new Color(
            normalColor.r * 0.9f,
            normalColor.g * 0.9f,
            normalColor.b * 0.9f,
            normalColor.a);

        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(
            normalColor.r,
            normalColor.g,
            normalColor.b,
            normalColor.a * 0.45f);

        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;

        return colors;
    }

    private Font GetBuiltinFont()
    {
        if (builtinFont != null)
        {
            return builtinFont;
        }

        builtinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        if (builtinFont == null)
        {
            Debug.LogError("Could not load the built-in runtime font for the cutscene UI.", this);
        }

        return builtinFont;
    }
}