using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Video;

[DisallowMultipleComponent]
public class IntroCutsceneController : MonoBehaviour
{
    public const string CutsceneSceneName = "IntroCutscene";

    private const string EventSystemObjectName = "CutsceneEventSystem";
    private const string CanvasObjectName = "CutsceneCanvas";
    private const string SkipButtonObjectName = "SkipButton";
    private const string SkipButtonLabelObjectName = "Label";
    private const float VideoPrepareTimeoutSeconds = 5f;

    private static string pendingNextSceneName;

    [SerializeField] private string fallbackNextSceneName = "Zoo";
    [SerializeField] private string skipButtonLabel = "SKIP";
    [SerializeField] private Color backgroundColor = Color.black;
    [SerializeField] private Color skipButtonColor = new Color(0f, 0f, 0f, 0.72f);
    [SerializeField] private Color skipButtonTextColor = Color.white;
    [SerializeField] private VideoClip introVideoClip;

    private Font builtinFont;
    private Camera cutsceneCamera;
    private Canvas cutsceneCanvas;
    private Button skipButton;
    private VideoPlayer videoPlayer;
    private Coroutine videoPrepareTimeoutCoroutine;
    private bool isLoadingNextScene;
    private bool isUsingVideoPlayback;

    public static void SetPendingNextScene(string sceneName)
    {
        pendingNextSceneName = sceneName;
    }

    private void Awake()
    {
        cutsceneCamera = EnsureMainCamera();
        PrimeVideoPlayerForManualPlayback();
        EnsureEventSystem();
        cutsceneCanvas = EnsureCanvas();
        skipButton = EnsureSkipButton(cutsceneCanvas.transform);
    }

    private void Start()
    {
        if (introVideoClip != null)
        {
            StartVideoPlayback();
            return;
        }

        StartImageCutscene();
    }

    private void OnDestroy()
    {
        UnregisterVideoCallbacks();
    }

    private void StartImageCutscene()
    {
        isUsingVideoPlayback = false;

        if (cutsceneCanvas == null)
        {
            cutsceneCanvas = EnsureCanvas();
        }

    }

    private void Update()
    {
        if (isLoadingNextScene || isUsingVideoPlayback || !ShouldAdvanceCutscene())
        {
            return;
        }
    }

    private void StartVideoPlayback()
    {
        if (cutsceneCamera == null)
        {
            Debug.LogError("IntroCutsceneController could not find or create a camera for video playback.", this);
            FallbackAfterVideoFailure();
            return;
        }

        isUsingVideoPlayback = true;

        videoPlayer = EnsureVideoPlayer();
        if (videoPlayer == null)
        {
            FallbackAfterVideoFailure();
            return;
        }

        videoPlayer.clip = introVideoClip;
        videoPlayer.Prepare();

        if (videoPlayer.isPrepared)
        {
            HandleVideoPrepared(videoPlayer);
            return;
        }

        RestartVideoPrepareTimeout();
    }

    private bool ShouldAdvanceCutscene()
    {
        bool mouseClick = Input.GetMouseButtonDown(0);
        // The whole cutscene is rendered with UI, so only the Skip button should block mouse-to-advance.
        if (mouseClick && IsPointerOverSkipButton())
        {
            return false;
        }

        return mouseClick
            || Input.GetKeyDown(KeyCode.Return)
            || Input.GetKeyDown(KeyCode.KeypadEnter)
            || Input.GetKeyDown(KeyCode.Space)
            || PlayerInputBindings.WasJumpPressedThisFrame()
            || PlayerInputBindings.WasInteractPressedThisFrame();
    }

    private bool IsPointerOverSkipButton()
    {
        if (skipButton == null)
        {
            return false;
        }

        RectTransform skipButtonRectTransform = skipButton.transform as RectTransform;
        if (skipButtonRectTransform == null)
        {
            return false;
        }

        return RectTransformUtility.RectangleContainsScreenPoint(skipButtonRectTransform, Input.mousePosition);
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

    private Camera EnsureMainCamera()
    {
        if (Camera.main != null)
        {
            Camera.main.backgroundColor = backgroundColor;
            Camera.main.clearFlags = CameraClearFlags.SolidColor;
            Camera.main.orthographic = true;
            return Camera.main;
        }

        GameObject cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";

        Camera cameraComponent = cameraObject.AddComponent<Camera>();
        cameraObject.AddComponent<AudioListener>();

        cameraComponent.backgroundColor = backgroundColor;
        cameraComponent.clearFlags = CameraClearFlags.SolidColor;
        cameraComponent.orthographic = true;
        return cameraComponent;
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

    private void PrimeVideoPlayerForManualPlayback()
    {
        if (introVideoClip == null)
        {
            return;
        }

        videoPlayer = EnsureVideoPlayer();
    }

    private VideoPlayer EnsureVideoPlayer()
    {
        if (videoPlayer == null)
        {
            videoPlayer = GetComponent<VideoPlayer>();
        }

        if (videoPlayer == null)
        {
            videoPlayer = gameObject.AddComponent<VideoPlayer>();
        }

        UnregisterVideoCallbacks();

        videoPlayer.source = VideoSource.VideoClip;
        videoPlayer.renderMode = VideoRenderMode.CameraNearPlane;
        videoPlayer.targetCamera = cutsceneCamera;
        videoPlayer.targetCameraAlpha = 1f;
        videoPlayer.aspectRatio = VideoAspectRatio.FitInside;
        videoPlayer.playOnAwake = false;
        videoPlayer.waitForFirstFrame = true;
        videoPlayer.isLooping = false;
        // The intro video is silent, so keep Unity's audio pipeline disabled.
        videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
        videoPlayer.skipOnDrop = true;
        videoPlayer.prepareCompleted += HandleVideoPrepared;
        videoPlayer.loopPointReached += HandleVideoPlaybackFinished;
        videoPlayer.errorReceived += HandleVideoErrorReceived;
        return videoPlayer;
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

        StopVideoPlayback();
        LoadNextScene();
    }

    private void HandleVideoPrepared(VideoPlayer source)
    {
        if (isLoadingNextScene || source != videoPlayer)
        {
            return;
        }

        CancelVideoPrepareTimeout();
        source.Play();
    }

    private void HandleVideoPlaybackFinished(VideoPlayer source)
    {
        if (isLoadingNextScene || source != videoPlayer)
        {
            return;
        }

        LoadNextScene();
    }

    private void HandleVideoErrorReceived(VideoPlayer source, string errorMessage)
    {
        if (source != videoPlayer)
        {
            return;
        }

        Debug.LogError($"Intro cutscene video playback failed: {errorMessage}", this);
        FallbackAfterVideoFailure();
    }

    private void FallbackAfterVideoFailure()
    {
        CancelVideoPrepareTimeout();
        StopVideoPlayback();
        isUsingVideoPlayback = false;

        LoadNextScene();
    }

    private void StopVideoPlayback()
    {
        CancelVideoPrepareTimeout();

        if (videoPlayer == null)
        {
            return;
        }

        if (videoPlayer.isPlaying || videoPlayer.isPrepared)
        {
            videoPlayer.Stop();
        }
    }

    private void UnregisterVideoCallbacks()
    {
        if (videoPlayer == null)
        {
            return;
        }

        videoPlayer.prepareCompleted -= HandleVideoPrepared;
        videoPlayer.loopPointReached -= HandleVideoPlaybackFinished;
        videoPlayer.errorReceived -= HandleVideoErrorReceived;
    }

    private void RestartVideoPrepareTimeout()
    {
        CancelVideoPrepareTimeout();
        videoPrepareTimeoutCoroutine = StartCoroutine(VideoPrepareTimeoutRoutine());
    }

    private void CancelVideoPrepareTimeout()
    {
        if (videoPrepareTimeoutCoroutine == null)
        {
            return;
        }

        StopCoroutine(videoPrepareTimeoutCoroutine);
        videoPrepareTimeoutCoroutine = null;
    }

    private IEnumerator VideoPrepareTimeoutRoutine()
    {
        yield return new WaitForSecondsRealtime(VideoPrepareTimeoutSeconds);

        if (isLoadingNextScene || !isUsingVideoPlayback || videoPlayer == null)
        {
            videoPrepareTimeoutCoroutine = null;
            yield break;
        }

        if (videoPlayer.isPrepared || videoPlayer.isPlaying)
        {
            videoPrepareTimeoutCoroutine = null;
            yield break;
        }

        videoPrepareTimeoutCoroutine = null;
        Debug.LogError("Intro cutscene video preparation timed out.", this);
        FallbackAfterVideoFailure();
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
