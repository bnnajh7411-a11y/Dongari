using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Collider2D leftWall;
    [SerializeField] private Collider2D rightWall;
    [SerializeField, Min(0f)] private float smoothTime = 0.15f;

    private const string PlayerObjectName = "Player";
    private const string LeftWallObjectName = "LWall";
    private const string RightWallObjectName = "RWall";

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
            GetClampedCameraX(target.position.x),
            target.position.y,
            target.position.z + depthOffset);

        if (smoothTime <= 0f)
        {
            transform.position = desiredPosition;
            return;
        }

        float nextX = Mathf.SmoothDamp(transform.position.x, desiredPosition.x, ref xVelocity, smoothTime);
        float nextY = Mathf.SmoothDamp(transform.position.y, desiredPosition.y, ref yVelocity, smoothTime);
        float clampedX = GetClampedCameraX(nextX);

        if (!Mathf.Approximately(clampedX, nextX))
        {
            xVelocity = 0f;
        }

        transform.position = new Vector3(clampedX, nextY, desiredPosition.z);
    }

    private void TryAssignReferences()
    {
        TryAssignTarget();
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

    private float GetClampedCameraX(float desiredCameraX)
    {
        if (leftWall == null || rightWall == null || attachedCamera == null || target == null)
        {
            return desiredCameraX;
        }

        float focusPlaneDistance = Mathf.Abs(transform.position.z - target.position.z);
        Vector3 leftViewportEdge = attachedCamera.ViewportToWorldPoint(new Vector3(0f, 0.5f, focusPlaneDistance));
        Vector3 rightViewportEdge = attachedCamera.ViewportToWorldPoint(new Vector3(1f, 0.5f, focusPlaneDistance));
        float cameraHalfWidth = (rightViewportEdge.x - leftViewportEdge.x) * 0.5f;

        float minCameraX = leftWall.bounds.min.x + cameraHalfWidth;
        float maxCameraX = rightWall.bounds.max.x - cameraHalfWidth;

        if (minCameraX > maxCameraX)
        {
            return (minCameraX + maxCameraX) * 0.5f;
        }

        return Mathf.Clamp(desiredCameraX, minCameraX, maxCameraX);
    }
}
