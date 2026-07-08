using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class ResultSceneController : MonoBehaviour
{
    private const string StartSceneName = "Start";
    private const string CreditsMusicAudioSourceObjectName = "CreditsMusicAudioSource";
    private const string CreditsMusicResourcesPath = "Audios/maksymmalko-historical-history-music-254182";
    private const string EventSystemObjectName = "EventSystem";
    private const string CanvasObjectName = "ResultCanvas";
    private const string BackgroundObjectName = "Background";
    private const string PanelObjectName = "ResultPanel";
    private const string CreditsViewportObjectName = "CreditsViewport";
    private const string CreditsContentObjectName = "CreditsContent";
    private const string FooterGroupObjectName = "FooterGroup";
    private const string CreditsSummaryObjectName = "CollectedItemsSummary";
    private const string TitleObjectName = "Title";
    private const string BodyObjectName = "Body";
    private const string ButtonObjectName = "StartButton";
    private const string GameOverTitleText = "\uc548\uc804\ud55c\u0020\uacf3\uc5d0\u0020\ub3c4\ucc29\ud558\uc9c0\u0020\ubabb\ud588\uc2b5\ub2c8\ub2e4";
    private const string CreditsBodyText = "\uc548\uc804\ud55c\u0020\uacf3\uc5d0\u0020\ub3c4\ucc29\ud588\uc2b5\ub2c8\ub2e4\u000d";
    private const string EmptyBodyText = "";
    private const string StartButtonText = "\ud0c0\uc774\ud2c0\ub85c";
    private const string CreditsCollectedItemsSummaryFormat = "\ud68d\ub4dd\ud55c\u0020\uc544\uc774\ud15c\u0020{0}/12";
    private const float GameOverPanelWidth = 700f;
    private const float GameOverPanelHeight = 320f;
    private const float GameOverTitleWidth = 640f;
    private const float GameOverTitleHeight = 96f;
    private const float GameOverFooterWidth = 420f;
    private const float GameOverFooterHeight = 120f;
    private const float GameOverFooterY = -86f;
    private const float GameOverTitleY = 40f;
    private const float CreditsScrollSpeed = 82f;
    private const float CreditsFastScrollMultiplier = 3.5f;
    private const float CreditsScrollPadding = 56f;
    private const float CreditsItemSize = 118f;
    private const float CreditsItemSpacing = 38f;
    private const float CreditsTextToItemsSpacing = 76f;
    private const float CreditsItemHorizontalPadding = 18f;
    private const float CreditsItemTextSpacing = 28f;
    private const float CreditsItemTextVerticalPadding = 14f;
    private const int CreditsItemTextFontSize = 30;
    private const int CreditsSummaryFontSize = 38;
    private const float FooterRevealDuration = 0.45f;
    private const int CanvasSortingOrder = 250;
    private static readonly Color GlassButtonColor = new Color(0.18f, 0.19f, 0.22f, 0.64f);
    private static readonly Color GlassPanelColor = new Color(0.14f, 0.15f, 0.18f, 0.76f);
    private static readonly Color GlassTextColor = new Color(0.97f, 0.98f, 1f, 1f);
    private static readonly Color GlassMutedTextColor = new Color(0.9f, 0.93f, 0.98f, 0.92f);
    private static readonly Color GlassOutlineColor = new Color(1f, 1f, 1f, 0.12f);

    private Canvas rootCanvas;
    private AudioSource creditsMusicAudioSource;
    private AudioClip creditsMusicClip;
    private Image backgroundImage;
    private Image panelImage;
    private Outline panelOutline;
    private RectTransform panelRectTransform;
    private Text titleText;
    private RectTransform titleRectTransform;
    private RectTransform creditsViewportRectTransform;
    private RectTransform creditsContentRectTransform;
    private Text bodyText;
    private RectTransform bodyRectTransform;
    private RectTransform footerRectTransform;
    private CanvasGroup footerCanvasGroup;
    private Text creditsSummaryText;
    private RectTransform creditsSummaryRectTransform;
    private Button startButton;
    private RectTransform startButtonRectTransform;
    private bool isLoadingStartScene;
    private bool isCreditsMode;
    private bool creditsScrollComplete;
    private float creditsEndY;
    private readonly List<GameObject> creditItemObjects = new List<GameObject>();

    private void Awake()
    {
        GamePauseState.SetPaused(false);
        Time.timeScale = 1f;

        EnsureEventSystem();
        rootCanvas = EnsureCanvas();
        BuildLayout();
        EnsureCreditsMusicAudioSource();
        ApplyDisplay(ResultSceneState.ConsumePendingDisplayMode(ResultSceneState.DisplayMode.GameOver));
    }

    private void OnDestroy()
    {
        if (creditsMusicAudioSource != null && creditsMusicAudioSource.isPlaying)
        {
            creditsMusicAudioSource.Stop();
        }

        if (startButton != null)
        {
            startButton.onClick.RemoveListener(HandleStartButtonPressed);
        }
    }

    private void Update()
    {
        if (isLoadingStartScene)
        {
            return;
        }

        if (isCreditsMode)
        {
            UpdateCreditsRoll();
        }

        if (!CanReturnToTitle())
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Return)
            || Input.GetKeyDown(KeyCode.Space)
            || PlayerInputBindings.WasInteractPressedThisFrame())
        {
            LoadStartScene();
        }
    }

    private void HandleStartButtonPressed()
    {
        LoadStartScene();
    }

    private void LoadStartScene()
    {
        if (isLoadingStartScene)
        {
            return;
        }

        isLoadingStartScene = SceneFadeTransition.LoadScene(StartSceneName);
    }

    private void ApplyDisplay(ResultSceneState.DisplayMode displayMode)
    {
        isCreditsMode = displayMode == ResultSceneState.DisplayMode.Credits;
        creditsScrollComplete = false;
        ApplyLayoutForDisplay(isCreditsMode);
        UpdateCreditsMusicPlayback();

        if (backgroundImage != null)
        {
            backgroundImage.color = isCreditsMode
                ? new Color(0.03f, 0.03f, 0.05f, 1f)
                : new Color(0.08f, 0.09f, 0.11f, 1f);
        }

        if (panelImage != null)
        {
            panelImage.color = isCreditsMode
                ? new Color(0f, 0f, 0f, 0f)
                : GlassPanelColor;
        }

        if (panelOutline != null)
        {
            panelOutline.enabled = !isCreditsMode;
            panelOutline.effectColor = GlassOutlineColor;
            panelOutline.effectDistance = new Vector2(2f, -2f);
        }

        if (titleText != null)
        {
            titleText.gameObject.SetActive(!isCreditsMode);
            titleText.text = GameOverTitleText;
            titleText.fontSize = 46;
            titleText.color = GlassTextColor;
        }

        if (bodyText != null)
        {
            bodyText.text = isCreditsMode ? CreditsBodyText : EmptyBodyText;
            bodyText.color = isCreditsMode
                ? GlassTextColor
                : GlassMutedTextColor;
            bodyText.fontSize = isCreditsMode ? 56 : 28;
        }

        if (creditsSummaryText != null)
        {
            creditsSummaryText.gameObject.SetActive(isCreditsMode);
            creditsSummaryText.text = string.Empty;
            creditsSummaryText.color = GlassTextColor;
        }


        if (isCreditsMode)
        {
            ConfigureCreditsRoll();
            SetFooterState(0f);
        }
        else
        {
            if (bodyRectTransform != null)
            {
                bodyRectTransform.anchoredPosition = Vector2.zero;
            }

            if (creditsContentRectTransform != null)
            {
                creditsContentRectTransform.anchoredPosition = Vector2.zero;
            }

            SetFooterState(1f);
        }
    }

    private void EnsureCreditsMusicAudioSource()
    {
        if (creditsMusicAudioSource == null)
        {
            Transform existingAudioSourceTransform = transform.Find(CreditsMusicAudioSourceObjectName);
            if (existingAudioSourceTransform != null)
            {
                creditsMusicAudioSource = existingAudioSourceTransform.GetComponent<AudioSource>();
            }
        }

        if (creditsMusicAudioSource == null)
        {
            GameObject audioSourceObject = new GameObject(CreditsMusicAudioSourceObjectName);
            audioSourceObject.transform.SetParent(transform, false);
            creditsMusicAudioSource = audioSourceObject.AddComponent<AudioSource>();
        }

        if (creditsMusicAudioSource == null)
        {
            return;
        }

        if (creditsMusicClip == null)
        {
            creditsMusicClip = Resources.Load<AudioClip>(CreditsMusicResourcesPath);
            if (creditsMusicClip == null)
            {
                Debug.LogWarning($"Could not load credits music clip at Resources path '{CreditsMusicResourcesPath}'.", this);
            }
        }

        creditsMusicAudioSource.playOnAwake = false;
        creditsMusicAudioSource.loop = true;
        creditsMusicAudioSource.spatialBlend = 0f;
        creditsMusicAudioSource.clip = creditsMusicClip;

        SceneAudioSource sceneAudioSource = creditsMusicAudioSource.GetComponent<SceneAudioSource>();
        if (sceneAudioSource == null)
        {
            sceneAudioSource = creditsMusicAudioSource.gameObject.AddComponent<SceneAudioSource>();
        }

        if (sceneAudioSource != null)
        {
            sceneAudioSource.SetConfiguredValues(AudioChannelType.BackgroundMusic, 1f);
        }
    }

    private void UpdateCreditsMusicPlayback()
    {
        EnsureCreditsMusicAudioSource();
        if (creditsMusicAudioSource == null || creditsMusicAudioSource.clip == null)
        {
            return;
        }

        if (!isCreditsMode)
        {
            if (creditsMusicAudioSource.isPlaying)
            {
                creditsMusicAudioSource.Stop();
            }

            return;
        }

        if (!creditsMusicAudioSource.isPlaying)
        {
            creditsMusicAudioSource.Play();
        }
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
        bool createdCanvas;
        Canvas canvas = RuntimeGaugeUiUtility.GetOrCreateOverlayCanvas(
            null,
            CanvasObjectName,
            CanvasSortingOrder,
            out createdCanvas);
        if (canvas != null && canvas.GetComponent<GraphicRaycaster>() == null)
        {
            canvas.gameObject.AddComponent<GraphicRaycaster>();
        }

        return canvas;
    }

    private void BuildLayout()
    {
        if (rootCanvas == null)
        {
            return;
        }

        Sprite uiSprite = RuntimeUiSpriteUtility.GetWhiteSprite();
        backgroundImage = CreateFullscreenImage(rootCanvas.transform, BackgroundObjectName, uiSprite);
        panelImage = CreatePanel(rootCanvas.transform, uiSprite);
        panelRectTransform = panelImage.GetComponent<RectTransform>();
        panelOutline = panelImage.GetComponent<Outline>();
        titleText = CreateText(
            panelImage.transform,
            TitleObjectName,
            new Vector2(0f, GameOverTitleY),
            new Vector2(GameOverTitleWidth, GameOverTitleHeight),
            46,
            FontStyle.Bold,
            TextAnchor.MiddleCenter);
        titleRectTransform = titleText.GetComponent<RectTransform>();
        creditsViewportRectTransform = CreateCreditsViewport(panelImage.transform);
        creditsContentRectTransform = CreateCreditsContent(creditsViewportRectTransform);
        bodyText = CreateText(
            creditsContentRectTransform,
            BodyObjectName,
            Vector2.zero,
            new Vector2(500f, 220f),
            56,
            FontStyle.Bold,
            TextAnchor.MiddleCenter);
        bodyRectTransform = bodyText.GetComponent<RectTransform>();
        footerRectTransform = CreateFooterGroup(panelImage.transform);
        creditsSummaryText = CreateText(
            footerRectTransform,
            CreditsSummaryObjectName,
            new Vector2(0f, 42f),
            new Vector2(440f, 48f),
            CreditsSummaryFontSize,
            FontStyle.Bold,
            TextAnchor.MiddleCenter);
        creditsSummaryRectTransform = creditsSummaryText.GetComponent<RectTransform>();
        creditsSummaryText.color = GlassTextColor;
        creditsSummaryText.text = string.Empty;
        startButton = CreateButton(footerRectTransform, uiSprite, new Vector2(0f, -12f));
        startButtonRectTransform = startButton.GetComponent<RectTransform>();
    }

    private Image CreateFullscreenImage(Transform parent, string objectName, Sprite sprite)
    {
        GameObject imageObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(Image));
        imageObject.transform.SetParent(parent, false);

        RectTransform imageRectTransform = imageObject.GetComponent<RectTransform>();
        ConfigureStretchRect(imageRectTransform, Vector2.zero, Vector2.zero);

        Image image = imageObject.GetComponent<Image>();
        image.sprite = sprite;
        image.type = Image.Type.Simple;
        image.raycastTarget = false;
        return image;
    }

    private Image CreatePanel(Transform parent, Sprite sprite)
    {
        GameObject panelObject = new GameObject(
            PanelObjectName,
            typeof(RectTransform),
            typeof(Image),
            typeof(Outline));
        panelObject.transform.SetParent(parent, false);

        RectTransform panelRectTransform = panelObject.GetComponent<RectTransform>();
        ConfigureAnchoredRect(
            panelRectTransform,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(GameOverPanelWidth, GameOverPanelHeight));

        Image panel = panelObject.GetComponent<Image>();
        panel.sprite = sprite;
        panel.type = Image.Type.Simple;

        Outline outline = panelObject.GetComponent<Outline>();
        outline.effectColor = GlassOutlineColor;
        outline.effectDistance = new Vector2(2f, -2f);
        return panel;
    }

    private RectTransform CreateCreditsViewport(Transform parent)
    {
        GameObject viewportObject = new GameObject(
            CreditsViewportObjectName,
            typeof(RectTransform),
            typeof(Image),
            typeof(RectMask2D));
        viewportObject.transform.SetParent(parent, false);

        RectTransform viewportRectTransform = viewportObject.GetComponent<RectTransform>();
        ConfigureAnchoredRect(
            viewportRectTransform,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0f, 18f),
            new Vector2(500f, 220f));

        Image viewportImage = viewportObject.GetComponent<Image>();
        viewportImage.color = new Color(0f, 0f, 0f, 0f);
        viewportImage.raycastTarget = false;
        return viewportRectTransform;
    }

    private RectTransform CreateCreditsContent(Transform parent)
    {
        GameObject contentObject = new GameObject(CreditsContentObjectName, typeof(RectTransform));
        contentObject.transform.SetParent(parent, false);

        RectTransform contentRectTransform = contentObject.GetComponent<RectTransform>();
        ConfigureAnchoredRect(
            contentRectTransform,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(500f, 220f));
        return contentRectTransform;
    }

    private RectTransform CreateFooterGroup(Transform parent)
    {
        GameObject footerObject = new GameObject(
            FooterGroupObjectName,
            typeof(RectTransform),
            typeof(CanvasGroup));
        footerObject.transform.SetParent(parent, false);

        RectTransform rectTransform = footerObject.GetComponent<RectTransform>();
        ConfigureAnchoredRect(
            rectTransform,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0f, GameOverFooterY),
            new Vector2(GameOverFooterWidth, GameOverFooterHeight));

        footerCanvasGroup = footerObject.GetComponent<CanvasGroup>();
        return rectTransform;
    }

    private Text CreateText(
        Transform parent,
        string objectName,
        Vector2 anchoredPosition,
        Vector2 sizeDelta,
        int fontSize,
        FontStyle fontStyle,
        TextAnchor alignment)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(parent, false);

        RectTransform textRectTransform = textObject.GetComponent<RectTransform>();
        ConfigureAnchoredRect(
            textRectTransform,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            anchoredPosition,
            sizeDelta);

        Text text = textObject.GetComponent<Text>();
        text.font = RuntimeGaugeUiUtility.GetBuiltinFont();
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.lineSpacing = 1.15f;
        text.raycastTarget = false;
        return text;
    }

    private Button CreateButton(Transform parent, Sprite sprite, Vector2 anchoredPosition)
    {
        GameObject buttonObject = new GameObject(
            ButtonObjectName,
            typeof(RectTransform),
            typeof(Image),
            typeof(Button),
            typeof(Outline));
        buttonObject.transform.SetParent(parent, false);

        RectTransform buttonRectTransform = buttonObject.GetComponent<RectTransform>();
        ConfigureAnchoredRect(
            buttonRectTransform,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            anchoredPosition,
            new Vector2(230f, 68f));

        Image buttonImage = buttonObject.GetComponent<Image>();
        buttonImage.sprite = sprite;
        buttonImage.type = Image.Type.Simple;
        buttonImage.color = GlassButtonColor;

        Outline buttonOutline = buttonObject.GetComponent<Outline>();
        buttonOutline.effectColor = GlassOutlineColor;
        buttonOutline.effectDistance = new Vector2(1.5f, -1.5f);

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = buttonImage;
        button.colors = CreateButtonColors(buttonImage.color);
        button.onClick.AddListener(HandleStartButtonPressed);

        Text label = CreateText(
            buttonObject.transform,
            "Label",
            Vector2.zero,
            buttonRectTransform.sizeDelta,
            28,
            FontStyle.Bold,
            TextAnchor.MiddleCenter);
        label.text = StartButtonText;
        label.color = GlassTextColor;

        return button;
    }

    private void ApplyLayoutForDisplay(bool isCreditsDisplay)
    {
        if (panelRectTransform != null)
        {
            if (isCreditsDisplay)
            {
                ConfigureStretchRect(panelRectTransform, Vector2.zero, Vector2.zero);
                panelRectTransform.pivot = new Vector2(0.5f, 0.5f);
                panelRectTransform.anchoredPosition = Vector2.zero;
            }
            else
            {
                ConfigureAnchoredRect(
                    panelRectTransform,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    Vector2.zero,
                    new Vector2(GameOverPanelWidth, GameOverPanelHeight));
            }
        }

        if (titleRectTransform != null)
        {
            if (isCreditsDisplay)
            {
                ConfigureAnchoredRect(
                    titleRectTransform,
                    new Vector2(0.5f, 1f),
                    new Vector2(0.5f, 1f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0f, -72f),
                    new Vector2(760f, 72f));
            }
            else
            {
                ConfigureAnchoredRect(
                    titleRectTransform,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0f, GameOverTitleY),
                    new Vector2(GameOverTitleWidth, GameOverTitleHeight));
            }
        }

        if (creditsViewportRectTransform != null)
        {
            if (isCreditsDisplay)
            {
                creditsViewportRectTransform.gameObject.SetActive(true);
                ConfigureStretchRect(
                    creditsViewportRectTransform,
                    new Vector2(220f, 0f),
                    new Vector2(-220f, 0f));
                creditsViewportRectTransform.pivot = new Vector2(0.5f, 0.5f);
            }
            else
            {
                creditsViewportRectTransform.gameObject.SetActive(false);
                ConfigureAnchoredRect(
                    creditsViewportRectTransform,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0f, 18f),
                    new Vector2(500f, 220f));
            }
        }

        if (footerRectTransform != null)
        {
            if (isCreditsDisplay)
            {
                ConfigureAnchoredRect(
                    footerRectTransform,
                    new Vector2(0.5f, 0f),
                    new Vector2(0.5f, 0f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0f, 94f),
                    new Vector2(480f, 150f));
            }
            else
            {
                ConfigureAnchoredRect(
                    footerRectTransform,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0f, GameOverFooterY),
                    new Vector2(GameOverFooterWidth, GameOverFooterHeight));
            }
        }

        if (creditsSummaryRectTransform != null)
        {
            creditsSummaryRectTransform.anchoredPosition = isCreditsDisplay
                ? new Vector2(0f, 42f)
                : new Vector2(0f, 34f);
        }

        if (startButtonRectTransform != null)
        {
            startButtonRectTransform.anchoredPosition = isCreditsDisplay
                ? new Vector2(0f, -28f)
                : new Vector2(0f, -12f);
        }
    }

    private void ConfigureCreditsRoll()
    {
        if (creditsContentRectTransform == null
            || bodyRectTransform == null
            || creditsViewportRectTransform == null
            || bodyText == null)
        {
            return;
        }

        ClearCreditItems();
        Canvas.ForceUpdateCanvases();
        float viewportWidth = creditsViewportRectTransform.rect.width;
        float viewportHeight = creditsViewportRectTransform.rect.height;

        creditsContentRectTransform.sizeDelta = new Vector2(viewportWidth, creditsContentRectTransform.sizeDelta.y);
        bodyRectTransform.sizeDelta = new Vector2(viewportWidth, bodyRectTransform.sizeDelta.y);
        Canvas.ForceUpdateCanvases();

        float textHeight = Mathf.Max(bodyText.preferredHeight, 1f);
        CollectedPickupCreditsState.CreditEntry[] collectedEntries =
            CollectedPickupCreditsState.GetCollectedEntries();
        if (creditsSummaryText != null)
        {
            creditsSummaryText.text = string.Format(
                CreditsCollectedItemsSummaryFormat,
                collectedEntries.Length);
        }

        float[] itemHeights = new float[collectedEntries.Length];
        float itemsHeight = 0f;
        int visibleItemCount = 0;
        for (int i = 0; i < collectedEntries.Length; i++)
        {
            CollectedPickupCreditsState.CreditEntry entry = collectedEntries[i];
            if (entry == null || entry.Sprite == null)
            {
                continue;
            }

            itemHeights[i] = GetCreditItemHeight(entry, viewportWidth);
            itemsHeight += itemHeights[i];
            visibleItemCount++;
        }

        if (visibleItemCount > 0)
        {
            itemsHeight += ((visibleItemCount - 1) * CreditsItemSpacing) + CreditsTextToItemsSpacing;
        }

        float contentHeight = textHeight + itemsHeight;
        creditsContentRectTransform.sizeDelta = new Vector2(viewportWidth, contentHeight);
        bodyRectTransform.sizeDelta = new Vector2(viewportWidth, textHeight);
        bodyRectTransform.anchoredPosition = new Vector2(0f, (contentHeight * 0.5f) - (textHeight * 0.5f));

        float currentTop = (contentHeight * 0.5f) - textHeight;
        if (visibleItemCount > 0)
        {
            currentTop -= CreditsTextToItemsSpacing;
            int visibleItemIndex = 0;
            for (int i = 0; i < collectedEntries.Length; i++)
            {
                CollectedPickupCreditsState.CreditEntry entry = collectedEntries[i];
                if (entry == null || entry.Sprite == null)
                {
                    continue;
                }

                float itemHeight = itemHeights[i];
                float centerY = currentTop - (itemHeight * 0.5f);
                CreateCreditItem(entry, new Vector2(0f, centerY), viewportWidth, itemHeight);
                currentTop -= itemHeight;
                if (visibleItemIndex < visibleItemCount - 1)
                {
                    currentTop -= CreditsItemSpacing;
                }

                visibleItemIndex++;
            }
        }

        float startY = -(viewportHeight * 0.5f) - (contentHeight * 0.5f) - CreditsScrollPadding;
        creditsEndY = (viewportHeight * 0.5f) + (contentHeight * 0.5f) + CreditsScrollPadding;
        creditsContentRectTransform.anchoredPosition = new Vector2(0f, startY);
    }

    private void UpdateCreditsRoll()
    {
        if (creditsContentRectTransform == null || footerCanvasGroup == null)
        {
            return;
        }

        if (!creditsScrollComplete)
        {
            float currentScrollSpeed = CreditsScrollSpeed
                * (Input.GetKey(KeyCode.Space) ? CreditsFastScrollMultiplier : 1f);
            float nextY = creditsContentRectTransform.anchoredPosition.y + (currentScrollSpeed * Time.unscaledDeltaTime);
            if (nextY >= creditsEndY)
            {
                nextY = creditsEndY;
                creditsScrollComplete = true;
            }

            creditsContentRectTransform.anchoredPosition = new Vector2(
                creditsContentRectTransform.anchoredPosition.x,
                nextY);
        }

        if (!creditsScrollComplete)
        {
            return;
        }

        float nextAlpha = Mathf.MoveTowards(
            footerCanvasGroup.alpha,
            1f,
            Time.unscaledDeltaTime / FooterRevealDuration);
        SetFooterState(nextAlpha);
    }

    private float GetCreditItemHeight(CollectedPickupCreditsState.CreditEntry entry, float viewportWidth)
    {
        if (entry == null)
        {
            return CreditsItemSize;
        }

        float textHeight = GetCreditItemTextHeight(entry.Description, viewportWidth);
        return Mathf.Max(CreditsItemSize, textHeight + (CreditsItemTextVerticalPadding * 2f));
    }

    private float GetCreditItemTextHeight(string description, float viewportWidth)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return 0f;
        }

        Font font = RuntimeGaugeUiUtility.GetBuiltinFont();
        if (font == null)
        {
            return 0f;
        }

        TextGenerationSettings settings = new TextGenerationSettings
        {
            alignByGeometry = false,
            color = Color.white,
            font = font,
            fontSize = CreditsItemTextFontSize,
            fontStyle = FontStyle.Normal,
            generationExtents = new Vector2(GetCreditItemTextWidth(viewportWidth), 0f),
            horizontalOverflow = HorizontalWrapMode.Wrap,
            lineSpacing = 1.15f,
            pivot = Vector2.zero,
            resizeTextForBestFit = false,
            resizeTextMaxSize = CreditsItemTextFontSize,
            resizeTextMinSize = CreditsItemTextFontSize,
            richText = false,
            scaleFactor = 1f,
            textAnchor = TextAnchor.UpperLeft,
            updateBounds = true,
            verticalOverflow = VerticalWrapMode.Overflow
        };

        TextGenerator textGenerator = new TextGenerator();
        return Mathf.Ceil(textGenerator.GetPreferredHeight(description, settings));
    }

    private float GetCreditItemTextWidth(float viewportWidth)
    {
        float availableWidth = viewportWidth
            - (CreditsItemHorizontalPadding * 2f)
            - CreditsItemSize
            - CreditsItemTextSpacing;
        return Mathf.Max(availableWidth, 1f);
    }

    private void CreateCreditItem(
        CollectedPickupCreditsState.CreditEntry entry,
        Vector2 anchoredPosition,
        float viewportWidth,
        float itemHeight)
    {
        if (creditsContentRectTransform == null || entry == null || entry.Sprite == null)
        {
            return;
        }

        GameObject itemObject = new GameObject($"CreditItem_{creditItemObjects.Count}", typeof(RectTransform));
        itemObject.transform.SetParent(creditsContentRectTransform, false);

        RectTransform itemRectTransform = itemObject.GetComponent<RectTransform>();
        ConfigureAnchoredRect(
            itemRectTransform,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            anchoredPosition,
            new Vector2(viewportWidth, itemHeight));

        GameObject imageObject = new GameObject("Image", typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(itemObject.transform, false);

        RectTransform imageRectTransform = imageObject.GetComponent<RectTransform>();
        ConfigureAnchoredRect(
            imageRectTransform,
            new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f),
            new Vector2(CreditsItemHorizontalPadding, 0f),
            new Vector2(CreditsItemSize, CreditsItemSize));

        Image itemImage = imageObject.GetComponent<Image>();
        itemImage.sprite = entry.Sprite;
        itemImage.preserveAspect = true;
        itemImage.raycastTarget = false;
        itemImage.color = Color.white;

        GameObject descriptionObject = new GameObject("Description", typeof(RectTransform), typeof(Text));
        descriptionObject.transform.SetParent(itemObject.transform, false);

        RectTransform descriptionRectTransform = descriptionObject.GetComponent<RectTransform>();
        ConfigureAnchoredRect(
            descriptionRectTransform,
            new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f),
            new Vector2(CreditsItemHorizontalPadding + CreditsItemSize + CreditsItemTextSpacing, 0f),
            new Vector2(GetCreditItemTextWidth(viewportWidth), itemHeight));

        Text descriptionText = descriptionObject.GetComponent<Text>();
        descriptionText.font = RuntimeGaugeUiUtility.GetBuiltinFont();
        descriptionText.fontSize = CreditsItemTextFontSize;
        descriptionText.fontStyle = FontStyle.Normal;
        descriptionText.alignment = TextAnchor.MiddleLeft;
        descriptionText.horizontalOverflow = HorizontalWrapMode.Wrap;
        descriptionText.verticalOverflow = VerticalWrapMode.Overflow;
        descriptionText.lineSpacing = 1.15f;
        descriptionText.raycastTarget = false;
        descriptionText.color = new Color(0.95f, 0.95f, 0.91f, 1f);
        descriptionText.text = entry.Description;

        creditItemObjects.Add(itemObject);
    }

    private void ClearCreditItems()
    {
        for (int i = 0; i < creditItemObjects.Count; i++)
        {
            if (creditItemObjects[i] != null)
            {
                Destroy(creditItemObjects[i]);
            }
        }

        creditItemObjects.Clear();
    }

    private bool CanReturnToTitle()
    {
        return !isCreditsMode || (footerCanvasGroup != null && footerCanvasGroup.alpha >= 0.999f);
    }

    private void SetFooterState(float alpha)
    {
        if (footerCanvasGroup == null)
        {
            return;
        }

        footerCanvasGroup.alpha = Mathf.Clamp01(alpha);
        bool isInteractable = footerCanvasGroup.alpha >= 0.999f;
        footerCanvasGroup.interactable = isInteractable;
        footerCanvasGroup.blocksRaycasts = isInteractable;

        if (startButton != null)
        {
            startButton.interactable = isInteractable;
        }
    }

    private static void ConfigureAnchoredRect(
        RectTransform rectTransform,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPosition,
        Vector2 sizeDelta)
    {
        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;
        rectTransform.pivot = pivot;
        rectTransform.anchoredPosition = anchoredPosition;
        rectTransform.sizeDelta = sizeDelta;
    }

    private static void ConfigureStretchRect(
        RectTransform rectTransform,
        Vector2 offsetMin,
        Vector2 offsetMax)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = offsetMin;
        rectTransform.offsetMax = offsetMax;
    }

    private static ColorBlock CreateButtonColors(Color normalColor)
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
        colors.disabledColor = new Color(normalColor.r, normalColor.g, normalColor.b, normalColor.a * 0.45f);
        return colors;
    }
}

public static class ResultSceneState
{
    public const string ResultSceneName = "Result";

    public enum DisplayMode
    {
        GameOver,
        Credits
    }

    private static bool hasPendingDisplayMode;
    private static DisplayMode pendingDisplayMode;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        hasPendingDisplayMode = false;
        pendingDisplayMode = DisplayMode.GameOver;
    }

    public static bool LoadGameOverResult()
    {
        return LoadResult(DisplayMode.GameOver);
    }

    public static bool LoadCreditsResult()
    {
        return LoadResult(DisplayMode.Credits);
    }

    public static DisplayMode ConsumePendingDisplayMode(DisplayMode fallbackDisplayMode)
    {
        if (!hasPendingDisplayMode)
        {
            return fallbackDisplayMode;
        }

        DisplayMode displayMode = pendingDisplayMode;
        hasPendingDisplayMode = false;
        return displayMode;
    }

    private static bool LoadResult(DisplayMode displayMode)
    {
        hasPendingDisplayMode = true;
        pendingDisplayMode = displayMode;

        if (!Application.CanStreamedLevelBeLoaded(ResultSceneName))
        {
            Debug.LogError($"Scene '{ResultSceneName}' is not available in Build Settings.");
            return false;
        }

        if (displayMode == DisplayMode.GameOver
            && PlayerDeathSequenceController.PlayAndLoadScene(ResultSceneName))
        {
            return true;
        }

        return SceneFadeTransition.LoadScene(ResultSceneName);
    }
}
