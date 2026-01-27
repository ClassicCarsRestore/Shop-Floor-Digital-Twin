using System.Collections;
using UnityEngine;
using Objects;

public class WarehouseSectionSelection : MonoBehaviour
{
    [SerializeField] private CameraSystem cameraSystem;
    [SerializeField] private WarehouseEditPanel warehouseEditPanel;
    [SerializeField] private SectionPlacementController placementController;

    public ShelfSection Selected { get; private set; }
    public bool IsEditing => Selected != null;

    private Coroutine showRoutine;



    public void SelectSection(ShelfSection section)
    {
        if (section == null) return;

        if (placementController != null && placementController.IsPlacing)
            return;

        Selected = section;

        if (cameraSystem != null)
            cameraSystem.DesactiveControls();

        if (cameraSystem != null)
            cameraSystem.FocusFrontOfSection(section.transform);

        // em vez de ShowFor imediato, espera 1 frame
        if (showRoutine != null) StopCoroutine(showRoutine);
        showRoutine = StartCoroutine(ShowPanelNextFrame(section));
    }

    private IEnumerator ShowPanelNextFrame(ShelfSection section)
    {
        yield return null; 
        if (Selected != section) yield break; 

        if (warehouseEditPanel != null)
            warehouseEditPanel.ShowFor(section);
    }

    public void ClearSelection()
    {
        if (showRoutine != null)
        {
            StopCoroutine(showRoutine);
            showRoutine = null;
        }

        Selected = null;

        if (warehouseEditPanel != null)
            warehouseEditPanel.Hide();

        if (cameraSystem != null)
            cameraSystem.ActiveControls();
    }
}
