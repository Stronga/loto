using UnityEngine;
using UnityEngine.Rendering;

public class LOTOHighlightTarget : MonoBehaviour
{
    public Material highlightMaterial;
    public Renderer[] targetRenderers;
    [Range(0.05f, 1f)]
    public float highlightAlpha = 0.45f;

    private Material[][] originalMaterials;
    private bool[] originalEnabledStates;
    private Material runtimeHighlightMaterial;
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
                    highlightedMaterials[j] = GetHighlightMaterial();
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

    private Material GetHighlightMaterial()
    {
        if (highlightAlpha >= 0.999f)
        {
            return highlightMaterial;
        }

        if (runtimeHighlightMaterial == null)
        {
            runtimeHighlightMaterial = new Material(highlightMaterial);
            runtimeHighlightMaterial.name = highlightMaterial.name + "_Transparent_Runtime";
            ConfigureTransparentMaterial(runtimeHighlightMaterial, highlightAlpha);
        }

        return runtimeHighlightMaterial;
    }

    private static void ConfigureTransparentMaterial(Material material, float alpha)
    {
        if (material == null)
        {
            return;
        }

        if (material.HasProperty("_BaseColor"))
        {
            Color color = material.GetColor("_BaseColor");
            color.a = alpha;
            material.SetColor("_BaseColor", color);
        }
        else if (material.HasProperty("_Color"))
        {
            Color color = material.GetColor("_Color");
            color.a = alpha;
            material.SetColor("_Color", color);
        }

        if (material.HasProperty("_Surface"))
        {
            material.SetFloat("_Surface", 1f);
        }

        material.SetOverrideTag("RenderType", "Transparent");
        material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        material.SetInt("_ZWrite", 0);
        material.renderQueue = (int)RenderQueue.Transparent;
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.EnableKeyword("_ALPHABLEND_ON");
    }
}
