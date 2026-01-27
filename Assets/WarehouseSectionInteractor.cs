using UnityEngine;

public class WarehouseSectionInteractor : MonoBehaviour
{
    [SerializeField] private Camera rayCamera;
    [SerializeField] private LayerMask sectionMask;
    [SerializeField] private WarehouseSectionSelection selection;
    [SerializeField] private SectionRemodelController remodelController;
    [SerializeField] private SectionPlacementController placementController; // ✅ add

    public bool IsActive { get; set; } = false;

    private HighlightTarget currentHover;

    private void Update()
    {
        if (!IsActive) return;

        // ✅ se estiver a remodelar -> nada
        if (remodelController != null && remodelController.IsRemodeling)
        {
            ClearHover();
            return;
        }

        // ✅ se estiver a colocar/mover -> nada
        if (placementController != null && placementController.IsPlacing)
        {
            ClearHover();
            return;
        }

        // ✅ se estiver em "editing" (há uma section selecionada) -> nada
        if (selection != null && selection.IsEditing)
        {
            ClearHover();
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
}
