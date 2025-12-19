using UnityEngine;

public class StorageBox : MonoBehaviour
{
    [Header("Car Info")]
    public string CarId;

    [Header("Visuals")]
    public Renderer boxRenderer;

    [SerializeField]
    bool isHighlighted = false;

    private Material originalMaterial;

    private void Awake()
    {
        if (boxRenderer != null)
        {
            originalMaterial = boxRenderer.material;
        }
    }

    public void Highlight(bool on)
    {
        if (boxRenderer == null || originalMaterial == null) return;

        
        if (on)
        {
            boxRenderer.material.color = Color.yellow;
        }
        else
        {
            boxRenderer.material = originalMaterial;
        }
    }
}
