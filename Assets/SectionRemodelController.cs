using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SectionRemodelController : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject remodelPanel;

    [SerializeField] private TMP_InputField widthInput;
    [SerializeField] private TMP_InputField heightInput;
    [SerializeField] private TMP_InputField lengthInput;

    [SerializeField] private Slider widthSlider;
    [SerializeField] private Slider heightSlider;
    [SerializeField] private Slider lengthSlider;

    [SerializeField] private Button saveRemodelButton;
    [SerializeField] private Button cancelRemodelButton;

    [SerializeField] private SectionPlacementController placementController;
    [SerializeField] private WarehouseSectionInteractor sectionInteractor;

    [Header("Scale Limits")]
    [SerializeField] private Vector2 widthRange = new Vector2(1f, 3f);
    [SerializeField] private Vector2 heightRange = new Vector2(1f, 3f);
    [SerializeField] private Vector2 lengthRange = new Vector2(1f, 3f);

    [Header("Remodel Validation")]
    [SerializeField] private Vector2 warehouseBoundsX = new Vector2(0, 0);
    [SerializeField] private Vector2 warehouseBoundsZ = new Vector2(0, 0);

    [SerializeField] private LayerMask collisionMask;       // layer das estantes existentes
    [SerializeField] private float overlapPadding = 0.05f;

    [Header("Remodel Visual Feedback")]
    [SerializeField] private Color validTint = new Color(0f, 1f, 0f, 0.35f);
    [SerializeField] private Color invalidTint = new Color(1f, 0f, 0f, 0.35f);

    private bool canSave = true;

    private Renderer[] sectionRenderers;
    private BoxCollider sectionCollider;

    // MaterialPropertyBlock (não altera materiais reais)
    private MaterialPropertyBlock mpb;
    private static readonly int ColorProp = Shader.PropertyToID("_Color");
    private static readonly int BaseColorProp = Shader.PropertyToID("_BaseColor");

    public event Action<ShelfSection> OnRemodelStarted;
    public event Action<ShelfSection, bool> OnRemodelFinished; // bool saved

    public bool IsRemodeling { get; private set; }

    private ShelfSection current;
    private Vector3 originalScale;

    private bool isSyncingUI = false;

    private void Awake()
    {
        if (remodelPanel != null) remodelPanel.SetActive(false);

        // listeners (uma vez) - apenas sliders
        if (widthSlider != null) widthSlider.onValueChanged.AddListener(_ => OnSliderChanged());
        if (heightSlider != null) heightSlider.onValueChanged.AddListener(_ => OnSliderChanged());
        if (lengthSlider != null) lengthSlider.onValueChanged.AddListener(_ => OnSliderChanged());

        if (saveRemodelButton != null) saveRemodelButton.onClick.AddListener(Save);
        if (cancelRemodelButton != null) cancelRemodelButton.onClick.AddListener(Cancel);

        mpb = new MaterialPropertyBlock();

        ApplyRangesToSliders();

        // garantir que os inputs são apenas display (sem edição)
        if (widthInput != null) widthInput.interactable = false;
        if (heightInput != null) heightInput.interactable = false;
        if (lengthInput != null) lengthInput.interactable = false;
    }

    private void ApplyRangesToSliders()
    {
        if (widthSlider != null) { widthSlider.minValue = widthRange.x; widthSlider.maxValue = widthRange.y; }
        if (heightSlider != null) { heightSlider.minValue = heightRange.x; heightSlider.maxValue = heightRange.y; }
        if (lengthSlider != null) { lengthSlider.minValue = lengthRange.x; lengthSlider.maxValue = lengthRange.y; }
    }

    public void StartRemodel(ShelfSection section)
    {
        if (section == null) return;

        if (placementController != null) placementController.SetAddButtonInteractable(false);
        if (sectionInteractor != null) sectionInteractor.IsActive = false;

        current = section;
        originalScale = current.transform.localScale;
        sectionCollider = current.GetComponentInChildren<BoxCollider>();
        sectionRenderers = current.GetComponentsInChildren<Renderer>(true);

        IsRemodeling = true;
        if (remodelPanel != null) remodelPanel.SetActive(true);

        OnRemodelStarted?.Invoke(current);

        // IMPORTANTE: ranges fixos e só sincronizar UI, sem aplicar scale
        ApplyRangesToSliders();
        SyncUIFromSection();
        ValidateAndApplyFeedback();
    }

    private void SyncUIFromSection()
    {
        if (current == null) return;

        isSyncingUI = true;

        Vector3 s = current.transform.localScale;

        // sliders mostram o valor “clamped” dentro do range
        float sx = Mathf.Clamp(s.x, widthRange.x, widthRange.y);
        float sy = Mathf.Clamp(s.y, heightRange.x, heightRange.y);
        float sz = Mathf.Clamp(s.z, lengthRange.x, lengthRange.y);

        if (widthSlider != null) widthSlider.SetValueWithoutNotify(sx);
        if (heightSlider != null) heightSlider.SetValueWithoutNotify(sy);
        if (lengthSlider != null) lengthSlider.SetValueWithoutNotify(sz);

        // inputs mostram o valor REAL atual (display)
        if (widthInput != null) widthInput.SetTextWithoutNotify(s.x.ToString("0.##"));
        if (heightInput != null) heightInput.SetTextWithoutNotify(s.y.ToString("0.##"));
        if (lengthInput != null) lengthInput.SetTextWithoutNotify(s.z.ToString("0.##"));

        isSyncingUI = false;
    }

    private void OnSliderChanged()
    {
        if (!IsRemodeling || current == null) return;
        if (isSyncingUI) return;

        ApplyScaleFromUI();
    }

    private void ApplyScaleFromUI()
    {
        float x = widthSlider != null ? widthSlider.value : current.transform.localScale.x;
        float y = heightSlider != null ? heightSlider.value : current.transform.localScale.y;
        float z = lengthSlider != null ? lengthSlider.value : current.transform.localScale.z;

        // clamp (mesmo que alguém force values)
        x = Mathf.Clamp(x, widthRange.x, widthRange.y);
        y = Mathf.Clamp(y, heightRange.x, heightRange.y);
        z = Mathf.Clamp(z, lengthRange.x, lengthRange.y);

        current.transform.localScale = new Vector3(x, y, z);

        isSyncingUI = true;
        if (widthInput != null) widthInput.SetTextWithoutNotify(x.ToString("0.##"));
        if (heightInput != null) heightInput.SetTextWithoutNotify(y.ToString("0.##"));
        if (lengthInput != null) lengthInput.SetTextWithoutNotify(z.ToString("0.##"));
        isSyncingUI = false;

        ValidateAndApplyFeedback();
    }

    private void Save()
    {
        if (!IsRemodeling || current == null) return;
        if (!canSave) return;

        var sec = current;

        ClearRemodelVisual();
        End();
        OnRemodelFinished?.Invoke(sec, true);
    }

    private void Cancel()
    {
        var sec = current;

        if (current != null)
            current.transform.localScale = originalScale;

        ClearRemodelVisual();
        End();
        OnRemodelFinished?.Invoke(sec, false);
    }

    private void End()
    {
        IsRemodeling = false;
        current = null;

        if (placementController != null) placementController.SetAddButtonInteractable(true);
        if (sectionInteractor != null) sectionInteractor.IsActive = true;

        if (remodelPanel != null) remodelPanel.SetActive(false);

        sectionCollider = null;
        sectionRenderers = null;
        canSave = true;
    }

    private void ValidateAndApplyFeedback()
    {
        if (current == null) return;

        Physics.SyncTransforms();
        canSave = ValidateBounds() && ValidateCollisions();

        if (saveRemodelButton != null)
            saveRemodelButton.interactable = canSave;

        UpdateRemodelVisual(canSave ? validTint : invalidTint);
    }

    private bool ValidateBounds()
    {
        if (sectionCollider == null) return true;

        Bounds b = sectionCollider.bounds;

        // limites X
        if (b.min.x < warehouseBoundsX.x) return false;
        if (b.max.x > warehouseBoundsX.y) return false;

        // limites Z
        if (b.min.z < warehouseBoundsZ.x) return false;
        if (b.max.z > warehouseBoundsZ.y) return false;

        return true;
    }

    private bool ValidateCollisions()
    {
        if (sectionCollider == null) return true;

        Vector3 center = sectionCollider.bounds.center;
        Vector3 halfExtents = sectionCollider.bounds.extents + Vector3.one * overlapPadding;
        Quaternion rot = current.transform.rotation;

        Collider[] hits = Physics.OverlapBox(center, halfExtents, rot, collisionMask, QueryTriggerInteraction.Ignore);

        foreach (var h in hits)
        {
            if (h == null) continue;

            // ignorar a própria section
            if (h.transform.IsChildOf(current.transform)) continue;

            return false;
        }

        return true;
    }

    private void UpdateRemodelVisual(Color tint)
    {
        if (sectionRenderers == null) return;

        for (int i = 0; i < sectionRenderers.Length; i++)
        {
            var r = sectionRenderers[i];
            if (r == null) continue;

            r.GetPropertyBlock(mpb);
            mpb.SetColor(BaseColorProp, tint); // URP
            mpb.SetColor(ColorProp, tint);     // Standard
            r.SetPropertyBlock(mpb);
        }
    }

    private void ClearRemodelVisual()
    {
        if (sectionRenderers == null) return;

        for (int i = 0; i < sectionRenderers.Length; i++)
        {
            var r = sectionRenderers[i];
            if (r == null) continue;

            r.SetPropertyBlock(null);
        }
    }
}
