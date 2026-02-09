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
    }

    public void Hide()
    {
        current = null;
        gameObject.SetActive(false);
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

        if (WarehouseManager.Instance != null)
            WarehouseManager.Instance.Sections.Remove(current);

        Destroy(current.gameObject);

        // depois de apagar, limpa seleção (reativa movement e fecha painel)
        if (selection != null) selection.ClearSelection();
        else Hide();
    }

    private void DeselectAndClose()
    {
        // restaurar pose da câmara
        if (cameraSystem != null)
            cameraSystem.RestoreAfterSectionFocus();

        // isto vai: esconder este painel + reativar movement
        if (selection != null) selection.ClearSelection();
        else Hide();
    }


    private void EditPlacement()
    {
        if (current == null) return;

        // desativa botões de edição imediatamente 
        SetEditButtonsInteractable(false);

        // ao começar move placement: controls ON
        if (cameraSystem != null)
            cameraSystem.ActiveControls();

        if (placementController != null)
            placementController.StartEditPlacement(current);
    }

    private void AddShelf()
    {
        if (current == null) return;

        var ctrl = current.GetComponent<ShelfSectionShelvesController>();
        if (ctrl == null)
        {
            Debug.LogWarning("[WarehouseEditPanel] Esta section não tem ShelfSectionShelvesController.");
            return;
        }

        ctrl.AddShelf();
    }

    private void RemoveShelf()
    {
        if (current == null) return;

        var ctrl = current.GetComponent<ShelfSectionShelvesController>();
        if (ctrl == null)
        {
            Debug.LogWarning("[WarehouseEditPanel] Esta section não tem ShelfSectionShelvesController.");
            return;
        }

        ctrl.RemoveShelf();
    }



    private void RemodelSection()
    {
        if (current == null) return;

       
        SetEditButtonsInteractable(false);

        if (remodelController != null)
            remodelController.StartRemodel(current);
    }


    // ----------------------------
    // EVENTS do SectionPlacementController
    // ----------------------------
    private void HandleEditPlacementStarted(ShelfSection section)
    {
        // só reage se for a section atualmente selecionada
        if (current != section) return;

        
        gameObject.SetActive(false);
    }

    private void HandleEditPlacementFinished(ShelfSection section, bool saved)
    {
        if (current != section) return;
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

        // esconder botões do edit enquanto remodel está aberto
        gameObject.SetActive(false);
    }

    private void HandleRemodelFinished(ShelfSection section, bool saved)
    {
        if (current != section) return;

        // voltar ao modo normal de edição: section continua selecionada
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
