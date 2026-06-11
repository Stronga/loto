using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider))]
public class LOTOClickable : MonoBehaviour
{
    public LOTOStateController stateController;
    public LOTOActionType actionType;
    public UnityEvent actionTriggered;

    private void Awake()
    {
        if (stateController == null)
        {
            ResolveStateController();
        }
    }

    private void OnMouseDown()
    {
        TriggerAction();
    }

    public void TriggerAction()
    {
        ResolveStateController();

        if (stateController == null)
        {
            Debug.LogWarning($"LOTOClickable on {name} has no LOTOStateController assigned.");
            return;
        }

        switch (actionType)
        {
            case LOTOActionType.OpenSwitchBox:
                stateController.OpenSwitchBox();
                break;
            case LOTOActionType.TogglePowerHandle:
                stateController.TogglePowerHandle();
                break;
            case LOTOActionType.ApplyLock:
                stateController.ApplyLock();
                break;
            case LOTOActionType.ApplyTag:
                stateController.ApplyTag();
                break;
            case LOTOActionType.TryOpenMainDoor:
                stateController.TryOpenMainDoor();
                break;
        }

        actionTriggered?.Invoke();
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
}
