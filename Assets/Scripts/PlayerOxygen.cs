using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class PlayerOxygen : MonoBehaviour
{
    private const string WaterObjectName = "Water";
    private const string HealthCanvasObjectName = "HealthCanvas";
    private const string OxygenCanvasObjectName = "OxygenCanvas";
    private const string OxygenGaugeObjectName = "OxygenGauge";

    [Header("Oxygen")]
    [SerializeField, Min(1f)] private float maxOxygen = 100f;
    [SerializeField, Min(0f)] private float oxygenDrainPerSecond = 25f;
    [SerializeField, Min(0f)] private float oxygenRecoverPerSecond = 30f;
    [SerializeField, Min(0.01f)] private float damageInterval = 1f;
    [SerializeField, Min(1)] private int damagePerTick = 1;

    [Header("UI")]
    [SerializeField] private Vector2 gaugeSize = new Vector2(320f, 24f);
    [SerializeField] private Vector2 gaugeAnchoredPosition = new Vector2(24f, -56f);
    [SerializeField] private bool showBackgroundFrame = false;
    [SerializeField] private Color backgroundColor = new Color(0f, 0f, 0f, 0.55f);
    [SerializeField] private Color oxygenFillColor = new Color(0.2f, 0.8f, 1f, 1f);

    public float CurrentOxygen { get; private set; }

    private Collider2D playerCollider;
    private Collider2D waterCollider;
    private PlayerHealth playerHealth;
    private Canvas oxygenCanvas;
    private Image oxygenFillImage;
    private RectTransform oxygenFillRectTransform;
    private float oxygenFillBaseWidth;
    private float oxygenFillHeight;
    private float depletionDamageTimer;
    private bool wasOutOfOxygen;
    private bool createdOwnCanvas;

    private void Awake()
    {
        playerCollider = GetComponent<Collider2D>();
        playerHealth = GetComponent<PlayerHealth>();

        if (playerHealth == null)
        {
            playerHealth = gameObject.AddComponent<PlayerHealth>();
        }

        CurrentOxygen = maxOxygen;
        waterCollider = FindWaterCollider();
        EnsureOxygenHud();
        RefreshHud();
    }

    private void Update()
    {
        if (GamePauseState.IsPaused)
        {
            return;
        }

        RefreshWaterCollider();

        float deltaTime = Time.deltaTime;
        bool isFullySubmerged = IsFullySubmerged();

        UpdateOxygenLevel(deltaTime, isFullySubmerged);
        UpdateOxygenDepletion(deltaTime);
        RefreshHud();
    }

    private void RefreshWaterCollider()
    {
        if (waterCollider == null)
        {
            waterCollider = FindWaterCollider();
        }
    }

    private void UpdateOxygenLevel(float deltaTime, bool isFullySubmerged)
    {
        if (isFullySubmerged)
        {
            CurrentOxygen -= (oxygenDrainPerSecond * deltaTime) / 3f;
        }
        else
        {
            CurrentOxygen += oxygenRecoverPerSecond * deltaTime;
        }

        CurrentOxygen = Mathf.Clamp(CurrentOxygen, 0f, maxOxygen);
    }

    private void UpdateOxygenDepletion(float deltaTime)
    {
        if (CurrentOxygen <= 0f)
        {
            if (!wasOutOfOxygen)
            {
                ApplyOxygenDepletionDamage();
                depletionDamageTimer = 0f;
            }
            else
            {
                depletionDamageTimer += deltaTime;

                if (depletionDamageTimer >= damageInterval)
                {
                    depletionDamageTimer -= damageInterval;
                    ApplyOxygenDepletionDamage();
                }
            }

            wasOutOfOxygen = true;
            return;
        }

        wasOutOfOxygen = false;
        depletionDamageTimer = 0f;
    }

    private Collider2D FindWaterCollider()
    {
        GameObject waterObject = GameObject.Find(WaterObjectName);
        if (waterObject == null)
        {
            return null;
        }

        return waterObject.GetComponent<Collider2D>();
    }

    private bool IsFullySubmerged()
    {
        if (playerCollider == null || waterCollider == null)
        {
            return false;
        }

        if (playerCollider is PolygonCollider2D polygonCollider)
        {
            return ArePolygonPointsInsideWater(polygonCollider, waterCollider);
        }

        Bounds bounds = playerCollider.bounds;
        Vector2 bottomLeft = new Vector2(bounds.min.x, bounds.min.y);
        Vector2 bottomRight = new Vector2(bounds.max.x, bounds.min.y);
        Vector2 topLeft = new Vector2(bounds.min.x, bounds.max.y);
        Vector2 topRight = new Vector2(bounds.max.x, bounds.max.y);
        Vector2 center = bounds.center;

        return waterCollider.OverlapPoint(bottomLeft)
            && waterCollider.OverlapPoint(bottomRight)
            && waterCollider.OverlapPoint(topLeft)
            && waterCollider.OverlapPoint(topRight)
            && waterCollider.OverlapPoint(center);
    }

    private bool ArePolygonPointsInsideWater(PolygonCollider2D polygonCollider, Collider2D water)
    {
        for (int pathIndex = 0; pathIndex < polygonCollider.pathCount; pathIndex++)
        {
            Vector2[] path = polygonCollider.GetPath(pathIndex);
            if (path == null || path.Length == 0)
            {
                continue;
            }

            for (int pointIndex = 0; pointIndex < path.Length; pointIndex++)
            {
                Vector2 currentPoint = polygonCollider.transform.TransformPoint(path[pointIndex]);
                Vector2 nextPoint = polygonCollider.transform.TransformPoint(path[(pointIndex + 1) % path.Length]);
                Vector2 midpoint = (currentPoint + nextPoint) * 0.5f;

                if (!water.OverlapPoint(currentPoint) || !water.OverlapPoint(midpoint))
                {
                    return false;
                }
            }
        }

        return water.OverlapPoint(polygonCollider.bounds.center);
    }

    private void ApplyOxygenDepletionDamage()
    {
        if (playerHealth == null)
        {
            return;
        }

        playerHealth.TakeDamage(damagePerTick);
    }

    private void EnsureOxygenHud()
    {
        if (oxygenFillImage != null)
        {
            return;
        }

        Canvas existingCanvas = null;

        GameObject healthCanvasObject = GameObject.Find(HealthCanvasObjectName);
        if (healthCanvasObject != null)
        {
            existingCanvas = healthCanvasObject.GetComponent<Canvas>();
        }
        if (existingCanvas == null)
        {
            GameObject canvasObject = new GameObject(
                OxygenCanvasObjectName,
                typeof(Canvas),
                typeof(CanvasScaler));

            oxygenCanvas = canvasObject.GetComponent<Canvas>();
            oxygenCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            oxygenCanvas.sortingOrder = 100;
            createdOwnCanvas = true;

            CanvasScaler canvasScaler = canvasObject.GetComponent<CanvasScaler>();
            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            canvasScaler.matchWidthOrHeight = 0.5f;
        }
        else
        {
            oxygenCanvas = existingCanvas;
        }

        if (oxygenCanvas == null)
        {
            return;
        }

        if (GameObject.Find(OxygenGaugeObjectName) != null)
        {
            return;
        }

        Sprite uiSprite = RuntimeUiSpriteUtility.GetWhiteSprite();
        GameObject gaugeObject = new GameObject(
            OxygenGaugeObjectName,
            typeof(RectTransform),
            typeof(Image));

        gaugeObject.transform.SetParent(oxygenCanvas.transform, false);

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
        oxygenFillBaseWidth = Mathf.Max(0f, gaugeSize.x - 6f);
        oxygenFillHeight = Mathf.Max(0f, gaugeSize.y - 6f);
        fillRectTransform.sizeDelta = new Vector2(oxygenFillBaseWidth, oxygenFillHeight);

        oxygenFillRectTransform = fillRectTransform;
        oxygenFillImage = fillObject.GetComponent<Image>();
        oxygenFillImage.sprite = uiSprite;
        oxygenFillImage.type = Image.Type.Simple;
        oxygenFillImage.color = oxygenFillColor;
        oxygenFillImage.raycastTarget = false;
    }

    private void OnDestroy()
    {
        if (createdOwnCanvas && oxygenCanvas != null)
        {
            Destroy(oxygenCanvas.gameObject);
        }
    }

    private void RefreshHud()
    {
        if (oxygenFillRectTransform == null)
        {
            return;
        }

        float oxygenRatio = Mathf.Approximately(maxOxygen, 0f) ? 0f : CurrentOxygen / maxOxygen;
        oxygenFillRectTransform.sizeDelta = new Vector2(oxygenFillBaseWidth * oxygenRatio, oxygenFillHeight);
    }
}
