using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class IntroCutsceneController : MonoBehaviour
{
    public const string CutsceneSceneName = "IntroCutscene";

    private const string EventSystemObjectName = "CutsceneEventSystem";
    private const string CanvasObjectName = "CutsceneCanvas";
    private const string ImageObjectName = "CutsceneImage";
    private const string PromptObjectName = "CutscenePrompt";
    private const string SkipButtonObjectName = "SkipButton";
    private const string SkipButtonLabelObjectName = "Label";

    private static string pendingNextSceneName;

    [SerializeField] private string fallbackNextSceneName = "Zoo";
    [SerializeField] private string continuePrompt = "Click or press Enter to continue";
    [SerializeField] private string finalPrompt = "Click or press Enter to start";
    [SerializeField] private string skipButtonLabel = "SKIP";
    [SerializeField] private Color backgroundColor = Color.black;
    [SerializeField] private Color promptTextColor = new Color(1f, 1f, 1f, 0.9f);
    [SerializeField] private Color skipButtonColor = new Color(0f, 0f, 0f, 0.72f);
    [SerializeField] private Color skipButtonTextColor = Color.white;
    [SerializeField] private Sprite[] pages;

    private Font builtinFont;
    private Image cutsceneImage;
    private Text promptText;
    private Button skipButton;
    private int currentPageIndex;
    private bool isLoadingNextScene;

    public static void SetPendingNextScene(string sceneName)
    {
        pendingNextSceneName = sceneName;
    }

    private void Awake()
    {
        EnsureMainCamera();
        EnsureEventSystem();
        Canvas canvas = EnsureCanvas();
        cutsceneImage = EnsureCutsceneImage(canvas.transform);
        promptText = EnsurePromptText(canvas.transform);
        skipButton = EnsureSkipButton(canvas.transform);
    }

    private void Start()
    {
        if (pages == null || pages.Length == 0)
        {
            Debug.LogWarning("IntroCutsceneController does not have any cutscene pages configured, so it will continue immediately.", this);
            LoadNextScene();
            return;
        }

        currentPageIndex = 0;
        ShowPage(currentPageIndex);
    }

    private void Update()
    {
        if (isLoadingNextScene || !ShouldAdvanceCutscene())
        {
            return;
        }

        currentPageIndex++;
        if (currentPageIndex >= pages.Length)
        {
            LoadNextScene();
            return;
        }

        ShowPage(currentPageIndex);
    }

    private void ShowPage(int pageIndex)
    {
        if (cutsceneImage == null || promptText == null)
        {
            return;
        }

        cutsceneImage.sprite = pages[pageIndex];
        promptText.text = pageIndex == pages.Length - 1 ? finalPrompt : continuePrompt;
    }

    private bool ShouldAdvanceCutscene()
    {
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            return false;
        }

        return Input.GetMouseButtonDown(0)
            || Input.GetKeyDown(KeyCode.Return)
            || Input.GetKeyDown(KeyCode.KeypadEnter)
            || Input.GetKeyDown(KeyCode.Space)
            || PlayerInputBindings.WasJumpPressedThisFrame()
            || PlayerInputBindings.WasInteractPressedThisFrame();
    }

    private void LoadNextScene()
    {
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

    private void EnsureMainCamera()
    {
        if (Camera.main != null)
        {
            Camera.main.backgroundColor = backgroundColor;
            Camera.main.clearFlags = CameraClearFlags.SolidColor;
            Camera.main.orthographic = true;
            return;
        }

        GameObject cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";

        Camera cameraComponent = cameraObject.AddComponent<Camera>();
        cameraObject.AddComponent<AudioListener>();

        cameraComponent.backgroundColor = backgroundColor;
        cameraComponent.clearFlags = CameraClearFlags.SolidColor;
        cameraComponent.orthographic = true;
    }

    private Canvas EnsureCanvas()
    {
        GameObject canvasObject = new GameObject(CanvasObjectName);

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

    private Image EnsureCutsceneImage(Transform parent)
    {
        GameObject imageObject = new GameObject(ImageObjectName);
        imageObject.transform.SetParent(parent, false);

        RectTransform rectTransform = imageObject.AddComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.offsetMin = new Vector2(48f, 120f);
        rectTransform.offsetMax = new Vector2(-48f, -48f);

        Image image = imageObject.AddComponent<Image>();
        image.preserveAspect = true;
        image.color = Color.white;
        return image;
    }

    private Text EnsurePromptText(Transform parent)
    {
        GameObject textObject = new GameObject(PromptObjectName);
        textObject.transform.SetParent(parent, false);

        RectTransform rectTransform = textObject.AddComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0.5f, 0f);
        rectTransform.anchorMax = new Vector2(0.5f, 0f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = new Vector2(0f, 54f);
        rectTransform.sizeDelta = new Vector2(920f, 72f);

        Text text = textObject.AddComponent<Text>();
        text.font = GetBuiltinFont();
        text.fontSize = 28;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = promptTextColor;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    private Button EnsureSkipButton(Transform parent)
    {
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
        colors.highlightedColor = Color.Lerp(normalColor, Color.white, 0.12f);
        colors.pressedColor = Color.Lerp(normalColor, Color.black, 0.16f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(normalColor.r, normalColor.g, normalColor.b, 0.35f);
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
