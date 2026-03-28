using UnityEngine;

public class HighlightOnHoverTarget : MonoBehaviour
{
    [SerializeField] private Behaviour outlineComponent; // arrasta aqui o componente Outline (ou similar)

    private void Awake()
    {
        SetHighlight(false);
    }

    public void SetHighlight(bool on)
    {
        if (outlineComponent != null)
            outlineComponent.enabled = on;
    }
}
