using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class LOTOStateController : MonoBehaviour
{
    public static LOTOStateController Active { get; private set; }

    [Header("Controllers")]
    public LOTOAnimationController animationController;
    public LOTOChecklistUI checklistUI;
    public LOTOWarningFeedback warningFeedback;

    [Header("Next Step Highlights")]
    public LOTOHighlightTarget switchBoxHighlight;
    public LOTOHighlightTarget powerHandleHighlight;
    public LOTOHighlightTarget lockHighlight;
    public LOTOHighlightTarget tagHighlight;
    public LOTOHighlightTarget mainDoorHighlight;

    [Header("Shutdown Timing")]
    public bool autoCompleteShutdown = true;
    public float shutdownDuration = 2.2f;
    public float fallbackPowerHandleDuration = 1f;

    [Header("Runtime State")]
    public bool switchBoxOpened;
    public bool switchBoxClosed;
    public bool powerHandleOff;
    public bool shutdownComplete;
    public bool lockApplied;
    public bool tagApplied;
    public bool mainDoorOpened;

    [Header("Events")]
    public UnityEvent stateChanged;
    public UnityEvent<string> warningIssued;

    private Coroutine shutdownCoroutine;
    private Material fallbackHighlightMaterial;

    public bool ShutdownInProgress { get; private set; }
    public bool CanApplyLock => switchBoxOpened && switchBoxClosed && powerHandleOff && shutdownComplete && !lockApplied;
    public bool CanApplyTag => lockApplied && !tagApplied;
    public bool CanOpenMainDoor => switchBoxOpened && switchBoxClosed && powerHandleOff && shutdownComplete && lockApplied && tagApplied;

    public bool IsCurrentAction(LOTOActionType actionType)
    {
        switch (actionType)
        {
            case LOTOActionType.OpenSwitchBox:
                return !switchBoxOpened || (switchBoxOpened && powerHandleOff && shutdownComplete && !switchBoxClosed);
            case LOTOActionType.TogglePowerHandle:
                return switchBoxOpened && !switchBoxClosed && !powerHandleOff;
            case LOTOActionType.ApplyLock:
                return CanApplyLock;
            case LOTOActionType.ApplyTag:
                return CanApplyTag;
            case LOTOActionType.TryOpenMainDoor:
                return CanOpenMainDoor && !mainDoorOpened;
            default:
                return false;
        }
    }

    private void Awake()
    {
        Active = this;

        if (animationController == null)
        {
            animationController = GetComponent<LOTOAnimationController>();
        }

        if (checklistUI == null)
        {
            checklistUI = GetComponent<LOTOChecklistUI>();
        }

        if (warningFeedback == null)
        {
            warningFeedback = GetComponent<LOTOWarningFeedback>();
        }

        RepairSceneReferences();
    }

    private void Start()
    {
        NotifyStateChanged();
    }

    private void Update()
    {
        SynchronizeStateFromSnappedObjects();
    }

    private void OnDestroy()
    {
        if (Active == this)
        {
            Active = null;
        }
    }

    public void OpenSwitchBox()
    {
        if (switchBoxOpened)
        {
            TryCloseSwitchBox();
            return;
        }

        switchBoxOpened = true;
        switchBoxClosed = false;
        animationController?.PlaySwitchBoxOpen();
        NotifyStateChanged();
    }

    public void TryCloseSwitchBox()
    {
        if (!switchBoxOpened)
        {
            ShowWarning("Open the switch box first.");
            return;
        }

        if (switchBoxClosed)
        {
            return;
        }

        if (!powerHandleOff)
        {
            ShowWarning("Turn the main power switch OFF before closing the switch box.");
            return;
        }

        if (!shutdownComplete)
        {
            ShowWarning("Wait for the generator to shut down before closing the switch box.");
            return;
        }

        switchBoxClosed = true;
        animationController?.PlaySwitchBoxClose();
        NotifyStateChanged();
    }

    public void TogglePowerHandle()
    {
        if (!switchBoxOpened)
        {
            ShowWarning("Open the switch box first.");
            return;
        }

        if (powerHandleOff)
        {
            return;
        }

        powerHandleOff = true;
        shutdownComplete = false;
        ShutdownInProgress = true;

        animationController?.PlayPowerHandleToggle();

        if (autoCompleteShutdown)
        {
            if (shutdownCoroutine != null)
            {
                StopCoroutine(shutdownCoroutine);
            }

            shutdownCoroutine = StartCoroutine(PlayShutdownAfterPowerHandle());
        }

        NotifyStateChanged();
    }

    public void CompleteShutdown()
    {
        if (!powerHandleOff)
        {
            ShowWarning("Turn the main power switch OFF before shutdown.");
            return;
        }

        if (shutdownComplete)
        {
            return;
        }

        ShutdownInProgress = false;
        shutdownComplete = true;
        shutdownCoroutine = null;
        NotifyStateChanged();
    }

    public void ApplyLock()
    {
        if (lockApplied)
        {
            return;
        }

        if (!switchBoxOpened)
        {
            ShowWarning("Open the switch box first.");
            return;
        }

        if (!powerHandleOff)
        {
            ShowWarning("Turn the main power switch OFF before shutdown.");
            return;
        }

        if (!switchBoxClosed)
        {
            ShowWarning("Close the switch box door before applying the lock.");
            return;
        }

        if (!shutdownComplete)
        {
            ShowWarning("Wait for the generator to shut down before applying lockout.");
            return;
        }

        lockApplied = true;
        NotifyStateChanged();
    }

    public void ApplyTag()
    {
        if (!lockApplied)
        {
            SynchronizeStateFromSnappedObjects();
        }

        if (tagApplied)
        {
            return;
        }

        if (!lockApplied)
        {
            ShowWarning("Apply the lock before attaching the warning tag.");
            return;
        }

        tagApplied = true;
        NotifyStateChanged();
    }

    public bool CompleteSnapAction(LOTOActionType actionType)
    {
        switch (actionType)
        {
            case LOTOActionType.ApplyLock:
                if (lockApplied)
                {
                    return true;
                }

                if (!CanApplyLock)
                {
                    ApplyLock();
                    return false;
                }

                lockApplied = true;
                NotifyStateChanged();
                return true;

            case LOTOActionType.ApplyTag:
                if (tagApplied)
                {
                    return true;
                }

                if (!CanApplyTag)
                {
                    ApplyTag();
                    return false;
                }

                tagApplied = true;
                NotifyStateChanged();
                return true;

            default:
                Debug.LogWarning("Only lock and tag snap actions can be completed by snapping.");
                return false;
        }
    }

    public void TryOpenMainDoor()
    {
        if (mainDoorOpened)
        {
            return;
        }

        if (!CanOpenMainDoor)
        {
            ShowWarning("Complete lockout/tagout before opening the service door.");
            return;
        }

        mainDoorOpened = true;
        animationController?.PlayMainDoorOpen();
        NotifyStateChanged();
    }

    public void ResetTraining()
    {
        if (shutdownCoroutine != null)
        {
            StopCoroutine(shutdownCoroutine);
            shutdownCoroutine = null;
        }

        switchBoxOpened = false;
        switchBoxClosed = false;
        powerHandleOff = false;
        shutdownComplete = false;
        lockApplied = false;
        tagApplied = false;
        mainDoorOpened = false;
        ShutdownInProgress = false;

        animationController?.ResetPoses();
        ResetSnapObjects();
        NotifyStateChanged();
    }

    public void ShowWarning(string message)
    {
        warningFeedback?.ShowWarning(message);
        warningIssued?.Invoke(message);
        Debug.LogWarning(message);
    }

    private IEnumerator CompleteShutdownAfterDelay()
    {
        yield return new WaitForSeconds(shutdownDuration);
        CompleteShutdown();
    }

    private IEnumerator PlayShutdownAfterPowerHandle()
    {
        float powerHandleDuration = animationController != null
            ? animationController.GetClipLength(animationController.powerHandleToggleClipName, fallbackPowerHandleDuration)
            : fallbackPowerHandleDuration;

        yield return new WaitForSeconds(powerHandleDuration);

        animationController?.PlayShutdownAndCableWiggle();

        float generatorShutdownDuration = animationController != null
            ? animationController.GetClipLength(animationController.generatorShutdownClipName, shutdownDuration)
            : shutdownDuration;

        yield return new WaitForSeconds(generatorShutdownDuration);
        CompleteShutdown();
    }

    private void NotifyStateChanged()
    {
        SynchronizeStateFromSnappedObjects(false);
        checklistUI?.UpdateChecklist(this);
        UpdateHighlights();
        stateChanged?.Invoke();
    }

    private void UpdateHighlights()
    {
        SetHighlight(switchBoxHighlight, !switchBoxOpened || (switchBoxOpened && powerHandleOff && shutdownComplete && !switchBoxClosed));
        SetHighlight(powerHandleHighlight, switchBoxOpened && !switchBoxClosed && !powerHandleOff);
        SetHighlight(lockHighlight, CanApplyLock);
        SetHighlight(tagHighlight, lockApplied && !tagApplied);
        SetHighlight(mainDoorHighlight, CanOpenMainDoor && !mainDoorOpened);
    }

    private static void SetHighlight(LOTOHighlightTarget target, bool active)
    {
        if (target != null)
        {
            target.SetHighlighted(active);
        }
    }

    private void RepairSceneReferences()
    {
        fallbackHighlightMaterial = FirstAssignedHighlightMaterial();

        if (checklistUI != null)
        {
            checklistUI.showLockTagSteps = true;
        }

        LOTOSnapObject padlock = ConfigureSnapObject(
            "Padlock",
            LOTOActionType.ApplyLock,
            "LockSnapTarget");

        LOTOSnapObject warningTag = ConfigureSnapObject(
            "WarningTag",
            LOTOActionType.ApplyTag,
            "TagSnapTarget");

        lockHighlight = EnsureSeparateIndicator("PadlockIndicator", padlock != null ? padlock.transform : null, lockHighlight);
        tagHighlight = EnsureSeparateIndicator("WarningTagIndicator", warningTag != null ? warningTag.transform : null, tagHighlight);

        checklistUI?.UpdateChecklist(this);
    }

    private void SynchronizeStateFromSnappedObjects(bool notifyWhenChanged = true)
    {
        bool changed = false;

        if (!lockApplied && switchBoxOpened && switchBoxClosed && powerHandleOff && shutdownComplete && IsObjectAtSnapTarget("Padlock", "LockSnapTarget"))
        {
            lockApplied = true;
            changed = true;
        }

        if (!tagApplied && lockApplied && IsObjectAtSnapTarget("WarningTag", "TagSnapTarget"))
        {
            tagApplied = true;
            changed = true;
        }

        if (changed && notifyWhenChanged)
        {
            NotifyStateChanged();
        }
    }

    private static bool IsObjectAtSnapTarget(string objectName, string targetName)
    {
        GameObject sourceObject = GameObject.Find(objectName);
        GameObject targetObject = GameObject.Find(targetName);
        if (sourceObject == null || targetObject == null)
        {
            return false;
        }

        return Vector3.Distance(sourceObject.transform.position, targetObject.transform.position) <= 0.05f;
    }

    private LOTOSnapObject ConfigureSnapObject(string objectName, LOTOActionType actionType, string snapTargetName)
    {
        GameObject snapObjectGameObject = GameObject.Find(objectName);
        if (snapObjectGameObject == null)
        {
            return null;
        }

        LOTOSnapObject snapObject = snapObjectGameObject.GetComponent<LOTOSnapObject>();
        if (snapObject == null)
        {
            snapObject = snapObjectGameObject.AddComponent<LOTOSnapObject>();
        }

        snapObject.stateController = this;
        snapObject.notifyAction = actionType;
        snapObject.minimumClickableWorldSize = Mathf.Max(snapObject.minimumClickableWorldSize, 0.45f);
        snapObject.hideOriginalOnSnap = false;

        GameObject targetObject = GameObject.Find(snapTargetName);
        if (targetObject != null)
        {
            snapObject.snapTarget = targetObject.transform;
        }

        foreach (LOTOHighlightTarget oldHighlight in snapObjectGameObject.GetComponents<LOTOHighlightTarget>())
        {
            oldHighlight.SetHighlighted(false);
            oldHighlight.enabled = false;
        }

        snapObject.ResetSnapState();
        return snapObject;
    }

    private static void ResetSnapObjects()
    {
#if UNITY_2023_1_OR_NEWER
        LOTOSnapObject[] snapObjects = FindObjectsByType<LOTOSnapObject>(FindObjectsSortMode.None);
#else
        LOTOSnapObject[] snapObjects = FindObjectsOfType<LOTOSnapObject>();
#endif

        foreach (LOTOSnapObject snapObject in snapObjects)
        {
            snapObject.ResetSnapState();
        }
    }

    private LOTOHighlightTarget EnsureSeparateIndicator(string indicatorName, Transform sourceTransform, LOTOHighlightTarget existingHighlight)
    {
        GameObject indicatorObject = GameObject.Find(indicatorName);
        if (indicatorObject == null)
        {
            indicatorObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            indicatorObject.name = indicatorName;

            Collider indicatorCollider = indicatorObject.GetComponent<Collider>();
            if (indicatorCollider != null)
            {
                Destroy(indicatorCollider);
            }
        }

        if (sourceTransform != null)
        {
            indicatorObject.transform.SetPositionAndRotation(sourceTransform.position, sourceTransform.rotation);
            indicatorObject.transform.localScale = sourceTransform.localScale * 1.3f;
        }

        Renderer indicatorRenderer = indicatorObject.GetComponent<Renderer>();
        if (indicatorRenderer != null)
        {
            indicatorRenderer.enabled = false;
        }

        LOTOHighlightTarget indicatorHighlight = indicatorObject.GetComponent<LOTOHighlightTarget>();
        if (indicatorHighlight == null)
        {
            indicatorHighlight = indicatorObject.AddComponent<LOTOHighlightTarget>();
        }

        indicatorHighlight.enabled = true;
        indicatorHighlight.highlightMaterial = existingHighlight != null && existingHighlight.highlightMaterial != null
            ? existingHighlight.highlightMaterial
            : fallbackHighlightMaterial;
        indicatorHighlight.targetRenderers = indicatorRenderer != null ? new[] { indicatorRenderer } : null;

        return indicatorHighlight;
    }

    private Material FirstAssignedHighlightMaterial()
    {
        if (lockHighlight != null && lockHighlight.highlightMaterial != null)
        {
            return lockHighlight.highlightMaterial;
        }

        if (tagHighlight != null && tagHighlight.highlightMaterial != null)
        {
            return tagHighlight.highlightMaterial;
        }

        if (switchBoxHighlight != null && switchBoxHighlight.highlightMaterial != null)
        {
            return switchBoxHighlight.highlightMaterial;
        }

        if (powerHandleHighlight != null && powerHandleHighlight.highlightMaterial != null)
        {
            return powerHandleHighlight.highlightMaterial;
        }

        return mainDoorHighlight != null ? mainDoorHighlight.highlightMaterial : null;
    }
}
