using System.Collections;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider))]
public class LOTOSnapObject : MonoBehaviour
{
    public LOTOStateController stateController;
    public LOTOActionType notifyAction = LOTOActionType.ApplyLock;
    public Transform snapTarget;
    public bool hideOriginalOnSnap;
    public AudioSource snapSound;
    public float minimumClickableWorldSize = 0.35f;
    public float snapDuration = 0.35f;
    public AnimationCurve snapEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    public UnityEvent snapCompleted;

    private bool hasSnapped;
    private Coroutine snapRoutine;
    private bool capturedInitialState;
    private Vector3 initialPosition;
    private Quaternion initialRotation;
    private Renderer[] cachedRenderers;
    private bool[] initialRendererStates;
    private Collider[] cachedColliders;
    private bool[] initialColliderStates;

    private void Awake()
    {
        CaptureInitialState();
        EnsureSnapAnimationDefaults();
        EnsureClickableCollider();

        if (stateController == null)
        {
            ResolveStateController();
        }
    }

    private void OnEnable()
    {
        if (!capturedInitialState)
        {
            CaptureInitialState();
        }

        ResetSnapState();
    }

    private void OnMouseDown()
    {
        TriggerSnap();
    }

    public void TriggerSnap()
    {
        EnsureSnapAnimationDefaults();
        ResolveStateController();

        if (hasSnapped || snapRoutine != null)
        {
            if (hasSnapped)
            {
                stateController?.CompleteSnapAction(notifyAction);
            }

            return;
        }

        if (snapTarget == null)
        {
            Debug.LogWarning($"LOTOSnapObject on {name} has no snap target assigned.");
            return;
        }

        if (!CanSnapNow())
        {
            return;
        }

        if (snapSound != null)
        {
            snapSound.Play();
        }

        if (snapDuration <= 0f)
        {
            CompleteSnap();
            return;
        }

        snapRoutine = StartCoroutine(AnimateSnap());
    }

    private IEnumerator AnimateSnap()
    {
        Vector3 startPosition = transform.position;
        Quaternion startRotation = transform.rotation;
        Vector3 targetPosition = snapTarget.position;
        Quaternion targetRotation = snapTarget.rotation;

        float elapsed = 0f;
        while (elapsed < snapDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / snapDuration);
            float easedT = snapEase != null ? snapEase.Evaluate(t) : t;
            transform.SetPositionAndRotation(
                Vector3.Lerp(startPosition, targetPosition, easedT),
                Quaternion.Slerp(startRotation, targetRotation, easedT));

            yield return null;
        }

        CompleteSnap();
        snapRoutine = null;
    }

    private void CompleteSnap()
    {
        transform.SetPositionAndRotation(snapTarget.position, snapTarget.rotation);

        ResolveStateController();
        if (stateController == null)
        {
            Debug.LogWarning($"LOTOSnapObject on {name} snapped but has no LOTO state controller to notify.");
            return;
        }

        if (!stateController.CompleteSnapAction(notifyAction))
        {
            Debug.LogWarning($"LOTOSnapObject on {name} snapped but could not notify the LOTO state controller.");
            return;
        }

        snapCompleted?.Invoke();
        hasSnapped = true;

        if (hideOriginalOnSnap)
        {
            HideRenderersAndColliders();
        }
    }

    public void ResetSnapState()
    {
        if (snapRoutine != null)
        {
            StopCoroutine(snapRoutine);
            snapRoutine = null;
        }

        hasSnapped = false;

        if (capturedInitialState)
        {
            transform.SetPositionAndRotation(initialPosition, initialRotation);
        }

        RestoreRendererAndColliderStates();
        EnsureClickableCollider();
    }

    private bool CanSnapNow()
    {
        ResolveStateController();

        if (stateController == null)
        {
            Debug.LogWarning($"LOTOSnapObject on {name} has no LOTOStateController assigned.");
            return false;
        }

        switch (notifyAction)
        {
            case LOTOActionType.ApplyLock:
                if (stateController.CanApplyLock)
                {
                    return true;
                }

                stateController.ApplyLock();
                return false;

            case LOTOActionType.ApplyTag:
                if (stateController.CanApplyTag)
                {
                    return true;
                }

                stateController.ApplyTag();
                return false;

            default:
                Debug.LogWarning("LOTOSnapObject only supports ApplyLock or ApplyTag notification actions.");
                return false;
        }
    }

    private void ResolveStateController()
    {
        if (stateController != null)
        {
            return;
        }

        if (LOTOStateController.Active != null)
        {
            stateController = LOTOStateController.Active;
            return;
        }

#if UNITY_2023_1_OR_NEWER
        stateController = FindFirstObjectByType<LOTOStateController>();
#else
        stateController = FindObjectOfType<LOTOStateController>();
#endif
    }

    private void EnsureSnapAnimationDefaults()
    {
        if (snapDuration <= 0f)
        {
            snapDuration = 0.35f;
        }

        if (snapEase == null || snapEase.length == 0)
        {
            snapEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        }
    }

    private void CaptureInitialState()
    {
        if (capturedInitialState)
        {
            return;
        }

        initialPosition = transform.position;
        initialRotation = transform.rotation;
        cachedRenderers = GetComponentsInChildren<Renderer>(true);
        initialRendererStates = new bool[cachedRenderers.Length];
        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            initialRendererStates[i] = cachedRenderers[i] != null && cachedRenderers[i].enabled;
        }

        cachedColliders = GetComponentsInChildren<Collider>(true);
        initialColliderStates = new bool[cachedColliders.Length];
        for (int i = 0; i < cachedColliders.Length; i++)
        {
            initialColliderStates[i] = cachedColliders[i] != null && cachedColliders[i].enabled;
        }

        capturedInitialState = true;
    }

    private void RestoreRendererAndColliderStates()
    {
        if (!capturedInitialState)
        {
            return;
        }

        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            if (cachedRenderers[i] != null && i < initialRendererStates.Length)
            {
                cachedRenderers[i].enabled = initialRendererStates[i];
            }
        }

        for (int i = 0; i < cachedColliders.Length; i++)
        {
            if (cachedColliders[i] != null && i < initialColliderStates.Length)
            {
                cachedColliders[i].enabled = initialColliderStates[i];
            }
        }
    }

    private void EnsureClickableCollider()
    {
        if (minimumClickableWorldSize <= 0f)
        {
            return;
        }

        BoxCollider boxCollider = GetComponent<BoxCollider>();
        if (boxCollider == null)
        {
            return;
        }

        Vector3 lossyScale = transform.lossyScale;
        Vector3 size = boxCollider.size;
        size.x = ExpandAxis(size.x, lossyScale.x);
        size.y = ExpandAxis(size.y, lossyScale.y);
        size.z = ExpandAxis(size.z, lossyScale.z);
        boxCollider.size = size;
    }

    private float ExpandAxis(float currentLocalSize, float worldScale)
    {
        float scale = Mathf.Abs(worldScale);
        if (scale <= 0.0001f)
        {
            return currentLocalSize;
        }

        float requiredLocalSize = minimumClickableWorldSize / scale;
        return Mathf.Max(currentLocalSize, requiredLocalSize);
    }

    private void HideRenderersAndColliders()
    {
        foreach (Renderer itemRenderer in GetComponentsInChildren<Renderer>())
        {
            itemRenderer.enabled = false;
        }

        foreach (Collider itemCollider in GetComponentsInChildren<Collider>())
        {
            itemCollider.enabled = false;
        }
    }
}
