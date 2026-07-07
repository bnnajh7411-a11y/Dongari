using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class SceneFadeTransition : MonoBehaviour
{
    private const string TransitionObjectName = "SceneFadeTransition";
    private const string TransitionCanvasName = "SceneFadeCanvas";
    private const string TransitionImageName = "FadeImage";
    private const string LoadingOverlayObjectName = "LoadingOverlay";
    private const string LoadingPanelObjectName = "LoadingPanel";
    private const string LoadingStatusTextObjectName = "LoadingStatusText";
    private const string LoadingProgressBarBackgroundObjectName = "LoadingProgressBarBackground";
    private const string LoadingProgressBarFillObjectName = "LoadingProgressBarFill";
    private const int TransitionSortingOrder = 10000;
    private const float FadeOutDuration = 0.35f;
    private const float FadeInDuration = 0.25f;
    private const float LoadingPanelWidth = 720f;
    private const float LoadingPanelHeight = 180f;
    private const float LoadingProgressBarWidth = 540f;
    private const float LoadingProgressBarHeight = 18f;
    private const int LoadingStatusFontSize = 34;

    private static readonly Color LoadingPanelColor = new Color(0.06f, 0.07f, 0.09f, 0.92f);
    private static readonly Color LoadingPanelOutlineColor = new Color(1f, 1f, 1f, 0.12f);
    private static readonly Color LoadingStatusTextColor = new Color(0.97f, 0.98f, 1f, 1f);
    private static readonly Color LoadingProgressBarBackgroundColor = new Color(1f, 1f, 1f, 0.12f);
    private static readonly Color LoadingProgressBarFillColor = new Color(0.97f, 0.98f, 1f, 0.96f);

    private static SceneFadeTransition instance;

    private Canvas transitionCanvas;
    private RawImage fadeImage;
    private CanvasGroup loadingOverlayGroup;
    private Image loadingProgressBarFillImage;
    private Text loadingStatusText;
    private Font builtinFont;
    private bool isTransitioning;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureExistsBeforeSceneLoad()
    {
        EnsureInstance();
    }

    public static bool LoadScene(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            return false;
        }

        SceneFadeTransition transition = EnsureInstance();
        if (transition == null || transition.isTransitioning)
        {
            return false;
        }

        transition.StartCoroutine(transition.FadeAndLoadSceneRoutine(sceneName));
        return true;
    }

    private static SceneFadeTransition EnsureInstance()
    {
        if (instance != null)
        {
            instance.EnsureOverlay();
            return instance;
        }

        SceneFadeTransition existingTransition = FindAnyObjectByType<SceneFadeTransition>();
        if (existingTransition != null)
        {
            instance = existingTransition;
            instance.Initialize();
            return instance;
        }

        GameObject transitionObject = new GameObject(TransitionObjectName);
        instance = transitionObject.AddComponent<SceneFadeTransition>();
        instance.Initialize();
        return instance;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        Initialize();
    }

    private void Initialize()
    {
        DontDestroyOnLoad(gameObject);
        EnsureOverlay();
    }

    private void EnsureOverlay()
    {
        if (transitionCanvas == null)
        {
            GameObject canvasObject = new GameObject(
                TransitionCanvasName,
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);

            transitionCanvas = canvasObject.GetComponent<Canvas>();
            transitionCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            transitionCanvas.sortingOrder = TransitionSortingOrder;

            CanvasScaler canvasScaler = canvasObject.GetComponent<CanvasScaler>();
            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            canvasScaler.matchWidthOrHeight = 0.5f;
        }

        if (fadeImage == null)
        {
            GameObject imageObject = new GameObject(TransitionImageName, typeof(RectTransform), typeof(RawImage));
            imageObject.transform.SetParent(transitionCanvas.transform, false);

            RectTransform imageRectTransform = imageObject.GetComponent<RectTransform>();
            imageRectTransform.anchorMin = Vector2.zero;
            imageRectTransform.anchorMax = Vector2.one;
            imageRectTransform.offsetMin = Vector2.zero;
            imageRectTransform.offsetMax = Vector2.zero;

            fadeImage = imageObject.GetComponent<RawImage>();
            fadeImage.texture = Texture2D.whiteTexture;
            fadeImage.color = Color.clear;
            fadeImage.raycastTarget = false;
        }

        if (loadingOverlayGroup == null)
        {
            GameObject loadingOverlayObject = new GameObject(LoadingOverlayObjectName, typeof(RectTransform), typeof(CanvasGroup));
            loadingOverlayObject.transform.SetParent(transitionCanvas.transform, false);

            RectTransform loadingOverlayRectTransform = loadingOverlayObject.GetComponent<RectTransform>();
            loadingOverlayRectTransform.anchorMin = Vector2.zero;
            loadingOverlayRectTransform.anchorMax = Vector2.one;
            loadingOverlayRectTransform.offsetMin = Vector2.zero;
            loadingOverlayRectTransform.offsetMax = Vector2.zero;

            loadingOverlayGroup = loadingOverlayObject.GetComponent<CanvasGroup>();
            loadingOverlayGroup.alpha = 0f;
            loadingOverlayGroup.interactable = false;
            loadingOverlayGroup.blocksRaycasts = false;

            GameObject loadingPanelObject = new GameObject(LoadingPanelObjectName, typeof(RectTransform), typeof(Image), typeof(Outline));
            loadingPanelObject.transform.SetParent(loadingOverlayObject.transform, false);

            RectTransform loadingPanelRectTransform = loadingPanelObject.GetComponent<RectTransform>();
            loadingPanelRectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            loadingPanelRectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            loadingPanelRectTransform.pivot = new Vector2(0.5f, 0.5f);
            loadingPanelRectTransform.sizeDelta = new Vector2(LoadingPanelWidth, LoadingPanelHeight);
            loadingPanelRectTransform.anchoredPosition = new Vector2(0f, -12f);

            Image loadingPanelImage = loadingPanelObject.GetComponent<Image>();
            loadingPanelImage.sprite = RuntimeUiSpriteUtility.GetWhiteSprite();
            loadingPanelImage.type = Image.Type.Simple;
            loadingPanelImage.color = LoadingPanelColor;
            loadingPanelImage.raycastTarget = false;

            Outline loadingPanelOutline = loadingPanelObject.GetComponent<Outline>();
            loadingPanelOutline.effectColor = LoadingPanelOutlineColor;
            loadingPanelOutline.effectDistance = new Vector2(2f, -2f);

            GameObject loadingStatusTextObject = new GameObject(LoadingStatusTextObjectName, typeof(RectTransform), typeof(Text));
            loadingStatusTextObject.transform.SetParent(loadingPanelObject.transform, false);

            RectTransform loadingStatusTextRectTransform = loadingStatusTextObject.GetComponent<RectTransform>();
            loadingStatusTextRectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            loadingStatusTextRectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            loadingStatusTextRectTransform.pivot = new Vector2(0.5f, 0.5f);
            loadingStatusTextRectTransform.sizeDelta = new Vector2(620f, 52f);
            loadingStatusTextRectTransform.anchoredPosition = new Vector2(0f, 38f);

            loadingStatusText = loadingStatusTextObject.GetComponent<Text>();
            loadingStatusText.font = GetBuiltinFont();
            loadingStatusText.fontSize = LoadingStatusFontSize;
            loadingStatusText.alignment = TextAnchor.MiddleCenter;
            loadingStatusText.color = LoadingStatusTextColor;
            loadingStatusText.horizontalOverflow = HorizontalWrapMode.Overflow;
            loadingStatusText.verticalOverflow = VerticalWrapMode.Overflow;
            loadingStatusText.raycastTarget = false;
            loadingStatusText.text = "\ub85c\ub529 \uc911... 0%";

            GameObject loadingProgressBarBackgroundObject = new GameObject(
                LoadingProgressBarBackgroundObjectName,
                typeof(RectTransform),
                typeof(Image));
            loadingProgressBarBackgroundObject.transform.SetParent(loadingPanelObject.transform, false);

            RectTransform loadingProgressBarBackgroundRectTransform = loadingProgressBarBackgroundObject.GetComponent<RectTransform>();
            loadingProgressBarBackgroundRectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            loadingProgressBarBackgroundRectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            loadingProgressBarBackgroundRectTransform.pivot = new Vector2(0.5f, 0.5f);
            loadingProgressBarBackgroundRectTransform.sizeDelta = new Vector2(LoadingProgressBarWidth, LoadingProgressBarHeight);
            loadingProgressBarBackgroundRectTransform.anchoredPosition = new Vector2(0f, -28f);

            Image loadingProgressBarBackgroundImage = loadingProgressBarBackgroundObject.GetComponent<Image>();
            loadingProgressBarBackgroundImage.sprite = RuntimeUiSpriteUtility.GetWhiteSprite();
            loadingProgressBarBackgroundImage.type = Image.Type.Simple;
            loadingProgressBarBackgroundImage.color = LoadingProgressBarBackgroundColor;
            loadingProgressBarBackgroundImage.raycastTarget = false;

            GameObject loadingProgressBarFillObject = new GameObject(
                LoadingProgressBarFillObjectName,
                typeof(RectTransform),
                typeof(Image));
            loadingProgressBarFillObject.transform.SetParent(loadingProgressBarBackgroundObject.transform, false);

            RectTransform loadingProgressBarFillRectTransform = loadingProgressBarFillObject.GetComponent<RectTransform>();
            loadingProgressBarFillRectTransform.anchorMin = Vector2.zero;
            loadingProgressBarFillRectTransform.anchorMax = Vector2.one;
            loadingProgressBarFillRectTransform.offsetMin = Vector2.zero;
            loadingProgressBarFillRectTransform.offsetMax = Vector2.zero;

            loadingProgressBarFillImage = loadingProgressBarFillObject.GetComponent<Image>();
            loadingProgressBarFillImage.sprite = RuntimeUiSpriteUtility.GetWhiteSprite();
            loadingProgressBarFillImage.type = Image.Type.Filled;
            loadingProgressBarFillImage.fillMethod = Image.FillMethod.Horizontal;
            loadingProgressBarFillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            loadingProgressBarFillImage.fillAmount = 0f;
            loadingProgressBarFillImage.color = LoadingProgressBarFillColor;
            loadingProgressBarFillImage.raycastTarget = false;
        }

        if (fadeImage != null)
        {
            fadeImage.transform.SetAsFirstSibling();
        }

        if (loadingOverlayGroup != null)
        {
            loadingOverlayGroup.transform.SetAsLastSibling();
        }
    }

    private IEnumerator FadeAndLoadSceneRoutine(string sceneName)
    {
        isTransitioning = true;
        EnsureOverlay();

        bool wasPaused = GamePauseState.IsPaused;
        float previousTimeScale = Time.timeScale;
        bool previousAudioPause = AudioListener.pause;

        PauseSceneActivity();
        SetLoadingOverlayVisible(false);
        SetLoadingProgress(0f);

        AsyncOperation loadOperation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        if (loadOperation == null)
        {
            Debug.LogError($"Failed to start async scene load for '{sceneName}'.", this);
            RestoreSceneActivity(wasPaused, previousTimeScale, previousAudioPause);
            SetLoadingOverlayVisible(false);
            SetLoadingProgress(0f);
            SetFadeAlpha(0f);
            isTransitioning = false;
            yield break;
        }

        loadOperation.allowSceneActivation = false;

        yield return FadeRoutine(0f, 1f, FadeOutDuration, false);

        SetLoadingOverlayVisible(true);
        yield return null;

        while (loadOperation.progress < 0.9f)
        {
            SetLoadingProgress(loadOperation.progress / 0.9f);
            yield return null;
        }

        SetLoadingProgress(1f);
        loadOperation.allowSceneActivation = true;

        while (!loadOperation.isDone)
        {
            yield return null;
        }

        RestoreSceneActivity(wasPaused, previousTimeScale, previousAudioPause);

        yield return null;
        yield return FadeRoutine(1f, 0f, FadeInDuration, true);

        SetLoadingOverlayVisible(false);
        SetLoadingProgress(0f);
        SetFadeAlpha(0f);
        isTransitioning = false;
    }

    private IEnumerator FadeRoutine(float startAlpha, float endAlpha, float duration, bool syncLoadingOverlay)
    {
        if (fadeImage == null)
        {
            yield break;
        }

        if (duration <= 0f)
        {
            SetFadeAlpha(endAlpha);
            if (syncLoadingOverlay)
            {
                SetLoadingOverlayAlpha(endAlpha);
            }
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float alpha = Mathf.Lerp(startAlpha, endAlpha, progress);
            SetFadeAlpha(alpha);
            if (syncLoadingOverlay)
            {
                SetLoadingOverlayAlpha(alpha);
            }
            yield return null;
        }

        SetFadeAlpha(endAlpha);
        if (syncLoadingOverlay)
        {
            SetLoadingOverlayAlpha(endAlpha);
        }
    }

    private void SetFadeAlpha(float alpha)
    {
        if (fadeImage == null)
        {
            return;
        }

        Color color = fadeImage.color;
        color.r = 0f;
        color.g = 0f;
        color.b = 0f;
        color.a = Mathf.Clamp01(alpha);
        fadeImage.color = color;
        fadeImage.raycastTarget = color.a > 0.001f;
    }

    private void SetLoadingOverlayVisible(bool isVisible)
    {
        if (loadingOverlayGroup == null)
        {
            return;
        }

        loadingOverlayGroup.alpha = isVisible ? 1f : 0f;
    }

    private void SetLoadingOverlayAlpha(float alpha)
    {
        if (loadingOverlayGroup == null)
        {
            return;
        }

        loadingOverlayGroup.alpha = Mathf.Clamp01(alpha);
    }

    private void SetLoadingProgress(float progress)
    {
        float clampedProgress = Mathf.Clamp01(progress);

        if (loadingProgressBarFillImage != null)
        {
            loadingProgressBarFillImage.fillAmount = clampedProgress;
        }

        if (loadingStatusText != null)
        {
            loadingStatusText.text = $"\ub85c\ub529 \uc911... {Mathf.RoundToInt(clampedProgress * 100f)}%";
        }
    }

    private void PauseSceneActivity()
    {
        GamePauseState.SetPaused(true);
        Time.timeScale = 0f;
        AudioListener.pause = true;
    }

    private void RestoreSceneActivity(bool wasPaused, float previousTimeScale, bool previousAudioPause)
    {
        GamePauseState.SetPaused(wasPaused);
        Time.timeScale = previousTimeScale;
        AudioListener.pause = previousAudioPause;
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
            Debug.LogError("Could not load the built-in runtime font for the transition UI.", this);
        }

        return builtinFont;
    }
}
