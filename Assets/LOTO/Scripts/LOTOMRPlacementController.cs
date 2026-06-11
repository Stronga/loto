using UnityEngine;

public class LOTOMRPlacementController : MonoBehaviour
{
    public Transform placementRoot;
    public Transform headCamera;
    public float forwardDistance = 1.8f;
    public float rightOffset = 0.5f;
    public float floorY = 0f;
    public bool faceUser = true;
    public float yawOffsetDegrees = 0f;
    public float rootScale = 1f;

    private const float MoveStep = 0.25f;
    private const float RotateStepDegrees = 15f;
    private bool placedAtStartup;

    private void Start()
    {
        if (placedAtStartup)
        {
            return;
        }

        PlaceBesideUser();
        placedAtStartup = true;
    }

    public void PlaceBesideUser()
    {
        if (placementRoot == null)
        {
            Debug.LogWarning("LOTOMRPlacementController has no placement root assigned.");
            return;
        }

        Transform head = ResolveHeadCamera();
        if (head == null)
        {
            Debug.LogWarning("LOTOMRPlacementController could not find a head camera.");
            return;
        }

        Vector3 forward = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = Vector3.forward;
        }

        Vector3 right = Vector3.ProjectOnPlane(head.right, Vector3.up).normalized;
        if (right.sqrMagnitude < 0.0001f)
        {
            right = Vector3.right;
        }

        Vector3 position = head.position + forward * forwardDistance + right * rightOffset;
        position.y = floorY;
        placementRoot.position = position;

        if (faceUser)
        {
            Vector3 toUser = Vector3.ProjectOnPlane(head.position - placementRoot.position, Vector3.up);
            if (toUser.sqrMagnitude > 0.0001f)
            {
                placementRoot.rotation = Quaternion.LookRotation(toUser.normalized, Vector3.up) * Quaternion.Euler(0f, yawOffsetDegrees, 0f);
            }
        }

        placementRoot.localScale = Vector3.one * rootScale;
    }

    public void MoveCloser()
    {
        MoveRelativeToHead(-MoveStep);
    }

    public void MoveFarther()
    {
        MoveRelativeToHead(MoveStep);
    }

    public void RotateLeft()
    {
        RotateRoot(-RotateStepDegrees);
    }

    public void RotateRight()
    {
        RotateRoot(RotateStepDegrees);
    }

    private Transform ResolveHeadCamera()
    {
        if (headCamera != null)
        {
            return headCamera;
        }

        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            headCamera = mainCamera.transform;
        }

        return headCamera;
    }

    private void MoveRelativeToHead(float distance)
    {
        if (placementRoot == null)
        {
            return;
        }

        Transform head = ResolveHeadCamera();
        Vector3 direction = head != null
            ? Vector3.ProjectOnPlane(placementRoot.position - head.position, Vector3.up).normalized
            : placementRoot.forward;

        if (direction.sqrMagnitude < 0.0001f)
        {
            direction = placementRoot.forward;
        }

        Vector3 position = placementRoot.position + direction * distance;
        position.y = floorY;
        placementRoot.position = position;
    }

    private void RotateRoot(float degrees)
    {
        if (placementRoot != null)
        {
            placementRoot.Rotate(Vector3.up, degrees, Space.World);
        }
    }
}
