using UnityEngine;
using UnityEngine.UI;
using Objects;
using UI;

public class WarehouseHUD : MonoBehaviour
{
    public static WarehouseHUD Instance;
    [SerializeField] private Button exitButton;
    [SerializeField] private CameraSystem cameraSystem;
    [SerializeField] private UIManager uiManager;
    [SerializeField] private WarehouseSectionInteractor sectionInteractor;
    [SerializeField] private WarehouseSectionSelection selection;

    private void Awake()
    {
        Instance = this;

        if (exitButton != null)
            exitButton.onClick.AddListener(ExitWarehouse);

        gameObject.SetActive(false);
    }

    public void Show()
    {
        uiManager.InteractableOff();
        gameObject.SetActive(true);

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

    private void ExitWarehouse()
    {
        if (cameraSystem != null)
            cameraSystem.ExitWarehouseFirstPerson();

        Hide();
    }
}
