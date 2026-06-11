using UnityEngine;
using UnityEngine.EventSystems;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

[DefaultExecutionOrder(-100)]
public class LOTOEventSystemBootstrap : MonoBehaviour
{
    public bool createEventSystemIfMissing = true;

    private void Awake()
    {
        EnsureEventSystem();
    }

    public void EnsureEventSystem()
    {
        if (!createEventSystemIfMissing || EventSystem.current != null)
        {
            return;
        }

        GameObject eventSystemObject = new GameObject("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();

#if ENABLE_INPUT_SYSTEM
        eventSystemObject.AddComponent<InputSystemUIInputModule>();
#elif ENABLE_LEGACY_INPUT_MANAGER
        eventSystemObject.AddComponent<StandaloneInputModule>();
#endif
    }
}
