using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Serialization;

public class LOTOMRPlacementController : MonoBehaviour
{
    public Transform placementRoot;
    public Transform headCamera;
    public float forwardDistance = 3f;
    public float rightOffset = 0.5f;
    public float floorY = 0f;
    public bool faceUser = true;
    [FormerlySerializedAs("yawOffsetDegrees")]
    public float modelYawCorrectionDegrees = 0f;
    public float rootScale = 1f;
    public bool snapToFloor = true;
    public float floorRayStartHeight = 2.0f;
    public float floorRayDistance = 5.0f;
    public LayerMask floorRaycastMask = ~0;
    public float floorYOffset = 0f;
    public bool usePhysicsFloorFallback = true;

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
        position = ResolveGroundedPosition(position);
        placementRoot.position = position;

        if (faceUser)
        {
            Vector3 toUser = Vector3.ProjectOnPlane(head.position - placementRoot.position, Vector3.up);
            if (toUser.sqrMagnitude > 0.0001f)
            {
                placementRoot.rotation = Quaternion.LookRotation(toUser.normalized, Vector3.up) * Quaternion.Euler(0f, modelYawCorrectionDegrees, 0f);
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
        position = ResolveGroundedPosition(position);
        placementRoot.position = position;
    }

    private void RotateRoot(float degrees)
    {
        if (placementRoot != null)
        {
            placementRoot.Rotate(Vector3.up, degrees, Space.World);
        }
    }

    private Vector3 ResolveGroundedPosition(Vector3 targetPosition)
    {
        if (!snapToFloor)
        {
            targetPosition.y = floorY;
            return targetPosition;
        }

        Vector3 rayOrigin = targetPosition + Vector3.up * floorRayStartHeight;
        Ray floorRay = new Ray(rayOrigin, Vector3.down);

        if (TryMetaEnvironmentFloorRaycast(floorRay, out Vector3 metaFloorPoint, out string metaHitName))
        {
            targetPosition.y = metaFloorPoint.y + floorYOffset;
            Debug.Log($"LOTOMRPlacementController snapped to Meta environment floor '{metaHitName}' at {metaFloorPoint}.");
            return targetPosition;
        }

        if (usePhysicsFloorFallback && TryPhysicsFloorRaycast(floorRay, out RaycastHit hit))
        {
            targetPosition.y = hit.point.y + floorYOffset;
            Debug.Log($"LOTOMRPlacementController snapped to physics floor '{hit.collider.name}' at {hit.point}.");
            return targetPosition;
        }

        targetPosition.y = floorY;
        Debug.LogWarning($"LOTOMRPlacementController could not find a floor under {rayOrigin}; using floorY fallback {floorY}.");
        return targetPosition;
    }

    private bool TryPhysicsFloorRaycast(Ray floorRay, out RaycastHit floorHit)
    {
        floorHit = default;

        RaycastHit[] hits = Physics.RaycastAll(
            floorRay,
            floorRayDistance,
            floorRaycastMask,
            QueryTriggerInteraction.Ignore);

        if (hits == null || hits.Length == 0)
        {
            return false;
        }

        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];
            if (hit.collider == null)
            {
                continue;
            }

            if (placementRoot != null && hit.collider.transform.IsChildOf(placementRoot))
            {
                continue;
            }

            if (Vector3.Dot(hit.normal.normalized, Vector3.up) < 0.5f)
            {
                continue;
            }

            floorHit = hit;
            return true;
        }

        return false;
    }

    private bool TryMetaEnvironmentFloorRaycast(Ray ray, out Vector3 hitPoint, out string hitName)
    {
        hitPoint = Vector3.zero;
        hitName = "EnvironmentRaycastManager";

        Component environmentRaycastManager = FindOrCreateMetaEnvironmentRaycastManager();
        if (environmentRaycastManager == null)
        {
            return false;
        }

        Type managerType = environmentRaycastManager.GetType();
        Type hitType = FindType("Meta.XR.EnvironmentRaycastHit");
        if (hitType == null)
        {
            return false;
        }

        MethodInfo raycastMethod = managerType.GetMethod(
            "Raycast",
            new[] { typeof(Ray), hitType.MakeByRefType(), typeof(float) });

        if (raycastMethod == null)
        {
            return false;
        }

        object[] args = { ray, Activator.CreateInstance(hitType), floorRayDistance };
        bool didHit;
        try
        {
            didHit = (bool)raycastMethod.Invoke(environmentRaycastManager, args);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"LOTOMRPlacementController Meta environment raycast failed: {exception.Message}");
            return false;
        }

        object hit = args[1];
        if (!didHit && !IsEnvironmentHitStatus(hit, hitType, "HitPointOccluded"))
        {
            return false;
        }

        if (!TryGetVector3Member(hit, hitType, "point", out hitPoint))
        {
            return false;
        }

        if (TryGetVector3Member(hit, hitType, "normal", out Vector3 normal) && Vector3.Dot(normal.normalized, Vector3.up) < 0.5f)
        {
            return false;
        }

        hitName = environmentRaycastManager.name;
        return true;
    }

    private Component FindOrCreateMetaEnvironmentRaycastManager()
    {
        Type managerType = FindType("Meta.XR.EnvironmentRaycastManager");
        if (managerType == null || !typeof(Component).IsAssignableFrom(managerType))
        {
            return null;
        }

#if UNITY_2023_1_OR_NEWER
        MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        MonoBehaviour[] behaviours = FindObjectsOfType<MonoBehaviour>(true);
#endif

        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour != null && managerType.IsInstanceOfType(behaviour))
            {
                return behaviour;
            }
        }

        GameObject managerObject = new GameObject("EnvironmentRaycastManager");
        Component manager = managerObject.AddComponent(managerType);
        Debug.Log("LOTOMRPlacementController created Meta EnvironmentRaycastManager for floor snapping.");
        return manager;
    }

    private static Type FindType(string fullName)
    {
        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
        for (int i = 0; i < assemblies.Length; i++)
        {
            Type type = assemblies[i].GetType(fullName);
            if (type != null)
            {
                return type;
            }
        }

        return null;
    }

    private static bool TryGetVector3Member(object target, Type targetType, string memberName, out Vector3 value)
    {
        value = Vector3.zero;
        if (target == null)
        {
            return false;
        }

        FieldInfo field = targetType.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (field != null && field.GetValue(target) is Vector3 fieldValue)
        {
            value = fieldValue;
            return true;
        }

        PropertyInfo property = targetType.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (property != null && property.GetValue(target) is Vector3 propertyValue)
        {
            value = propertyValue;
            return true;
        }

        return false;
    }

    private static bool IsEnvironmentHitStatus(object target, Type targetType, string expectedStatusName)
    {
        if (target == null)
        {
            return false;
        }

        object statusValue = null;
        FieldInfo statusField = targetType.GetField("status", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (statusField != null)
        {
            statusValue = statusField.GetValue(target);
        }

        PropertyInfo statusProperty = targetType.GetProperty("status", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (statusValue == null && statusProperty != null)
        {
            statusValue = statusProperty.GetValue(target);
        }

        return statusValue != null && string.Equals(statusValue.ToString(), expectedStatusName, StringComparison.Ordinal);
    }
}
