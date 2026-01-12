using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SectionPlacementController : MonoBehaviour
{
    [Header("Placement Prefab")]
    [SerializeField] private GameObject sectionPrefab;

    [Header("Placement References")]
    [SerializeField] private Transform cameraRigOrCamera;

    [Header("Warehouse Limits (X/Z)")]
    [SerializeField] private Vector2 warehouseBoundsX = new Vector2(0, 0);
    [SerializeField] private Vector2 warehouseBoundsZ = new Vector2(0, 0);

    [Header("Placement Tuning")]
    [SerializeField] private float spawnDistance = 2.5f;
    [SerializeField] private float fixedY = 0f;
    [SerializeField] private LayerMask collisionMask;       // layer das estantes existentes
    [SerializeField] private float overlapPadding = 0.05f;  // padding para colisões

    [Header("UI")]
    [SerializeField] private Button addButton;              // "+"
    [SerializeField] private Button saveButton;             // "Save"
    [SerializeField] private Button cancelButton;           // "X"
    [SerializeField] private GameObject placementControls;  // painel que contém Save/X
    //[SerializeField] private GameObject editControls;

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

    // startedEditingSection: dispara quando entras no modo edit placement
    public System.Action<ShelfSection> OnEditPlacementStarted;

    // finishedEditingSection: dispara quando terminas um edit placement (save = true / cancel = false)
    public System.Action<ShelfSection, bool> OnEditPlacementFinished;


    private Renderer[] ghostRenderers;

    // EDIT state
    private bool isEditing = false;
    private Vector3 editOriginalPos;
    private Quaternion editOriginalRot;

    // MaterialPropertyBlock (não altera materiais reais)
    private MaterialPropertyBlock mpb;
    private static readonly int ColorProp = Shader.PropertyToID("_Color");
    private static readonly int BaseColorProp = Shader.PropertyToID("_BaseColor");

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
    }

    private void Update()
    {
        if (!isPlacing || ghostInstance == null) return;

        UpdateGhostTransform();
        ValidateGhost();
        UpdateGhostVisual();
    }

    // -------------------------
    // PUBLIC
    // -------------------------
    public void StartPlacement()
    {
        if (isPlacing) return;

        if (sectionPrefab == null)
        {
            Debug.LogError("[SectionPlacementController] sectionPrefab não atribuído.");
            return;
        }

        if (cameraRigOrCamera == null)
        {
            Debug.LogError("[SectionPlacementController] cameraRigOrCamera não atribuído.");
            return;
        }

        // --- (mantido igual na estrutura) ---
        ghostInstance = Instantiate(sectionPrefab);
        ghostInstance.name = "SECTION_GHOST";
        ghostSection = ghostInstance.GetComponent<ShelfSection>();
        if (ghostSection == null)
        {
            Debug.LogError("[SectionPlacementController] O prefab não tem ShelfSection no root.");
        }

        // collider para overlap
        ghostCollider = ghostInstance.GetComponentInChildren<BoxCollider>();
        if (ghostCollider == null)
        {
            Debug.LogWarning("[SectionPlacementController] Ghost não tem BoxCollider. Adiciona um no prefab.");
        }

        // Renderers para feedback
        ghostRenderers = ghostInstance.GetComponentsInChildren<Renderer>(true);

        isEditing = false; // create mode
        isPlacing = true;

        if (placementControls != null)
            placementControls.SetActive(true);

        if (addButton != null)
            addButton.interactable = false;

        if (saveButton != null)
            saveButton.interactable = false;

        UpdateGhostTransform();
        ValidateGhost();
        UpdateGhostVisual();
    }

    // MOVE / EDIT placement (não mexe no StartPlacement)
    public void StartEditPlacement(ShelfSection section)
    {
        if (isPlacing) return;

        if (section == null)
        {
            Debug.LogWarning("[SectionPlacementController] StartEditPlacement: section is null");
            return;
        }

        if (cameraRigOrCamera == null)
        {
            Debug.LogError("[SectionPlacementController] cameraRigOrCamera não atribuído.");
            return;
        }

        

        // usa a estante existente como "ghost"
        ghostSection = section;
        ghostInstance = section.gameObject;

        editOriginalPos = ghostInstance.transform.position;
        editOriginalRot = ghostInstance.transform.rotation;

        ghostCollider = ghostInstance.GetComponentInChildren<BoxCollider>();
        if (ghostCollider == null)
        {
            Debug.LogWarning("[SectionPlacementController] Section a editar não tem BoxCollider.");
        }

        ghostRenderers = ghostInstance.GetComponentsInChildren<Renderer>(true);

        isEditing = true;
        isPlacing = true;

        if (placementControls != null)
            placementControls.SetActive(true);

        if (addButton != null)
            addButton.interactable = false;

        if (saveButton != null)
            saveButton.interactable = false;


        OnEditPlacementStarted?.Invoke(section);

        UpdateGhostTransform();
        ValidateGhost();
        UpdateGhostVisual();
        

    }

    // -------------------------
    // CORE
    // -------------------------
    private void UpdateGhostTransform()
    {
        Vector3 target = cameraRigOrCamera.position + cameraRigOrCamera.forward * spawnDistance;
        target.y = fixedY;

        // clamp X/Z pelos limites do armazém
        target.x = Mathf.Clamp(target.x, warehouseBoundsX.x, warehouseBoundsX.y);
        target.z = Mathf.Clamp(target.z, warehouseBoundsZ.x, warehouseBoundsZ.y);

        ghostInstance.transform.position = target;

        // Alinha com a direção da câmera
        Vector3 fwd = cameraRigOrCamera.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude > 0.001f)
        {
            ghostInstance.transform.rotation = Quaternion.LookRotation(fwd.normalized, Vector3.up);
        }
    }

    private void ValidateGhost()
    {
        canPlace = true;

        if (ghostCollider != null)
        {
            Vector3 center = ghostCollider.bounds.center;
            Vector3 halfExtents = ghostCollider.bounds.extents + Vector3.one * overlapPadding;
            Quaternion rot = ghostInstance.transform.rotation;

            Collider[] hits = Physics.OverlapBox(center, halfExtents, rot, collisionMask, QueryTriggerInteraction.Ignore);

            foreach (var h in hits)
            {
                if (h == null) continue;

                // Ignorar o próprio ghost / section em edição
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

            // NÃO tocar em r.material / r.sharedMaterial
            // Só override visual por instância:
            r.GetPropertyBlock(mpb);

            // tenta URP (_BaseColor) e Standard (_Color)
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

            // limpa overrides (volta ao material normal)
            r.SetPropertyBlock(null);
        }
    }

    // -------------------------
    // CONFIRM / CANCEL
    // -------------------------
    private void SavePlacement()
    {
        if (!isPlacing || ghostInstance == null) return;
        if (!canPlace) return;

        ClearGhostVisual();

        // CREATE: adiciona à lista (como já fazias)
        if (!isEditing)
        {
            if (WarehouseManager.Instance != null)
            {
                WarehouseManager.Instance.AddSectionRuntime(ghostInstance);
            }
            else
            {
                Debug.LogWarning("[SectionPlacementController] WarehouseManager.Instance é null.");
            }

            EndPlacement(keepObject: true);
            return;
        }

        // EDIT: não mexe em listas, ids, nomes. Só termina.
        var finishedSection = ghostSection;
        OnEditPlacementFinished?.Invoke(finishedSection, true);
        EndPlacement(keepObject: true);

      
    }

    private void CancelPlacement()
    {
        if (!isPlacing) return;

        // EDIT: voltar à pos/rot original
        if (isEditing && ghostInstance != null)
        {
            ghostInstance.transform.position = editOriginalPos;
            ghostInstance.transform.rotation = editOriginalRot;
            ClearGhostVisual();
            var finishedSection = ghostSection;
            OnEditPlacementFinished?.Invoke(finishedSection, false);
            EndPlacement(keepObject: true);

            return;
        }

        // CREATE: destruir ghost
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

        // se era criação e cancelou, destrói
        if (!keepObject && ghostInstance != null)
            Destroy(ghostInstance);

        ghostInstance = null;
        ghostSection = null;
        ghostCollider = null;
        ghostRenderers = null;

        isEditing = false;

        if (saveButton != null)
            saveButton.interactable = false;
    }

    public void SetAddButtonInteractable(bool on)
    {
        if (addButton != null)
            addButton.interactable = on;
    }

 


    // -------------------------
    // OPTIONAL: set bounds at runtime
    // -------------------------
    public void SetWarehouseBounds(Vector2 boundsX, Vector2 boundsZ, float y)
    {
        warehouseBoundsX = boundsX;
        warehouseBoundsZ = boundsZ;
        fixedY = y;
    }
}
