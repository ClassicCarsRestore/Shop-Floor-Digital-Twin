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

        // 1) bloquear movement (WASD/QE)
        if (cameraSystem != null)
            cameraSystem.DesactiveControls();

        // 2) focar a camera na frente da estante
        if (cameraSystem != null)
            cameraSystem.FocusFrontOfSection(section.transform);

        // 3) mostrar botões de edição (inclui o X)
        if (warehouseEditPanel != null)
            warehouseEditPanel.ShowFor(section);
    }

    public void ClearSelection()
    {
        Selected = null;

        // esconder botões
        if (warehouseEditPanel != null)
            warehouseEditPanel.Hide();

        // voltar a permitir movement
        if (cameraSystem != null)
            cameraSystem.ActiveControls();
    }
}
