using UnityEngine;
using UnityEngine.Splines;

[DisallowMultipleComponent]
public class SecurityPatrolController : MonoBehaviour
{
    private const string RouteObjectName = "Route";
    private const float FacingThreshold = 0.001f;

    [SerializeField, Min(0.01f)] private float patrolSpeed = 3f;
    [SerializeField, Min(0.01f)] private float chaseSpeed = 5f;
    [SerializeField, Range(8, 128)] private int nearestPointSamples = 48;
    [SerializeField, Min(1)] private int contactDamage = 1;

    private SpriteRenderer securityRenderer;
    private BoxCollider2D securityCollider;
    private SplineContainer routeContainer;
    private float routeLength;
    private float patrolT;
    private bool hasRoute;
    private bool isChasing;
    private Transform chaseTarget;
    private bool defaultFlipX;
    private float originalZ;

    private void Awake()
    {
        securityRenderer = GetComponent<SpriteRenderer>();
        defaultFlipX = securityRenderer != null && securityRenderer.flipX;
        originalZ = transform.position.z;

        TryCacheRoute();
        EnsureContactCollider();
        InitializePatrolPosition();
    }

    private void Update()
    {
        if (GamePauseState.IsPaused)
        {
            return;
        }

        if (isChasing)
        {
            if (chaseTarget != null)
            {
                FollowTarget();
            }
            else
            {
                StopChasing(true);
            }

            return;
        }

        if (!IsRouteAvailable())
        {
            return;
        }

        PatrolAlongRoute();
    }

    private void OnDisable()
    {
        isChasing = false;
        chaseTarget = null;

        if (securityRenderer != null)
        {
            securityRenderer.flipX = defaultFlipX;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryDamagePlayer(other);
    }

    public void BeginChase(Transform target)
    {
        if (target == null)
        {
            return;
        }

        chaseTarget = target;
        isChasing = true;
    }

    public void StopChase(Transform target)
    {
        if (chaseTarget != null && target != null && chaseTarget.root != target.root)
        {
            return;
        }

        StopChasing(true);
    }

    public void CancelChase()
    {
        StopChasing(false);
    }

    private void StopChasing(bool restorePatrolPosition)
    {
        isChasing = false;
        chaseTarget = null;

        if (!restorePatrolPosition || !IsRouteAvailable())
        {
            return;
        }

        patrolT = FindClosestRouteT(transform.position);
    }

    private void TryCacheRoute()
    {
        GameObject routeObject = GameObject.Find(RouteObjectName);
        if (routeObject == null)
        {
            hasRoute = false;
            return;
        }

        routeContainer = routeObject.GetComponent<SplineContainer>();
        if (routeContainer == null)
        {
            Debug.LogWarning($"{name} could not find a SplineContainer on '{RouteObjectName}'.", this);
            hasRoute = false;
            return;
        }

        routeLength = routeContainer.CalculateLength();
        hasRoute = routeLength > 0f;

        if (!hasRoute)
        {
            Debug.LogWarning($"{name} found '{RouteObjectName}' but the spline length was zero.", this);
        }
    }

    private void InitializePatrolPosition()
    {
        if (!IsRouteAvailable())
        {
            return;
        }

        patrolT = FindClosestRouteT(transform.position);
        SnapToRoute(patrolT);
    }

    private void PatrolAlongRoute()
    {
        if (!IsRouteAvailable())
        {
            return;
        }

        float deltaT = (patrolSpeed / routeLength) * Time.deltaTime;
        patrolT = Mathf.Repeat(patrolT + deltaT, 1f);

        Vector3 routePosition = EvaluateRoutePosition(patrolT);
        Vector3 movementDelta = routePosition - transform.position;
        SnapToPosition(routePosition);
        UpdateFacingFromDelta(movementDelta);
    }

    private void FollowTarget()
    {
        Vector3 targetPosition = chaseTarget.position;
        targetPosition.z = originalZ;

        Vector3 currentPosition = transform.position;
        Vector3 nextPosition = Vector3.MoveTowards(currentPosition, targetPosition, chaseSpeed * Time.deltaTime);
        Vector3 movementDelta = nextPosition - currentPosition;

        SnapToPosition(nextPosition);
        UpdateFacingFromDelta(movementDelta);
    }

    private float FindClosestRouteT(Vector3 worldPosition)
    {
        if (!IsRouteAvailable())
        {
            return 0f;
        }

        int samples = Mathf.Max(8, nearestPointSamples);
        float bestT = 0f;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)samples;
            Vector3 samplePosition = EvaluateRoutePosition(t);
            float distance = (samplePosition - worldPosition).sqrMagnitude;

            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestT = t;
            }
        }

        return bestT;
    }

    private Vector3 EvaluateRoutePosition(float t)
    {
        if (!IsRouteAvailable())
        {
            return transform.position;
        }

        var routePosition = routeContainer.EvaluatePosition(t);
        return new Vector3(routePosition.x, routePosition.y, originalZ);
    }

    private Vector3 EvaluateRouteTangent(float t)
    {
        if (!IsRouteAvailable())
        {
            return Vector3.right;
        }

        var routeTangent = routeContainer.EvaluateTangent(t);
        return new Vector3(routeTangent.x, routeTangent.y, routeTangent.z);
    }

    private void SnapToRoute(float t)
    {
        SnapToPosition(EvaluateRoutePosition(t));
        UpdateFacingFromTangent(t);
    }

    private void SnapToPosition(Vector3 position)
    {
        position.z = originalZ;
        transform.position = position;
    }

    private void UpdateFacingFromTangent(float t)
    {
        if (securityRenderer == null)
        {
            return;
        }

        UpdateFacingFromDelta(EvaluateRouteTangent(t));
    }

    private void UpdateFacingFromDelta(Vector3 delta)
    {
        if (securityRenderer == null || Mathf.Abs(delta.x) <= FacingThreshold)
        {
            return;
        }

        securityRenderer.flipX = delta.x < 0f ? !defaultFlipX : defaultFlipX;
    }

    private void EnsureContactCollider()
    {
        securityCollider = GetComponent<BoxCollider2D>();
        if (securityCollider == null)
        {
            securityCollider = gameObject.AddComponent<BoxCollider2D>();
        }

        securityCollider.isTrigger = true;

        if (securityRenderer != null && securityRenderer.sprite != null)
        {
            SpriteColliderSizer.FitBoxCollidersToSpriteRenderers(transform);
        }
    }

    private void TryDamagePlayer(Collider2D other)
    {
        if (other == null)
        {
            return;
        }

        PlayerHealth playerHealth = other.GetComponentInParent<PlayerHealth>();
        if (playerHealth == null)
        {
            return;
        }

        playerHealth.TakeDamage(contactDamage);
    }

    private bool IsRouteAvailable()
    {
        return hasRoute && routeContainer != null && routeLength > 0f;
    }
}
