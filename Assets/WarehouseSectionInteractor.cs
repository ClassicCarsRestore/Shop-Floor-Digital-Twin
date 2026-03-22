using UnityEngine;
using UnityEngine.EventSystems;

public class WarehouseSectionInteractor : MonoBehaviour
{
    [SerializeField] private Camera rayCamera;
    [SerializeField] private LayerMask sectionMask;
    [SerializeField] private WarehouseSectionSelection selection;
    [SerializeField] private SectionRemodelController remodelController;
    [SerializeField] private SectionPlacementController placementController;
    [SerializeField] private LayerMask boxClickMask = ~0;
    [SerializeField] private bool blockEditClicksWhenPointerOverUI = false;

    public bool IsActive { get; set; } = false;

    private HighlightTarget currentHover;

    private void Update()
    {
        if (!IsActive) return;

        //  se estiver a remodelar -> nada
        if (remodelController != null && remodelController.IsRemodeling)
        {
            ClearHover();
            return;
        }

        //  se estiver a colocar/mover -> nada
        if (placementController != null && placementController.IsPlacing)
        {
            ClearHover();
            return;
        }

        //  se estiver em "editing" (há uma section selecionada) -> nada
        if (selection != null && selection.IsEditing)
        {
            ClearHover();
            HandleBoxClickDuringEditMode();
            return;
        }

        if (rayCamera == null) rayCamera = Camera.main;
        if (rayCamera == null) return;

        Ray ray = rayCamera.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out var hit, 5000f, sectionMask, QueryTriggerInteraction.Ignore))
        {
            var ht = hit.collider.GetComponentInParent<HighlightTarget>();

            if (ht != currentHover)
            {
                if (currentHover != null) currentHover.SetHighlight(false);
                currentHover = ht;
                if (currentHover != null) currentHover.SetHighlight(true);
            }

            if (Input.GetMouseButtonDown(0))
            {
                var sec = hit.collider.GetComponentInParent<ShelfSection>();
                if (sec != null && selection != null)
                    selection.SelectSection(sec);
            }
        }
        else
        {
            ClearHover();
        }
    }

    public void ClearHover()
    {
        if (currentHover != null) currentHover.SetHighlight(false);
        currentHover = null;
    }

    private void HandleBoxClickDuringEditMode()
    {
        if (!Input.GetMouseButtonDown(0)) return;

        if (blockEditClicksWhenPointerOverUI && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        if (rayCamera == null) rayCamera = Camera.main;
        if (rayCamera == null) return;

        var ray = rayCamera.ScreenPointToRay(Input.mousePosition);
        var hits = Physics.RaycastAll(ray, 5000f, boxClickMask, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0)
            return;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        for (int i = 0; i < hits.Length; i++)
        {
            var hit = hits[i];
            var clickedBox = hit.collider.GetComponentInParent<StorageBox>();
            if (clickedBox != null)
            {
                if (WarehouseManager.Instance != null)
                    WarehouseManager.Instance.TryHandleStorageBoxClick(clickedBox);
                return;
            }

            var area = hit.collider.GetComponentInParent<StorageArea>();
            if (area != null)
            {
                if (WarehouseManager.Instance != null)
                    WarehouseManager.Instance.TryHandleStorageAreaClick(area);
                return;
            }
        }
    }
}
