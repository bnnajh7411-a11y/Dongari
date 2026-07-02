using UnityEngine;

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

        staminaCanvas = RuntimeGaugeUiUtility.GetOrCreateOverlayCanvas(
            transform,
            StaminaCanvasObjectName,
            105,
            out _);
        if (staminaCanvas == null)
        {
            return;
        }

        staminaFillRectTransform = RuntimeGaugeUiUtility.GetOrCreateGaugeFillRect(
            staminaCanvas,
            StaminaGaugeObjectName,
            StaminaLabelObjectName,
            gaugeSize,
            gaugeAnchoredPosition,
            showBackgroundFrame,
            backgroundColor,
            staminaFillColor,
            gaugeLabel,
            gaugeLabelOffset,
            gaugeLabelSize,
            gaugeLabelFontSize,
            gaugeLabelColor,
            out staminaFillBaseWidth,
            out staminaFillHeight);
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
