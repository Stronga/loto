using UnityEngine;

public class LOTOHighlightTarget : MonoBehaviour
{
    public Material highlightMaterial;
    public Renderer[] targetRenderers;

    private Material[][] originalMaterials;
    private bool[] originalEnabledStates;
    private bool initialized;

    private void Awake()
    {
        CacheOriginalMaterials();
    }

    public void SetHighlighted(bool active)
    {
        CacheOriginalMaterials();

        if (highlightMaterial == null || targetRenderers == null)
        {
            return;
        }

        for (int i = 0; i < targetRenderers.Length; i++)
        {
            Renderer targetRenderer = targetRenderers[i];
            if (targetRenderer == null)
            {
                continue;
            }

            if (active)
            {
                targetRenderer.enabled = true;

                Material[] highlightedMaterials = new Material[targetRenderer.sharedMaterials.Length];
                for (int j = 0; j < highlightedMaterials.Length; j++)
                {
                    highlightedMaterials[j] = highlightMaterial;
                }

                targetRenderer.sharedMaterials = highlightedMaterials;
            }
            else if (originalMaterials != null && i < originalMaterials.Length)
            {
                targetRenderer.sharedMaterials = originalMaterials[i];
                if (originalEnabledStates != null && i < originalEnabledStates.Length)
                {
                    targetRenderer.enabled = originalEnabledStates[i];
                }
            }
        }
    }

    private void CacheOriginalMaterials()
    {
        if (initialized)
        {
            return;
        }

        if (targetRenderers == null || targetRenderers.Length == 0)
        {
            targetRenderers = GetComponentsInChildren<Renderer>();
        }

        originalMaterials = new Material[targetRenderers.Length][];
        originalEnabledStates = new bool[targetRenderers.Length];
        for (int i = 0; i < targetRenderers.Length; i++)
        {
            originalMaterials[i] = targetRenderers[i] != null ? targetRenderers[i].sharedMaterials : null;
            originalEnabledStates[i] = targetRenderers[i] != null && targetRenderers[i].enabled;
        }

        initialized = true;
    }
}
