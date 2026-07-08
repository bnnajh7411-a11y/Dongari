using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

[DisallowMultipleComponent]
internal sealed class PlayerDeathSequenceController : MonoBehaviour
{
    private const string ControllerObjectName = "PlayerDeathSequenceController";
    private const string StartSceneName = "Start";
    private const string OverlayCanvasObjectName = "PlayerDeathOverlayCanvas";
    private const string OverlayPanelObjectName = "PlayerDeathOverlayPanel";
    private const string VolumeObjectName = "PlayerDeathVolume";
    private const int OverlayCanvasSortingOrder = 9990;
    private const float DeathSequenceDuration = 0.55f;
    private const float DeathOverlayMaxAlpha = 0.58f;
    private const float DeathShakeMagnitude = 0.22f;
    private const float DeathShakeFrequency = 28f;
    private const float DeathMotionBlurIntensity = 0.7f;
    private const float DeathVignetteIntensity = 0.34f;
    private const float DeathChromaticAberrationIntensity = 0.16f;
    private const float DeathSaturation = -28f;
    private const float DeathContrast = 10f;
    private const float DeathPostExposure = -0.12f;
    private const float DeathCleanupDelay = 0.45f;

    private static PlayerDeathSequenceController instance;

    private Canvas overlayCanvas;
    private Image overlayPanelImage;
    private GameObject volumeObject;
    private Volume volume;
    private VolumeProfile volumeProfile;
    private Camera targetCamera;
    private UniversalAdditionalCameraData cameraData;
    private bool originalRenderPostProcessing;
    private Vector3 cameraBasePosition;
    private float shakeSeed;
    private bool isSequenceRunning;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        instance = null;
    }

    public static bool PlayAndLoadScene(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            return false;
        }

        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"Scene '{sceneName}' is not available in Build Settings.");
            return false;
        }

        PlayerDeathSequenceController controller = EnsureInstance();
        if (controller == null || controller.isSequenceRunning)
        {
            return false;
        }

        controller.StartCoroutine(controller.PlaySequenceAndLoadSceneRoutine(sceneName));
        return true;
    }

    private static PlayerDeathSequenceController EnsureInstance()
    {
        if (instance != null)
        {
            return instance;
        }

        PlayerDeathSequenceController existingController = FindAnyObjectByType<PlayerDeathSequenceController>();
        if (existingController != null)
        {
            instance = existingController;
            return instance;
        }

        GameObject controllerObject = new GameObject(ControllerObjectName);
        instance = controllerObject.AddComponent<PlayerDeathSequenceController>();
        DontDestroyOnLoad(controllerObject);
        return instance;
    }

    private IEnumerator PlaySequenceAndLoadSceneRoutine(string sceneName)
    {
        isSequenceRunning = true;

        EnsureOverlay();
        EnsureVolume();
        CacheTargetCamera();

        PauseGameplayForDeath();
        ApplyDeathVisuals(0f);

        float elapsed = 0f;
        while (elapsed < DeathSequenceDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / DeathSequenceDuration);
            ApplyDeathVisuals(progress);
            yield return null;
        }

        ApplyDeathVisuals(1f);

        if (!SceneFadeTransition.LoadScene(sceneName, false))
        {
            Debug.LogError($"Failed to start game over scene transition for '{sceneName}'.", this);
            CleanupDeathRuntimeObjects(restoreGameplayState: true);
            if (!SceneFadeTransition.LoadScene(StartSceneName))
            {
                Debug.LogError($"Failed to fall back to start scene '{StartSceneName}'.", this);
            }
            isSequenceRunning = false;
            yield break;
        }

        yield return new WaitForSecondsRealtime(DeathCleanupDelay);
        CleanupDeathRuntimeObjects(restoreGameplayState: false);
        isSequenceRunning = false;
    }

    private void PauseGameplayForDeath()
    {
        GamePauseState.SetPaused(true);
        Time.timeScale = 0f;
    }

    private void CacheTargetCamera()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main != null
                ? Camera.main
                : FindAnyObjectByType<Camera>();
        }

        if (targetCamera == null)
        {
            return;
        }

        cameraBasePosition = targetCamera.transform.position;
        shakeSeed = Random.Range(0f, 1000f);

        cameraData = targetCamera.GetComponent<UniversalAdditionalCameraData>();
        if (cameraData == null)
        {
            cameraData = targetCamera.gameObject.AddComponent<UniversalAdditionalCameraData>();
        }

        if (cameraData != null)
        {
            originalRenderPostProcessing = cameraData.renderPostProcessing;
            cameraData.renderPostProcessing = true;
        }
    }

    private void EnsureOverlay()
    {
        if (overlayCanvas == null)
        {
            overlayCanvas = RuntimeGaugeUiUtility.GetOrCreateOverlayCanvas(
                null,
                OverlayCanvasObjectName,
                OverlayCanvasSortingOrder,
                out _);
        }

        if (overlayCanvas == null)
        {
            return;
        }

        if (overlayPanelImage == null)
        {
            GameObject overlayPanelObject = new GameObject(
                OverlayPanelObjectName,
                typeof(RectTransform),
                typeof(Image));
            overlayPanelObject.transform.SetParent(overlayCanvas.transform, false);

            RectTransform overlayPanelRectTransform = overlayPanelObject.GetComponent<RectTransform>();
            overlayPanelRectTransform.anchorMin = Vector2.zero;
            overlayPanelRectTransform.anchorMax = Vector2.one;
            overlayPanelRectTransform.offsetMin = Vector2.zero;
            overlayPanelRectTransform.offsetMax = Vector2.zero;

            overlayPanelImage = overlayPanelObject.GetComponent<Image>();
            overlayPanelImage.sprite = RuntimeUiSpriteUtility.GetWhiteSprite();
            overlayPanelImage.type = Image.Type.Simple;
            overlayPanelImage.color = new Color(0.02f, 0.02f, 0.03f, 0f);
            overlayPanelImage.raycastTarget = false;
        }
    }

    private void EnsureVolume()
    {
        if (volume != null)
        {
            return;
        }

        volumeObject = new GameObject(VolumeObjectName);
        volume = volumeObject.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 100f;
        volume.weight = 0f;
        volumeProfile = CreateDeathVolumeProfile();
        volume.sharedProfile = volumeProfile;
    }

    private VolumeProfile CreateDeathVolumeProfile()
    {
        VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();

        MotionBlur motionBlur = profile.Add<MotionBlur>(true);
        motionBlur.active = true;
        motionBlur.intensity.overrideState = true;
        motionBlur.intensity.value = DeathMotionBlurIntensity;
        motionBlur.clamp.overrideState = true;
        motionBlur.clamp.value = 0.18f;

        Vignette vignette = profile.Add<Vignette>(true);
        vignette.active = true;
        vignette.color.overrideState = true;
        vignette.color.value = Color.black;
        vignette.intensity.overrideState = true;
        vignette.intensity.value = DeathVignetteIntensity;
        vignette.smoothness.overrideState = true;
        vignette.smoothness.value = 0.3f;
        vignette.rounded.overrideState = true;
        vignette.rounded.value = false;

        ChromaticAberration chromaticAberration = profile.Add<ChromaticAberration>(true);
        chromaticAberration.active = true;
        chromaticAberration.intensity.overrideState = true;
        chromaticAberration.intensity.value = DeathChromaticAberrationIntensity;

        ColorAdjustments colorAdjustments = profile.Add<ColorAdjustments>(true);
        colorAdjustments.active = true;
        colorAdjustments.saturation.overrideState = true;
        colorAdjustments.saturation.value = DeathSaturation;
        colorAdjustments.contrast.overrideState = true;
        colorAdjustments.contrast.value = DeathContrast;
        colorAdjustments.postExposure.overrideState = true;
        colorAdjustments.postExposure.value = DeathPostExposure;

        return profile;
    }

    private void ApplyDeathVisuals(float progress)
    {
        float easedProgress = Mathf.SmoothStep(0f, 1f, progress);

        if (overlayPanelImage != null)
        {
            Color overlayColor = overlayPanelImage.color;
            overlayColor.a = DeathOverlayMaxAlpha * easedProgress;
            overlayPanelImage.color = overlayColor;
        }

        if (volume != null)
        {
            volume.weight = easedProgress;
        }

        if (targetCamera != null)
        {
            float shakeStrength = DeathShakeMagnitude * (1f - easedProgress);
            float noiseTime = Time.unscaledTime * DeathShakeFrequency;
            float offsetX = (Mathf.PerlinNoise(shakeSeed, noiseTime) - 0.5f) * 2f * shakeStrength;
            float offsetY = (Mathf.PerlinNoise(shakeSeed + 31.7f, noiseTime) - 0.5f) * 2f * shakeStrength;
            targetCamera.transform.position = cameraBasePosition + new Vector3(offsetX, offsetY, 0f);
        }
    }

    private void CleanupDeathRuntimeObjects(bool restoreGameplayState)
    {
        if (restoreGameplayState)
        {
            GamePauseState.SetPaused(false);
            Time.timeScale = 1f;
        }

        if (cameraData != null)
        {
            cameraData.renderPostProcessing = originalRenderPostProcessing;
        }

        if (targetCamera != null)
        {
            targetCamera.transform.position = cameraBasePosition;
        }

        if (overlayCanvas != null)
        {
            Destroy(overlayCanvas.gameObject);
        }

        if (volumeObject != null)
        {
            Destroy(volumeObject);
        }

        if (volumeProfile != null)
        {
            Destroy(volumeProfile);
        }

        overlayCanvas = null;
        overlayPanelImage = null;
        volumeObject = null;
        volume = null;
        volumeProfile = null;
        targetCamera = null;
        cameraData = null;
    }
}
