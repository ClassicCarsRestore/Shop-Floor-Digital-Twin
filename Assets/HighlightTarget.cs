using System.Collections.Generic;
using UnityEngine;

public class HighlightTarget : MonoBehaviour
{
    [Header("Optional outline component (ex: Outline)")]
    [SerializeField] private Behaviour outlineComponent;

    [Header("Fallback tint (if no outline)")]
    [SerializeField] private Color highlightColor = new Color(1f, 1f, 1f, 0.35f);

    private Renderer[] renderers;
    private readonly List<Color> originalColors = new List<Color>();

    private void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>(true);
        originalColors.Clear();
        foreach (var r in renderers)
            originalColors.Add(r.material.color);

        SetHighlight(false);
    }

    public void SetHighlight(bool on)
    {
        if (outlineComponent != null)
        {
            outlineComponent.enabled = on;
            return;
        }

        // Fallback: tint
        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null) continue;

            Color baseColor = originalColors.Count > i ? originalColors[i] : r.material.color;

            if (on)
            {
                Color newColor = baseColor;
                newColor.r = Mathf.Lerp(baseColor.r, highlightColor.r, 0.6f);
                newColor.g = Mathf.Lerp(baseColor.g, highlightColor.g, 0.6f);
                newColor.b = Mathf.Lerp(baseColor.b, highlightColor.b, 0.6f);
                newColor.a = Mathf.Clamp01(highlightColor.a);
                r.material.color = newColor;
            }
            else
            {
                var c = baseColor;
                c.a = 1f;
                r.material.color = c;
            }
        }
    }
}
