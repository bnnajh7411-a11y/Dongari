using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public class DrainMazePickup : TriggerSpritePickupBase
{
    private const string DrainSceneName = "Drain";
    private const string MazeItemObjectName = "MiroItem";

    private static bool hasRegisteredSceneLoadedHandler;

    private bool hasCollected;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        hasRegisteredSceneLoadedHandler = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InitializeAfterSceneLoad()
    {
        EnsureSceneLoadedHandler();
        TryAttachToScene(SceneManager.GetActiveScene());
    }

    private static void EnsureSceneLoadedHandler()
    {
        if (hasRegisteredSceneLoadedHandler)
        {
            return;
        }

        SceneManager.sceneLoaded += HandleSceneLoaded;
        hasRegisteredSceneLoadedHandler = true;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode loadMode)
    {
        TryAttachToScene(scene);
    }

    private static void TryAttachToScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded || scene.name != DrainSceneName)
        {
            return;
        }

        GameObject mazeItemObject = GameObject.Find(MazeItemObjectName);
        if (mazeItemObject == null || mazeItemObject.GetComponent<DrainMazePickup>() != null)
        {
            return;
        }

        mazeItemObject.AddComponent<DrainMazePickup>();
    }

    protected override void HandleTriggerEnter(Collider2D other)
    {
        if (hasCollected || other == null)
        {
            return;
        }

        if (GetCollectorInParent<PlayerMovement>(other) == null)
        {
            return;
        }

        hasCollected = true;
        DrainMazeOverlay.Show(PickupSprite);
        CollectAndDestroy(registerCredits: false);
    }
}

[DisallowMultipleComponent]
internal sealed class DrainMazeOverlay : MonoBehaviour
{
    private const string OverlayObjectName = "DrainMazeOverlay";
    private const string CanvasObjectName = "DrainMazeCanvas";
    private const string PreviewPanelObjectName = "PreviewPanel";
    private const string PreviewImageObjectName = "PreviewImage";
    private const string BackdropObjectName = "ExpandedBackdrop";
    private const string ExpandedPanelObjectName = "ExpandedPanel";
    private const string ExpandedImageObjectName = "ExpandedImage";
    private const string ExpandedHintTextObjectName = "ExpandedHintText";
    private const string ExpandedHintText = "\uc544\ubb34\u0020\uacf3\uc774\ub098\u0020\ud074\ub9ad\ud558\uc5ec\u0020\ub2eb\uae30";
    private const int OverlaySortingOrder = 115;
    private const float ReferenceWidth = 1920f;
    private const float ReferenceHeight = 1080f;
    private const float PreviewWidth = 220f;
    private const float PreviewHeight = 96f;
    private const float PreviewMargin = 26f;
    private const float PreviewInnerPadding = 10f;
    private const float ExpandedHorizontalMargin = 5f;
    private const float ExpandedVerticalMargin = 24f;
    private const float ExpandedInnerPadding = 0f;
    private const int ExpandedHintFontSize = 30;
    private const float ExpandedHintBottomMargin = 18f;
    private const float ExpandedHintHeight = 400f;

    private static DrainMazeOverlay instance;

    private Canvas overlayCanvas;
    private RectTransform previewPanelRectTransform;
    private Image previewPanelImage;
    private Image previewImage;
    private Image backdropImage;
    private RectTransform expandedPanelRectTransform;
    private Image expandedPanelImage;
    private Image expandedImage;
    private Text expandedHintText;
    private Sprite mazeSprite;
    private bool isExpanded;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOverlayState()
    {
        instance = null;
    }

    public static void Show(Sprite sprite)
    {
        if (sprite == null)
        {
            return;
        }

        if (instance == null)
        {
            GameObject overlayObject = new GameObject(OverlayObjectName);
            instance = overlayObject.AddComponent<DrainMazeOverlay>();
        }

        instance.Initialize(sprite);
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        EnsureCanvas();
        EnsureLayout();
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    private void Update()
    {
        if (mazeSprite == null || !Input.GetMouseButtonDown(0))
        {
            return;
        }

        if (isExpanded)
        {
            SetExpanded(false);
            return;
        }

        if (previewPanelRectTransform != null
            && RectTransformUtility.RectangleContainsScreenPoint(
                previewPanelRectTransform,
                Input.mousePosition,
                null))
        {
            SetExpanded(true);
        }
    }

    private void Initialize(Sprite sprite)
    {
        mazeSprite = sprite;
        EnsureCanvas();
        EnsureLayout();
        ApplySprite();
        SetExpanded(false);
    }

    private void EnsureCanvas()
    {
        if (overlayCanvas != null)
        {
            return;
        }

        GameObject existingCanvasObject = GameObject.Find(CanvasObjectName);
        if (existingCanvasObject != null && existingCanvasObject.TryGetComponent(out Canvas existingCanvas))
        {
            overlayCanvas = existingCanvas;
            overlayCanvas.sortingOrder = OverlaySortingOrder;
            return;
        }

        GameObject canvasObject = new GameObject(
            CanvasObjectName,
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler));
        canvasObject.transform.SetParent(transform, false);

        overlayCanvas = canvasObject.GetComponent<Canvas>();
        overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        overlayCanvas.sortingOrder = OverlaySortingOrder;

        CanvasScaler canvasScaler = canvasObject.GetComponent<CanvasScaler>();
        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
        canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        canvasScaler.matchWidthOrHeight = 0.5f;
    }

    private void EnsureLayout()
    {
        if (overlayCanvas == null)
        {
            return;
        }

        if (previewPanelRectTransform == null)
        {
            GameObject previewPanelObject = new GameObject(
                PreviewPanelObjectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            previewPanelObject.transform.SetParent(overlayCanvas.transform, false);

            previewPanelRectTransform = previewPanelObject.GetComponent<RectTransform>();
            previewPanelRectTransform.anchorMin = new Vector2(1f, 1f);
            previewPanelRectTransform.anchorMax = new Vector2(1f, 1f);
            previewPanelRectTransform.pivot = new Vector2(1f, 1f);
            previewPanelRectTransform.anchoredPosition = new Vector2(-PreviewMargin, -PreviewMargin);
            previewPanelRectTransform.sizeDelta = new Vector2(PreviewWidth, PreviewHeight);

            previewPanelImage = previewPanelObject.GetComponent<Image>();
            previewPanelImage.color = Color.clear;
            previewPanelImage.raycastTarget = false;

            GameObject previewImageObject = new GameObject(
                PreviewImageObjectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            previewImageObject.transform.SetParent(previewPanelObject.transform, false);

            RectTransform previewImageRectTransform = previewImageObject.GetComponent<RectTransform>();
            previewImageRectTransform.anchorMin = Vector2.zero;
            previewImageRectTransform.anchorMax = Vector2.one;
            previewImageRectTransform.offsetMin = new Vector2(PreviewInnerPadding, PreviewInnerPadding);
            previewImageRectTransform.offsetMax = new Vector2(-PreviewInnerPadding, -PreviewInnerPadding);

            previewImage = previewImageObject.GetComponent<Image>();
            previewImage.preserveAspect = true;
            previewImage.raycastTarget = false;
            previewImage.color = Color.white;
        }

        if (backdropImage == null)
        {
            GameObject backdropObject = new GameObject(
                BackdropObjectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            backdropObject.transform.SetParent(overlayCanvas.transform, false);

            RectTransform backdropRectTransform = backdropObject.GetComponent<RectTransform>();
            backdropRectTransform.anchorMin = Vector2.zero;
            backdropRectTransform.anchorMax = Vector2.one;
            backdropRectTransform.offsetMin = Vector2.zero;
            backdropRectTransform.offsetMax = Vector2.zero;

            backdropImage = backdropObject.GetComponent<Image>();
            backdropImage.color = new Color(0f, 0f, 0f, 0.72f);
            backdropImage.raycastTarget = false;

            GameObject expandedPanelObject = new GameObject(
                ExpandedPanelObjectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            expandedPanelObject.transform.SetParent(backdropObject.transform, false);

            expandedPanelRectTransform = expandedPanelObject.GetComponent<RectTransform>();
            expandedPanelRectTransform.anchorMin = Vector2.zero;
            expandedPanelRectTransform.anchorMax = Vector2.one;
            expandedPanelRectTransform.offsetMin = new Vector2(
                ExpandedHorizontalMargin,
                ExpandedVerticalMargin);
            expandedPanelRectTransform.offsetMax = new Vector2(
                -ExpandedHorizontalMargin,
                -ExpandedVerticalMargin);

            expandedPanelImage = expandedPanelObject.GetComponent<Image>();
            expandedPanelImage.color = Color.clear;
            expandedPanelImage.raycastTarget = false;

            GameObject expandedImageObject = new GameObject(
                ExpandedImageObjectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            expandedImageObject.transform.SetParent(expandedPanelObject.transform, false);

            RectTransform expandedImageRectTransform = expandedImageObject.GetComponent<RectTransform>();
            expandedImageRectTransform.anchorMin = Vector2.zero;
            expandedImageRectTransform.anchorMax = Vector2.one;
            expandedImageRectTransform.offsetMin = new Vector2(ExpandedInnerPadding, ExpandedInnerPadding);
            expandedImageRectTransform.offsetMax = new Vector2(-ExpandedInnerPadding, -ExpandedInnerPadding);

            expandedImage = expandedImageObject.GetComponent<Image>();
            expandedImage.preserveAspect = true;
            expandedImage.raycastTarget = false;
            expandedImage.color = Color.white;

            GameObject expandedHintTextObject = new GameObject(
                ExpandedHintTextObjectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Text));
            expandedHintTextObject.transform.SetParent(backdropObject.transform, false);

            RectTransform expandedHintTextRectTransform = expandedHintTextObject.GetComponent<RectTransform>();
            expandedHintTextRectTransform.anchorMin = new Vector2(0.5f, 0f);
            expandedHintTextRectTransform.anchorMax = new Vector2(0.5f, 0f);
            expandedHintTextRectTransform.pivot = new Vector2(0.5f, 0f);
            expandedHintTextRectTransform.anchoredPosition = new Vector2(0f, ExpandedHintBottomMargin);
            expandedHintTextRectTransform.sizeDelta = new Vector2(640f, ExpandedHintHeight);

            expandedHintText = expandedHintTextObject.GetComponent<Text>();
            expandedHintText.font = RuntimeGaugeUiUtility.GetBuiltinFont();
            expandedHintText.fontSize = ExpandedHintFontSize;
            expandedHintText.alignment = TextAnchor.MiddleCenter;
            expandedHintText.color = Color.white;
            expandedHintText.raycastTarget = false;
            expandedHintText.text = ExpandedHintText;
        }
    }

    private void ApplySprite()
    {
        if (previewImage != null)
        {
            previewImage.sprite = mazeSprite;
            previewImage.enabled = mazeSprite != null;
        }

        if (expandedImage != null)
        {
            expandedImage.sprite = mazeSprite;
            expandedImage.enabled = mazeSprite != null;
        }
    }

    private void SetExpanded(bool expanded)
    {
        isExpanded = expanded;

        if (previewPanelRectTransform != null)
        {
            previewPanelRectTransform.gameObject.SetActive(!expanded);
        }

        if (backdropImage != null)
        {
            backdropImage.gameObject.SetActive(expanded);
        }

        if (expandedHintText != null)
        {
            expandedHintText.gameObject.SetActive(expanded);
        }
    }
}
