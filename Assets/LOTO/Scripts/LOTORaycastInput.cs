using UnityEngine;
using UnityEngine.EventSystems;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DefaultExecutionOrder(-25)]
public class LOTORaycastInput : MonoBehaviour
{
    public Camera targetCamera;
    public LayerMask interactionMask = ~0;
    public float maxDistance = 100f;
    public bool blockWhenPointerOverUi;
    public bool debugHits;

    private void Awake()
    {
        if (targetCamera == null)
        {
            targetCamera = GetComponent<Camera>();
        }

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }
    }

    private void Update()
    {
        if (!TryGetPointerDownPosition(out Vector2 screenPosition))
        {
            return;
        }

        if (blockWhenPointerOverUi && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        TriggerAtScreenPosition(screenPosition);
    }

    public bool TriggerAtScreenPosition(Vector2 screenPosition)
    {
        if (targetCamera == null)
        {
            Debug.LogWarning("LOTORaycastInput has no target camera assigned.");
            return false;
        }

        Ray ray = targetCamera.ScreenPointToRay(screenPosition);
        return TriggerRaycast(ray);
    }

    public bool TriggerAtRay(Ray ray)
    {
        return TriggerRaycast(ray);
    }

    private bool TriggerRaycast(Ray ray)
    {
        RaycastHit[] hits = Physics.RaycastAll(ray, maxDistance, interactionMask, QueryTriggerInteraction.Collide);
        if (hits == null || hits.Length == 0)
        {
            if (debugHits)
            {
                Debug.Log($"LOTORaycastInput missed from ray origin {ray.origin}.");
            }

            return false;
        }

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        LOTOClickable fallbackClickable = null;
        LOTOSnapObject fallbackSnapObject = null;
        RaycastHit fallbackHit = new RaycastHit();

        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];
            LOTOClickable clickable = hit.collider.GetComponentInParent<LOTOClickable>();
            if (clickable != null && clickable.isActiveAndEnabled)
            {
                if (fallbackClickable == null && fallbackSnapObject == null)
                {
                    fallbackClickable = clickable;
                    fallbackHit = hit;
                }

                if (IsCurrentAction(clickable.actionType, clickable.stateController))
                {
                    LogTrigger(hit, clickable.actionType, true, "clickable");
                    clickable.TriggerAction();
                    return true;
                }

                continue;
            }

            LOTOSnapObject snapObject = hit.collider.GetComponentInParent<LOTOSnapObject>();
            if (snapObject != null && snapObject.isActiveAndEnabled)
            {
                if (fallbackClickable == null && fallbackSnapObject == null)
                {
                    fallbackSnapObject = snapObject;
                    fallbackHit = hit;
                }

                if (IsCurrentAction(snapObject.notifyAction, snapObject.stateController))
                {
                    LogTrigger(hit, snapObject.notifyAction, true, "snap object");
                    snapObject.TriggerSnap();
                    return true;
                }
            }
        }

        if (fallbackClickable != null)
        {
            LogTrigger(fallbackHit, fallbackClickable.actionType, false, "clickable");
            fallbackClickable.TriggerAction();
            return true;
        }

        if (fallbackSnapObject != null)
        {
            LogTrigger(fallbackHit, fallbackSnapObject.notifyAction, false, "snap object");
            fallbackSnapObject.TriggerSnap();
            return true;
        }

        if (debugHits)
        {
            Debug.Log($"LOTORaycastInput hit {hits[0].collider.name}, but no LOTO interaction was found.");
        }

        return false;
    }

    private static bool IsCurrentAction(LOTOActionType actionType, LOTOStateController stateController)
    {
        LOTOStateController controller = stateController != null ? stateController : LOTOStateController.Active;
        return controller != null && controller.IsCurrentAction(actionType);
    }

    private void LogTrigger(RaycastHit hit, LOTOActionType actionType, bool currentAction, string targetType)
    {
        if (!debugHits)
        {
            return;
        }

        string priority = currentAction ? "current step" : "fallback";
        Debug.Log($"LOTORaycastInput triggered {targetType} '{hit.collider.name}' for {actionType} ({priority}).");
    }

    private bool TryGetPointerDownPosition(out Vector2 screenPosition)
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            screenPosition = Mouse.current.position.ReadValue();
            return true;
        }

        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            screenPosition = Touchscreen.current.primaryTouch.position.ReadValue();
            return true;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetMouseButtonDown(0))
        {
            screenPosition = Input.mousePosition;
            return true;
        }

        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
        {
            screenPosition = Input.GetTouch(0).position;
            return true;
        }
#endif

        screenPosition = Vector2.zero;
        return false;
    }
}
