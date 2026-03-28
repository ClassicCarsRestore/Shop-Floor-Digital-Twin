using System.Collections.Generic;
using UnityEngine;

public class HighlightTarget : MonoBehaviour
{
    [Header("Optional outline component ")]
    [SerializeField] private Behaviour outlineComponent;

    [Header("Fallback tint (if no outline)")]
    [SerializeField] private Color highlightColor = new Color(1f, 1f, 1f, 0.35f);

    private Renderer[] renderers;

    // MPB (não altera materiais reais)
    private MaterialPropertyBlock mpb;

    // cache de propriedade suportada por renderer (URP vs Standard)
    private bool[] supportsBaseColor;
    private bool[] supportsColor;

    private static readonly int BaseColorProp = Shader.PropertyToID("_BaseColor"); // URP
    private static readonly int ColorProp = Shader.PropertyToID("_Color");         // Standard

    private void Awake()
    {
        mpb = new MaterialPropertyBlock();
        RefreshRenderers();
        SetHighlight(false);
    }

    //  chamado após add/remove shelves para incluir novos renderers
    public void RefreshRenderers()
    {
        renderers = GetComponentsInChildren<Renderer>(true);

        supportsBaseColor = new bool[renderers.Length];
        supportsColor = new bool[renderers.Length];

        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null) continue;

            // Deteta se o material do renderer tem estas propriedades
            // (não cria instância — usa sharedMaterial)
            var m = r.sharedMaterial;
            if (m != null)
            {
                supportsBaseColor[i] = m.HasProperty(BaseColorProp);
                supportsColor[i] = m.HasProperty(ColorProp);
            }
        }
    }

    public void SetHighlight(bool on)
    {
        if (outlineComponent != null)
        {
            outlineComponent.enabled = on;
            return;
        }

        if (renderers == null || supportsBaseColor == null || supportsColor == null)
            RefreshRenderers();

        if (!on)
        {
            // limpar overrides (volta ao material normal)
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null) continue;
                r.SetPropertyBlock(null);
            }
            return;
        }

        // Aplicar tint por instância (MPB) sem tocar em materiais
        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null) continue;

            r.GetPropertyBlock(mpb);

            // Mantém o “alpha” como no highlightColor
            Color tint = highlightColor;

            // Set nas props que o shader suporta
            if (supportsBaseColor != null && i < supportsBaseColor.Length && supportsBaseColor[i])
                mpb.SetColor(BaseColorProp, tint);

            if (supportsColor != null && i < supportsColor.Length && supportsColor[i])
                mpb.SetColor(ColorProp, tint);

            // fallback: se não sabemos se suporta, tenta setar as duas (não faz mal)
            if ((supportsBaseColor == null || i >= supportsBaseColor.Length) &&
                (supportsColor == null || i >= supportsColor.Length))
            {
                mpb.SetColor(BaseColorProp, tint);
                mpb.SetColor(ColorProp, tint);
            }

            r.SetPropertyBlock(mpb);
        }
    }
}
