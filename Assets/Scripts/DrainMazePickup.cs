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
    private Image previewImage;
    private Image backdropImage;
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

        bool createdCanvas;
        overlayCanvas = RuntimeGaugeUiUtility.GetOrCreateOverlayCanvas(
            transform,
            CanvasObjectName,
            OverlaySortingOrder,
            out createdCanvas);
    }

    private void EnsureLayout()
    {
        if (overlayCanvas == null)
        {
            return;
        }

        EnsurePreviewPanel();
        EnsureExpandedOverlay();
    }

    private void EnsurePreviewPanel()
    {
        if (previewPanelRectTransform != null)
        {
            return;
        }

        Image panelImage = CreateImageObject(
            overlayCanvas.transform,
            PreviewPanelObjectName,
            out previewPanelRectTransform);
        ConfigureAnchoredRect(
            previewPanelRectTransform,
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(-PreviewMargin, -PreviewMargin),
            new Vector2(PreviewWidth, PreviewHeight));
        panelImage.color = Color.clear;
        panelImage.raycastTarget = false;

        previewImage = CreateImageObject(
            previewPanelRectTransform,
            PreviewImageObjectName,
            out RectTransform previewImageRectTransform);
        ConfigureStretchRect(
            previewImageRectTransform,
            new Vector2(PreviewInnerPadding, PreviewInnerPadding),
            new Vector2(-PreviewInnerPadding, -PreviewInnerPadding));
        previewImage.preserveAspect = true;
        previewImage.raycastTarget = false;
        previewImage.color = Color.white;
    }

    private void EnsureExpandedOverlay()
    {
        if (backdropImage != null)
        {
            return;
        }

        backdropImage = CreateImageObject(
            overlayCanvas.transform,
            BackdropObjectName,
            out RectTransform backdropRectTransform);
        ConfigureStretchRect(backdropRectTransform, Vector2.zero, Vector2.zero);
        backdropImage.color = new Color(0f, 0f, 0f, 0.46f);
        backdropImage.raycastTarget = false;

        Image panelImage = CreateImageObject(
            backdropRectTransform,
            ExpandedPanelObjectName,
            out RectTransform expandedPanelRectTransform);
        ConfigureStretchRect(
            expandedPanelRectTransform,
            new Vector2(ExpandedHorizontalMargin, ExpandedVerticalMargin),
            new Vector2(-ExpandedHorizontalMargin, -ExpandedVerticalMargin));
        panelImage.color = Color.clear;
        panelImage.raycastTarget = false;

        expandedImage = CreateImageObject(
            expandedPanelRectTransform,
            ExpandedImageObjectName,
            out RectTransform expandedImageRectTransform);
        ConfigureStretchRect(
            expandedImageRectTransform,
            new Vector2(ExpandedInnerPadding, ExpandedInnerPadding),
            new Vector2(-ExpandedInnerPadding, -ExpandedInnerPadding));
        expandedImage.preserveAspect = true;
        expandedImage.raycastTarget = false;
        expandedImage.color = Color.white;

        expandedHintText = CreateTextObject(
            backdropRectTransform,
            ExpandedHintTextObjectName,
            out RectTransform expandedHintTextRectTransform);
        ConfigureAnchoredRect(
            expandedHintTextRectTransform,
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0f, ExpandedHintBottomMargin),
            new Vector2(640f, ExpandedHintHeight));
        expandedHintText.font = RuntimeGaugeUiUtility.GetBuiltinFont();
        expandedHintText.fontSize = ExpandedHintFontSize;
        expandedHintText.alignment = TextAnchor.MiddleCenter;
        expandedHintText.color = new Color(0.97f, 0.98f, 1f, 1f);
        expandedHintText.raycastTarget = false;
        expandedHintText.text = ExpandedHintText;
    }

    private static Image CreateImageObject(
        Transform parent,
        string objectName,
        out RectTransform rectTransform)
    {
        GameObject imageObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        imageObject.transform.SetParent(parent, false);
        rectTransform = imageObject.GetComponent<RectTransform>();
        return imageObject.GetComponent<Image>();
    }

    private static Text CreateTextObject(
        Transform parent,
        string objectName,
        out RectTransform rectTransform)
    {
        GameObject textObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Text));
        textObject.transform.SetParent(parent, false);
        rectTransform = textObject.GetComponent<RectTransform>();
        return textObject.GetComponent<Text>();
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
