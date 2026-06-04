using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField, Min(0f)] private float smoothTime = 0.15f;

    private Vector3 offset;
    private Vector3 velocity;
    private bool hasOffset;

    private void Awake()
    {
        TryAssignTarget();
        CacheOffset();
    }

    private void LateUpdate()
    {
        TryAssignTarget();
        CacheOffset();

        if (target == null)
        {
            return;
        }

        Vector3 desiredPosition = target.position + offset;

        if (smoothTime <= 0f)
        {
            transform.position = desiredPosition;
            return;
        }

        transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref velocity, smoothTime);
    }

    private void TryAssignTarget()
    {
        if (target != null)
        {
            return;
        }

        GameObject playerObject = GameObject.Find("Player");
        if (playerObject != null)
        {
            target = playerObject.transform;
            hasOffset = false;
        }
    }

    private void CacheOffset()
    {
        if (target == null || hasOffset)
        {
            return;
        }

        offset = transform.position - target.position;
        hasOffset = true;
    }
}
