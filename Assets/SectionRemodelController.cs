using System;
using System.Collections.Generic;
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
    [SerializeField] private LayerMask wallsMask;           // paredes (colliders)

    [Header("Remodel Bounds From Walls")]
    [SerializeField] private bool useWallsAsHardBounds = true;
    [SerializeField] private float boundsEpsilon = 0.001f;

    [Header("Remodel Visual Feedback")]
    [SerializeField] private Color validTint = new Color(0f, 1f, 0f, 0.35f);
    [SerializeField] private Color invalidTint = new Color(1f, 0f, 0f, 0.35f);


    [Header("UI - Areas Per Shelf")]
    [SerializeField] private Transform areasListContent;
    [SerializeField] private ShelfAreasRow rowPrefab; // script simples no prefab da linha

    [Header("Area Limits")]
    [SerializeField] private int minAreas = 1;
    [SerializeField] private int maxAreas = 24;



    // estado editável (por shelf index)
    private readonly List<int> areaCountPerShelf = new List<int>();
    private readonly List<ShelfAreasRow> spawnedRows = new List<ShelfAreasRow>();

    private bool canSave = true;
    private bool lastBoundsValid = true;
    private bool lastCollisionsValid = true;
    private bool wallBoundsReady = false;
    private Vector2 wallBoundsX;
    private Vector2 wallBoundsZ;

    private Renderer[] sectionRenderers;
    private BoxCollider sectionCollider;

    // MaterialPropertyBlock (não altera materiais reais)
    private MaterialPropertyBlock mpb;
    private static readonly int ColorProp = Shader.PropertyToID("_Color");
    private static readonly int BaseColorProp = Shader.PropertyToID("_BaseColor");

    public event Action<ShelfSection> OnRemodelStarted;
    public event Action<ShelfSection, bool> OnRemodelFinished;

    public bool IsRemodeling { get; private set; }

    private ShelfSection current;
    private Vector3 originalScale;

    private bool isSyncingUI = false;

    private const float RowHeight = 50f;
    private const float RowSpacing = 10f;

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

        BuildAreasRowsFromSection(section);
        RebuildWallBoundsFromColliders();


        sectionCollider = ResolveSectionCollider(current);
        sectionRenderers = current.GetComponentsInChildren<Renderer>(true);

        IsRemodeling = true;
        if (remodelPanel != null) remodelPanel.SetActive(true);

        OnRemodelStarted?.Invoke(current);


        ApplyRangesToSliders();
        SyncUIFromSection();
        ValidateAndApplyFeedback();
    }

    private void SyncUIFromSection()
    {
        if (current == null) return;

        isSyncingUI = true;

        Vector3 s = current.transform.localScale;


        float sx = Mathf.Clamp(s.x, widthRange.x, widthRange.y);
        float sy = Mathf.Clamp(s.y, heightRange.x, heightRange.y);
        float sz = Mathf.Clamp(s.z, lengthRange.x, lengthRange.y);

        if (widthSlider != null) widthSlider.SetValueWithoutNotify(sx);
        if (heightSlider != null) heightSlider.SetValueWithoutNotify(sy);
        if (lengthSlider != null) lengthSlider.SetValueWithoutNotify(sz);


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





    private void BuildAreasRowsFromSection(ShelfSection section)
    {
        ClearRows();
        areaCountPerShelf.Clear();

        if (section == null || section.Shelves == null) return;

        for (int i = 0; i < section.Shelves.Count; i++)
        {
            var shelf = section.Shelves[i];
            int count = shelf != null && shelf.Areas != null ? shelf.Areas.Count : 1;
            count = Mathf.Clamp(count, minAreas, maxAreas);
            areaCountPerShelf.Add(count);

            var row = Instantiate(rowPrefab);
            row.transform.SetParent(areasListContent, false);

            // guarda referência para depois conseguir limpar em ClearRows()
            spawnedRows.Add(row);

            var rt = (RectTransform)row.transform;

            // garante anchors/pivot corretos (top-left)
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);

            // layout manual
            float rowH = 30f;
            float spacing = 5f;
            rt.anchoredPosition = new Vector2(0f, -i * (rowH + spacing));
            rt.sizeDelta = new Vector2(420f, rowH); // ou usa o width do content


            int idx = i;

            row.SetLabel($"Shelf {i + 1}");
            row.SetCount(areaCountPerShelf[idx]);

            row.OnMinus = () =>
 {
     TryDecreaseAreaCount(idx, row);
 };

            row.OnPlus = () =>
            {
                // aumenta SEM restrições (não remove nada, por isso é seguro)
                int before = areaCountPerShelf[idx];
                int after = Mathf.Min(maxAreas, before + 1);

                areaCountPerShelf[idx] = after;
                row.SetCount(after);

                // ao aumentar, o "-" pode voltar a ficar ativo
                RefreshRowInteractivity(idx, row);
            };

            // define interatividade inicial dos botões
            RefreshRowInteractivity(idx, row);
        }
    }


    private bool ValidateAllShelvesBeforeSave()
    {
        for (int i = 0; i < current.Shelves.Count; i++)
        {
            var shelf = current.Shelves[i];
            if (shelf == null) continue;

            int desired = (i < areaCountPerShelf.Count) ? areaCountPerShelf[i] : minAreas;
            if (!CanReduceAreas(shelf, desired, out string blockingAreaId))
            {
                Debug.LogWarning($"[Remodel] Save blocked. Shelf {i + 1} would remove occupied area: {blockingAreaId}");
                return false;
            }
        }
        return true;
    }


    private void ClearRows()
    {
        for (int i = 0; i < spawnedRows.Count; i++)
        {
            if (spawnedRows[i] != null)
                Destroy(spawnedRows[i].gameObject);
        }
        spawnedRows.Clear();
    }

    private void Save()
    {
        if (!IsRemodeling || current == null) return;
        if (!canSave) return;

        if (!ValidateAllShelvesBeforeSave())
        {
            Debug.LogWarning("[Remodel] Cannot save. Validation failed for one or more shelves.");
            return;
        }

        var sec = current;

        Physics.SyncTransforms();

        // 2) aplicar areas por shelf
        for (int i = 0; i < current.Shelves.Count; i++)
        {
            var shelf = current.Shelves[i];
            if (shelf == null) continue;

            int desired = (i < areaCountPerShelf.Count) ? areaCountPerShelf[i] : 1;

            int shelfIndex = i + 1;
            ShelfAreasBuilder.RebuildAreas(shelf, desired, current.SectionId, shelfIndex);
        }


        ClearRemodelVisual();
        End();
        OnRemodelFinished?.Invoke(sec, true);
    }

    // Considera "ocupada" se Status == "occupied" OU se ItemId não está vazio.
    // (Isto cobre inconsistências: Status pode falhar, mas ItemId é a verdade.)
    private static bool IsAreaOccupied(StorageArea area)
    {
        if (area == null) return false;

        bool hasItem = !string.IsNullOrWhiteSpace(area.ItemId);
        bool statusOccupied = string.Equals(area.Status, "occupied", StringComparison.OrdinalIgnoreCase);

        return hasItem || statusOccupied;
    }

    /// <summary>
    /// Regra: só permite reduzir para desiredCount se TODAS as áreas no "tail"
    /// (indices desiredCount..currentCount-1) estiverem livres.
    /// </summary>
    private bool CanReduceAreas(Shelf shelf, int desiredCount, out string blockingAreaId)
    {
        blockingAreaId = null;

        if (shelf == null || shelf.Areas == null) return true;

        int currentCount = shelf.Areas.Count;
        desiredCount = Mathf.Max(minAreas, desiredCount);

        // não é redução
        if (desiredCount >= currentCount) return true;

        // vamos remover: desiredCount+1 .. currentCount (1-based)
        // em 0-based: desiredCount .. currentCount-1
        for (int i = currentCount - 1; i >= desiredCount; i--)
        {
            var area = shelf.Areas[i];
            if (IsAreaOccupied(area))
            {
                blockingAreaId = area != null ? area.AreaId : $"index={i}";
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Tenta diminuir 1 área na shelf idx, respeitando a regra.
    /// Atualiza UI (contador + estado do botão).
    /// </summary>
    private void TryDecreaseAreaCount(int shelfIdx, ShelfAreasRow row)
    {
        if (current == null || current.Shelves == null) return;
        if (shelfIdx < 0 || shelfIdx >= current.Shelves.Count) return;

        var shelf = current.Shelves[shelfIdx];
        if (shelf == null) return;

        int currentDesired = areaCountPerShelf[shelfIdx];
        int nextDesired = Mathf.Max(minAreas, currentDesired - 1);

        // se já está no mínimo
        if (nextDesired == currentDesired)
        {
            row?.SetMinusInteractable(false);
            return;
        }

        if (!CanReduceAreas(shelf, nextDesired, out string blockingAreaId))
        {
            Debug.LogWarning($"[Remodel] Cannot reduce Shelf {shelfIdx + 1} to {nextDesired}. Blocking occupied area: {blockingAreaId}");

            // Mantém valor, e bloqueia o botão "-" para deixar claro
            row?.SetMinusInteractable(false);
            return;
        }

        // OK: reduz
        areaCountPerShelf[shelfIdx] = nextDesired;
        row?.SetCount(nextDesired);

        // Depois de reduzir, ainda pode reduzir mais?
        row?.SetMinusInteractable(CanReduceAreas(shelf, nextDesired - 1, out _));
    }

    /// <summary>
    /// Atualiza o estado do botão "-" (e opcionalmente "+") sempre que desenhas linhas.
    /// </summary>
    private void RefreshRowInteractivity(int shelfIdx, ShelfAreasRow row)
    {
        if (current == null || current.Shelves == null) return;
        if (row == null) return;

        var shelf = current.Shelves[shelfIdx];
        if (shelf == null) return;

        int desired = areaCountPerShelf[shelfIdx];

        // Pode reduzir mais 1?
        bool canMinus = desired > minAreas && CanReduceAreas(shelf, desired - 1, out _);
        row.SetMinusInteractable(canMinus);

        // Pode aumentar? (regra simples)
        bool canPlus = desired < maxAreas;
        row.SetPlusInteractable(canPlus);
    }

    private void Cancel()
    {
        var sec = current;

        if (current != null)
            current.transform.localScale = originalScale;

        Physics.SyncTransforms();

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

        ClearRows();
        areaCountPerShelf.Clear();

    }

    private void ValidateAndApplyFeedback()
    {
        if (current == null) return;

        Physics.SyncTransforms();
        lastBoundsValid = ValidateBounds();
        lastCollisionsValid = ValidateCollisions();
        canSave = lastBoundsValid && lastCollisionsValid;

        if (!canSave)
            Debug.Log($"[SectionRemodelController][VALIDATION] invalid bounds={lastBoundsValid} collisions={lastCollisionsValid} section={current.SectionId}");

        if (saveRemodelButton != null)
            saveRemodelButton.interactable = canSave;

        UpdateRemodelVisual(canSave ? validTint : invalidTint);
    }

    private bool ValidateBounds()
    {
        if (sectionCollider == null) return true;

        Bounds b = sectionCollider.bounds;

        if (placementController != null && placementController.TryGetPlacementBounds(out var placementX, out var placementZ))
        {
            bool insideX = b.min.x >= placementX.x - boundsEpsilon && b.max.x <= placementX.y + boundsEpsilon;
            bool insideZ = b.min.z >= placementZ.x - boundsEpsilon && b.max.z <= placementZ.y + boundsEpsilon;
            return insideX && insideZ;
        }

        if (useWallsAsHardBounds && wallBoundsReady)
        {
            bool insideX = b.min.x >= wallBoundsX.x - boundsEpsilon && b.max.x <= wallBoundsX.y + boundsEpsilon;
            bool insideZ = b.min.z >= wallBoundsZ.x - boundsEpsilon && b.max.z <= wallBoundsZ.y + boundsEpsilon;
            return insideX && insideZ;
        }

        // Se era para usar paredes mas não conseguimos calcular wall bounds,
        // não bloquear o remodel por configuração incompleta.
        if (useWallsAsHardBounds && !wallBoundsReady)
            return true;

        // Se bounds manuais não estão configurados no Inspector, não invalidar.
        bool manualBoundsConfigured = warehouseBoundsX.y > warehouseBoundsX.x && warehouseBoundsZ.y > warehouseBoundsZ.x;
        if (!manualBoundsConfigured)
            return true;

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

        Vector3 center = sectionCollider.transform.TransformPoint(sectionCollider.center);
        Vector3 halfExtents = Vector3.Scale(AbsVec3(sectionCollider.size) * 0.5f, AbsVec3(sectionCollider.transform.lossyScale));
        halfExtents += Vector3.one * overlapPadding;
        Quaternion rot = sectionCollider.transform.rotation;

        LayerMask mask = collisionMask | (useWallsAsHardBounds ? wallsMask : 0);
        Collider[] hits = Physics.OverlapBox(center, halfExtents, rot, mask, QueryTriggerInteraction.Ignore);

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
            if (IsStorageAreaRenderer(r)) continue;

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
            if (IsStorageAreaRenderer(r)) continue;

            r.SetPropertyBlock(null);
        }

        RestoreAreasVisuals(current);
    }

    private static bool IsStorageAreaRenderer(Renderer renderer)
    {
        return renderer != null && renderer.GetComponentInParent<StorageArea>() != null;
    }

    private static void RestoreAreasVisuals(ShelfSection section)
    {
        if (section == null || section.Shelves == null) return;

        for (int i = 0; i < section.Shelves.Count; i++)
        {
            var shelf = section.Shelves[i];
            if (shelf == null || shelf.Areas == null) continue;

            for (int a = 0; a < shelf.Areas.Count; a++)
            {
                var area = shelf.Areas[a];
                if (area == null) continue;
                area.UpdateVisual();
            }
        }
    }

    private static Vector3 AbsVec3(Vector3 v)
    {
        return new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
    }

    private static BoxCollider ResolveSectionCollider(ShelfSection section)
    {
        if (section == null) return null;

        // manter alinhado com o placement (GetComponentInChildren)
        var fromPlacementStyle = section.GetComponentInChildren<BoxCollider>();
        if (fromPlacementStyle != null) return fromPlacementStyle;

        var rootCol = section.GetComponent<BoxCollider>();
        if (rootCol != null) return rootCol;

        var all = section.GetComponentsInChildren<BoxCollider>(true);
        BoxCollider best = null;
        float bestVolume = -1f;

        for (int i = 0; i < all.Length; i++)
        {
            var c = all[i];
            if (c == null || !c.enabled) continue;
            if (c.GetComponentInParent<StorageArea>() != null) continue;

            Vector3 size = Vector3.Scale(AbsVec3(c.size), AbsVec3(c.transform.lossyScale));
            float volume = Mathf.Abs(size.x * size.y * size.z);
            if (volume > bestVolume)
            {
                bestVolume = volume;
                best = c;
            }
        }

        return best;
    }

    private void RebuildWallBoundsFromColliders()
    {
        wallBoundsReady = false;

        if (!useWallsAsHardBounds) return;
        if (wallsMask.value == 0)
        {
            Debug.LogWarning("[SectionRemodelController] wallsMask vazio. Remodel vai ignorar validação por wall bounds.");
            return;
        }

        Collider[] all = FindObjectsOfType<Collider>(true);
        Bounds b = default;
        bool hasAny = false;

        for (int i = 0; i < all.Length; i++)
        {
            Collider c = all[i];
            if (c == null) continue;

            int layerBit = 1 << c.gameObject.layer;
            if ((wallsMask.value & layerBit) == 0) continue;

            if (!hasAny)
            {
                b = c.bounds;
                hasAny = true;
            }
            else
            {
                b.Encapsulate(c.bounds);
            }
        }

        if (!hasAny)
        {
            Debug.LogWarning("[SectionRemodelController] Não encontrei colliders nas layers do wallsMask.");
            return;
        }

        wallBoundsX = new Vector2(b.min.x, b.max.x);
        wallBoundsZ = new Vector2(b.min.z, b.max.z);
        wallBoundsReady = true;
    }
}
