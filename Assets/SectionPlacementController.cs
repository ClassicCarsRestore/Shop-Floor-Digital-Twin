using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Objects;

public class SectionPlacementController : MonoBehaviour
{
    [Header("Placement Prefab")]
    [SerializeField] private GameObject sectionPrefab;

    [Header("Placement References")]
    [SerializeField] private Transform cameraRigOrCamera;

    [Header("Camera System")]
    [SerializeField] private CameraSystem cameraSystem;

    [SerializeField] private WarehouseSectionInteractor sectionInteractor;


    [Header("Warehouse Limits (X/Z) - ONLY FOR FP CAMERA")]
    [SerializeField] private Vector2 warehouseBoundsX = new Vector2(0, 0);
    [SerializeField] private Vector2 warehouseBoundsZ = new Vector2(0, 0);

    [Header("Placement Tuning")]
    [SerializeField] private float fixedY = 0f;
    [SerializeField] private LayerMask collisionMask;       // estantes/sections existentes
    [SerializeField] private float overlapPadding = 0.05f;

    [Header("Warehouse Walls (Layer)")]
    [SerializeField] private LayerMask wallsMask;           // paredes (colliders)
    [SerializeField] private bool requireHoldLeftMouseToMove = true;

    [Header("Rotation (Placement)")]
    [SerializeField] private float snapAngle = 90f;

    [Header("Initial Spawn (Inside Warehouse)")]
    [SerializeField] private Vector3 initialSpawnWorldPos = new Vector3(513f, 85f, -326f);

    // ==========================================================
    // Auto bounds (opcional). Serve só para "não ir para fora"
    // NÃO resolve paredes internas (portas/recortes).
    // ==========================================================
    [Header("Auto Bounds From Walls (Hard Limit)")]
    [SerializeField] private bool useWallsAsHardBounds = true;
    [SerializeField] private float boundsEpsilon = 0.001f;

    private bool wallBoundsReady = false;
    private Vector2 wallBoundsX;
    private Vector2 wallBoundsZ;

    [Header("Axis Slide (robusto p/ CAD)")]
    [SerializeField] private int maxSubSteps = 4; // ajuda se mexes muito por frame
    [SerializeField] private bool preferXFirst = true;

    [Header("UI")]
    [SerializeField] private Button addButton;
    [SerializeField] private Button saveButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private Button cancelAllButton;
    [SerializeField] private Button saveToBDButton;
    [SerializeField] private Button exitWarehouseButton;


    [SerializeField] private GameObject placementControls;

    [Header("Ghost Visual Feedback")]
    [SerializeField] private Color validTint = new Color(0f, 1f, 0f, 0.35f);
    [SerializeField] private Color invalidTint = new Color(1f, 0f, 0f, 0.35f);

    private GameObject ghostInstance;
    private ShelfSection ghostSection;
    private BoxCollider ghostCollider;

    private bool isPlacing = false;
    private bool canPlace = false;

    public bool IsPlacing => isPlacing;
    public bool IsEditing => isEditing;

    public System.Action<ShelfSection> OnEditPlacementStarted;
    public System.Action<ShelfSection, bool> OnEditPlacementFinished;

    private Renderer[] ghostRenderers;

    private bool isEditing = false;
    private Vector3 editOriginalPos;
    private Quaternion editOriginalRot;

    private MaterialPropertyBlock mpb;
    private static readonly int ColorProp = Shader.PropertyToID("_Color");
    private static readonly int BaseColorProp = Shader.PropertyToID("_BaseColor");

    // Drag (sem teleport)
    private bool isDragging = false;
    private Vector3 grabOffset = Vector3.zero;
    private bool hasGrabOffset = false;

    private Camera mainCam;

    // buffer p/ evitar GC
    private Collider[] overlapBuffer = new Collider[64];
    [SerializeField] private bool debugOverlaps = true;

    private void DebugOverlapAtCurrent()
    {
        if (!debugOverlaps || ghostCollider == null) return;

        LayerMask mask = wallsMask | collisionMask;

        Vector3 center = ghostCollider.transform.TransformPoint(ghostCollider.center);
        Quaternion rot = ghostCollider.transform.rotation;
        Vector3 halfExtents =
     Vector3.Scale(AbsVec3(ghostCollider.size) * 0.5f,
                   AbsVec3(ghostCollider.transform.lossyScale));

        halfExtents = AbsVec3(halfExtents); // “cinto e suspensórios”
        halfExtents += Vector3.one * overlapPadding;

        

        Collider[] hits = Physics.OverlapBox(center, halfExtents, rot, mask, QueryTriggerInteraction.Ignore);

        Debug.Log($"[Overlap] hits={hits.Length} at center={center} halfExt={halfExtents}");

        Debug.Log($"size={ghostCollider.size} lossy={ghostCollider.transform.lossyScale}");


        foreach (var h in hits)
        {
            if (h == null) continue;
            if (h.transform.IsChildOf(ghostInstance.transform)) continue;

            Debug.Log($"  -> {h.name} | layer={LayerMask.LayerToName(h.gameObject.layer)} | enabled={h.enabled} | bounds={h.bounds}");
        }
    }

    private static Vector3 AbsVec3(Vector3 v)
    {
        return new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
    }



    private void Awake()
    {
        if (placementControls != null)
            placementControls.SetActive(false);

        if (saveButton != null)
            saveButton.interactable = false;

        if (addButton != null)
            addButton.onClick.AddListener(StartPlacement);

        if (saveButton != null)
            saveButton.onClick.AddListener(SavePlacement);

        if (cancelButton != null)
            cancelButton.onClick.AddListener(CancelPlacement);

        mpb = new MaterialPropertyBlock();
        mainCam = Camera.main;
    }

    private void Update()
    {
        if (!isPlacing || ghostInstance == null) return;
        if (mainCam == null) mainCam = Camera.main;

        HandleRotationInput();
        UpdateGhostTransformDrag_NoTeleport_WithAxisSlide();
        if (Input.GetMouseButton(0))
            DebugOverlapAtCurrent();


        ValidateGhost();
        UpdateGhostVisual();
    }

    // -------------------------
    // PUBLIC
    // -------------------------
    public void StartPlacement()
    {
        if (isPlacing) return;

        WarehouseHUD.Instance?.EnterEditMode();

        if (sectionPrefab == null)
        {
            Debug.LogError("[SectionPlacementController] sectionPrefab não atribuído.");
            return;
        }



        ghostInstance = Instantiate(sectionPrefab);
        ghostInstance.name = "SECTION_GHOST";

        ghostSection = ghostInstance.GetComponent<ShelfSection>();
        if (ghostSection == null)
            Debug.LogError("[SectionPlacementController] O prefab não tem ShelfSection no root.");

        ghostCollider = ghostInstance.GetComponentInChildren<BoxCollider>();
        if (ghostCollider == null)
            Debug.LogWarning("[SectionPlacementController] Ghost não tem BoxCollider. Adiciona um no prefab.");

        ghostRenderers = ghostInstance.GetComponentsInChildren<Renderer>(true);

        isEditing = false;
        isPlacing = true;

        if (sectionInteractor != null)
        {
            sectionInteractor.ClearHover();
            sectionInteractor.IsActive = false;
        }


        if (cameraSystem != null)
            cameraSystem.EnterTopPlacementView();

        RebuildWallBoundsFromColliders();

        if (placementControls != null)
            placementControls.SetActive(true);

        if (addButton != null)
            addButton.interactable = false;

        if (saveButton != null)
            saveButton.interactable = false;

        Vector3 spawn = initialSpawnWorldPos;
        spawn.y = fixedY;

        ghostInstance.transform.position = spawn;
        ForceSnapIntoWarehouseBoundsIfNeeded();

        isDragging = false;
        hasGrabOffset = false;

        ValidateGhost();
        UpdateGhostVisual();
    }

    public void StartEditPlacement(ShelfSection section)
    {
        if (isPlacing) return;
        if (section == null) return;

        WarehouseHUD.Instance?.EnterEditMode();

        ghostSection = section;
        ghostInstance = section.gameObject;

        editOriginalPos = ghostInstance.transform.position;
        editOriginalRot = ghostInstance.transform.rotation;

        ghostCollider = ghostInstance.GetComponentInChildren<BoxCollider>();
        ghostRenderers = ghostInstance.GetComponentsInChildren<Renderer>(true);

        isEditing = true;
        isPlacing = true;

        if (sectionInteractor != null)
        {
            sectionInteractor.ClearHover();
            sectionInteractor.IsActive = false;
        }


        if (cameraSystem != null)
            cameraSystem.EnterTopPlacementView();

        RebuildWallBoundsFromColliders();

        if (placementControls != null)
            placementControls.SetActive(true);

        if (addButton != null)
            addButton.interactable = false;

        if (saveButton != null)
            saveButton.interactable = false;

        OnEditPlacementStarted?.Invoke(section);

        ForceSnapIntoWarehouseBoundsIfNeeded();

        isDragging = false;
        hasGrabOffset = false;

        ValidateGhost();
        UpdateGhostVisual();
    }

    // -------------------------
    // INPUT
    // -------------------------
    private void HandleRotationInput()
    {
        if (Input.GetKeyDown(KeyCode.Q))
        {
            ghostInstance.transform.rotation = Quaternion.Euler(0f, -snapAngle, 0f) * ghostInstance.transform.rotation;
            ForceSnapIntoWarehouseBoundsIfNeeded();
        }

        if (Input.GetKeyDown(KeyCode.E))
        {
            ghostInstance.transform.rotation = Quaternion.Euler(0f, +snapAngle, 0f) * ghostInstance.transform.rotation;
            ForceSnapIntoWarehouseBoundsIfNeeded();
        }
    }

    // Drag por plano Y=fixedY, com offset, e movimento com "axis slide" (OverlapBox)
    private void UpdateGhostTransformDrag_NoTeleport_WithAxisSlide()
    {
        if (mainCam == null) return;

        if (Input.GetMouseButtonDown(0))
        {
            if (IsPointerOverPlacementUI()) return;

            if (TryGetMousePointOnPlane(out Vector3 hitPoint))
            {
                grabOffset = ghostInstance.transform.position - hitPoint;
                grabOffset.y = 0f;
                hasGrabOffset = true;
                isDragging = true;
            }
        }

        if (Input.GetMouseButtonUp(0))
        {
            isDragging = false;
            return;
        }

        if (requireHoldLeftMouseToMove && !Input.GetMouseButton(0))
            return;

        if (!isDragging || !hasGrabOffset) return;

        if (!TryGetMousePointOnPlane(out Vector3 planePoint))
            return;

        Vector3 target = planePoint + grabOffset;
        target.y = fixedY;

        Vector3 current = ghostInstance.transform.position;
        current.y = fixedY;

        if (useWallsAsHardBounds)
            target = ClampToWallBounds(target);

        Vector3 desiredMove = target - current;
        desiredMove.y = 0f;

        if (desiredMove.sqrMagnitude < 0.0000005f) return;

        Vector3 newPos = MoveWithAxisSlide(current, desiredMove);
        newPos.y = fixedY;

        if (useWallsAsHardBounds)
            newPos = ClampToWallBounds(newPos);

        newPos.y = fixedY;
        ghostInstance.transform.position = newPos;
    }

    private bool TryGetMousePointOnPlane(out Vector3 point)
    {
        point = default;

        Ray ray = mainCam.ScreenPointToRay(Input.mousePosition);
        Plane plane = new Plane(Vector3.up, new Vector3(0f, fixedY, 0f));
        if (!plane.Raycast(ray, out float enter))
            return false;

        point = ray.GetPoint(enter);
        point.y = fixedY;
        return true;
    }

    // ==========================================================
    // CORE: Axis slide com OverlapBox (robusto)
    // ==========================================================
    private Vector3 MoveWithAxisSlide(Vector3 startPos, Vector3 desiredMove)
    {
        if (ghostCollider == null)
            return startPos + desiredMove;

        LayerMask blockMask = wallsMask | collisionMask;
        if (blockMask.value == 0)
            return startPos + desiredMove;

        // sub-steps (evita "saltar" por cima de paredes finas em moves grandes)
        int steps = Mathf.Clamp(maxSubSteps, 1, 20);
        Vector3 stepMove = desiredMove / steps;

        Vector3 pos = startPos;

        for (int s = 0; s < steps; s++)
        {
            pos = StepAxisSlide(pos, stepMove, blockMask);
        }

        return pos;
    }

    private Vector3 StepAxisSlide(Vector3 pos, Vector3 move, LayerMask blockMask)
    {
        // tenta mexer nos eixos separadamente para permitir deslizar
        if (preferXFirst)
        {
            pos = TryAxis(pos, new Vector3(move.x, 0f, 0f), blockMask);
            pos = TryAxis(pos, new Vector3(0f, 0f, move.z), blockMask);
        }
        else
        {
            pos = TryAxis(pos, new Vector3(0f, 0f, move.z), blockMask);
            pos = TryAxis(pos, new Vector3(move.x, 0f, 0f), blockMask);
        }

        return pos;
    }

    private Vector3 TryAxis(Vector3 pos, Vector3 axisMove, LayerMask blockMask)
    {
        if (axisMove.sqrMagnitude < 0.0000001f) return pos;

        Vector3 candidate = pos + axisMove;
        candidate.y = fixedY;

        if (WouldOverlapAt(candidate, blockMask))
        {
            // bloqueia este eixo
            return pos;
        }

        return candidate;
    }

    private bool WouldOverlapAt(Vector3 candidateRootPos, LayerMask blockMask)
    {
        // Queremos testar o ghostCollider como OBB (center/size/rot do collider)
        // Mas precisamos do centro do collider "como se" o root estivesse em candidateRootPos.

        // offset do centro do collider em world em relação ao root (no estado atual)
        Vector3 centerNow = ghostCollider.transform.TransformPoint(ghostCollider.center);
        Vector3 centerOffsetWorld = centerNow - ghostInstance.transform.position;

        Vector3 testCenter = candidateRootPos + centerOffsetWorld;

        Quaternion rot = ghostCollider.transform.rotation;
        Vector3 halfExtents =
     Vector3.Scale(AbsVec3(ghostCollider.size) * 0.5f,
                   AbsVec3(ghostCollider.transform.lossyScale));

        halfExtents = AbsVec3(halfExtents); // “cinto e suspensórios”
        halfExtents += Vector3.one * overlapPadding;


        int count = Physics.OverlapBoxNonAlloc(
            testCenter,
            halfExtents,
            overlapBuffer,
            rot,
            blockMask,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < count; i++)
        {
            Collider h = overlapBuffer[i];
            if (h == null) continue;
            if (h.transform.IsChildOf(ghostInstance.transform)) continue;
            return true;
        }

        return false;
    }

    // -------------------------
    // WALL BOUNDS (AUTO) - só exterior
    // -------------------------
    private void RebuildWallBoundsFromColliders()
    {
        wallBoundsReady = false;

        if (!useWallsAsHardBounds) return;

        if (wallsMask.value == 0)
        {
            Debug.LogWarning("[SectionPlacementController] wallsMask vazio. Não dá para calcular bounds.");
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
            Debug.LogWarning("[SectionPlacementController] Não encontrei colliders nas layers do wallsMask.");
            return;
        }

        wallBoundsX = new Vector2(b.min.x, b.max.x);
        wallBoundsZ = new Vector2(b.min.z, b.max.z);
        wallBoundsReady = true;
    }

    private Vector3 ClampToWallBounds(Vector3 pos)
    {
        if (!useWallsAsHardBounds || !wallBoundsReady || ghostCollider == null)
            return pos;

        Vector3 ext = ghostCollider.bounds.extents;

        float minX = wallBoundsX.x + ext.x;
        float maxX = wallBoundsX.y - ext.x;
        float minZ = wallBoundsZ.x + ext.z;
        float maxZ = wallBoundsZ.y - ext.z;

        pos.x = Mathf.Clamp(pos.x, minX, maxX);
        pos.z = Mathf.Clamp(pos.z, minZ, maxZ);

        return pos;
    }

    private bool IsGhostInsideWallBounds()
    {
        if (!useWallsAsHardBounds || !wallBoundsReady || ghostCollider == null)
            return true;

        Bounds gb = ghostCollider.bounds;

        bool insideX = gb.min.x >= wallBoundsX.x - boundsEpsilon && gb.max.x <= wallBoundsX.y + boundsEpsilon;
        bool insideZ = gb.min.z >= wallBoundsZ.x - boundsEpsilon && gb.max.z <= wallBoundsZ.y + boundsEpsilon;

        return insideX && insideZ;
    }

    private void ForceSnapIntoWarehouseBoundsIfNeeded()
    {
        if (!useWallsAsHardBounds || !wallBoundsReady || ghostCollider == null || ghostInstance == null)
            return;

        Vector3 p = ghostInstance.transform.position;
        p.y = fixedY;

        Vector3 snapped = ClampToWallBounds(p);
        snapped.y = fixedY;
        ghostInstance.transform.position = snapped;
    }

    // -------------------------
    // UI
    // -------------------------
    private bool IsPointerOverPlacementUI()
    {
        if (EventSystem.current == null) return false;

        if (placementControls != null)
        {
            var rt = placementControls.GetComponent<RectTransform>();
            if (rt != null && RectTransformUtility.RectangleContainsScreenPoint(rt, Input.mousePosition))
                return true;
        }

        if (IsOverButton(addButton)) return true;
        if (IsOverButton(saveButton)) return true;
        if (IsOverButton(cancelButton)) return true;

        return false;
    }

    private bool IsOverButton(Button b)
    {
        if (b == null) return false;
        var rt = b.GetComponent<RectTransform>();
        return rt != null && RectTransformUtility.RectangleContainsScreenPoint(rt, Input.mousePosition);
    }

    // -------------------------
    // VALIDATION + VISUAL
    // -------------------------
    private void ValidateGhost()
    {
        canPlace = true;

        if (!IsGhostInsideWallBounds())
        {
            canPlace = false;
            if (saveButton != null) saveButton.interactable = false;
            return;
        }

        if (ghostCollider != null)
        {
            LayerMask combined = collisionMask | wallsMask;

            Vector3 center = ghostCollider.transform.TransformPoint(ghostCollider.center);
            Quaternion rot = ghostCollider.transform.rotation;
            Vector3 halfExtents =
     Vector3.Scale(AbsVec3(ghostCollider.size) * 0.5f,
                   AbsVec3(ghostCollider.transform.lossyScale));

            halfExtents = AbsVec3(halfExtents); // “cinto e suspensórios”
            halfExtents += Vector3.one * overlapPadding;


            int count = Physics.OverlapBoxNonAlloc(center, halfExtents, overlapBuffer, rot, combined, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                var h = overlapBuffer[i];
                if (h == null) continue;
                if (h.transform.IsChildOf(ghostInstance.transform)) continue;

                canPlace = false;
                break;
            }
        }

        if (saveButton != null)
            saveButton.interactable = canPlace;
    }

    private void UpdateGhostVisual()
    {
        if (ghostRenderers == null) return;

        Color tint = canPlace ? validTint : invalidTint;

        for (int i = 0; i < ghostRenderers.Length; i++)
        {
            var r = ghostRenderers[i];
            if (r == null) continue;

            r.GetPropertyBlock(mpb);
            mpb.SetColor(BaseColorProp, tint);
            mpb.SetColor(ColorProp, tint);
            r.SetPropertyBlock(mpb);
        }
    }

    private void ClearGhostVisual()
    {
        if (ghostRenderers == null) return;
        for (int i = 0; i < ghostRenderers.Length; i++)
        {
            var r = ghostRenderers[i];
            if (r == null) continue;
            r.SetPropertyBlock(null);
        }
    }

    // -------------------------
    // SAVE / CANCEL
    // -------------------------
    private void SavePlacement()
    {
        if (!isPlacing || ghostInstance == null) return;
        if (!canPlace) return;

        ClearGhostVisual();

        // Novo placement (criação de section)
        if (!isEditing)
        {
            ShelfSection sec = null;

            if (WarehouseManager.Instance != null)
                sec = WarehouseManager.Instance.AddSectionRuntime(ghostInstance);

            if (sec != null)
            {
                var shelvesCtrl = sec.GetComponent<ShelfSectionShelvesController>();
                if (shelvesCtrl != null)
                    shelvesCtrl.RebuildShelves();

                // default areas em todas as shelves (A')
                if (sec.Shelves != null)
                {
                    for (int i = 0; i < sec.Shelves.Count; i++)
                    {
                        var shelf = sec.Shelves[i];
                        if (shelf == null) continue;

                        int shelfIndex = i + 1;
                        ShelfAreasBuilder.RebuildAreas(shelf, 6, sec.SectionId, shelfIndex);
                    }
                }

                shelvesCtrl?.RebuildShelves(); // reforço final ids
            }

            EndPlacement(keepObject: true);
            return;
        }

        // Edição de placement de uma section existente
        var finishedSection = ghostSection;
        EndPlacement(keepObject: true);
        OnEditPlacementFinished?.Invoke(finishedSection, true);
        
    }

    private void CancelPlacement()
    {
        if (!isPlacing) return;

        if (isEditing && ghostInstance != null)
        {
            ghostInstance.transform.position = editOriginalPos;
            ghostInstance.transform.rotation = editOriginalRot;
            ClearGhostVisual();
            var finishedSection = ghostSection;
           
            EndPlacement(keepObject: true);
            OnEditPlacementFinished?.Invoke(finishedSection, false);     
            return;
        }

        ClearGhostVisual();
        EndPlacement(keepObject: false);
    }

    private void EndPlacement(bool keepObject)
    {
        isPlacing = false;
        canPlace = false;

        if (placementControls != null)
            placementControls.SetActive(false);

        if (addButton != null)
            addButton.interactable = true;

        WarehouseHUD.Instance?.ExitEditMode();

        if (!keepObject && ghostInstance != null)
            Destroy(ghostInstance);

        ghostInstance = null;
        ghostSection = null;
        ghostCollider = null;
        ghostRenderers = null;

        isEditing = false;
        isDragging = false;
        hasGrabOffset = false;

        if (saveButton != null)
            saveButton.interactable = false;

        if (sectionInteractor != null)
        {
            sectionInteractor.IsActive = true;
        }

        if (cameraSystem != null)
            cameraSystem.ExitTopPlacementViewRestore();
        
    }

    public void SetWarehouseBounds(Vector2 boundsX, Vector2 boundsZ, float y)
    {
        warehouseBoundsX = boundsX;
        warehouseBoundsZ = boundsZ;
        fixedY = y;
    }

    public void SetAddButtonInteractable(bool on)
    {
        if (addButton != null)
            addButton.interactable = on;
    }
}
