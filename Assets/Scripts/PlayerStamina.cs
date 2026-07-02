using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class PlayerStamina : MonoBehaviour
{
    private const string StaminaCanvasObjectName = "StaminaCanvas";
    private const string StaminaGaugeObjectName = "StaminaGauge";
    private const string StaminaLabelObjectName = "Label";
    private static float savedCurrentStamina;
    private static bool hasSavedCurrentStamina;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        ResetPersistentStamina();
    }

    [SerializeField, Min(1f)] private float maxStamina = 50f;
    [SerializeField, Min(0f)] private float runDrainPerSecond = 10f;
    [SerializeField, Min(0f)] private float staminaRegenPerSecond = 6f;
    [SerializeField, Min(0f)] private float staminaRegenDelay = 0.4f;
    [SerializeField, Min(0f)] private float stationaryVelocityThreshold = 0.05f;

    [Header("UI")]
    [SerializeField] private Vector2 gaugeSize = new Vector2(320f, 24f);
    [SerializeField] private Vector2 gaugeAnchoredPosition = new Vector2(24f, -56f);
    [SerializeField] private bool showBackgroundFrame = false;
    [SerializeField] private Color backgroundColor = new Color(0f, 0f, 0f, 0.55f);
    [SerializeField] private Color staminaFillColor = new Color(0.2f, 0.8f, 0.35f, 1f);
    [SerializeField] private string gaugeLabel = "\uc2a4\ud0dc\ubbf8\ub098";
    [SerializeField] private Vector2 gaugeLabelOffset = new Vector2(16f, 0f);
    [SerializeField] private Vector2 gaugeLabelSize = new Vector2(180f, 28f);
    [SerializeField] private int gaugeLabelFontSize = 30;
    [SerializeField] private Color gaugeLabelColor = Color.white;

    public float CurrentStamina { get; private set; }
    public bool CanSprint => CurrentStamina > 0f;

    private Canvas staminaCanvas;
    private RectTransform staminaFillRectTransform;
    private float staminaFillBaseWidth;
    private float staminaFillHeight;
    private Rigidbody2D playerRigidbody;
    private float stationaryTimer;

    private void Awake()
    {
        playerRigidbody = GetComponent<Rigidbody2D>();
        CurrentStamina = hasSavedCurrentStamina
            ? Mathf.Clamp(savedCurrentStamina, 0f, maxStamina)
            : maxStamina;

        EnsureStaminaHud();
        RefreshHud();
        SaveCurrentStamina();
    }

    private void Update()
    {
        if (GamePauseState.IsPaused)
        {
            return;
        }

        if (CurrentStamina >= maxStamina)
        {
            stationaryTimer = 0f;
            return;
        }

        if (!IsStationary())
        {
            stationaryTimer = 0f;
            return;
        }

        stationaryTimer += Time.deltaTime;
        if (stationaryTimer < staminaRegenDelay)
        {
            return;
        }

        RestoreStamina(staminaRegenPerSecond * Time.deltaTime);
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

    private bool IsStationary()
    {
        if (playerRigidbody == null)
        {
            playerRigidbody = GetComponent<Rigidbody2D>();
        }

        if (playerRigidbody == null)
        {
            return false;
        }

        return playerRigidbody.linearVelocity.sqrMagnitude <= stationaryVelocityThreshold * stationaryVelocityThreshold;
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

        CreateGaugeLabel(gaugeObject.transform);
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

    private void CreateGaugeLabel(Transform parent)
    {
        GameObject labelObject = new GameObject(StaminaLabelObjectName, typeof(RectTransform), typeof(Text));
        labelObject.transform.SetParent(parent, false);

        RectTransform labelRectTransform = labelObject.GetComponent<RectTransform>();
        labelRectTransform.anchorMin = new Vector2(1f, 0.5f);
        labelRectTransform.anchorMax = new Vector2(1f, 0.5f);
        labelRectTransform.pivot = new Vector2(0f, 0.5f);
        labelRectTransform.anchoredPosition = gaugeLabelOffset;
        labelRectTransform.sizeDelta = gaugeLabelSize;

        Text labelText = labelObject.GetComponent<Text>();
        labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        labelText.fontSize = gaugeLabelFontSize;
        labelText.color = gaugeLabelColor;
        labelText.alignment = TextAnchor.MiddleLeft;
        labelText.horizontalOverflow = HorizontalWrapMode.Overflow;
        labelText.verticalOverflow = VerticalWrapMode.Overflow;
        labelText.raycastTarget = false;
        labelText.text = gaugeLabel;
    }
}
