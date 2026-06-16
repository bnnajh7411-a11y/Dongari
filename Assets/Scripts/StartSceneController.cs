using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class StartSceneController : MonoBehaviour
{
    private const string StartSceneName = "Start";
    private const string EventSystemObjectName = "EventSystem";
    private const string CanvasObjectName = "StartCanvas";
    private const string PauseCanvasObjectName = "PauseMenuCanvas";
    private const string PauseMenuObjectName = "PauseMenuController";
    private const string MenuButtonsContainerObjectName = "MenuButtonsContainer";
    private const string ButtonObjectName = "StartButton";
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
    private const string DefaultStatusText = "";
    private const float MenuButtonVerticalSpacing = 92f;
    private static readonly Vector2 KeyBindingScrollAreaSize = new Vector2(760f, 340f);
    private static readonly Vector2 KeyBindingScrollAreaPosition = new Vector2(0f, -24f);
    private static readonly Vector2 KeyBindingStatusPosition = new Vector2(0f, 28f);
    private static readonly Vector2 CancelButtonPosition = new Vector2(-150f, 96f);
    private static readonly Vector2 ConfirmButtonPosition = new Vector2(150f, 96f);
    private static readonly Vector2 OptionsWindowSize = new Vector2(760f, 420f);
    private static readonly Vector2 OptionsCloseButtonPosition = new Vector2(0f, 58f);
    private const float KeyBindingRowSpacing = 68f;
    private const float KeyBindingTopPadding = 24f;
    private const float KeyBindingBottomPadding = 24f;
    private const float KeyBindingMinimumContentHeight = 340f;

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
    [SerializeField] private string buttonLabel = "START";
    [SerializeField] private string reconfigureButtonLabel = "KEY SETUP";
    [SerializeField] private string optionButtonLabel = "OPTION";
    [SerializeField] private string exitButtonLabel = "EXIT";

    private readonly Dictionary<InputActionType, Text> bindingValueTexts = new Dictionary<InputActionType, Text>();

    private static bool isCreatingPauseMenuInstance;
    private static StartSceneController pauseMenuInstance;

    private Button startButton;
    private Button reconfigureButton;
    private Button optionButton;
    private Button exitButton;
    private Canvas rootCanvas;
    private Font builtinFont;
    private GameObject menuButtonsContainer;
    private GameObject keyMappingPanel;
    private GameObject optionsPanel;
    private ScrollRect bindingScrollRect;
    private Slider backgroundMusicSlider;
    private Slider soundEffectSlider;
    private Text backgroundMusicValueText;
    private Text soundEffectValueText;
    private Text statusText;
    private bool isWaitingForBinding;
    private bool isRefreshingAudioControls;
    private bool shouldLoadSceneAfterConfirm;
    private bool isPauseMenu;
    private InputActionType pendingBindingAction;

    public static void EnsurePauseMenuInstance()
    {
        if (pauseMenuInstance != null || SceneManager.GetActiveScene().name == StartSceneName)
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

        if (!isPauseMenu && SceneManager.GetActiveScene().name == StartSceneName)
        {
            PlayerHealth.ResetPersistentHealth();
            PlayerStamina.ResetPersistentStamina();
        }

        EnsureEventSystem();
        rootCanvas = EnsureCanvas();
        menuButtonsContainer = EnsureMenuButtonsContainer(rootCanvas.transform);
        EnsureStartButton(menuButtonsContainer.transform);
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
            if (SceneManager.GetActiveScene().name == StartSceneName)
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
            UpdateStatusText($"{PlayerInputBindings.GetKeyDisplayName(pressedKey)} is already used for {PlayerInputBindings.GetActionLabel(duplicateAction)}.");
            return;
        }

        PlayerInputBindings.SetKey(pendingBindingAction, pressedKey);
        isWaitingForBinding = false;
        RefreshBindingValueTexts();
        UpdateStatusText($"{PlayerInputBindings.GetActionLabel(pendingBindingAction)} is now set to {PlayerInputBindings.GetKeyDisplayName(pressedKey)}.");
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
            "Audio Settings",
            38,
            FontStyle.Bold,
            TextAnchor.MiddleCenter,
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -56f),
            new Vector2(620f, 60f),
            new Color(0.13f, 0.2f, 0.14f, 1f));

        CreateTextElement(
            windowObject.transform,
            "OptionsSubtitle",
            "Adjust the volume levels for background music and sound effects.",
            21,
            FontStyle.Normal,
            TextAnchor.MiddleCenter,
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -112f),
            new Vector2(640f, 56f),
            new Color(0.26f, 0.32f, 0.27f, 1f));

        CreateAudioSliderRow(
            windowObject.transform,
            "BackgroundMusic",
            "Background Music",
            28f,
            out backgroundMusicSlider,
            out backgroundMusicValueText);

        CreateAudioSliderRow(
            windowObject.transform,
            "SoundEffect",
            "Sound Effects",
            -54f,
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

        Button closeButton = CreateButton(
            windowObject.transform,
            "OptionsCloseButton",
            "Close",
            OptionsCloseButtonPosition,
            new Vector2(240f, 64f),
            new Color(0.16f, 0.44f, 0.25f, 1f));
        SetBottomAnchoredRect(closeButton.GetComponent<RectTransform>());
        closeButton.onClick.AddListener(CloseOptionsPanel);
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
            "Set Your Controls",
            38,
            FontStyle.Bold,
            TextAnchor.MiddleCenter,
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -60f),
            new Vector2(760f, 60f),
            new Color(0.13f, 0.2f, 0.14f, 1f));

        CreateTextElement(
            windowObject.transform,
            "Subtitle",
            "Press each button to change a key. Press Escape to cancel a key selection.",
            22,
            FontStyle.Normal,
            TextAnchor.MiddleCenter,
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -118f),
            new Vector2(760f, 56f),
            new Color(0.26f, 0.32f, 0.27f, 1f));

        IReadOnlyList<InputActionType> actions = PlayerInputBindings.Actions;
        RectTransform bindingContentTransform = CreateBindingScrollArea(windowObject.transform);
        for (int i = 0; i < actions.Count; i++)
        {
            CreateBindingRow(bindingContentTransform, actions[i], KeyBindingTopPadding + (KeyBindingRowSpacing * i));
        }
        ConfigureBindingScrollContent(bindingContentTransform, actions.Count);

        statusText = CreateTextElement(
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

        Button cancelButton = CreateButton(
            windowObject.transform,
            "CancelButton",
            "Cancel",
            CancelButtonPosition,
            new Vector2(220f, 64f),
            new Color(0.52f, 0.57f, 0.52f, 1f));
        SetBottomAnchoredRect(cancelButton.GetComponent<RectTransform>());
        cancelButton.onClick.AddListener(CloseKeyMappingPanel);

        Button confirmButton = CreateButton(
            windowObject.transform,
            "ConfirmButton",
            "Confirm",
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

    private RectTransform CreateBindingScrollArea(Transform parent)
    {
        GameObject scrollAreaObject = new GameObject(
            KeyBindingScrollAreaObjectName,
            typeof(RectTransform),
            typeof(Image),
            typeof(ScrollRect));

        scrollAreaObject.transform.SetParent(parent, false);

        RectTransform scrollAreaRectTransform = scrollAreaObject.GetComponent<RectTransform>();
        scrollAreaRectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        scrollAreaRectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        scrollAreaRectTransform.pivot = new Vector2(0.5f, 0.5f);
        scrollAreaRectTransform.anchoredPosition = KeyBindingScrollAreaPosition;
        scrollAreaRectTransform.sizeDelta = KeyBindingScrollAreaSize;

        Image scrollAreaImage = scrollAreaObject.GetComponent<Image>();
        scrollAreaImage.color = new Color(0.86f, 0.89f, 0.82f, 0.7f);

        GameObject viewportObject = new GameObject(
            KeyBindingViewportObjectName,
            typeof(RectTransform),
            typeof(Image),
            typeof(RectMask2D));

        viewportObject.transform.SetParent(scrollAreaObject.transform, false);

        RectTransform viewportRectTransform = viewportObject.GetComponent<RectTransform>();
        viewportRectTransform.anchorMin = Vector2.zero;
        viewportRectTransform.anchorMax = Vector2.one;
        viewportRectTransform.offsetMin = new Vector2(18f, 18f);
        viewportRectTransform.offsetMax = new Vector2(-18f, -18f);

        Image viewportImage = viewportObject.GetComponent<Image>();
        viewportImage.color = new Color(1f, 1f, 1f, 0.02f);

        GameObject contentObject = new GameObject(KeyBindingContentObjectName, typeof(RectTransform));
        contentObject.transform.SetParent(viewportObject.transform, false);

        RectTransform contentRectTransform = contentObject.GetComponent<RectTransform>();
        contentRectTransform.anchorMin = new Vector2(0.5f, 1f);
        contentRectTransform.anchorMax = new Vector2(0.5f, 1f);
        contentRectTransform.pivot = new Vector2(0.5f, 1f);
        contentRectTransform.anchoredPosition = Vector2.zero;
        contentRectTransform.sizeDelta = new Vector2(680f, KeyBindingMinimumContentHeight);

        bindingScrollRect = scrollAreaObject.GetComponent<ScrollRect>();
        bindingScrollRect.viewport = viewportRectTransform;
        bindingScrollRect.content = contentRectTransform;
        bindingScrollRect.horizontal = false;
        bindingScrollRect.vertical = true;
        bindingScrollRect.movementType = ScrollRect.MovementType.Clamped;
        bindingScrollRect.scrollSensitivity = 28f;

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
        rowRectTransform.anchorMin = new Vector2(0.5f, 1f);
        rowRectTransform.anchorMax = new Vector2(0.5f, 1f);
        rowRectTransform.pivot = new Vector2(0.5f, 1f);
        rowRectTransform.anchoredPosition = new Vector2(0f, -topOffset);
        rowRectTransform.sizeDelta = new Vector2(680f, 56f);

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
            bindingValueTexts[action] = bindingButtonText;
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

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!isPauseMenu)
        {
            return;
        }

        EnsureEventSystem();
        SetKeyMappingPanelVisible(false);
        SetOptionsPanelVisible(false);
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
        SetOptionsPanelVisible(false);
        RefreshMenuButtons();
        SetMenuButtonsInteractable(true);
    }

    private void BeginRebind(InputActionType action)
    {
        pendingBindingAction = action;
        isWaitingForBinding = true;
        RefreshBindingValueTexts();

        if (bindingValueTexts.TryGetValue(action, out Text bindingValueText))
        {
            bindingValueText.text = "Press key...";
        }

        UpdateStatusText($"Press a key for {PlayerInputBindings.GetActionLabel(action)}.");
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
            UpdateStatusText("Finish the current key selection before continuing.");
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
        foreach (KeyValuePair<InputActionType, Text> entry in bindingValueTexts)
        {
            if (entry.Value == null)
            {
                continue;
            }

            entry.Value.text = PlayerInputBindings.GetKeyDisplayName(entry.Key);
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
        if (statusText != null)
        {
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
    }

    private void SetMenuButtonsVisible(bool isVisible)
    {
        if (menuButtonsContainer != null)
        {
            menuButtonsContainer.SetActive(isVisible);
        }
    }

    private void ResetBindingScrollPosition()
    {
        if (bindingScrollRect == null)
        {
            return;
        }

        Canvas.ForceUpdateCanvases();
        bindingScrollRect.verticalNormalizedPosition = 1f;
    }

    private void RefreshMenuButtons()
    {
        bool isConfigured = PlayerInputBindings.IsConfigured;
        if (reconfigureButton != null)
        {
            reconfigureButton.gameObject.SetActive(isConfigured);
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
        if (string.IsNullOrWhiteSpace(sceneToLoad))
        {
            Debug.LogError("StartSceneController does not have a destination scene configured.", this);
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(sceneToLoad))
        {
            Debug.LogError($"Scene '{sceneToLoad}' is not available in Build Settings.", this);
            return;
        }

        if (isPauseMenu)
        {
            HidePauseMenu();
        }

        SceneManager.LoadScene(sceneToLoad);
    }
}
