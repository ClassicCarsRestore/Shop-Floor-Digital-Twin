using UnityEngine;

public class StorageBox : MonoBehaviour
{
    [Header("Car Info")]
    public string CarId;

    [Header("Location Key (section-shelf-area)")]
    public string LocationKey;

    [Header("Visuals")]
    public Renderer boxRenderer;

    [Header("Fit")]
    [SerializeField, Range(0.1f, 1f)] private float padding = 0.98f;
    [SerializeField] private bool autoFit = true;

    private Vector3 baseLocalScale;
    private bool hasBaseScale;

    private Color originalColor;
    private bool hasOriginal = false;

    private void Awake()
    {
        baseLocalScale = transform.localScale;
        hasBaseScale = true;

        if (boxRenderer != null)
        {
            // renderer.material cria instância -> ok para mudar cor, mas cuidado com leaks (ver nota no fim)
            originalColor = boxRenderer.material.color;
            hasOriginal = true;
        }
        if (autoFit) FitToAreaSlot();

    }



    public void Highlight(bool on)
    {
        if (boxRenderer == null || !hasOriginal) return;
        boxRenderer.material.color = on ? Color.yellow : originalColor;
    }

    private void OnTransformParentChanged()
    {
        if (autoFit) FitToAreaSlot();
    }

    public void FitToAreaSlot()
    {
        var area = GetComponentInParent<StorageArea>();
        if (area == null) return;

        // preferir collider do slot
        if (area.SlotCollider == null) return;

        var slotBounds = area.SlotCollider.bounds;

        if (boxRenderer == null) return;

        // IMPORTANTE: Fit pode ser chamado várias vezes (OnTransformParentChanged).
        // Para evitar encolher/crescer cumulativamente, recomeçamos sempre da escala base.
        if (!hasBaseScale)
        {
            baseLocalScale = transform.localScale;
            hasBaseScale = true;
        }
        transform.localScale = baseLocalScale;

        // colocar no centro (X/Z) do slot primeiro
        transform.position = new Vector3(slotBounds.center.x, slotBounds.center.y, slotBounds.center.z);

        // tamanho alvo (queremos que a box tenha o MESMO footprint do slot)
        Vector3 desired = slotBounds.size * padding;

        // tamanho atual do mesh (world) com a escala base
        var meshB = boxRenderer.bounds;
        if (meshB.size.x < 1e-4f || meshB.size.z < 1e-4f) return;

        // fit baseado em X/Z (footprint). Não usamos Y porque a altura do modelo
        // não deve tornar a box minúscula.
        float fx = desired.x / meshB.size.x;
        float fz = desired.z / meshB.size.z;
        float f = Mathf.Min(fx, fz);

        transform.localScale = baseLocalScale * f;

        // reposicionar para ficar em cima do slot (evita ficar "metade enterrada")
        var newBounds = boxRenderer.bounds;
        float lift = (slotBounds.max.y - newBounds.min.y) + 0.002f;
        transform.position += new Vector3(0f, lift, 0f);
    }

    private bool TryGetWorldBounds(Transform t, out Bounds bounds)
    {
        bounds = default;

        // Preferir Collider: dá bounds mais estáveis para "área"
        var c = t.GetComponentInChildren<Collider>();
        if (c != null) { bounds = c.bounds; return true; }

        var r = t.GetComponentInChildren<Renderer>();
        if (r != null) { bounds = r.bounds; return true; }

        return false;
    }
}

