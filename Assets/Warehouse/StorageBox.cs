using UnityEngine;

public class StorageBox : MonoBehaviour
{
    [Header("Car Info")]
    public string CarId;

    [Header("Location Key (section-shelf-area)")]
    public string LocationKey;

    [Header("Visuals")]
    public Renderer boxRenderer;

    private Material originalMaterial;
    private Color originalColor;
    private bool hasOriginal = false;

    private void Awake()
    {
        if (boxRenderer != null)
        {
            // material instance (renderer.material cria instance)
            originalMaterial = boxRenderer.material;
            originalColor = boxRenderer.material.color;
            hasOriginal = true;
        }
    }

    public void Highlight(bool on)
    {
        if (boxRenderer == null || !hasOriginal) return;

        if (on)
        {
            boxRenderer.material.color = Color.yellow;
        }
        else
        {
            // volta ao original (cor original)
            boxRenderer.material.color = originalColor;
        }
    }
}
