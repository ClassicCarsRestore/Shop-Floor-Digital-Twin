using UnityEngine;
using UnityEngine.UI;
using Objects;
using UI;

public class WarehouseHUD : MonoBehaviour
{
    public static WarehouseHUD Instance;

    [Header("Main")]
    [SerializeField] private Button exitButton;
    [SerializeField] private CameraSystem cameraSystem;
    [SerializeField] private UIManager uiManager;
    [SerializeField] private WarehouseSectionInteractor sectionInteractor;
    [SerializeField] private WarehouseSectionSelection selection;

    [Header("Layout Save/Cancel")]
    [SerializeField] private WarehouseLayoutController layoutController;
    [SerializeField] private Button saveToBdButton;
    [SerializeField] private Button cancelAllButton;

    [Header("Unsaved Changes Dialog")]
    [SerializeField] private GameObject unsavedChangesDialog;
    [SerializeField] private Button unsavedExitAnywayButton;
    [SerializeField] private Button unsavedCancelButton;

    public void EnterEditMode() => SetMainHudButtonsVisible(false);
    public void ExitEditMode() => SetMainHudButtonsVisible(true);


    private void Awake()
    {
        Instance = this;

        if (exitButton != null)
            exitButton.onClick.AddListener(OnExitWarehouseClicked);

        if (saveToBdButton != null)
            saveToBdButton.onClick.AddListener(OnSaveToBD);
        if (cancelAllButton != null)
            cancelAllButton.onClick.AddListener(OnCancelAll);

        if (unsavedChangesDialog != null)
            unsavedChangesDialog.SetActive(false);
        if (unsavedExitAnywayButton != null)
            unsavedExitAnywayButton.onClick.AddListener(ExitWarehouseAndHideDialog);
        if (unsavedCancelButton != null)
            unsavedCancelButton.onClick.AddListener(HideUnsavedDialog);

        gameObject.SetActive(false);
    }

    public void Show()
    {
        uiManager.InteractableOff();
        gameObject.SetActive(true);

        SetMainHudButtonsVisible(true);

        if (sectionInteractor != null) sectionInteractor.IsActive = true;
    }

    public void Hide()
    {
        if (sectionInteractor != null)
        {
            sectionInteractor.IsActive = false;
            sectionInteractor.ClearHover();
        }

        if (selection != null) selection.ClearSelection();

        gameObject.SetActive(false);
        uiManager.InteractableOn();
    }

    private void OnExitWarehouseClicked()
    {
        if (layoutController != null && layoutController.HasUnsavedChanges() && unsavedChangesDialog != null)
        {
            unsavedChangesDialog.SetActive(true);
            return;
        }

        ExitWarehouse();
    }

    private void ExitWarehouseAndHideDialog()
    {
        if (unsavedChangesDialog != null)
            unsavedChangesDialog.SetActive(false);
        ExitWarehouse();
    }

    private void HideUnsavedDialog()
    {
        if (unsavedChangesDialog != null)
            unsavedChangesDialog.SetActive(false);
    }

    private void ExitWarehouse()
    {
        if (cameraSystem != null)
            cameraSystem.ExitWarehouseFirstPerson();

        Hide();
    }

    private void OnSaveToBD()
    {
        if (layoutController != null)
            layoutController.OnClickSaveToBD();
    }

    private void OnCancelAll()
    {
        if (layoutController != null)
            layoutController.OnClickCancelAll();
    }

    public void SetMainHudButtonsVisible(bool visible)
    {
        if (exitButton != null) exitButton.gameObject.SetActive(visible);
        if (saveToBdButton != null) saveToBdButton.gameObject.SetActive(visible);
        if (cancelAllButton != null) cancelAllButton.gameObject.SetActive(visible);

       
    }

}
