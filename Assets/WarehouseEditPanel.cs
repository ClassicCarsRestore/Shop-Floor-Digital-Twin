using UnityEngine;
using UnityEngine.UI;

public class WarehouseEditPanel : MonoBehaviour
{
    [SerializeField] private Button deleteButton;
    [SerializeField] private Button moveButton;
    [SerializeField] private Button addShelfButton;
    [SerializeField] private Button removeShelfButton;
    [SerializeField] private Button remodelShelfButton;

    [SerializeField] private SectionPlacementController placementController;

    private ShelfSection current;

    private void Awake()
    {
        Hide();

        if (deleteButton != null) deleteButton.onClick.AddListener(DeleteSelected);
        if (moveButton != null) moveButton.onClick.AddListener(EditPlacement);

        // por agora podem ser stubs, até definires como mexer nas shelves
        if (addShelfButton != null) addShelfButton.onClick.AddListener(AddShelf);
        if (removeShelfButton != null) removeShelfButton.onClick.AddListener(RemoveShelf);
        if (remodelShelfButton != null) remodelShelfButton.onClick.AddListener(RemodelSection);
    }

    public void ShowFor(ShelfSection section)
    {
        current = section;
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        current = null;
        gameObject.SetActive(false);
    }

    private void DeleteSelected()
    {
        if (current == null) return;

        // remove da lista e destroy
        if (WarehouseManager.Instance != null)
        {
            WarehouseManager.Instance.Sections.Remove(current);
        }

        Destroy(current.gameObject);
        Hide();
    }

    private void EditPlacement()
    {
        if (current == null) return;

        // Editar a posição da section (TODO)

        //if (placementController != null)
        //placementController.StartEditPlacement(current);
    }

    private void AddShelf()
    {
        if (current == null) return;
        Debug.Log("[WarehouseEditPanel] AddShelf (TODO)");
    }

    private void RemoveShelf()
    {
        if (current == null) return;
        Debug.Log("[WarehouseEditPanel] RemoveShelf (TODO)");
    }

    private void RemodelSection()
    {
        if (current == null) return;
        Debug.Log("[WarehouseEditPanel] RemodelSection (TODO)");
    }
}
