using UnityEngine;

public class LOTOYawFollowUI : MonoBehaviour
{
    public Transform headCamera;
    public Vector3 headSpaceOffset = new Vector3(0f, -0.08f, 1.45f);
    public float positionFollowSpeed = 4f;
    public float yawFollowSpeed = 4f;
    public float yawDeadZoneDegrees = 3f;
    public float yawOffsetDegrees = 0f;
    public bool followPosition = true;
    public bool followYaw = true;
    public bool snapOnEnable = true;
    public bool autoFindHeadCamera = true;
    public bool debugLogs;

    private bool snapped;

    private void OnEnable()
    {
        snapped = false;
        ResolveHeadCamera();
    }

    private void LateUpdate()
    {
        Transform head = ResolveHeadCamera();
        if (head == null)
        {
            return;
        }

        Quaternion headYaw = Quaternion.Euler(0f, head.eulerAngles.y, 0f);
        Vector3 desiredPosition = head.position + headYaw * headSpaceOffset;
        Quaternion desiredRotation = headYaw * Quaternion.Euler(0f, yawOffsetDegrees, 0f);

        if (snapOnEnable && !snapped)
        {
            transform.SetPositionAndRotation(desiredPosition, desiredRotation);
            snapped = true;
            LogPose("snapped");
            return;
        }

        float positionT = 1f - Mathf.Exp(-Mathf.Max(0.01f, positionFollowSpeed) * Time.deltaTime);
        float yawT = 1f - Mathf.Exp(-Mathf.Max(0.01f, yawFollowSpeed) * Time.deltaTime);

        if (followPosition)
        {
            transform.position = Vector3.Lerp(transform.position, desiredPosition, positionT);
        }

        if (followYaw)
        {
            float yawDelta = Mathf.Abs(Mathf.DeltaAngle(transform.eulerAngles.y, desiredRotation.eulerAngles.y));
            if (yawDelta > yawDeadZoneDegrees)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, yawT);
            }
        }
    }

    public void RecenterNow()
    {
        Transform head = ResolveHeadCamera();
        if (head == null)
        {
            return;
        }

        Quaternion headYaw = Quaternion.Euler(0f, head.eulerAngles.y, 0f);
        transform.SetPositionAndRotation(
            head.position + headYaw * headSpaceOffset,
            headYaw * Quaternion.Euler(0f, yawOffsetDegrees, 0f));
        snapped = true;
        LogPose("recentered");
    }

    private Transform ResolveHeadCamera()
    {
        if (headCamera != null || !autoFindHeadCamera)
        {
            return headCamera;
        }

        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            headCamera = mainCamera.transform;
            return headCamera;
        }

#if UNITY_2023_1_OR_NEWER
        OVRCameraRig cameraRig = FindFirstObjectByType<OVRCameraRig>(FindObjectsInactive.Include);
#else
        OVRCameraRig cameraRig = FindObjectOfType<OVRCameraRig>(true);
#endif
        if (cameraRig != null && cameraRig.centerEyeAnchor != null)
        {
            headCamera = cameraRig.centerEyeAnchor;
        }

        return headCamera;
    }

    private void LogPose(string action)
    {
        if (!debugLogs)
        {
            return;
        }

        Debug.Log($"LOTOYawFollowUI {name} {action} at {transform.position} yaw {transform.eulerAngles.y:0.0}.");
    }
}
