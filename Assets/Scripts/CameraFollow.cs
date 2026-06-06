using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Camera))]
public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Collider2D leftWall;
    [SerializeField] private Collider2D rightWall;
    [SerializeField] private Collider2D groundBounds;
    [SerializeField, Min(0f)] private float smoothTime = 0.15f;

    private const string PlayerObjectName = "Player";
    private const string LeftWallObjectName = "LWall";
    private const string RightWallObjectName = "RWall";
    private const string GroundObjectName = "Ground";
    private const string RoadSceneName = "Road";

    private Camera attachedCamera;
    private float depthOffset;
    private float xVelocity;
    private float yVelocity;
    private bool hasDepthOffset;

    private void Awake()
    {
        attachedCamera = GetComponent<Camera>();
        TryAssignReferences();
        CacheDepthOffset();
    }

    private void LateUpdate()
    {
        TryAssignReferences();
        CacheDepthOffset();

        if (target == null)
        {
            return;
        }

        Vector3 desiredPosition = new Vector3(
            target.position.x,
            target.position.y,
            target.position.z + depthOffset);

        if (smoothTime <= 0f)
        {
            transform.position = GetClampedCameraPosition(desiredPosition);
            return;
        }

        float nextX = Mathf.SmoothDamp(transform.position.x, desiredPosition.x, ref xVelocity, smoothTime);
        float nextY = Mathf.SmoothDamp(transform.position.y, desiredPosition.y, ref yVelocity, smoothTime);
        Vector3 clampedPosition = GetClampedCameraPosition(new Vector3(nextX, nextY, desiredPosition.z));

        if (!Mathf.Approximately(clampedPosition.x, nextX))
        {
            xVelocity = 0f;
        }

        if (!Mathf.Approximately(clampedPosition.y, nextY))
        {
            yVelocity = 0f;
        }

        transform.position = clampedPosition;
    }

    private void TryAssignReferences()
    {
        TryAssignTarget();

        if (IsTopDownScene())
        {
            TryAssignGroundBounds();
            return;
        }

        TryAssignWall(ref leftWall, LeftWallObjectName);
        TryAssignWall(ref rightWall, RightWallObjectName);
    }

    private void TryAssignTarget()
    {
        if (target != null)
        {
            return;
        }

        GameObject playerObject = GameObject.Find(PlayerObjectName);
        if (playerObject != null)
        {
            target = playerObject.transform;
            hasDepthOffset = false;
        }
    }

    private void TryAssignWall(ref Collider2D wallCollider, string wallObjectName)
    {
        if (wallCollider != null)
        {
            return;
        }

        GameObject wallObject = GameObject.Find(wallObjectName);
        if (wallObject != null)
        {
            wallCollider = wallObject.GetComponent<Collider2D>();
        }
    }

    private void CacheDepthOffset()
    {
        if (target == null || hasDepthOffset)
        {
            return;
        }

        depthOffset = transform.position.z - target.position.z;
        hasDepthOffset = true;
    }

    private void TryAssignGroundBounds()
    {
        if (groundBounds != null)
        {
            return;
        }

        GameObject groundObject = GameObject.Find(GroundObjectName);
        if (groundObject != null)
        {
            groundBounds = groundObject.GetComponent<Collider2D>();
        }
    }

    private bool IsTopDownScene()
    {
        return SceneManager.GetActiveScene().name == RoadSceneName;
    }

    private Vector3 GetClampedCameraPosition(Vector3 desiredPosition)
    {
        if (IsTopDownScene())
        {
            return GetGroundClampedPosition(desiredPosition);
        }

        return new Vector3(GetClampedCameraX(desiredPosition.x), desiredPosition.y, desiredPosition.z);
    }

    private Vector3 GetGroundClampedPosition(Vector3 desiredPosition)
    {
        if (groundBounds == null || attachedCamera == null)
        {
            return desiredPosition;
        }

        GetCameraHalfExtents(out float cameraHalfWidth, out float cameraHalfHeight);

        float minCameraX = groundBounds.bounds.min.x + cameraHalfWidth;
        float maxCameraX = groundBounds.bounds.max.x - cameraHalfWidth;
        float minCameraY = groundBounds.bounds.min.y + cameraHalfHeight;
        float maxCameraY = groundBounds.bounds.max.y - cameraHalfHeight;

        float clampedX = ClampAxisToBounds(desiredPosition.x, minCameraX, maxCameraX);
        float clampedY = ClampAxisToBounds(desiredPosition.y, minCameraY, maxCameraY);

        return new Vector3(clampedX, clampedY, desiredPosition.z);
    }

    private float GetClampedCameraX(float desiredCameraX)
    {
        if (leftWall == null || rightWall == null || attachedCamera == null || target == null)
        {
            return desiredCameraX;
        }

        GetCameraHalfExtents(out float cameraHalfWidth, out float ignoredHalfHeight);

        float minCameraX = leftWall.bounds.min.x + cameraHalfWidth;
        float maxCameraX = rightWall.bounds.max.x - cameraHalfWidth;

        return ClampAxisToBounds(desiredCameraX, minCameraX, maxCameraX);
    }

    private void GetCameraHalfExtents(out float halfWidth, out float halfHeight)
    {
        float focusPlaneDistance = 0f;
        if (target != null)
        {
            focusPlaneDistance = Mathf.Abs(transform.position.z - target.position.z);
        }

        Vector3 leftViewportEdge = attachedCamera.ViewportToWorldPoint(new Vector3(0f, 0.5f, focusPlaneDistance));
        Vector3 rightViewportEdge = attachedCamera.ViewportToWorldPoint(new Vector3(1f, 0.5f, focusPlaneDistance));
        Vector3 bottomViewportEdge = attachedCamera.ViewportToWorldPoint(new Vector3(0.5f, 0f, focusPlaneDistance));
        Vector3 topViewportEdge = attachedCamera.ViewportToWorldPoint(new Vector3(0.5f, 1f, focusPlaneDistance));

        halfWidth = Mathf.Abs(rightViewportEdge.x - leftViewportEdge.x) * 0.5f;
        halfHeight = Mathf.Abs(topViewportEdge.y - bottomViewportEdge.y) * 0.5f;
    }

    private float ClampAxisToBounds(float desiredValue, float minValue, float maxValue)
    {
        if (minValue > maxValue)
        {
            return (minValue + maxValue) * 0.5f;
        }

        return Mathf.Clamp(desiredValue, minValue, maxValue);
    }
}
