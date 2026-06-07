using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class StartSceneController : MonoBehaviour
{
    private const string EventSystemObjectName = "EventSystem";
    private const string ButtonObjectName = "StartButton";

    [SerializeField] private string sceneToLoad = "Building";
    [SerializeField] private string buttonLabel = "START";

    private void Awake()
    {
        EnsureEventSystem();
        Canvas canvas = EnsureCanvas();
        EnsureStartButton(canvas.transform);
    }

    private void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null)
        {
            return;
        }

        new GameObject(EventSystemObjectName, typeof(EventSystem), typeof(StandaloneInputModule));
    }

    private Canvas EnsureCanvas()
    {
        Canvas existingCanvas = FindObjectOfType<Canvas>();
        if (existingCanvas != null)
        {
            return existingCanvas;
        }

        GameObject canvasObject = new GameObject(
            "StartCanvas",
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
        if (GameObject.Find(ButtonObjectName) != null)
        {
            return;
        }

        GameObject buttonObject = new GameObject(
            ButtonObjectName,
            typeof(RectTransform),
            typeof(Image),
            typeof(Button));

        buttonObject.transform.SetParent(parent, false);

        RectTransform buttonRectTransform = buttonObject.GetComponent<RectTransform>();
        buttonRectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        buttonRectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        buttonRectTransform.pivot = new Vector2(0.5f, 0.5f);
        buttonRectTransform.sizeDelta = new Vector2(280f, 80f);
        buttonRectTransform.anchoredPosition = Vector2.zero;

        Image buttonImage = buttonObject.GetComponent<Image>();
        buttonImage.color = new Color(0.16f, 0.44f, 0.25f, 1f);

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = buttonImage;
        button.colors = CreateButtonColors(buttonImage.color);
        button.onClick.AddListener(LoadScene);

        GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
        labelObject.transform.SetParent(buttonObject.transform, false);

        RectTransform labelRectTransform = labelObject.GetComponent<RectTransform>();
        labelRectTransform.anchorMin = Vector2.zero;
        labelRectTransform.anchorMax = Vector2.one;
        labelRectTransform.offsetMin = Vector2.zero;
        labelRectTransform.offsetMax = Vector2.zero;

        Text label = labelObject.GetComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        label.text = buttonLabel;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;
        label.fontSize = 32;
        label.fontStyle = FontStyle.Bold;
        label.raycastTarget = false;
    }

    private ColorBlock CreateButtonColors(Color normalColor)
    {
        ColorBlock colors = ColorBlock.defaultColorBlock;
        colors.normalColor = normalColor;
        colors.highlightedColor = new Color(0.23f, 0.56f, 0.31f, 1f);
        colors.pressedColor = new Color(0.12f, 0.33f, 0.19f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(0.4f, 0.4f, 0.4f, 0.7f);
        return colors;
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
