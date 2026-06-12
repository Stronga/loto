using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

public class LOTOXRControllerRayInput : MonoBehaviour
{
    public LOTORaycastInput raycastInput;
    public Transform rightRayOrigin;
    public Transform leftRayOrigin;
    public bool useRightController = true;
    public bool useLeftController = true;
    public bool enableFallbackTriggerInput = true;
    public bool disableWhenMetaInteractionRigPresent = false;
    public string metaInteractionRigName = "Controllers";
    public bool enableRayGrabSnapObjects = true;
    public float minimumGrabDistance = 0.25f;
    public bool enableToolkitUiInteraction = true;
    public LOTOChecklistUI checklistUI;
    public bool debugLogs = true;
    public bool drawDebugRays = true;
    public float debugRayLength = 10f;
    public bool enableVisibleRay = true;
    public LineRenderer rayLine;
    public float rayLength = 10f;
    public float rayWidth = 0.01f;
    public Color normalColor = Color.white;
    public Color hitColor = Color.green;
    public Color missColor = Color.red;
    public Transform hitReticle;

    private readonly List<InputDevice> devices = new List<InputDevice>();
    private bool rightWasPressed;
    private bool leftWasPressed;
    private bool loggedMissingRaycastInput;
    private bool loggedMissingRightOrigin;
    private bool loggedMissingLeftOrigin;
    private bool loggedMissingRightDevice;
    private bool loggedMissingLeftDevice;
    private bool loggedMissingChecklistUi;
    private bool loggedRayVisualActive;
    private Collider lastRayHitCollider;
    private bool lastRayHitWasUseful;
    private string lastUiHitName;
    private bool lastUiHitWasActionable;
    private LOTOSnapObject grabbedSnapObject;
    private Transform grabbedRayOrigin;
    private float grabbedDistance;
    private Rigidbody grabbedRigidbody;
    private bool grabbedRigidbodyWasKinematic;

    private void Awake()
    {
        if (raycastInput == null)
        {
#if UNITY_2023_1_OR_NEWER
            raycastInput = FindFirstObjectByType<LOTORaycastInput>();
#else
            raycastInput = FindObjectOfType<LOTORaycastInput>();
#endif
        }

        ResolveChecklistUi();
        EnsureRayLine();
    }

    private void Start()
    {
        DisableForMetaInteractionRigIfPresent();
    }

    private void Update()
    {
        if (raycastInput == null)
        {
            LogOnce(ref loggedMissingRaycastInput, "LOTOXRControllerRayInput has no LOTORaycastInput assigned.");
            return;
        }

        if (enableVisibleRay)
        {
            UpdateVisibleRay(GetActiveRayOrigin());
        }
        else
        {
            SetRayVisible(false);
            SetReticleVisible(false);
        }

        if (!enableFallbackTriggerInput)
        {
            return;
        }

        if (useRightController)
        {
            HandleController(
                InputDeviceCharacteristics.Controller | InputDeviceCharacteristics.HeldInHand | InputDeviceCharacteristics.Right,
                rightRayOrigin,
                ref rightWasPressed,
                "right",
                ref loggedMissingRightOrigin,
                ref loggedMissingRightDevice);
        }

        if (useLeftController)
        {
            HandleController(
                InputDeviceCharacteristics.Controller | InputDeviceCharacteristics.HeldInHand | InputDeviceCharacteristics.Left,
                leftRayOrigin,
                ref leftWasPressed,
                "left",
                ref loggedMissingLeftOrigin,
                ref loggedMissingLeftDevice);
        }
    }

    private void HandleController(
        InputDeviceCharacteristics characteristics,
        Transform rayOrigin,
        ref bool wasPressed,
        string controllerName,
        ref bool loggedMissingOrigin,
        ref bool loggedMissingDevice)
    {
        if (rayOrigin == null)
        {
            LogOnce(ref loggedMissingOrigin, $"LOTOXRControllerRayInput has no {controllerName} ray origin assigned.");
            return;
        }

        if (drawDebugRays)
        {
            Debug.DrawRay(rayOrigin.position, rayOrigin.forward * debugRayLength, Color.green);
        }

        bool isPressed = IsSelectPressed(characteristics, controllerName, ref loggedMissingDevice);
        if (isPressed && !wasPressed)
        {
            Ray ray = new Ray(rayOrigin.position, rayOrigin.forward);
            Log($"LOTOXRControllerRayInput {controllerName} trigger pressed.");

            if (TryTriggerToolkitUi(ray, controllerName))
            {
                wasPressed = isPressed;
                return;
            }

            if (TryBeginSnapObjectGrab(rayOrigin, ray, controllerName))
            {
                wasPressed = isPressed;
                return;
            }

            bool didTrigger = raycastInput.TriggerAtRay(ray);
            Log($"LOTOXRControllerRayInput TriggerAtRay returned {didTrigger} for {controllerName} controller.");
        }
        else if (isPressed && grabbedSnapObject != null && grabbedRayOrigin == rayOrigin)
        {
            UpdateGrabbedSnapObject();
        }
        else if (!isPressed && wasPressed && grabbedSnapObject != null && grabbedRayOrigin == rayOrigin)
        {
            ReleaseGrabbedSnapObject(controllerName);
        }

        wasPressed = isPressed;
    }

    private bool TryBeginSnapObjectGrab(Transform rayOrigin, Ray ray, string controllerName)
    {
        if (!enableRayGrabSnapObjects || rayOrigin == null)
        {
            return false;
        }

        if (!TryFindSnapObject(ray, out LOTOSnapObject snapObject, out RaycastHit hit))
        {
            return false;
        }

        if (!IsCurrentSnapAction(snapObject))
        {
            snapObject.TriggerSnap();
            return true;
        }

        grabbedSnapObject = snapObject;
        grabbedRayOrigin = rayOrigin;
        grabbedDistance = Mathf.Clamp(hit.distance, minimumGrabDistance, rayLength);
        grabbedRigidbody = grabbedSnapObject.GetComponent<Rigidbody>();
        if (grabbedRigidbody != null)
        {
            grabbedRigidbodyWasKinematic = grabbedRigidbody.isKinematic;
            grabbedRigidbody.isKinematic = true;
        }

        UpdateGrabbedSnapObject();
        Log($"LOTOXRControllerRayInput {controllerName} grabbed '{grabbedSnapObject.name}' at distance {grabbedDistance:0.00}.");
        return true;
    }

    private void UpdateGrabbedSnapObject()
    {
        if (grabbedSnapObject == null || grabbedRayOrigin == null)
        {
            return;
        }

        Vector3 targetPosition = grabbedRayOrigin.position + grabbedRayOrigin.forward * grabbedDistance;
        grabbedSnapObject.transform.position = targetPosition;
    }

    private void ReleaseGrabbedSnapObject(string controllerName)
    {
        LOTOSnapObject releasedSnapObject = grabbedSnapObject;
        Rigidbody releasedRigidbody = grabbedRigidbody;

        grabbedSnapObject = null;
        grabbedRayOrigin = null;
        grabbedRigidbody = null;

        if (releasedRigidbody != null)
        {
            releasedRigidbody.isKinematic = grabbedRigidbodyWasKinematic;
        }

        if (releasedSnapObject == null)
        {
            return;
        }

        Log($"LOTOXRControllerRayInput {controllerName} released '{releasedSnapObject.name}', triggering snap.");
        releasedSnapObject.TriggerSnap();
    }

    private bool TryFindSnapObject(Ray ray, out LOTOSnapObject snapObject, out RaycastHit snapHit)
    {
        snapObject = null;
        snapHit = default;

        RaycastHit[] hits = Physics.RaycastAll(
            ray,
            rayLength,
            raycastInput.interactionMask,
            QueryTriggerInteraction.Collide);

        if (hits == null || hits.Length == 0)
        {
            return false;
        }

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        for (int i = 0; i < hits.Length; i++)
        {
            LOTOSnapObject hitSnapObject = hits[i].collider.GetComponentInParent<LOTOSnapObject>();
            if (hitSnapObject == null || !hitSnapObject.isActiveAndEnabled)
            {
                continue;
            }

            snapObject = hitSnapObject;
            snapHit = hits[i];
            return true;
        }

        return false;
    }

    private bool TryTriggerToolkitUi(Ray ray, string controllerName)
    {
        if (!enableToolkitUiInteraction)
        {
            return false;
        }

        LOTOChecklistUI ui = ResolveChecklistUi();
        if (ui == null)
        {
            LogOnce(ref loggedMissingChecklistUi, "LOTOXRControllerRayInput has no LOTOChecklistUI assigned for UI ray interaction.");
            return false;
        }

        bool didTrigger = ui.TryTriggerToolkitAtRay(ray, rayLength, out Vector3 hitPoint, out float distance, out string targetName);
        if (didTrigger)
        {
            Log($"LOTOXRControllerRayInput {controllerName} UI trigger selected '{targetName}' at distance {distance:0.00}.");
        }

        return didTrigger;
    }

    private LOTOChecklistUI ResolveChecklistUi()
    {
        if (checklistUI != null)
        {
            return checklistUI;
        }

#if UNITY_2023_1_OR_NEWER
        checklistUI = FindFirstObjectByType<LOTOChecklistUI>(FindObjectsInactive.Include);
#else
        checklistUI = FindObjectOfType<LOTOChecklistUI>(true);
#endif
        return checklistUI;
    }

    private static bool IsCurrentSnapAction(LOTOSnapObject snapObject)
    {
        if (snapObject == null)
        {
            return false;
        }

        LOTOStateController controller = snapObject.stateController != null
            ? snapObject.stateController
            : LOTOStateController.Active;

        return controller != null && controller.IsCurrentAction(snapObject.notifyAction);
    }

    private bool IsSelectPressed(InputDeviceCharacteristics characteristics, string controllerName, ref bool loggedMissingDevice)
    {
        devices.Clear();
        InputDevices.GetDevicesWithCharacteristics(characteristics, devices);

        if (devices.Count == 0)
        {
            LogOnce(ref loggedMissingDevice, $"LOTOXRControllerRayInput found no {controllerName} controller devices for {characteristics}.");
            return false;
        }

        loggedMissingDevice = false;

        for (int i = 0; i < devices.Count; i++)
        {
            InputDevice device = devices[i];
            if (!device.isValid)
            {
                continue;
            }

            if (device.TryGetFeatureValue(CommonUsages.triggerButton, out bool triggerPressed) && triggerPressed)
            {
                return true;
            }

            if (device.TryGetFeatureValue(CommonUsages.primaryButton, out bool primaryPressed) && primaryPressed)
            {
                return true;
            }
        }

        return false;
    }

    private Transform GetActiveRayOrigin()
    {
        InputDeviceCharacteristics rightCharacteristics =
            InputDeviceCharacteristics.Controller | InputDeviceCharacteristics.HeldInHand | InputDeviceCharacteristics.Right;
        InputDeviceCharacteristics leftCharacteristics =
            InputDeviceCharacteristics.Controller | InputDeviceCharacteristics.HeldInHand | InputDeviceCharacteristics.Left;

        if (useRightController && rightRayOrigin != null && HasControllerDevice(rightCharacteristics))
        {
            return rightRayOrigin;
        }

        if (useLeftController && leftRayOrigin != null && HasControllerDevice(leftCharacteristics))
        {
            return leftRayOrigin;
        }

        if (useRightController && rightRayOrigin != null)
        {
            return rightRayOrigin;
        }

        if (useLeftController && leftRayOrigin != null)
        {
            return leftRayOrigin;
        }

        return null;
    }

    private bool HasControllerDevice(InputDeviceCharacteristics characteristics)
    {
        devices.Clear();
        InputDevices.GetDevicesWithCharacteristics(characteristics, devices);

        for (int i = 0; i < devices.Count; i++)
        {
            if (devices[i].isValid)
            {
                return true;
            }
        }

        return false;
    }

    private void EnsureRayLine()
    {
        if (rayLine == null)
        {
            GameObject lineObject = new GameObject("LOTO Visible Controller Ray");
            lineObject.transform.SetParent(transform, false);
            rayLine = lineObject.AddComponent<LineRenderer>();
        }

        rayLine.positionCount = 2;
        rayLine.useWorldSpace = true;
        rayLine.widthMultiplier = rayWidth;
        rayLine.numCapVertices = 4;
        rayLine.enabled = false;

        if (rayLine.sharedMaterial == null)
        {
            rayLine.sharedMaterial = CreateRayMaterial();
        }

        SetRayColor(normalColor);
        LogOnce(ref loggedRayVisualActive, "LOTOXRControllerRayInput ray visual active.");
    }

    private void DisableForMetaInteractionRigIfPresent()
    {
        if (!disableWhenMetaInteractionRigPresent || string.IsNullOrEmpty(metaInteractionRigName))
        {
            return;
        }

        GameObject metaInteractionRig = GameObject.Find(metaInteractionRigName);
        if (metaInteractionRig == null)
        {
            return;
        }

        enableFallbackTriggerInput = false;
        enableVisibleRay = false;
        drawDebugRays = false;
        SetRayVisible(false);
        SetReticleVisible(false);
        Log($"LOTOXRControllerRayInput disabled because Meta interaction rig '{metaInteractionRigName}' is present.");
        enabled = false;
    }

    private static Material CreateRayMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }

        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        if (shader == null)
        {
            shader = Shader.Find("Hidden/Internal-Colored");
        }

        if (shader == null)
        {
            Debug.LogWarning("LOTOXRControllerRayInput could not find an unlit shader for the visible ray.");
            return null;
        }

        Material material = new Material(shader);
        material.name = "LOTO Controller Ray Material";
        return material;
    }

    private void UpdateVisibleRay(Transform rayOrigin)
    {
        EnsureRayLine();

        if (rayOrigin == null)
        {
            SetRayVisible(false);
            SetReticleVisible(false);
            return;
        }

        Ray ray = new Ray(rayOrigin.position, rayOrigin.forward);
        bool hasUiHit = TryGetToolkitUiHit(ray, out Vector3 uiHitPoint, out float uiHitDistance, out string uiHitName, out bool uiHitActionable);
        RaycastHit[] hits = Physics.RaycastAll(
            ray,
            rayLength,
            raycastInput.interactionMask,
            QueryTriggerInteraction.Collide);

        if (hits != null && hits.Length > 0)
        {
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            RaycastHit hit = hits[0];
            if (hasUiHit && uiHitDistance <= hit.distance)
            {
                DrawRay(ray.origin, uiHitPoint, uiHitActionable ? hitColor : missColor);
                UpdateReticle(uiHitPoint, true);
                LogUiVisualHit(uiHitName, uiHitDistance, uiHitActionable);
                return;
            }

            bool usefulHit = IsLOTOInteraction(hit.collider);

            DrawRay(ray.origin, hit.point, usefulHit ? hitColor : missColor);
            UpdateReticle(hit.point, true);
            LogVisualHit(hit, usefulHit);
            return;
        }

        if (hasUiHit)
        {
            DrawRay(ray.origin, uiHitPoint, uiHitActionable ? hitColor : missColor);
            UpdateReticle(uiHitPoint, true);
            LogUiVisualHit(uiHitName, uiHitDistance, uiHitActionable);
            return;
        }

        DrawRay(ray.origin, ray.origin + ray.direction * rayLength, normalColor);
        UpdateReticle(Vector3.zero, false);
        LogVisualMiss();
    }

    private bool TryGetToolkitUiHit(Ray ray, out Vector3 hitPoint, out float hitDistance, out string targetName, out bool actionable)
    {
        hitPoint = Vector3.zero;
        hitDistance = 0f;
        targetName = string.Empty;
        actionable = false;

        if (!enableToolkitUiInteraction)
        {
            return false;
        }

        LOTOChecklistUI ui = ResolveChecklistUi();
        return ui != null && ui.TryGetToolkitRayHit(ray, rayLength, out hitPoint, out hitDistance, out targetName, out actionable);
    }

    private static bool IsLOTOInteraction(Collider collider)
    {
        if (collider == null)
        {
            return false;
        }

        LOTOClickable clickable = collider.GetComponentInParent<LOTOClickable>();
        if (clickable != null && clickable.isActiveAndEnabled)
        {
            return true;
        }

        LOTOSnapObject snapObject = collider.GetComponentInParent<LOTOSnapObject>();
        return snapObject != null && snapObject.isActiveAndEnabled;
    }

    private void DrawRay(Vector3 start, Vector3 end, Color color)
    {
        SetRayVisible(true);
        rayLine.widthMultiplier = rayWidth;
        rayLine.SetPosition(0, start);
        rayLine.SetPosition(1, end);
        SetRayColor(color);
    }

    private void SetRayVisible(bool visible)
    {
        if (rayLine != null)
        {
            rayLine.enabled = visible;
        }
    }

    private void SetRayColor(Color color)
    {
        if (rayLine == null)
        {
            return;
        }

        rayLine.startColor = color;
        rayLine.endColor = color;

        Material material = rayLine.material;
        if (material == null)
        {
            return;
        }

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }
        else if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }
    }

    private void UpdateReticle(Vector3 position, bool visible)
    {
        if (hitReticle == null)
        {
            return;
        }

        hitReticle.gameObject.SetActive(visible);
        if (visible)
        {
            hitReticle.position = position;
        }
    }

    private void SetReticleVisible(bool visible)
    {
        if (hitReticle != null)
        {
            hitReticle.gameObject.SetActive(visible);
        }
    }

    private void LogVisualHit(RaycastHit hit, bool usefulHit)
    {
        if (hit.collider == lastRayHitCollider && usefulHit == lastRayHitWasUseful && string.IsNullOrEmpty(lastUiHitName))
        {
            return;
        }

        lastRayHitCollider = hit.collider;
        lastRayHitWasUseful = usefulHit;
        lastUiHitName = string.Empty;
        Log($"LOTOXRControllerRayInput ray hit '{hit.collider.name}' at distance {hit.distance:0.00}. Useful LOTO target: {usefulHit}.");
    }

    private void LogUiVisualHit(string targetName, float distance, bool actionable)
    {
        if (targetName == lastUiHitName && actionable == lastUiHitWasActionable)
        {
            return;
        }

        lastRayHitCollider = null;
        lastRayHitWasUseful = false;
        lastUiHitName = targetName;
        lastUiHitWasActionable = actionable;
        Log($"LOTOXRControllerRayInput ray hit UI '{targetName}' at distance {distance:0.00}. Actionable UI target: {actionable}.");
    }

    private void LogVisualMiss()
    {
        if (lastRayHitCollider == null && string.IsNullOrEmpty(lastUiHitName))
        {
            return;
        }

        lastRayHitCollider = null;
        lastRayHitWasUseful = false;
        lastUiHitName = string.Empty;
        lastUiHitWasActionable = false;
        Log("LOTOXRControllerRayInput ray hit nothing useful.");
    }

    private void Log(string message)
    {
        if (debugLogs)
        {
            Debug.Log(message);
        }
    }

    private void LogOnce(ref bool logged, string message)
    {
        if (logged)
        {
            return;
        }

        logged = true;
        Log(message);
    }
}
