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
    private const int TransitionSortingOrder = 10000;
    private const float FadeOutDuration = 0.35f;
    private const float FadeInDuration = 0.25f;

    private static SceneFadeTransition instance;

    private Canvas transitionCanvas;
    private RawImage fadeImage;
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
                typeof(CanvasScaler));
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
    }

    private IEnumerator FadeAndLoadSceneRoutine(string sceneName)
    {
        isTransitioning = true;
        EnsureOverlay();

        yield return FadeRoutine(0f, 1f, FadeOutDuration);

        SceneManager.LoadScene(sceneName);

        yield return null;
        yield return FadeRoutine(1f, 0f, FadeInDuration);

        isTransitioning = false;
    }

    private IEnumerator FadeRoutine(float startAlpha, float endAlpha, float duration)
    {
        if (fadeImage == null)
        {
            yield break;
        }

        if (duration <= 0f)
        {
            SetFadeAlpha(endAlpha);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            SetFadeAlpha(Mathf.Lerp(startAlpha, endAlpha, progress));
            yield return null;
        }

        SetFadeAlpha(endAlpha);
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
    }
}
