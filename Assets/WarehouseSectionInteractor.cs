using UnityEngine;

public class WarehouseSectionInteractor : MonoBehaviour
{
    [SerializeField] private Camera rayCamera;                 // normalmente Camera.main
    [SerializeField] private LayerMask sectionMask;            // WarehouseSection
    [SerializeField] private WarehouseSectionSelection selection;
    [SerializeField] private SectionRemodelController remodelController;

    public bool IsActive { get; set; } = false;

    private HighlightTarget currentHover;

    private void Update()
    {
        if (!IsActive) return;

        if (remodelController != null && remodelController.IsRemodeling)
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

            // click para selecionar
            if (Input.GetMouseButtonDown(0))
            {
                var sec = hit.collider.GetComponentInParent<ShelfSection>();
                if (sec != null && selection != null)
                {
                    selection.SelectSection(sec);
                }
            }
        }
        else
        {
            if (currentHover != null) currentHover.SetHighlight(false);
            currentHover = null;
        }
    }

    public void ClearHover()
    {
        if (currentHover != null) currentHover.SetHighlight(false);
        currentHover = null;
    }
}
