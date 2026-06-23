using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class StartSceneController : MonoBehaviour
{
    private enum OptionsCategory
    {
        Audio,
        Resolution,
        KeySetup
    }

    private const string StartSceneName = "Start";
    private const string IntroCutsceneSceneName = "IntroCutscene";
    private const string EventSystemObjectName = "EventSystem";
    private const string CanvasObjectName = "StartCanvas";
    private const string PauseCanvasObjectName = "PauseMenuCanvas";
    private const string PauseMenuObjectName = "PauseMenuController";
    private const string PauseBackgroundOverlayObjectName = "PauseBackgroundOverlay";
    private const string MenuButtonsContainerObjectName = "MenuButtonsContainer";
    private const string ButtonObjectName = "StartButton";
    private const string PauseResumeButtonLabel = "계속하기";
    private const string ReconfigureButtonObjectName = "ReconfigureButton";
    private const string OptionButtonObjectName = "OptionButton";
    private const string ExitButtonObjectName = "ExitButton";
    private const string ButtonLabelObjectName = "Label";
    private const string KeyMappingPanelObjectName = "KeyMappingPanel";
    private const string KeyMappingWindowObjectName = "KeyMappingWindow";
    private const string OptionsPanelObjectName = "OptionsPanel";
    private const string OptionsWindowObjectName = "OptionsWindow";
    private const string KeyBindingScrollAreaObjectName = "KeyBindingScrollArea";
    private const string KeyBindingViewportObjectName = "KeyBindingViewport";
    private const string KeyBindingContentObjectName = "KeyBindingContent";
    private const string DisplayFullscreenPrefKey = "DisplaySettings.Fullscreen";
    private const string DisplayWidthPrefKey = "DisplaySettings.Width";
    private const string DisplayHeightPrefKey = "DisplaySettings.Height";
    private const string DefaultStatusText = "";
    private const float MenuButtonVerticalSpacing = 92f;
    private static readonly Vector2 KeyBindingScrollAreaSize = new Vector2(760f, 340f);
    private static readonly Vector2 KeyBindingScrollAreaPosition = new Vector2(0f, -24f);
    private static readonly Vector2 KeyBindingStatusPosition = new Vector2(0f, 28f);
    private static readonly Vector2 CancelButtonPosition = new Vector2(-150f, 96f);
    private static readonly Vector2 ConfirmButtonPosition = new Vector2(150f, 96f);
    private static readonly Vector2 OptionsWindowSize = new Vector2(980f, 880f);
    private static readonly Vector2 OptionsCloseButtonPosition = new Vector2(0f, 58f);
    private static readonly Vector2Int[] CommonResolutionOptions =
    {
        new Vector2Int(3840, 2160),
        new Vector2Int(2560, 1440),
        new Vector2Int(1920, 1200),
        new Vector2Int(1920, 1080),
        new Vector2Int(1680, 1050),
        new Vector2Int(1600, 900),
        new Vector2Int(1440, 900),
        new Vector2Int(1366, 768),
        new Vector2Int(1360, 768),
        new Vector2Int(1280, 800),
        new Vector2Int(1280, 720)
    };
    private const float KeyBindingRowSpacing = 68f;
    private const float KeyBindingTopPadding = 24f;
    private const float KeyBindingBottomPadding = 24f;
    private const float KeyBindingMinimumContentHeight = 340f;
    private const float ResolutionRowSpacing = 58f;
    private const float ResolutionTopPadding = 20f;
    private const float ResolutionBottomPadding = 20f;
    private const float ResolutionMinimumContentHeight = 220f;
    private const float OptionsCategoryContentLift = 64f;

    private static readonly KeyCode[] RebindableKeys =
    {
        KeyCode.A, KeyCode.B, KeyCode.C, KeyCode.D, KeyCode.E, KeyCode.F, KeyCode.G,
        KeyCode.H, KeyCode.I, KeyCode.J, KeyCode.K, KeyCode.L, KeyCode.M, KeyCode.N,
        KeyCode.O, KeyCode.P, KeyCode.Q, KeyCode.R, KeyCode.S, KeyCode.T, KeyCode.U,
        KeyCode.V, KeyCode.W, KeyCode.X, KeyCode.Y, KeyCode.Z,
        KeyCode.Alpha0, KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4,
        KeyCode.Alpha5, KeyCode.Alpha6, KeyCode.Alpha7, KeyCode.Alpha8, KeyCode.Alpha9,
        KeyCode.UpArrow, KeyCode.DownArrow, KeyCode.LeftArrow, KeyCode.RightArrow,
        KeyCode.Space, KeyCode.Return, KeyCode.Tab, KeyCode.Backspace,
        KeyCode.LeftShift, KeyCode.RightShift, KeyCode.LeftControl, KeyCode.RightControl,
        KeyCode.LeftAlt, KeyCode.RightAlt
    };

    [SerializeField] private string sceneToLoad = "Zoo";
    [SerializeField] private bool playIntroCutsceneBeforeFirstScene = true;
    [SerializeField] private string cutsceneSceneToLoad = IntroCutsceneSceneName;
    [SerializeField] private string buttonLabel = "시작";
    [SerializeField] private string reconfigureButtonLabel = "키 설정";
    [SerializeField] private string optionButtonLabel = "옵션";
    [SerializeField] private string exitButtonLabel = "종료";

    private readonly Dictionary<InputActionType, List<Text>> bindingValueTexts = new Dictionary<InputActionType, List<Text>>();
    private readonly List<Text> statusTexts = new List<Text>();
    private readonly List<Vector2Int> availableResolutionOptions = new List<Vector2Int>();
    private readonly List<Button> resolutionButtons = new List<Button>();

    private static bool isCreatingPauseMenuInstance;
    private static StartSceneController pauseMenuInstance;

    private Button startButton;
    private Button reconfigureButton;
    private Button optionButton;
    private Button exitButton;
    private Canvas rootCanvas;
    private Font builtinFont;
    private GameObject menuButtonsContainer;
    private GameObject pauseBackgroundOverlay;
    private GameObject keyMappingPanel;
    private GameObject optionsPanel;
    private GameObject optionsAudioContent;
    private GameObject optionsResolutionContent;
    private GameObject optionsKeySetupContent;
    private ScrollRect bindingScrollRect;
    private ScrollRect resolutionOptionsScrollRect;
    private ScrollRect optionsBindingScrollRect;
    private Slider backgroundMusicSlider;
    private Slider soundEffectSlider;
    private Text backgroundMusicValueText;
    private Text soundEffectValueText;
    private Button optionsAudioTabButton;
    private Button optionsResolutionTabButton;
    private Button optionsKeySetupTabButton;
    private Button fullscreenModeButton;
    private Image fullscreenModeCheckboxImage;
    private Image fullscreenModeCheckboxFillImage;
    private Button resolutionToggleButton;
    private Text resolutionToggleButtonText;
    private GameObject resolutionOptionsContainer;
    private bool isWaitingForBinding;
    private bool isRefreshingAudioControls;
    private bool shouldLoadSceneAfterConfirm;
    private bool isPauseMenu;
    private bool isResolutionListExpanded;
    private bool isFullscreenEnabled;
    private InputActionType pendingBindingAction;
    private OptionsCategory activeOptionsCategory = OptionsCategory.Audio;
    private int selectedResolutionIndex = -1;

    public static void EnsurePauseMenuInstance()
    {
        if (pauseMenuInstance != null || IsPauseMenuBlockedScene(SceneManager.GetActiveScene().name))
        {
            return;
        }

        isCreatingPauseMenuInstance = true;
        GameObject pauseMenuObject = new GameObject(PauseMenuObjectName);
        DontDestroyOnLoad(pauseMenuObject);
        pauseMenuInstance = pauseMenuObject.AddComponent<StartSceneController>();
        isCreatingPauseMenuInstance = false;
    }

    private void Awake()
    {
        isPauseMenu = isCreatingPauseMenuInstance;
        NormalizeMenuLabels();

        if (!isPauseMenu && SceneManager.GetActiveScene().name == StartSceneName)
        {
            PlayerHealth.ResetPersistentHealth();
            PlayerStamina.ResetPersistentStamina();
        }

        BuildAvailableResolutionOptions();
        ApplySavedDisplaySettings();
        EnsureEventSystem();
        rootCanvas = EnsureCanvas();
        if (isPauseMenu)
        {
            pauseBackgroundOverlay = EnsurePauseBackgroundOverlay(rootCanvas.transform);
            SetPauseBackgroundVisible(false);
        }

        menuButtonsContainer = EnsureMenuButtonsContainer(rootCanvas.transform);
        EnsureStartButton(menuButtonsContainer.transform);
        UpdatePrimaryButtonLabel();
        EnsureReconfigureButton(menuButtonsContainer.transform);
        EnsureOptionButton(menuButtonsContainer.transform);
        EnsureExitButton(menuButtonsContainer.transform);
        EnsureKeyMappingPanel(rootCanvas.transform);
        EnsureOptionsPanel(rootCanvas.transform);
        SetKeyMappingPanelVisible(false);
        SetOptionsPanelVisible(false);
        RefreshMenuButtons();
        RefreshBindingValueTexts();
        UpdateStatusText(DefaultStatusText);

        if (isPauseMenu)
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
            SetMenuButtonsVisible(false);
        }
    }

    private void OnDestroy()
    {
        if (pauseMenuInstance == this)
        {
            pauseMenuInstance = null;
        }

        if (isPauseMenu)
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            ResumeSceneActivity();
        }
    }

    private void Update()
    {
        if (isPauseMenu)
        {
            if (IsPauseMenuBlockedScene(SceneManager.GetActiveScene().name))
            {
                return;
            }

            if (isWaitingForBinding && Input.GetKeyDown(KeyCode.Escape))
            {
                CancelPendingRebind();
                return;
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (keyMappingPanel != null && keyMappingPanel.activeSelf)
                {
                    CloseKeyMappingPanel();
                    return;
                }

                if (optionsPanel != null && optionsPanel.activeSelf)
                {
                    CloseOptionsPanel();
                    return;
                }

                TogglePauseMenu();
                return;
            }
        }

        if (!isWaitingForBinding)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            CancelPendingRebind();
            return;
        }

        if (!TryGetPressedBindableKey(out KeyCode pressedKey))
        {
            return;
        }

        if (TryFindActionUsingKey(pressedKey, pendingBindingAction, out InputActionType duplicateAction))
        {
            UpdateStatusText($"{PlayerInputBindings.GetKeyDisplayName(pressedKey)} 키는 이미 {PlayerInputBindings.GetActionLabel(duplicateAction)}에 사용 중입니다.");
            return;
        }

        PlayerInputBindings.SetKey(pendingBindingAction, pressedKey);
        bool shouldAutoSaveOptionBindings = IsOptionsKeySetupVisible();
        if (shouldAutoSaveOptionBindings)
        {
            PlayerInputBindings.SaveAndMarkConfigured();
        }

        isWaitingForBinding = false;
        RefreshBindingValueTexts();
        string updateMessage = $"{PlayerInputBindings.GetActionLabel(pendingBindingAction)} 키가 {PlayerInputBindings.GetKeyDisplayName(pressedKey)}(으)로 설정되었습니다.";
        if (shouldAutoSaveOptionBindings)
        {
            updateMessage += " 변경 사항이 저장되었습니다.";
        }

        UpdateStatusText(updateMessage);
    }

    private void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() != null)
        {
            return;
        }

        new GameObject(EventSystemObjectName, typeof(EventSystem), typeof(StandaloneInputModule));
    }

    private Canvas EnsureCanvas()
    {
        string canvasObjectName = isPauseMenu ? PauseCanvasObjectName : CanvasObjectName;
        GameObject existingCanvasObject = GameObject.Find(canvasObjectName);
        if (existingCanvasObject != null && existingCanvasObject.TryGetComponent(out Canvas existingCanvas))
        {
            return existingCanvas;
        }

        GameObject canvasObject = new GameObject(
            canvasObjectName,
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = isPauseMenu ? 500 : 200;

        CanvasScaler canvasScaler = canvasObject.GetComponent<CanvasScaler>();
        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        canvasScaler.matchWidthOrHeight = 0.5f;

        return canvas;
    }

    private GameObject EnsurePauseBackgroundOverlay(Transform parent)
    {
        if (parent == null)
        {
            return null;
        }

        Transform existingOverlayTransform = parent.Find(PauseBackgroundOverlayObjectName);
        if (existingOverlayTransform != null)
        {
            existingOverlayTransform.SetAsFirstSibling();
            return existingOverlayTransform.gameObject;
        }

        GameObject overlayObject = new GameObject(
            PauseBackgroundOverlayObjectName,
            typeof(RectTransform),
            typeof(Image));
        overlayObject.transform.SetParent(parent, false);
        overlayObject.transform.SetAsFirstSibling();

        RectTransform overlayRectTransform = overlayObject.GetComponent<RectTransform>();
        overlayRectTransform.anchorMin = Vector2.zero;
        overlayRectTransform.anchorMax = Vector2.one;
        overlayRectTransform.offsetMin = Vector2.zero;
        overlayRectTransform.offsetMax = Vector2.zero;

        Image overlayImage = overlayObject.GetComponent<Image>();
        overlayImage.color = new Color(1f, 1f, 1f, 0.52f);
        overlayImage.raycastTarget = false;

        overlayObject.SetActive(false);
        return overlayObject;
    }

    private GameObject EnsureMenuButtonsContainer(Transform parent)
    {
        Transform existingContainerTransform = parent.Find(MenuButtonsContainerObjectName);
        if (existingContainerTransform != null)
        {
            return existingContainerTransform.gameObject;
        }

        GameObject containerObject = new GameObject(MenuButtonsContainerObjectName, typeof(RectTransform));
        containerObject.transform.SetParent(parent, false);

        RectTransform containerRectTransform = containerObject.GetComponent<RectTransform>();
        containerRectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        containerRectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        containerRectTransform.pivot = new Vector2(0.5f, 0.5f);
        containerRectTransform.sizeDelta = new Vector2(320f, 420f);
        containerRectTransform.anchoredPosition = Vector2.zero;

        return containerObject;
    }

    private void EnsureStartButton(Transform parent)
    {
        GameObject existingButtonObject = GameObject.Find(ButtonObjectName);
        if (existingButtonObject != null)
        {
            startButton = existingButtonObject.GetComponent<Button>();
            if (startButton != null)
            {
                startButton.onClick.RemoveListener(HandleStartButtonPressed);
                startButton.onClick.AddListener(HandleStartButtonPressed);
            }

            return;
        }

        startButton = CreateButton(
            parent,
            ButtonObjectName,
            buttonLabel,
            Vector2.zero,
            new Vector2(280f, 80f),
            new Color(0.16f, 0.44f, 0.25f, 1f));
        startButton.onClick.AddListener(HandleStartButtonPressed);
    }

    private void EnsureReconfigureButton(Transform parent)
    {
        GameObject existingButtonObject = GameObject.Find(ReconfigureButtonObjectName);
        if (existingButtonObject != null)
        {
            reconfigureButton = existingButtonObject.GetComponent<Button>();
            if (reconfigureButton != null)
            {
                reconfigureButton.onClick.RemoveListener(HandleReconfigureButtonPressed);
                reconfigureButton.onClick.AddListener(HandleReconfigureButtonPressed);
                SetButtonLabel(reconfigureButton, reconfigureButtonLabel);
            }

            return;
        }

        reconfigureButton = CreateButton(
            parent,
            ReconfigureButtonObjectName,
            reconfigureButtonLabel,
            Vector2.zero,
            new Vector2(280f, 68f),
            new Color(0.74f, 0.81f, 0.72f, 1f));
        reconfigureButton.onClick.AddListener(HandleReconfigureButtonPressed);

        Text label = reconfigureButton.GetComponentInChildren<Text>();
        if (label != null)
        {
            label.color = new Color(0.12f, 0.22f, 0.16f, 1f);
        }
    }

    private void EnsureOptionButton(Transform parent)
    {
        GameObject existingButtonObject = GameObject.Find(OptionButtonObjectName);
        if (existingButtonObject != null)
        {
            optionButton = existingButtonObject.GetComponent<Button>();
            if (optionButton != null)
            {
                optionButton.onClick.RemoveListener(HandleOptionButtonPressed);
                optionButton.onClick.AddListener(HandleOptionButtonPressed);
                SetButtonLabel(optionButton, optionButtonLabel);
            }

            return;
        }

        optionButton = CreateButton(
            parent,
            OptionButtonObjectName,
            optionButtonLabel,
            Vector2.zero,
            new Vector2(280f, 68f),
            new Color(0.84f, 0.74f, 0.46f, 1f));
        optionButton.onClick.AddListener(HandleOptionButtonPressed);

        Text label = optionButton.GetComponentInChildren<Text>();
        if (label != null)
        {
            label.color = new Color(0.18f, 0.16f, 0.09f, 1f);
        }
    }

    private void EnsureExitButton(Transform parent)
    {
        GameObject existingButtonObject = GameObject.Find(ExitButtonObjectName);
        if (existingButtonObject != null)
        {
            exitButton = existingButtonObject.GetComponent<Button>();
            if (exitButton != null)
            {
                exitButton.onClick.RemoveListener(HandleExitButtonPressed);
                exitButton.onClick.AddListener(HandleExitButtonPressed);
                SetButtonLabel(exitButton, exitButtonLabel);
            }

            return;
        }

        exitButton = CreateButton(
            parent,
            ExitButtonObjectName,
            exitButtonLabel,
            Vector2.zero,
            new Vector2(280f, 68f),
            new Color(0.71f, 0.35f, 0.3f, 1f));
        exitButton.onClick.AddListener(HandleExitButtonPressed);
    }

    private void EnsureOptionsPanel(Transform parent)
    {
        if (optionsPanel != null)
        {
            return;
        }

        optionsPanel = new GameObject(
            OptionsPanelObjectName,
            typeof(RectTransform),
            typeof(Image));

        optionsPanel.transform.SetParent(parent, false);

        RectTransform panelRectTransform = optionsPanel.GetComponent<RectTransform>();
        panelRectTransform.anchorMin = Vector2.zero;
        panelRectTransform.anchorMax = Vector2.one;
        panelRectTransform.offsetMin = Vector2.zero;
        panelRectTransform.offsetMax = Vector2.zero;

        Image panelOverlay = optionsPanel.GetComponent<Image>();
        panelOverlay.color = new Color(0f, 0f, 0f, 0.72f);

        GameObject windowObject = new GameObject(
            OptionsWindowObjectName,
            typeof(RectTransform),
            typeof(Image));

        windowObject.transform.SetParent(optionsPanel.transform, false);

        RectTransform windowRectTransform = windowObject.GetComponent<RectTransform>();
        windowRectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        windowRectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        windowRectTransform.pivot = new Vector2(0.5f, 0.5f);
        windowRectTransform.sizeDelta = OptionsWindowSize;
        windowRectTransform.anchoredPosition = Vector2.zero;

        Image windowImage = windowObject.GetComponent<Image>();
        windowImage.color = new Color(0.95f, 0.96f, 0.9f, 1f);

        CreateTextElement(
            windowObject.transform,
            "OptionsTitle",
            "옵션",
            42,
            FontStyle.Bold,
            TextAnchor.MiddleCenter,
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -56f),
            new Vector2(620f, 60f),
            new Color(0.13f, 0.2f, 0.14f, 1f));

        optionsAudioTabButton = CreateOptionsTabButton(
            windowObject.transform,
            "OptionsAudioTabButton",
            "오디오",
            new Vector2(-250f, -132f));
        optionsAudioTabButton.onClick.AddListener(() => SetActiveOptionsCategory(OptionsCategory.Audio));

        optionsResolutionTabButton = CreateOptionsTabButton(
            windowObject.transform,
            "OptionsResolutionTabButton",
            "해상도",
            new Vector2(0f, -132f));
        optionsResolutionTabButton.onClick.AddListener(() => SetActiveOptionsCategory(OptionsCategory.Resolution));

        optionsKeySetupTabButton = CreateOptionsTabButton(
            windowObject.transform,
            "OptionsKeySetupTabButton",
            reconfigureButtonLabel,
            new Vector2(250f, -132f));
        optionsKeySetupTabButton.onClick.AddListener(() => SetActiveOptionsCategory(OptionsCategory.KeySetup));

        GameObject contentRootObject = new GameObject("OptionsContentRoot", typeof(RectTransform));
        contentRootObject.transform.SetParent(windowObject.transform, false);

        RectTransform contentRootRectTransform = contentRootObject.GetComponent<RectTransform>();
        contentRootRectTransform.anchorMin = Vector2.zero;
        contentRootRectTransform.anchorMax = Vector2.one;
        contentRootRectTransform.offsetMin = new Vector2(36f, 126f);
        contentRootRectTransform.offsetMax = new Vector2(-36f, -186f);

        optionsAudioContent = CreateOptionsAudioContent(contentRootObject.transform);
        optionsResolutionContent = CreateOptionsResolutionContent(contentRootObject.transform);
        optionsKeySetupContent = CreateOptionsKeySetupContent(contentRootObject.transform);

        Button closeButton = CreateButton(
            windowObject.transform,
            "OptionsCloseButton",
            "닫기",
            OptionsCloseButtonPosition,
            new Vector2(240f, 64f),
            new Color(0.16f, 0.44f, 0.25f, 1f));
        SetBottomAnchoredRect(closeButton.GetComponent<RectTransform>());
        closeButton.onClick.AddListener(CloseOptionsPanel);
        contentRootObject.transform.SetAsLastSibling();

        SetActiveOptionsCategory(OptionsCategory.Audio);
    }

    private void EnsureKeyMappingPanel(Transform parent)
    {
        if (keyMappingPanel != null)
        {
            return;
        }

        keyMappingPanel = new GameObject(
            KeyMappingPanelObjectName,
            typeof(RectTransform),
            typeof(Image));

        keyMappingPanel.transform.SetParent(parent, false);

        RectTransform panelRectTransform = keyMappingPanel.GetComponent<RectTransform>();
        panelRectTransform.anchorMin = Vector2.zero;
        panelRectTransform.anchorMax = Vector2.one;
        panelRectTransform.offsetMin = Vector2.zero;
        panelRectTransform.offsetMax = Vector2.zero;

        Image panelOverlay = keyMappingPanel.GetComponent<Image>();
        panelOverlay.color = new Color(0f, 0f, 0f, 0.72f);

        GameObject windowObject = new GameObject(
            KeyMappingWindowObjectName,
            typeof(RectTransform),
            typeof(Image));

        windowObject.transform.SetParent(keyMappingPanel.transform, false);

        RectTransform windowRectTransform = windowObject.GetComponent<RectTransform>();
        windowRectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        windowRectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        windowRectTransform.pivot = new Vector2(0.5f, 0.5f);
        windowRectTransform.sizeDelta = new Vector2(860f, 760f);
        windowRectTransform.anchoredPosition = Vector2.zero;

        Image windowImage = windowObject.GetComponent<Image>();
        windowImage.color = new Color(0.95f, 0.96f, 0.9f, 1f);

        CreateTextElement(
            windowObject.transform,
            "Title",
            "키 설정",
            38,
            FontStyle.Bold,
            TextAnchor.MiddleCenter,
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -60f),
            new Vector2(760f, 60f),
            new Color(0.13f, 0.2f, 0.14f, 1f));

        IReadOnlyList<InputActionType> actions = PlayerInputBindings.Actions;
        RectTransform bindingContentTransform = CreateBindingScrollArea(windowObject.transform);
        for (int i = 0; i < actions.Count; i++)
        {
            CreateBindingRow(bindingContentTransform, actions[i], KeyBindingTopPadding + (KeyBindingRowSpacing * i));
        }
        ConfigureBindingScrollContent(bindingContentTransform, actions.Count);

        Text keyMappingStatusText = CreateTextElement(
            windowObject.transform,
            "StatusText",
            DefaultStatusText,
            20,
            FontStyle.Italic,
            TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            KeyBindingStatusPosition,
            new Vector2(760f, 60f),
            new Color(0.2f, 0.28f, 0.23f, 1f));
        statusTexts.Add(keyMappingStatusText);

        Button cancelButton = CreateButton(
            windowObject.transform,
            "CancelButton",
            "취소",
            CancelButtonPosition,
            new Vector2(220f, 64f),
            new Color(0.52f, 0.57f, 0.52f, 1f));
        SetBottomAnchoredRect(cancelButton.GetComponent<RectTransform>());
        cancelButton.onClick.AddListener(CloseKeyMappingPanel);

        Button confirmButton = CreateButton(
            windowObject.transform,
            "ConfirmButton",
            "확인",
            ConfirmButtonPosition,
            new Vector2(220f, 64f),
            new Color(0.16f, 0.44f, 0.25f, 1f));
        SetBottomAnchoredRect(confirmButton.GetComponent<RectTransform>());
        confirmButton.onClick.AddListener(ConfirmKeyMappingAndLoadScene);
    }

    private void CreateAudioSliderRow(
        Transform parent,
        string objectPrefix,
        string labelText,
        float anchoredY,
        out Slider slider,
        out Text valueText)
    {
        GameObject rowObject = new GameObject($"{objectPrefix}Row", typeof(RectTransform));
        rowObject.transform.SetParent(parent, false);

        RectTransform rowRectTransform = rowObject.GetComponent<RectTransform>();
        rowRectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rowRectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rowRectTransform.pivot = new Vector2(0.5f, 0.5f);
        rowRectTransform.anchoredPosition = new Vector2(0f, anchoredY);
        rowRectTransform.sizeDelta = new Vector2(620f, 72f);

        CreateTextElement(
            rowObject.transform,
            $"{objectPrefix}Label",
            labelText,
            24,
            FontStyle.Bold,
            TextAnchor.MiddleLeft,
            new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f),
            new Vector2(110f, 0f),
            new Vector2(220f, 44f),
            new Color(0.14f, 0.22f, 0.16f, 1f));

        slider = CreateSlider(
            rowObject.transform,
            $"{objectPrefix}Slider",
            new Vector2(90f, 0f),
            new Vector2(280f, 26f));

        valueText = CreateTextElement(
            rowObject.transform,
            $"{objectPrefix}Value",
            "100%",
            22,
            FontStyle.Bold,
            TextAnchor.MiddleCenter,
            new Vector2(1f, 0.5f),
            new Vector2(1f, 0.5f),
            new Vector2(-56f, 0f),
            new Vector2(96f, 40f),
            new Color(0.14f, 0.22f, 0.16f, 1f));
    }

    private Button CreateOptionsTabButton(
        Transform parent,
        string objectName,
        string labelText,
        Vector2 anchoredPosition)
    {
        Button tabButton = CreateButton(
            parent,
            objectName,
            labelText,
            anchoredPosition,
            new Vector2(240f, 62f),
            new Color(0.72f, 0.77f, 0.68f, 1f));

        SetTopAnchoredRect(tabButton.GetComponent<RectTransform>());

        Text label = tabButton.GetComponentInChildren<Text>();
        if (label != null)
        {
            label.fontSize = 24;
        }

        return tabButton;
    }

    private GameObject CreateOptionsAudioContent(Transform parent)
    {
        GameObject contentObject = new GameObject("OptionsAudioContent", typeof(RectTransform));
        contentObject.transform.SetParent(parent, false);

        RectTransform contentRectTransform = contentObject.GetComponent<RectTransform>();
        contentRectTransform.anchorMin = Vector2.zero;
        contentRectTransform.anchorMax = Vector2.one;
        contentRectTransform.offsetMin = Vector2.zero;
        contentRectTransform.offsetMax = Vector2.zero;

        CreateTextElement(
            contentObject.transform,
            "OptionsAudioHeading",
            "오디오 설정",
            34,
            FontStyle.Bold,
            TextAnchor.MiddleCenter,
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -38f),
            new Vector2(620f, 52f),
            new Color(0.13f, 0.2f, 0.14f, 1f));
        HideOptionsCategoryHeading(contentObject.transform, "OptionsAudioHeading");

        CreateAudioSliderRow(
            contentObject.transform,
            "OptionsBackgroundMusic",
            "배경 음악",
            46f + OptionsCategoryContentLift,
            out backgroundMusicSlider,
            out backgroundMusicValueText);

        CreateAudioSliderRow(
            contentObject.transform,
            "OptionsSoundEffect",
            "효과음",
            -36f + OptionsCategoryContentLift,
            out soundEffectSlider,
            out soundEffectValueText);

        if (backgroundMusicSlider != null)
        {
            backgroundMusicSlider.onValueChanged.AddListener(HandleBackgroundMusicSliderChanged);
        }

        if (soundEffectSlider != null)
        {
            soundEffectSlider.onValueChanged.AddListener(HandleSoundEffectSliderChanged);
        }

        return contentObject;
    }

    private GameObject CreateOptionsResolutionContent(Transform parent)
    {
        GameObject contentObject = new GameObject("OptionsResolutionContent", typeof(RectTransform));
        contentObject.transform.SetParent(parent, false);

        RectTransform contentRectTransform = contentObject.GetComponent<RectTransform>();
        contentRectTransform.anchorMin = Vector2.zero;
        contentRectTransform.anchorMax = Vector2.one;
        contentRectTransform.offsetMin = Vector2.zero;
        contentRectTransform.offsetMax = Vector2.zero;

        CreateTextElement(
            contentObject.transform,
            "OptionsResolutionHeading",
            "해상도",
            34,
            FontStyle.Bold,
            TextAnchor.MiddleCenter,
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -38f),
            new Vector2(620f, 52f),
            new Color(0.13f, 0.2f, 0.14f, 1f));
        HideOptionsCategoryHeading(contentObject.transform, "OptionsResolutionHeading");

        CreateTextElement(
            contentObject.transform,
            "OptionsDisplayModeLabel",
            "화면 모드",
            24,
            FontStyle.Bold,
            TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0f, 96f + OptionsCategoryContentLift),
            new Vector2(320f, 40f),
            new Color(0.14f, 0.22f, 0.16f, 1f));

        fullscreenModeButton = CreateButton(
            contentObject.transform,
            "FullscreenModeButton",
            "전체화면",
            new Vector2(0f, 42f + OptionsCategoryContentLift),
            new Vector2(280f, 56f),
            new Color(1f, 1f, 1f, 0f));
        fullscreenModeButton.onClick.AddListener(HandleDisplayModeButtonPressed);
        ConfigureDisplayModeCheckboxButton(
            fullscreenModeButton,
            out fullscreenModeCheckboxImage,
            out fullscreenModeCheckboxFillImage);

        CreateTextElement(
            contentObject.transform,
            "OptionsResolutionListLabel",
            "해상도 선택",
            24,
            FontStyle.Bold,
            TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0f, -26f + OptionsCategoryContentLift),
            new Vector2(420f, 40f),
            new Color(0.14f, 0.22f, 0.16f, 1f));

        resolutionToggleButton = CreateButton(
            contentObject.transform,
            "ResolutionToggleButton",
            string.Empty,
            new Vector2(0f, -84f + OptionsCategoryContentLift),
            new Vector2(340f, 52f),
            new Color(0.72f, 0.82f, 0.72f, 1f));
        resolutionToggleButton.onClick.AddListener(ToggleResolutionOptionsList);
        resolutionToggleButtonText = resolutionToggleButton.GetComponentInChildren<Text>();

        resolutionOptionsContainer = new GameObject("ResolutionOptionsContainer", typeof(RectTransform));
        resolutionOptionsContainer.transform.SetParent(contentObject.transform, false);

        RectTransform resolutionContainerRectTransform = resolutionOptionsContainer.GetComponent<RectTransform>();
        resolutionContainerRectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        resolutionContainerRectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        resolutionContainerRectTransform.pivot = new Vector2(0.5f, 0.5f);
        resolutionContainerRectTransform.anchoredPosition = new Vector2(0f, -206f + OptionsCategoryContentLift);
        resolutionContainerRectTransform.sizeDelta = new Vector2(560f, 240f);

        RectTransform resolutionContentTransform = CreateBindingScrollArea(
            resolutionOptionsContainer.transform,
            "ResolutionOptions",
            Vector2.zero,
            new Vector2(520f, 220f),
            out resolutionOptionsScrollRect);

        resolutionButtons.Clear();
        for (int i = 0; i < availableResolutionOptions.Count; i++)
        {
            CreateResolutionOptionRow(
                resolutionContentTransform,
                i,
                availableResolutionOptions[i],
                ResolutionTopPadding + (ResolutionRowSpacing * i));
        }

        ConfigureResolutionOptionsContent(resolutionContentTransform, availableResolutionOptions.Count);
        SetResolutionOptionsExpanded(false);
        return contentObject;
    }

    private GameObject CreateOptionsKeySetupContent(Transform parent)
    {
        GameObject contentObject = new GameObject("OptionsKeySetupContent", typeof(RectTransform));
        contentObject.transform.SetParent(parent, false);

        RectTransform contentRectTransform = contentObject.GetComponent<RectTransform>();
        contentRectTransform.anchorMin = Vector2.zero;
        contentRectTransform.anchorMax = Vector2.one;
        contentRectTransform.offsetMin = Vector2.zero;
        contentRectTransform.offsetMax = Vector2.zero;

        CreateTextElement(
            contentObject.transform,
            "OptionsKeySetupHeading",
            "키 설정",
            34,
            FontStyle.Bold,
            TextAnchor.MiddleCenter,
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -38f),
            new Vector2(620f, 52f),
            new Color(0.13f, 0.2f, 0.14f, 1f));
        HideOptionsCategoryHeading(contentObject.transform, "OptionsKeySetupHeading");

        IReadOnlyList<InputActionType> actions = PlayerInputBindings.Actions;
        RectTransform bindingContentTransform = CreateBindingScrollArea(
            contentObject.transform,
            "OptionsKeyBinding",
            new Vector2(0f, -10f + OptionsCategoryContentLift),
            new Vector2(780f, 380f),
            out optionsBindingScrollRect);

        for (int i = 0; i < actions.Count; i++)
        {
            CreateBindingRow(bindingContentTransform, actions[i], KeyBindingTopPadding + (KeyBindingRowSpacing * i));
        }

        ConfigureBindingScrollContent(bindingContentTransform, actions.Count);

        Text optionsStatusText = CreateTextElement(
            contentObject.transform,
            "OptionsStatusText",
            DefaultStatusText,
            18,
            FontStyle.Italic,
            TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0f, 28f + OptionsCategoryContentLift),
            new Vector2(720f, 48f),
            new Color(0.2f, 0.28f, 0.23f, 1f));
        statusTexts.Add(optionsStatusText);

        return contentObject;
    }

    private static void HideOptionsCategoryHeading(Transform parent, string headingObjectName)
    {
        Transform headingTransform = parent.Find(headingObjectName);
        if (headingTransform != null)
        {
            headingTransform.gameObject.SetActive(false);
        }
    }

    private void CreateResolutionOptionRow(Transform parent, int optionIndex, Vector2Int resolution, float topOffset)
    {
        GameObject rowObject = new GameObject($"ResolutionRow{optionIndex}", typeof(RectTransform));
        rowObject.transform.SetParent(parent, false);

        RectTransform rowRectTransform = rowObject.GetComponent<RectTransform>();
        rowRectTransform.anchorMin = new Vector2(0.5f, 1f);
        rowRectTransform.anchorMax = new Vector2(0.5f, 1f);
        rowRectTransform.pivot = new Vector2(0.5f, 1f);
        rowRectTransform.anchoredPosition = new Vector2(0f, -topOffset);
        rowRectTransform.sizeDelta = new Vector2(420f, 48f);

        Button resolutionButton = CreateButton(
            rowObject.transform,
            $"ResolutionButton{optionIndex}",
            GetResolutionLabel(resolution),
            Vector2.zero,
            new Vector2(360f, 44f),
            new Color(0.72f, 0.82f, 0.72f, 1f));
        resolutionButtons.Add(resolutionButton);

        int capturedIndex = optionIndex;
        resolutionButton.onClick.AddListener(() => HandleResolutionOptionPressed(capturedIndex));
    }

    private void ConfigureResolutionOptionsContent(RectTransform contentTransform, int rowCount)
    {
        if (contentTransform == null)
        {
            return;
        }

        float contentHeight = ResolutionTopPadding
            + ResolutionBottomPadding
            + ((rowCount - 1) * ResolutionRowSpacing)
            + 48f;

        contentTransform.sizeDelta = new Vector2(
            contentTransform.sizeDelta.x,
            Mathf.Max(ResolutionMinimumContentHeight, contentHeight));
    }

    private RectTransform CreateBindingScrollArea(Transform parent)
    {
        return CreateBindingScrollArea(
            parent,
            KeyBindingScrollAreaObjectName,
            KeyBindingViewportObjectName,
            KeyBindingContentObjectName,
            KeyBindingScrollAreaPosition,
            KeyBindingScrollAreaSize,
            out bindingScrollRect);
    }

    private RectTransform CreateBindingScrollArea(
        Transform parent,
        string objectPrefix,
        Vector2 anchoredPosition,
        Vector2 sizeDelta,
        out ScrollRect createdScrollRect)
    {
        return CreateBindingScrollArea(
            parent,
            $"{objectPrefix}ScrollArea",
            $"{objectPrefix}Viewport",
            $"{objectPrefix}Content",
            anchoredPosition,
            sizeDelta,
            out createdScrollRect);
    }

    private RectTransform CreateBindingScrollArea(
        Transform parent,
        string scrollAreaObjectName,
        string viewportObjectName,
        string contentObjectName,
        Vector2 anchoredPosition,
        Vector2 sizeDelta,
        out ScrollRect createdScrollRect)
    {
        const float viewportPadding = 18f;
        const float scrollbarWidth = 18f;
        const float scrollbarSpacing = 10f;

        GameObject scrollAreaObject = new GameObject(
            scrollAreaObjectName,
            typeof(RectTransform),
            typeof(Image),
            typeof(ScrollRect));

        scrollAreaObject.transform.SetParent(parent, false);

        RectTransform scrollAreaRectTransform = scrollAreaObject.GetComponent<RectTransform>();
        scrollAreaRectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        scrollAreaRectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        scrollAreaRectTransform.pivot = new Vector2(0.5f, 0.5f);
        scrollAreaRectTransform.anchoredPosition = anchoredPosition;
        scrollAreaRectTransform.sizeDelta = sizeDelta;

        Image scrollAreaImage = scrollAreaObject.GetComponent<Image>();
        scrollAreaImage.color = new Color(0.86f, 0.89f, 0.82f, 0.7f);

        GameObject viewportObject = new GameObject(
            viewportObjectName,
            typeof(RectTransform),
            typeof(Image),
            typeof(RectMask2D));

        viewportObject.transform.SetParent(scrollAreaObject.transform, false);

        RectTransform viewportRectTransform = viewportObject.GetComponent<RectTransform>();
        viewportRectTransform.anchorMin = Vector2.zero;
        viewportRectTransform.anchorMax = Vector2.one;
        viewportRectTransform.offsetMin = new Vector2(viewportPadding, viewportPadding);
        viewportRectTransform.offsetMax = new Vector2(
            -(viewportPadding + scrollbarWidth + scrollbarSpacing),
            -viewportPadding);

        Image viewportImage = viewportObject.GetComponent<Image>();
        viewportImage.color = new Color(1f, 1f, 1f, 0.02f);

        GameObject contentObject = new GameObject(contentObjectName, typeof(RectTransform));
        contentObject.transform.SetParent(viewportObject.transform, false);

        RectTransform contentRectTransform = contentObject.GetComponent<RectTransform>();
        contentRectTransform.anchorMin = new Vector2(0.5f, 1f);
        contentRectTransform.anchorMax = new Vector2(0.5f, 1f);
        contentRectTransform.pivot = new Vector2(0.5f, 1f);
        contentRectTransform.anchoredPosition = Vector2.zero;
        contentRectTransform.sizeDelta = new Vector2(
            Mathf.Max(360f, sizeDelta.x - 72f),
            KeyBindingMinimumContentHeight);

        GameObject scrollbarObject = new GameObject(
            $"{scrollAreaObjectName}Scrollbar",
            typeof(RectTransform),
            typeof(Image),
            typeof(Scrollbar));
        scrollbarObject.transform.SetParent(scrollAreaObject.transform, false);

        RectTransform scrollbarRectTransform = scrollbarObject.GetComponent<RectTransform>();
        scrollbarRectTransform.anchorMin = new Vector2(1f, 0f);
        scrollbarRectTransform.anchorMax = new Vector2(1f, 1f);
        scrollbarRectTransform.offsetMin = new Vector2(
            -(scrollbarWidth + scrollbarSpacing),
            viewportPadding);
        scrollbarRectTransform.offsetMax = new Vector2(
            -scrollbarSpacing,
            -viewportPadding);

        Image scrollbarImage = scrollbarObject.GetComponent<Image>();
        scrollbarImage.color = new Color(0.28f, 0.36f, 0.28f, 0.32f);

        GameObject slidingAreaObject = new GameObject("SlidingArea", typeof(RectTransform));
        slidingAreaObject.transform.SetParent(scrollbarObject.transform, false);

        RectTransform slidingAreaRectTransform = slidingAreaObject.GetComponent<RectTransform>();
        slidingAreaRectTransform.anchorMin = Vector2.zero;
        slidingAreaRectTransform.anchorMax = Vector2.one;
        slidingAreaRectTransform.offsetMin = new Vector2(3f, 3f);
        slidingAreaRectTransform.offsetMax = new Vector2(-3f, -3f);

        GameObject handleObject = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handleObject.transform.SetParent(slidingAreaObject.transform, false);

        RectTransform handleRectTransform = handleObject.GetComponent<RectTransform>();
        handleRectTransform.anchorMin = Vector2.zero;
        handleRectTransform.anchorMax = Vector2.one;
        handleRectTransform.offsetMin = Vector2.zero;
        handleRectTransform.offsetMax = Vector2.zero;

        Image handleImage = handleObject.GetComponent<Image>();
        handleImage.color = new Color(0.5f, 0.66f, 0.49f, 0.98f);

        createdScrollRect = scrollAreaObject.GetComponent<ScrollRect>();
        createdScrollRect.viewport = viewportRectTransform;
        createdScrollRect.content = contentRectTransform;
        createdScrollRect.horizontal = false;
        createdScrollRect.vertical = true;
        createdScrollRect.movementType = ScrollRect.MovementType.Clamped;
        createdScrollRect.scrollSensitivity = 28f;
        createdScrollRect.verticalScrollbar = scrollbarObject.GetComponent<Scrollbar>();
        createdScrollRect.verticalScrollbar.handleRect = handleRectTransform;
        createdScrollRect.verticalScrollbar.targetGraphic = handleImage;
        createdScrollRect.verticalScrollbar.direction = Scrollbar.Direction.BottomToTop;
        createdScrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
        createdScrollRect.verticalScrollbarSpacing = scrollbarSpacing;

        return contentRectTransform;
    }

    private void ConfigureBindingScrollContent(RectTransform contentTransform, int rowCount)
    {
        if (contentTransform == null)
        {
            return;
        }

        float contentHeight = KeyBindingTopPadding
            + KeyBindingBottomPadding
            + ((rowCount - 1) * KeyBindingRowSpacing)
            + 56f;

        contentTransform.sizeDelta = new Vector2(
            contentTransform.sizeDelta.x,
            Mathf.Max(KeyBindingMinimumContentHeight, contentHeight));
    }

    private void CreateBindingRow(Transform parent, InputActionType action, float topOffset)
    {
        GameObject rowObject = new GameObject($"{action}Row", typeof(RectTransform));
        rowObject.transform.SetParent(parent, false);

        RectTransform rowRectTransform = rowObject.GetComponent<RectTransform>();
        float rowWidth = parent is RectTransform parentRectTransform
            ? Mathf.Max(600f, parentRectTransform.sizeDelta.x)
            : 680f;
        rowRectTransform.anchorMin = new Vector2(0.5f, 1f);
        rowRectTransform.anchorMax = new Vector2(0.5f, 1f);
        rowRectTransform.pivot = new Vector2(0.5f, 1f);
        rowRectTransform.anchoredPosition = new Vector2(0f, -topOffset);
        rowRectTransform.sizeDelta = new Vector2(rowWidth, 56f);

        CreateTextElement(
            rowObject.transform,
            $"{action}Label",
            PlayerInputBindings.GetActionLabel(action),
            26,
            FontStyle.Bold,
            TextAnchor.MiddleLeft,
            new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f),
            new Vector2(120f, 0f),
            new Vector2(220f, 44f),
            new Color(0.14f, 0.22f, 0.16f, 1f));

        Button bindingButton = CreateButton(
            rowObject.transform,
            $"{action}Button",
            PlayerInputBindings.GetKeyDisplayName(action),
            new Vector2(180f, 0f),
            new Vector2(260f, 46f),
            new Color(0.72f, 0.82f, 0.72f, 1f));

        Text bindingButtonText = bindingButton.GetComponentInChildren<Text>();
        if (bindingButtonText != null)
        {
            if (!bindingValueTexts.TryGetValue(action, out List<Text> texts))
            {
                texts = new List<Text>();
                bindingValueTexts[action] = texts;
            }

            texts.Add(bindingButtonText);
        }

        InputActionType capturedAction = action;
        bindingButton.onClick.AddListener(() => BeginRebind(capturedAction));
    }

    private Text CreateTextElement(
        Transform parent,
        string objectName,
        string textValue,
        int fontSize,
        FontStyle fontStyle,
        TextAnchor alignment,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 anchoredPosition,
        Vector2 sizeDelta,
        Color textColor)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(parent, false);

        RectTransform textRectTransform = textObject.GetComponent<RectTransform>();
        textRectTransform.anchorMin = anchorMin;
        textRectTransform.anchorMax = anchorMax;
        textRectTransform.pivot = new Vector2(0.5f, 0.5f);
        textRectTransform.anchoredPosition = anchoredPosition;
        textRectTransform.sizeDelta = sizeDelta;

        Text textComponent = textObject.GetComponent<Text>();
        textComponent.font = GetBuiltinFont();
        textComponent.text = textValue;
        textComponent.fontSize = fontSize;
        textComponent.fontStyle = fontStyle;
        textComponent.alignment = alignment;
        textComponent.color = textColor;
        textComponent.raycastTarget = false;
        return textComponent;
    }

    private Button CreateButton(
        Transform parent,
        string objectName,
        string labelText,
        Vector2 anchoredPosition,
        Vector2 sizeDelta,
        Color backgroundColor)
    {
        GameObject buttonObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(Image),
            typeof(Button));

        buttonObject.transform.SetParent(parent, false);

        RectTransform buttonRectTransform = buttonObject.GetComponent<RectTransform>();
        buttonRectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        buttonRectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        buttonRectTransform.pivot = new Vector2(0.5f, 0.5f);
        buttonRectTransform.anchoredPosition = anchoredPosition;
        buttonRectTransform.sizeDelta = sizeDelta;

        Image buttonImage = buttonObject.GetComponent<Image>();
        buttonImage.color = backgroundColor;

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = buttonImage;
        button.colors = CreateButtonColors(backgroundColor);

        CreateButtonLabel(buttonObject.transform, labelText);
        return button;
    }

    private Slider CreateSlider(
        Transform parent,
        string objectName,
        Vector2 anchoredPosition,
        Vector2 sizeDelta)
    {
        GameObject sliderObject = new GameObject(objectName, typeof(RectTransform), typeof(Slider));
        sliderObject.transform.SetParent(parent, false);

        RectTransform sliderRectTransform = sliderObject.GetComponent<RectTransform>();
        sliderRectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        sliderRectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        sliderRectTransform.pivot = new Vector2(0.5f, 0.5f);
        sliderRectTransform.anchoredPosition = anchoredPosition;
        sliderRectTransform.sizeDelta = sizeDelta;

        GameObject backgroundObject = new GameObject("Background", typeof(RectTransform), typeof(Image));
        backgroundObject.transform.SetParent(sliderObject.transform, false);

        RectTransform backgroundRectTransform = backgroundObject.GetComponent<RectTransform>();
        backgroundRectTransform.anchorMin = Vector2.zero;
        backgroundRectTransform.anchorMax = Vector2.one;
        backgroundRectTransform.offsetMin = Vector2.zero;
        backgroundRectTransform.offsetMax = Vector2.zero;

        Image backgroundImage = backgroundObject.GetComponent<Image>();
        backgroundImage.color = new Color(0.72f, 0.77f, 0.7f, 1f);

        GameObject fillAreaObject = new GameObject("Fill Area", typeof(RectTransform));
        fillAreaObject.transform.SetParent(sliderObject.transform, false);

        RectTransform fillAreaRectTransform = fillAreaObject.GetComponent<RectTransform>();
        fillAreaRectTransform.anchorMin = Vector2.zero;
        fillAreaRectTransform.anchorMax = Vector2.one;
        fillAreaRectTransform.offsetMin = new Vector2(10f, 5f);
        fillAreaRectTransform.offsetMax = new Vector2(-10f, -5f);

        GameObject fillObject = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fillObject.transform.SetParent(fillAreaObject.transform, false);

        RectTransform fillRectTransform = fillObject.GetComponent<RectTransform>();
        fillRectTransform.anchorMin = new Vector2(0f, 0f);
        fillRectTransform.anchorMax = new Vector2(1f, 1f);
        fillRectTransform.offsetMin = Vector2.zero;
        fillRectTransform.offsetMax = Vector2.zero;

        Image fillImage = fillObject.GetComponent<Image>();
        fillImage.color = new Color(0.16f, 0.44f, 0.25f, 1f);

        GameObject handleAreaObject = new GameObject("Handle Slide Area", typeof(RectTransform));
        handleAreaObject.transform.SetParent(sliderObject.transform, false);

        RectTransform handleAreaRectTransform = handleAreaObject.GetComponent<RectTransform>();
        handleAreaRectTransform.anchorMin = Vector2.zero;
        handleAreaRectTransform.anchorMax = Vector2.one;
        handleAreaRectTransform.offsetMin = new Vector2(10f, 0f);
        handleAreaRectTransform.offsetMax = new Vector2(-10f, 0f);

        GameObject handleObject = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handleObject.transform.SetParent(handleAreaObject.transform, false);

        RectTransform handleRectTransform = handleObject.GetComponent<RectTransform>();
        handleRectTransform.sizeDelta = new Vector2(24f, 24f);

        Image handleImage = handleObject.GetComponent<Image>();
        handleImage.color = new Color(0.96f, 0.98f, 0.92f, 1f);

        Slider slider = sliderObject.GetComponent<Slider>();
        slider.targetGraphic = handleImage;
        slider.fillRect = fillRectTransform;
        slider.handleRect = handleRectTransform;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;
        slider.value = 1f;
        return slider;
    }

    private Text CreateButtonLabel(Transform parent, string labelText)
    {
        GameObject labelObject = new GameObject(ButtonLabelObjectName, typeof(RectTransform), typeof(Text));
        labelObject.transform.SetParent(parent, false);

        RectTransform labelRectTransform = labelObject.GetComponent<RectTransform>();
        labelRectTransform.anchorMin = Vector2.zero;
        labelRectTransform.anchorMax = Vector2.one;
        labelRectTransform.offsetMin = Vector2.zero;
        labelRectTransform.offsetMax = Vector2.zero;

        Text label = labelObject.GetComponent<Text>();
        label.font = GetBuiltinFont();
        label.text = labelText;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;
        label.fontSize = 28;
        label.fontStyle = FontStyle.Bold;
        label.raycastTarget = false;
        return label;
    }

    private void SetBottomAnchoredRect(RectTransform rectTransform)
    {
        if (rectTransform == null)
        {
            return;
        }

        rectTransform.anchorMin = new Vector2(0.5f, 0f);
        rectTransform.anchorMax = new Vector2(0.5f, 0f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
    }

    private void SetTopAnchoredRect(RectTransform rectTransform)
    {
        if (rectTransform == null)
        {
            return;
        }

        rectTransform.anchorMin = new Vector2(0.5f, 1f);
        rectTransform.anchorMax = new Vector2(0.5f, 1f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!isPauseMenu)
        {
            return;
        }

        EnsureEventSystem();
        SetKeyMappingPanelVisible(false);
        SetOptionsPanelVisible(false);
        SetPauseBackgroundVisible(false);
        SetMenuButtonsVisible(false);
        ResumeSceneActivity();
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
            Debug.LogError("Could not load the built-in runtime font for the start scene UI.", this);
        }

        return builtinFont;
    }

    private ColorBlock CreateButtonColors(Color normalColor)
    {
        ColorBlock colors = ColorBlock.defaultColorBlock;
        colors.normalColor = normalColor;
        colors.highlightedColor = Color.Lerp(normalColor, Color.white, 0.15f);
        colors.pressedColor = Color.Lerp(normalColor, Color.black, 0.18f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(0.4f, 0.4f, 0.4f, 0.7f);
        return colors;
    }

    private void HandleStartButtonPressed()
    {
        if (isPauseMenu)
        {
            HidePauseMenu();
            return;
        }

        if (PlayerInputBindings.IsConfigured)
        {
            LoadScene();
            return;
        }

        OpenKeyMappingPanel(true);
    }

    private void HandleReconfigureButtonPressed()
    {
        OpenKeyMappingPanel(false);
    }

    private void HandleOptionButtonPressed()
    {
        OpenOptionsPanel();
    }

    private void HandleExitButtonPressed()
    {
        ResumeSceneActivity();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void TogglePauseMenu()
    {
        if (menuButtonsContainer != null && menuButtonsContainer.activeSelf)
        {
            HidePauseMenu();
            return;
        }

        ShowPauseMenu();
    }

    private void ShowPauseMenu()
    {
        if (!isPauseMenu)
        {
            return;
        }

        shouldLoadSceneAfterConfirm = false;
        RefreshMenuButtons();
        SetKeyMappingPanelVisible(false);
        SetOptionsPanelVisible(false);
        SetPauseBackgroundVisible(true);
        SetMenuButtonsVisible(true);
        SetMenuButtonsInteractable(true);
        PauseSceneActivity();
    }

    private void HidePauseMenu()
    {
        if (!isPauseMenu)
        {
            return;
        }

        CancelPendingRebind();
        shouldLoadSceneAfterConfirm = false;
        SetKeyMappingPanelVisible(false);
        SetOptionsPanelVisible(false);
        SetMenuButtonsVisible(false);
        SetPauseBackgroundVisible(false);
        ResumeSceneActivity();
    }

    private void PauseSceneActivity()
    {
        GamePauseState.SetPaused(true);
        Time.timeScale = 0f;
        AudioListener.pause = true;
    }

    private void ResumeSceneActivity()
    {
        GamePauseState.SetPaused(false);
        Time.timeScale = 1f;
        AudioListener.pause = false;
    }

    private void OpenKeyMappingPanel(bool loadSceneAfterConfirm)
    {
        shouldLoadSceneAfterConfirm = loadSceneAfterConfirm;
        isWaitingForBinding = false;
        RefreshBindingValueTexts();
        UpdateStatusText(DefaultStatusText);
        SetKeyMappingPanelVisible(true);
        ResetBindingScrollPosition();
        SetMenuButtonsInteractable(false);
    }

    private void OpenOptionsPanel()
    {
        RefreshAudioControls();
        RefreshBindingValueTexts();
        SetActiveOptionsCategory(activeOptionsCategory);
        SetOptionsPanelVisible(true);
        SetMenuButtonsInteractable(false);
    }

    private void CloseKeyMappingPanel()
    {
        CancelPendingRebind();
        SetKeyMappingPanelVisible(false);
        shouldLoadSceneAfterConfirm = false;
        RefreshMenuButtons();
        SetMenuButtonsInteractable(true);
    }

    private void CloseOptionsPanel()
    {
        CancelPendingRebind();
        SetOptionsPanelVisible(false);
        RefreshMenuButtons();
        SetMenuButtonsInteractable(true);
    }

    private void BeginRebind(InputActionType action)
    {
        pendingBindingAction = action;
        isWaitingForBinding = true;
        RefreshBindingValueTexts();

        if (bindingValueTexts.TryGetValue(action, out List<Text> bindingValueTextList))
        {
            for (int i = 0; i < bindingValueTextList.Count; i++)
            {
                Text bindingValueText = bindingValueTextList[i];
                if (bindingValueText == null)
                {
                    continue;
                }

                bindingValueText.text = "키 입력...";
            }
        }

        UpdateStatusText($"{PlayerInputBindings.GetActionLabel(action)}에 사용할 키를 눌러주세요.");
    }

    private void CancelPendingRebind()
    {
        if (!isWaitingForBinding)
        {
            return;
        }

        isWaitingForBinding = false;
        RefreshBindingValueTexts();
        UpdateStatusText(DefaultStatusText);
    }

    private void ConfirmKeyMappingAndLoadScene()
    {
        if (isWaitingForBinding)
        {
            UpdateStatusText("현재 키 선택을 먼저 완료해 주세요.");
            return;
        }

        PlayerInputBindings.SaveAndMarkConfigured();
        if (shouldLoadSceneAfterConfirm)
        {
            LoadScene();
            return;
        }

        CloseKeyMappingPanel();
    }

    private void HandleBackgroundMusicSliderChanged(float value)
    {
        if (isRefreshingAudioControls)
        {
            return;
        }

        AudioSettingsStore.SetBackgroundMusicVolume(value);
        UpdateAudioValueText(backgroundMusicValueText, value);
    }

    private void HandleSoundEffectSliderChanged(float value)
    {
        if (isRefreshingAudioControls)
        {
            return;
        }

        AudioSettingsStore.SetSoundEffectVolume(value);
        UpdateAudioValueText(soundEffectValueText, value);
    }

    private void RefreshBindingValueTexts()
    {
        foreach (KeyValuePair<InputActionType, List<Text>> entry in bindingValueTexts)
        {
            if (entry.Value == null)
            {
                continue;
            }

            string bindingDisplayName = PlayerInputBindings.GetKeyDisplayName(entry.Key);
            for (int i = 0; i < entry.Value.Count; i++)
            {
                Text bindingValueText = entry.Value[i];
                if (bindingValueText == null)
                {
                    continue;
                }

                bindingValueText.text = bindingDisplayName;
            }
        }
    }

    private void RefreshAudioControls()
    {
        isRefreshingAudioControls = true;

        if (backgroundMusicSlider != null)
        {
            backgroundMusicSlider.value = AudioSettingsStore.BackgroundMusicVolume;
        }

        if (soundEffectSlider != null)
        {
            soundEffectSlider.value = AudioSettingsStore.SoundEffectVolume;
        }

        UpdateAudioValueText(backgroundMusicValueText, AudioSettingsStore.BackgroundMusicVolume);
        UpdateAudioValueText(soundEffectValueText, AudioSettingsStore.SoundEffectVolume);

        isRefreshingAudioControls = false;
    }

    private void RefreshResolutionControls()
    {
        selectedResolutionIndex = FindClosestResolutionOptionIndex(Screen.width, Screen.height);
        UpdateDisplayModeButtonAppearance(
            fullscreenModeButton,
            fullscreenModeCheckboxImage,
            fullscreenModeCheckboxFillImage,
            isFullscreenEnabled,
            "전체화면");

        for (int i = 0; i < resolutionButtons.Count; i++)
        {
            UpdateResolutionButtonAppearance(resolutionButtons[i], i == selectedResolutionIndex);
        }

        if (resolutionToggleButtonText != null)
        {
            string selectedResolutionLabel = selectedResolutionIndex >= 0 && selectedResolutionIndex < availableResolutionOptions.Count
                ? GetResolutionLabel(availableResolutionOptions[selectedResolutionIndex])
                : GetResolutionLabel(new Vector2Int(Screen.width, Screen.height));
            string toggleIndicator = isResolutionListExpanded ? "▲" : "▼";
            resolutionToggleButtonText.text = $"{selectedResolutionLabel} {toggleIndicator}";
        }
    }

    private void BuildAvailableResolutionOptions()
    {
        availableResolutionOptions.Clear();
        HashSet<string> systemResolutionKeys = new HashSet<string>();

        Resolution[] systemResolutions = Screen.resolutions;
        for (int i = 0; i < systemResolutions.Length; i++)
        {
            systemResolutionKeys.Add(GetResolutionKey(systemResolutions[i].width, systemResolutions[i].height));
        }

        systemResolutionKeys.Add(GetResolutionKey(Screen.width, Screen.height));

        HashSet<string> seenResolutionKeys = new HashSet<string>();
        for (int i = 0; i < CommonResolutionOptions.Length; i++)
        {
            Vector2Int resolution = CommonResolutionOptions[i];
            if (!systemResolutionKeys.Contains(GetResolutionKey(resolution.x, resolution.y)))
            {
                continue;
            }

            AddResolutionOption(resolution.x, resolution.y, seenResolutionKeys);
        }

        AddResolutionOption(Screen.width, Screen.height, seenResolutionKeys);

        availableResolutionOptions.Sort((left, right) =>
        {
            int heightComparison = right.y.CompareTo(left.y);
            if (heightComparison != 0)
            {
                return heightComparison;
            }

            return right.x.CompareTo(left.x);
        });

        if (availableResolutionOptions.Count == 0)
        {
            availableResolutionOptions.Add(new Vector2Int(1280, 720));
        }
    }

    private void ApplySavedDisplaySettings()
    {
        bool shouldUseFullscreen = PlayerPrefs.HasKey(DisplayFullscreenPrefKey)
            ? PlayerPrefs.GetInt(DisplayFullscreenPrefKey) == 1
            : Screen.fullScreen;
        int savedWidth = PlayerPrefs.GetInt(DisplayWidthPrefKey, Screen.width);
        int savedHeight = PlayerPrefs.GetInt(DisplayHeightPrefKey, Screen.height);
        int savedResolutionIndex = FindClosestResolutionOptionIndex(savedWidth, savedHeight);
        Vector2Int targetResolution = savedResolutionIndex >= 0
            ? availableResolutionOptions[savedResolutionIndex]
            : new Vector2Int(Screen.width, Screen.height);

        isFullscreenEnabled = shouldUseFullscreen;
        ApplyDisplaySettings(targetResolution.x, targetResolution.y, shouldUseFullscreen, false);
    }

    private void AddResolutionOption(int width, int height, HashSet<string> seenResolutionKeys)
    {
        if (width <= 0 || height <= 0 || seenResolutionKeys == null)
        {
            return;
        }

        string resolutionKey = GetResolutionKey(width, height);
        if (!seenResolutionKeys.Add(resolutionKey))
        {
            return;
        }

        availableResolutionOptions.Add(new Vector2Int(width, height));
    }

    private string GetResolutionKey(int width, int height)
    {
        return $"{width}x{height}";
    }

    private int FindClosestResolutionOptionIndex(int width, int height)
    {
        if (availableResolutionOptions.Count == 0)
        {
            return -1;
        }

        int bestIndex = 0;
        int bestDistance = int.MaxValue;

        for (int i = 0; i < availableResolutionOptions.Count; i++)
        {
            Vector2Int option = availableResolutionOptions[i];
            int distance = Mathf.Abs(option.x - width) + Mathf.Abs(option.y - height);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = i;
            }

            if (distance == 0)
            {
                return i;
            }
        }

        return bestIndex;
    }

    private void ApplyDisplaySettings(int width, int height, bool useFullscreen, bool savePreference)
    {
        int resolutionIndex = FindClosestResolutionOptionIndex(width, height);
        Vector2Int targetResolution = resolutionIndex >= 0
            ? availableResolutionOptions[resolutionIndex]
            : new Vector2Int(width, height);

        selectedResolutionIndex = resolutionIndex;
        isFullscreenEnabled = useFullscreen;
        Screen.SetResolution(targetResolution.x, targetResolution.y, isFullscreenEnabled);

        if (savePreference)
        {
            PlayerPrefs.SetInt(DisplayFullscreenPrefKey, isFullscreenEnabled ? 1 : 0);
            PlayerPrefs.SetInt(DisplayWidthPrefKey, targetResolution.x);
            PlayerPrefs.SetInt(DisplayHeightPrefKey, targetResolution.y);
            PlayerPrefs.Save();
        }

        UpdateDisplayModeButtonAppearance(
            fullscreenModeButton,
            fullscreenModeCheckboxImage,
            fullscreenModeCheckboxFillImage,
            isFullscreenEnabled,
            "전체화면");
        for (int i = 0; i < resolutionButtons.Count; i++)
        {
            UpdateResolutionButtonAppearance(resolutionButtons[i], i == selectedResolutionIndex);
        }
    }

    private void HandleDisplayModeButtonPressed()
    {
        Vector2Int resolutionToApply = selectedResolutionIndex >= 0 && selectedResolutionIndex < availableResolutionOptions.Count
            ? availableResolutionOptions[selectedResolutionIndex]
            : new Vector2Int(Screen.width, Screen.height);
        ApplyDisplaySettings(resolutionToApply.x, resolutionToApply.y, !isFullscreenEnabled, true);
    }

    private void HandleResolutionOptionPressed(int optionIndex)
    {
        if (optionIndex < 0 || optionIndex >= availableResolutionOptions.Count)
        {
            return;
        }

        Vector2Int selectedResolution = availableResolutionOptions[optionIndex];
        ApplyDisplaySettings(selectedResolution.x, selectedResolution.y, isFullscreenEnabled, true);
        SetResolutionOptionsExpanded(false);
    }

    private void ToggleResolutionOptionsList()
    {
        SetResolutionOptionsExpanded(!isResolutionListExpanded);
    }

    private void UpdateAudioValueText(Text valueText, float value)
    {
        if (valueText == null)
        {
            return;
        }

        valueText.text = $"{Mathf.RoundToInt(Mathf.Clamp01(value) * 100f)}%";
    }

    private void UpdateStatusText(string message)
    {
        for (int i = 0; i < statusTexts.Count; i++)
        {
            Text statusText = statusTexts[i];
            if (statusText == null)
            {
                continue;
            }

            statusText.text = message;
            statusText.gameObject.SetActive(!string.IsNullOrWhiteSpace(message));
        }
    }

    private void SetKeyMappingPanelVisible(bool isVisible)
    {
        if (keyMappingPanel != null)
        {
            keyMappingPanel.SetActive(isVisible);
        }
    }

    private void SetOptionsPanelVisible(bool isVisible)
    {
        if (optionsPanel != null)
        {
            optionsPanel.SetActive(isVisible);
        }

        if (!isVisible)
        {
            SetResolutionOptionsExpanded(false);
        }
    }

    private void SetActiveOptionsCategory(OptionsCategory category)
    {
        if (activeOptionsCategory != category && isWaitingForBinding)
        {
            CancelPendingRebind();
        }

        activeOptionsCategory = category;

        if (optionsAudioContent != null)
        {
            optionsAudioContent.SetActive(category == OptionsCategory.Audio);
        }

        if (optionsResolutionContent != null)
        {
            optionsResolutionContent.SetActive(category == OptionsCategory.Resolution);
        }

        if (optionsKeySetupContent != null)
        {
            optionsKeySetupContent.SetActive(category == OptionsCategory.KeySetup);
        }

        UpdateOptionsTabAppearance(optionsAudioTabButton, category == OptionsCategory.Audio);
        UpdateOptionsTabAppearance(optionsResolutionTabButton, category == OptionsCategory.Resolution);
        UpdateOptionsTabAppearance(optionsKeySetupTabButton, category == OptionsCategory.KeySetup);

        if (category == OptionsCategory.Audio)
        {
            RefreshAudioControls();
            SetResolutionOptionsExpanded(false);
        }
        else if (category == OptionsCategory.Resolution)
        {
            RefreshResolutionControls();
            ResetBindingScrollPosition(resolutionOptionsScrollRect);
        }
        else
        {
            RefreshBindingValueTexts();
            ResetBindingScrollPosition(optionsBindingScrollRect);
            SetResolutionOptionsExpanded(false);
        }

        UpdateStatusText(DefaultStatusText);
    }

    private void SetMenuButtonsVisible(bool isVisible)
    {
        if (menuButtonsContainer != null)
        {
            menuButtonsContainer.SetActive(isVisible);
        }
    }

    private void SetPauseBackgroundVisible(bool isVisible)
    {
        if (pauseBackgroundOverlay != null)
        {
            pauseBackgroundOverlay.SetActive(isVisible);
        }
    }

    private void UpdatePrimaryButtonLabel()
    {
        if (startButton == null)
        {
            return;
        }

        SetButtonLabel(startButton, isPauseMenu ? PauseResumeButtonLabel : buttonLabel);
    }

    private void SetButtonLabel(Button button, string labelText)
    {
        if (button == null)
        {
            return;
        }

        Text label = button.GetComponentInChildren<Text>();
        if (label != null)
        {
            label.text = labelText;
        }
    }

    private void ResetBindingScrollPosition()
    {
        ResetBindingScrollPosition(bindingScrollRect);
    }

    private void ResetBindingScrollPosition(ScrollRect scrollRect)
    {
        if (scrollRect == null)
        {
            return;
        }

        Canvas.ForceUpdateCanvases();
        scrollRect.verticalNormalizedPosition = 1f;
    }

    private void RefreshMenuButtons()
    {
        if (reconfigureButton != null)
        {
            reconfigureButton.gameObject.SetActive(false);
        }

        if (optionButton != null)
        {
            optionButton.gameObject.SetActive(true);
        }

        if (exitButton != null)
        {
            exitButton.gameObject.SetActive(true);
        }

        List<Button> visibleButtons = new List<Button>();
        AddVisibleMenuButton(visibleButtons, startButton);
        AddVisibleMenuButton(visibleButtons, reconfigureButton);
        AddVisibleMenuButton(visibleButtons, optionButton);
        AddVisibleMenuButton(visibleButtons, exitButton);

        if (visibleButtons.Count == 0)
        {
            return;
        }

        float topOffset = (visibleButtons.Count - 1) * MenuButtonVerticalSpacing * 0.5f;
        for (int i = 0; i < visibleButtons.Count; i++)
        {
            RectTransform buttonRectTransform = visibleButtons[i].GetComponent<RectTransform>();
            if (buttonRectTransform == null)
            {
                continue;
            }

            buttonRectTransform.anchoredPosition = new Vector2(0f, topOffset - (i * MenuButtonVerticalSpacing));
        }
    }

    private void AddVisibleMenuButton(List<Button> buttons, Button button)
    {
        if (button == null || !button.gameObject.activeSelf)
        {
            return;
        }

        buttons.Add(button);
    }

    private void SetMenuButtonsInteractable(bool isInteractable)
    {
        if (startButton != null)
        {
            startButton.interactable = isInteractable;
        }

        if (reconfigureButton != null)
        {
            reconfigureButton.interactable = isInteractable;
        }

        if (optionButton != null)
        {
            optionButton.interactable = isInteractable;
        }

        if (exitButton != null)
        {
            exitButton.interactable = isInteractable;
        }
    }

    private void UpdateOptionsTabAppearance(Button button, bool isActive)
    {
        if (button == null)
        {
            return;
        }

        Color backgroundColor = isActive
            ? new Color(0.9f, 0.94f, 0.86f, 1f)
            : new Color(0.72f, 0.77f, 0.68f, 1f);
        Color labelColor = isActive
            ? new Color(0.11f, 0.18f, 0.13f, 1f)
            : new Color(0.25f, 0.31f, 0.26f, 1f);

        if (button.TryGetComponent(out Image buttonImage))
        {
            buttonImage.color = backgroundColor;
        }

        button.colors = CreateButtonColors(backgroundColor);

        Text label = button.GetComponentInChildren<Text>();
        if (label != null)
        {
            label.color = labelColor;
        }
    }

    private void ConfigureDisplayModeCheckboxButton(
        Button button,
        out Image checkboxImage,
        out Image checkboxFillImage)
    {
        checkboxImage = null;
        checkboxFillImage = null;

        if (button == null)
        {
            return;
        }

        if (button.TryGetComponent(out Image buttonImage))
        {
            buttonImage.color = new Color(1f, 1f, 1f, 0f);
        }

        button.colors = CreateButtonColors(new Color(1f, 1f, 1f, 0f));

        Text label = button.GetComponentInChildren<Text>();
        if (label == null)
        {
            return;
        }

        label.alignment = TextAnchor.MiddleLeft;
        label.fontSize = 24;

        RectTransform labelRectTransform = label.GetComponent<RectTransform>();
        if (labelRectTransform != null)
        {
            labelRectTransform.offsetMin = new Vector2(84f, 0f);
            labelRectTransform.offsetMax = new Vector2(-12f, 0f);
        }

        GameObject checkboxObject = new GameObject(
            "Checkbox",
            typeof(RectTransform),
            typeof(Image),
            typeof(Outline));
        checkboxObject.transform.SetParent(button.transform, false);

        RectTransform checkboxRectTransform = checkboxObject.GetComponent<RectTransform>();
        checkboxRectTransform.anchorMin = new Vector2(0f, 0.5f);
        checkboxRectTransform.anchorMax = new Vector2(0f, 0.5f);
        checkboxRectTransform.pivot = new Vector2(0.5f, 0.5f);
        checkboxRectTransform.anchoredPosition = new Vector2(34f, 0f);
        checkboxRectTransform.sizeDelta = new Vector2(28f, 28f);

        checkboxImage = checkboxObject.GetComponent<Image>();
        checkboxImage.color = new Color(0.98f, 0.99f, 0.98f, 1f);
        checkboxImage.raycastTarget = false;

        Outline checkboxOutline = checkboxObject.GetComponent<Outline>();
        checkboxOutline.effectColor = new Color(0.26f, 0.34f, 0.28f, 1f);
        checkboxOutline.effectDistance = new Vector2(1.5f, -1.5f);

        GameObject checkboxFillObject = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        checkboxFillObject.transform.SetParent(checkboxObject.transform, false);

        RectTransform checkboxFillRectTransform = checkboxFillObject.GetComponent<RectTransform>();
        checkboxFillRectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        checkboxFillRectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        checkboxFillRectTransform.pivot = new Vector2(0.5f, 0.5f);
        checkboxFillRectTransform.anchoredPosition = Vector2.zero;
        checkboxFillRectTransform.sizeDelta = new Vector2(16f, 16f);

        checkboxFillImage = checkboxFillObject.GetComponent<Image>();
        checkboxFillImage.color = new Color(0.16f, 0.44f, 0.25f, 1f);
        checkboxFillImage.raycastTarget = false;
    }

    private void UpdateDisplayModeButtonAppearance(
        Button button,
        Image checkboxImage,
        Image checkboxFillImage,
        bool isActive,
        string labelText)
    {
        if (button == null)
        {
            return;
        }

        Color backgroundColor = new Color(1f, 1f, 1f, 0f);
        Color labelColor = isActive
            ? new Color(0.1f, 0.33f, 0.18f, 1f)
            : new Color(0.12f, 0.22f, 0.16f, 1f);

        if (button.TryGetComponent(out Image buttonImage))
        {
            buttonImage.color = backgroundColor;
        }

        button.colors = CreateButtonColors(backgroundColor);

        if (checkboxImage != null)
        {
            checkboxImage.color = isActive
                ? new Color(0.92f, 0.97f, 0.9f, 1f)
                : new Color(0.98f, 0.99f, 0.98f, 1f);

            if (checkboxImage.TryGetComponent(out Outline checkboxOutline))
            {
                checkboxOutline.effectColor = isActive
                    ? new Color(0.16f, 0.44f, 0.25f, 1f)
                    : new Color(0.26f, 0.34f, 0.28f, 1f);
            }
        }

        if (checkboxFillImage != null)
        {
            checkboxFillImage.enabled = isActive;
        }

        Text label = button.GetComponentInChildren<Text>();
        if (label != null)
        {
            label.text = labelText;
            label.color = labelColor;
        }
    }

    private void UpdateResolutionButtonAppearance(Button button, bool isSelected)
    {
        if (button == null)
        {
            return;
        }

        Color backgroundColor = isSelected
            ? new Color(0.16f, 0.44f, 0.25f, 1f)
            : new Color(0.72f, 0.82f, 0.72f, 1f);
        Color labelColor = isSelected
            ? Color.white
            : new Color(0.12f, 0.22f, 0.16f, 1f);

        if (button.TryGetComponent(out Image buttonImage))
        {
            buttonImage.color = backgroundColor;
        }

        button.colors = CreateButtonColors(backgroundColor);

        Text label = button.GetComponentInChildren<Text>();
        if (label != null)
        {
            label.color = labelColor;
        }
    }

    private void SetResolutionOptionsExpanded(bool isExpanded)
    {
        isResolutionListExpanded = isExpanded;

        if (resolutionOptionsContainer != null)
        {
            resolutionOptionsContainer.SetActive(isExpanded);

            if (isExpanded)
            {
                resolutionOptionsContainer.transform.SetAsLastSibling();
            }
        }

        if (isExpanded)
        {
            ResetBindingScrollPosition(resolutionOptionsScrollRect);
        }

        RefreshResolutionControls();
    }

    private void NormalizeMenuLabels()
    {
        buttonLabel = NormalizeLegacyLabel(buttonLabel, "START", "시작");
        reconfigureButtonLabel = NormalizeLegacyLabel(reconfigureButtonLabel, "KEY SETUP", "키 설정");
        optionButtonLabel = NormalizeLegacyLabel(optionButtonLabel, "OPTION", "옵션");
        exitButtonLabel = NormalizeLegacyLabel(exitButtonLabel, "EXIT", "종료");
    }

    private string NormalizeLegacyLabel(string currentValue, string legacyEnglishValue, string localizedValue)
    {
        if (string.IsNullOrWhiteSpace(currentValue) || string.Equals(currentValue, legacyEnglishValue, StringComparison.OrdinalIgnoreCase))
        {
            return localizedValue;
        }

        return currentValue;
    }

    private bool IsOptionsKeySetupVisible()
    {
        return optionsPanel != null
            && optionsPanel.activeSelf
            && activeOptionsCategory == OptionsCategory.KeySetup;
    }

    private string GetResolutionLabel(Vector2Int resolution)
    {
        return $"{resolution.x} x {resolution.y}";
    }

    private bool TryGetPressedBindableKey(out KeyCode pressedKey)
    {
        for (int i = 0; i < RebindableKeys.Length; i++)
        {
            if (Input.GetKeyDown(RebindableKeys[i]))
            {
                pressedKey = RebindableKeys[i];
                return true;
            }
        }

        pressedKey = KeyCode.None;
        return false;
    }

    private bool TryFindActionUsingKey(KeyCode key, InputActionType ignoredAction, out InputActionType actionUsingKey)
    {
        IReadOnlyList<InputActionType> actions = PlayerInputBindings.Actions;
        for (int i = 0; i < actions.Count; i++)
        {
            InputActionType action = actions[i];
            if (action == ignoredAction)
            {
                continue;
            }

            if (PlayerInputBindings.GetKey(action) == key)
            {
                actionUsingKey = action;
                return true;
            }
        }

        actionUsingKey = ignoredAction;
        return false;
    }

    private void LoadScene()
    {
        if (!TryResolveSceneTransitionTarget(out string targetSceneName))
        {
            return;
        }

        if (isPauseMenu)
        {
            HidePauseMenu();
        }

        SceneFadeTransition.LoadScene(targetSceneName);
    }

    private bool TryResolveSceneTransitionTarget(out string targetSceneName)
    {
        targetSceneName = sceneToLoad;

        if (!ValidateSceneAvailability(sceneToLoad, "destination scene"))
        {
            return false;
        }

        if (isPauseMenu || !playIntroCutsceneBeforeFirstScene || string.IsNullOrWhiteSpace(cutsceneSceneToLoad))
        {
            return true;
        }

        if (string.Equals(cutsceneSceneToLoad, sceneToLoad, StringComparison.Ordinal))
        {
            Debug.LogWarning("The intro cutscene scene matches the destination scene, so the cutscene transition will be skipped.", this);
            return true;
        }

        if (!ValidateSceneAvailability(cutsceneSceneToLoad, "intro cutscene scene"))
        {
            return false;
        }

        IntroCutsceneController.SetPendingNextScene(sceneToLoad);
        targetSceneName = cutsceneSceneToLoad;
        return true;
    }

    private bool ValidateSceneAvailability(string sceneName, string sceneDescription)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogError($"StartSceneController does not have a {sceneDescription} configured.", this);
            return false;
        }

        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"Scene '{sceneName}' ({sceneDescription}) is not available in Build Settings.", this);
            return false;
        }

        return true;
    }

    private static bool IsPauseMenuBlockedScene(string sceneName)
    {
        return sceneName == StartSceneName || sceneName == IntroCutsceneSceneName;
    }
}
