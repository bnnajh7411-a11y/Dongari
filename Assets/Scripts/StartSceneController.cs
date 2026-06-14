using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class StartSceneController : MonoBehaviour
{
    private const string EventSystemObjectName = "EventSystem";
    private const string CanvasObjectName = "StartCanvas";
    private const string ButtonObjectName = "StartButton";
    private const string ReconfigureButtonObjectName = "ReconfigureButton";
    private const string ButtonLabelObjectName = "Label";
    private const string KeyMappingPanelObjectName = "KeyMappingPanel";
    private const string KeyMappingWindowObjectName = "KeyMappingWindow";
    private const string KeyBindingScrollAreaObjectName = "KeyBindingScrollArea";
    private const string KeyBindingViewportObjectName = "KeyBindingViewport";
    private const string KeyBindingContentObjectName = "KeyBindingContent";
    private const string DefaultStatusText = "";
    private static readonly Vector2 SingleButtonPosition = Vector2.zero;
    private static readonly Vector2 StartButtonWithReconfigurePosition = new Vector2(0f, 56f);
    private static readonly Vector2 ReconfigureButtonPosition = new Vector2(0f, -56f);
    private static readonly Vector2 KeyBindingScrollAreaSize = new Vector2(760f, 340f);
    private static readonly Vector2 KeyBindingScrollAreaPosition = new Vector2(0f, -24f);
    private static readonly Vector2 KeyBindingStatusPosition = new Vector2(0f, 28f);
    private static readonly Vector2 CancelButtonPosition = new Vector2(-150f, 96f);
    private static readonly Vector2 ConfirmButtonPosition = new Vector2(150f, 96f);
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

    private readonly Dictionary<InputActionType, Text> bindingValueTexts = new Dictionary<InputActionType, Text>();

    private Button startButton;
    private Button reconfigureButton;
    private Canvas rootCanvas;
    private Font builtinFont;
    private GameObject keyMappingPanel;
    private ScrollRect bindingScrollRect;
    private Text statusText;
    private bool isWaitingForBinding;
    private bool shouldLoadSceneAfterConfirm;
    private InputActionType pendingBindingAction;

    private void Awake()
    {
        EnsureEventSystem();
        rootCanvas = EnsureCanvas();
        EnsureStartButton(rootCanvas.transform);
        EnsureReconfigureButton(rootCanvas.transform);
        EnsureKeyMappingPanel(rootCanvas.transform);
        SetKeyMappingPanelVisible(false);
        RefreshMenuButtons();
        RefreshBindingValueTexts();
        UpdateStatusText(DefaultStatusText);
    }

    private void Update()
    {
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
        Canvas existingCanvas = FindAnyObjectByType<Canvas>();
        if (existingCanvas != null)
        {
            return existingCanvas;
        }

        GameObject canvasObject = new GameObject(
            CanvasObjectName,
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler canvasScaler = canvasObject.GetComponent<CanvasScaler>();
        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        canvasScaler.matchWidthOrHeight = 0.5f;

        return canvas;
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
            SingleButtonPosition,
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
            ReconfigureButtonPosition,
            new Vector2(280f, 68f),
            new Color(0.74f, 0.81f, 0.72f, 1f));
        reconfigureButton.onClick.AddListener(HandleReconfigureButtonPressed);

        Text label = reconfigureButton.GetComponentInChildren<Text>();
        if (label != null)
        {
            label.color = new Color(0.12f, 0.22f, 0.16f, 1f);
        }
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

    private void CloseKeyMappingPanel()
    {
        CancelPendingRebind();
        SetKeyMappingPanelVisible(false);
        shouldLoadSceneAfterConfirm = false;
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

        if (startButton != null)
        {
            RectTransform startButtonRect = startButton.GetComponent<RectTransform>();
            if (startButtonRect != null)
            {
                startButtonRect.anchoredPosition = isConfigured
                    ? StartButtonWithReconfigurePosition
                    : SingleButtonPosition;
            }
        }

        if (reconfigureButton != null)
        {
            reconfigureButton.gameObject.SetActive(isConfigured);

            RectTransform reconfigureButtonRect = reconfigureButton.GetComponent<RectTransform>();
            if (reconfigureButtonRect != null)
            {
                reconfigureButtonRect.anchoredPosition = ReconfigureButtonPosition;
            }
        }
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

        SceneManager.LoadScene(sceneToLoad);
    }
}
