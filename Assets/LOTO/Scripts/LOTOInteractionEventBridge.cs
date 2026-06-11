using UnityEngine;

public class LOTOInteractionEventBridge : MonoBehaviour
{
    public LOTOClickable lotoClickable;
    public LOTOSnapObject snapObject;
    public bool debugLogs = true;

    private void Reset()
    {
        ResolveTargets();
    }

    private void Awake()
    {
        ResolveTargets();
    }

    public void Select()
    {
        ResolveTargets();

        if (lotoClickable != null && lotoClickable.isActiveAndEnabled)
        {
            Log($"LOTOInteractionEventBridge selected clickable '{lotoClickable.name}'.");
            lotoClickable.TriggerAction();
            return;
        }

        if (snapObject != null && snapObject.isActiveAndEnabled)
        {
            Log($"LOTOInteractionEventBridge selected snap object '{snapObject.name}'.");
            snapObject.TriggerSnap();
            return;
        }

        Log($"LOTOInteractionEventBridge on '{name}' has no active LOTO target.");
    }

    private void ResolveTargets()
    {
        if (lotoClickable == null)
        {
            lotoClickable = GetComponent<LOTOClickable>();
        }

        if (snapObject == null)
        {
            snapObject = GetComponent<LOTOSnapObject>();
        }
    }

    private void Log(string message)
    {
        if (debugLogs)
        {
            Debug.Log(message);
        }
    }
}
