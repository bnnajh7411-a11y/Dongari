using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class PlayerStamina : MonoBehaviour
{
    private const string StaminaCanvasObjectName = "StaminaCanvas";
    private const string StaminaGaugeObjectName = "StaminaGauge";
    private static float savedCurrentStamina;
    private static bool hasSavedCurrentStamina;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        ResetPersistentStamina();
    }

    [SerializeField, Min(1f)] private float maxStamina = 50f;
    [SerializeField, Min(0f)] private float runDrainPerSecond = 10f;

    [Header("UI")]
    [SerializeField] private Vector2 gaugeSize = new Vector2(320f, 24f);
    [SerializeField] private Vector2 gaugeAnchoredPosition = new Vector2(24f, -88f);
    [SerializeField] private bool showBackgroundFrame = false;
    [SerializeField] private Color backgroundColor = new Color(0f, 0f, 0f, 0.55f);
    [SerializeField] private Color staminaFillColor = new Color(0.2f, 0.8f, 0.35f, 1f);

    public float CurrentStamina { get; private set; }
    public bool CanSprint => CurrentStamina > 0f;

    private Canvas staminaCanvas;
    private RectTransform staminaFillRectTransform;
    private float staminaFillBaseWidth;
    private float staminaFillHeight;

    private void Awake()
    {
        CurrentStamina = hasSavedCurrentStamina
            ? Mathf.Clamp(savedCurrentStamina, 0f, maxStamina)
            : maxStamina;

        EnsureStaminaHud();
        RefreshHud();
        SaveCurrentStamina();
    }

    public static void ResetPersistentStamina()
    {
        savedCurrentStamina = 0f;
        hasSavedCurrentStamina = false;
    }

    public float ConsumeRunStamina(float deltaTime)
    {
        if (runDrainPerSecond <= 0f || deltaTime <= 0f)
        {
            return 0f;
        }

        return AdjustStamina(-runDrainPerSecond * deltaTime);
    }

    public float RestoreStamina(float amount)
    {
        if (amount <= 0f)
        {
            return 0f;
        }

        return AdjustStamina(amount);
    }

    private float AdjustStamina(float amount)
    {
        float previousStamina = CurrentStamina;
        CurrentStamina = Mathf.Clamp(CurrentStamina + amount, 0f, maxStamina);

        if (!Mathf.Approximately(previousStamina, CurrentStamina))
        {
            SaveCurrentStamina();
            RefreshHud();
        }

        return CurrentStamina - previousStamina;
    }

    private void SaveCurrentStamina()
    {
        savedCurrentStamina = CurrentStamina;
        hasSavedCurrentStamina = true;
    }

    private void EnsureStaminaHud()
    {
        if (staminaFillRectTransform != null)
        {
            return;
        }

        GameObject existingCanvasObject = GameObject.Find(StaminaCanvasObjectName);
        if (existingCanvasObject != null && existingCanvasObject.TryGetComponent(out Canvas existingCanvas))
        {
            staminaCanvas = existingCanvas;
        }
        else
        {
            GameObject canvasObject = new GameObject(
                StaminaCanvasObjectName,
                typeof(Canvas),
                typeof(CanvasScaler));

            canvasObject.transform.SetParent(transform, false);

            staminaCanvas = canvasObject.GetComponent<Canvas>();
            staminaCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            staminaCanvas.sortingOrder = 105;

            CanvasScaler canvasScaler = canvasObject.GetComponent<CanvasScaler>();
            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            canvasScaler.matchWidthOrHeight = 0.5f;
        }

        if (staminaCanvas != null)
        {
            staminaCanvas.sortingOrder = 105;
        }

        if (staminaCanvas == null || GameObject.Find(StaminaGaugeObjectName) != null)
        {
            return;
        }

        Sprite uiSprite = RuntimeUiSpriteUtility.GetWhiteSprite();
        GameObject gaugeObject = new GameObject(
            StaminaGaugeObjectName,
            typeof(RectTransform),
            typeof(Image));

        gaugeObject.transform.SetParent(staminaCanvas.transform, false);

        RectTransform gaugeRectTransform = gaugeObject.GetComponent<RectTransform>();
        gaugeRectTransform.anchorMin = new Vector2(0f, 1f);
        gaugeRectTransform.anchorMax = new Vector2(0f, 1f);
        gaugeRectTransform.pivot = new Vector2(0f, 1f);
        gaugeRectTransform.sizeDelta = gaugeSize;
        gaugeRectTransform.anchoredPosition = gaugeAnchoredPosition;

        Image backgroundImage = gaugeObject.GetComponent<Image>();
        backgroundImage.sprite = uiSprite;
        backgroundImage.type = Image.Type.Simple;
        backgroundImage.color = showBackgroundFrame ? backgroundColor : Color.clear;
        backgroundImage.enabled = showBackgroundFrame;
        backgroundImage.raycastTarget = false;

        GameObject fillObject = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fillObject.transform.SetParent(gaugeObject.transform, false);

        RectTransform fillRectTransform = fillObject.GetComponent<RectTransform>();
        fillRectTransform.anchorMin = new Vector2(0f, 1f);
        fillRectTransform.anchorMax = new Vector2(0f, 1f);
        fillRectTransform.pivot = new Vector2(0f, 1f);
        fillRectTransform.anchoredPosition = new Vector2(3f, -3f);

        staminaFillBaseWidth = Mathf.Max(0f, gaugeSize.x - 6f);
        staminaFillHeight = Mathf.Max(0f, gaugeSize.y - 6f);
        fillRectTransform.sizeDelta = new Vector2(staminaFillBaseWidth, staminaFillHeight);

        staminaFillRectTransform = fillRectTransform;

        Image fillImage = fillObject.GetComponent<Image>();
        fillImage.sprite = uiSprite;
        fillImage.type = Image.Type.Simple;
        fillImage.color = staminaFillColor;
        fillImage.raycastTarget = false;
    }

    private void RefreshHud()
    {
        if (staminaFillRectTransform == null)
        {
            return;
        }

        float staminaRatio = Mathf.Approximately(maxStamina, 0f) ? 0f : CurrentStamina / maxStamina;
        staminaFillRectTransform.sizeDelta = new Vector2(staminaFillBaseWidth * staminaRatio, staminaFillHeight);
    }
}
