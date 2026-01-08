using UnityEngine;
using Objects;

public class WarehouseSectionSelection : MonoBehaviour
{
    [SerializeField] private CameraSystem cameraSystem;
    [SerializeField] private WarehouseEditPanel warehouseEditPanel;

    public ShelfSection Selected { get; private set; }

    public void SelectSection(ShelfSection section)
    {
        if (section == null) return;

        Selected = section;

        // focar a camera na frente da estante
        if (cameraSystem != null)
            cameraSystem.FocusFrontOfSection(section.transform);

        // mostrar botões de edição
        if (warehouseEditPanel != null)
            warehouseEditPanel.ShowFor(section);
    }

    public void ClearSelection()
    {
        Selected = null;
        if (warehouseEditPanel != null)
            warehouseEditPanel.Hide();
    }
}
