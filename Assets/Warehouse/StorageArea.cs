using UnityEngine;

public class StorageArea : MonoBehaviour
{
    [Header("Area Info")]
    public string AreaId;

    [Header("State")]
    public string Status;  // "free" | "occupied"
    public string ItemId;  // null ou "CAR-01"

    [Header("Slot")]
    public Transform BoxAnchor;

    // Visual/Collider do slot (a “fatia” da prateleira)
    public Transform SlotVisual;
    public BoxCollider SlotCollider;

    [Header("Visual")]
    [SerializeField] private Color freeColor = new Color(0.02f, 0.43f, 0.02f, 0.11f);
    [SerializeField] private Color occupiedColor = new Color(1f, 0f, 0f, 0.15f);
    [SerializeField] private bool tintWhenOccupied = false;

    private MaterialPropertyBlock mpb;
    private static readonly int BaseColorProp = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorProp = Shader.PropertyToID("_Color");

    private void Awake()
    {
        mpb ??= new MaterialPropertyBlock();
    }

    public bool IsOccupied()
    {
        if (!string.IsNullOrWhiteSpace(ItemId)) return true;
        return string.Equals(Status, "occupied", System.StringComparison.OrdinalIgnoreCase);
    }

    public void UpdateVisual()
    {
        if (SlotVisual == null) return;

        var r = SlotVisual.GetComponent<Renderer>();
        if (r == null) return;

        // Se estiver ocupado, tornar slot invisível (transparência 0)
        bool occupied = IsOccupied();
        Color tint = occupied ? new Color(0f, 0f, 0f, 0f) : freeColor;

        r.GetPropertyBlock(mpb);
        mpb.SetColor(BaseColorProp, tint);
        mpb.SetColor(ColorProp, tint);
        r.SetPropertyBlock(mpb);
    }
}