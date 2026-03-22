using Objects;
using UnityEngine;
using UnityEngine.UI;

public class WarehouseEditPanel : MonoBehaviour
{
    [SerializeField] private Button deleteButton;
    [SerializeField] private Button moveButton;
    [SerializeField] private Button addShelfButton;
    [SerializeField] private Button removeShelfButton;
    [SerializeField] private Button remodelShelfButton;
    [SerializeField] private Button exitSectionButton;

    [SerializeField] private SectionPlacementController placementController;
    [SerializeField] private SectionRemodelController remodelController;
    private ShelfSectionShelvesController shelvesController;



    [SerializeField] private WarehouseSectionSelection selection;
    [SerializeField] private CameraSystem cameraSystem;

    private ShelfSection current;

    private void Awake()
    {


        if (deleteButton != null) deleteButton.onClick.AddListener(DeleteSelected);
        if (moveButton != null) moveButton.onClick.AddListener(EditPlacement);


        if (addShelfButton != null) addShelfButton.onClick.AddListener(AddShelf);
        if (removeShelfButton != null) removeShelfButton.onClick.AddListener(RemoveShelf);
        if (remodelShelfButton != null) remodelShelfButton.onClick.AddListener(RemodelSection);
        if (exitSectionButton != null) exitSectionButton.onClick.AddListener(DeselectAndClose);

        if (placementController != null)
        {
            placementController.OnEditPlacementStarted += HandleEditPlacementStarted;
            placementController.OnEditPlacementFinished += HandleEditPlacementFinished;
        }

        if (remodelController != null)
        {
            remodelController.OnRemodelStarted += HandleRemodelStarted;
            remodelController.OnRemodelFinished += HandleRemodelFinished;
        }

    }

    private void OnDestroy()
    {
        if (placementController != null)
        {
            placementController.OnEditPlacementStarted -= HandleEditPlacementStarted;
            placementController.OnEditPlacementFinished -= HandleEditPlacementFinished;
        }

        if (remodelController != null)
        {
            remodelController.OnRemodelStarted -= HandleRemodelStarted;
            remodelController.OnRemodelFinished -= HandleRemodelFinished;
        }

    }

    public void ShowFor(ShelfSection section)
    {
        current = section;
        gameObject.SetActive(true);
        SetEditButtonsInteractable(true);
        WarehouseBoxDetailsPanel.Instance?.Hide();

        WarehouseHUD.Instance?.EnterEditMode();

    }

    public void Hide()
    {
        current = null;
        gameObject.SetActive(false);
        WarehouseBoxDetailsPanel.Instance?.Hide();

        WarehouseHUD.Instance?.ExitEditMode();
    }

    private void SetEditButtonsInteractable(bool on)
    {
        if (deleteButton != null) deleteButton.interactable = on;
        if (moveButton != null) moveButton.interactable = on;
        if (addShelfButton != null) addShelfButton.interactable = on;
        if (removeShelfButton != null) removeShelfButton.interactable = on;
        if (remodelShelfButton != null) remodelShelfButton.interactable = on;
    }

    private void DeleteSelected()
    {
        if (current == null) return;
        WarehouseBoxDetailsPanel.Instance?.Hide();

        if (WarehouseManager.Instance != null)
            WarehouseManager.Instance.Sections.Remove(current);

        Destroy(current.gameObject);

        // depois de apagar, limpa sele��o (reativa movement e fecha painel)
        if (selection != null) selection.ClearSelection();
        else Hide();


        WarehouseHUD.Instance?.ExitEditMode();
    }

    private void DeselectAndClose()
    {
        WarehouseBoxDetailsPanel.Instance?.Hide();

        // restaurar pose da c�mara
        if (cameraSystem != null)
            cameraSystem.RestoreAfterSectionFocus();

        // isto vai: esconder este painel + reativar movement
        if (selection != null) selection.ClearSelection();
        else Hide();

        WarehouseHUD.Instance?.ExitEditMode();
    }


    private void EditPlacement()
    {
        if (current == null) return;
        WarehouseBoxDetailsPanel.Instance?.Hide();

        // desativa bot�es de edi��o imediatamente 
        SetEditButtonsInteractable(false);

        // ao come�ar move placement: controls ON
        if (cameraSystem != null)
            cameraSystem.ActiveControls();

        if (placementController != null)
            placementController.StartEditPlacement(current);
    }

    private void AddShelf()
    {
        if (current == null) return;
        WarehouseBoxDetailsPanel.Instance?.Hide();

        var ctrl = current.GetComponent<ShelfSectionShelvesController>();
        if (ctrl == null)
        {
            Debug.LogWarning("[WarehouseEditPanel] Esta section n�o tem ShelfSectionShelvesController.");
            return;
        }

        ctrl.AddShelf();
    }

    private void RemoveShelf()
    {
        if (current == null) return;
        WarehouseBoxDetailsPanel.Instance?.Hide();

        var ctrl = current.GetComponent<ShelfSectionShelvesController>();
        if (ctrl == null)
        {
            Debug.LogWarning("[WarehouseEditPanel] Esta section n�o tem ShelfSectionShelvesController.");
            return;
        }

        ctrl.RemoveShelf();
    }



    private void RemodelSection()
    {
        if (current == null) return;
        WarehouseBoxDetailsPanel.Instance?.Hide();


        SetEditButtonsInteractable(false);

        if (remodelController != null)
            remodelController.StartRemodel(current);
    }


    // ----------------------------
    // EVENTS do SectionPlacementController
    // ----------------------------
    private void HandleEditPlacementStarted(ShelfSection section)
    {
        // s� reage se for a section atualmente selecionada
        if (current != section) return;

        WarehouseBoxDetailsPanel.Instance?.Hide();


        gameObject.SetActive(false);

        WarehouseHUD.Instance?.EnterEditMode();
    }

    private void HandleEditPlacementFinished(ShelfSection section, bool saved)
    {
        if (current != section) return;
        WarehouseHUD.Instance?.EnterEditMode();
        SetEditButtonsInteractable(true);
        if (selection != null)
        {
            selection.SelectSection(section); //focar camera + mostrar painel + desativar controls
        }
        else
        {
            // fallback: mostra painel e desativa controls
            gameObject.SetActive(true);
            if (cameraSystem != null) cameraSystem.DesactiveControls();
        }
    }






    // ----------------------------
    // EVENTS do SectionRemodelController
    // ----------------------------

    private void HandleRemodelStarted(ShelfSection section)
    {
        if (current != section) return;

        WarehouseBoxDetailsPanel.Instance?.Hide();

        // esconder bot�es do edit enquanto remodel est� aberto
        gameObject.SetActive(false);

        WarehouseHUD.Instance?.EnterEditMode();
    }

    private void HandleRemodelFinished(ShelfSection section, bool saved)
    {
        if (current != section) return;

        WarehouseHUD.Instance?.EnterEditMode();
        // voltar ao modo normal de edi��o: section continua selecionada
        if (selection != null)
        {
            selection.SelectSection(section); // foca camera + mostra edit panel + controls OFF
        }
        else
        {
            gameObject.SetActive(true);
            SetEditButtonsInteractable(true);
            if (cameraSystem != null) cameraSystem.DesactiveControls();
        }
    }


}
