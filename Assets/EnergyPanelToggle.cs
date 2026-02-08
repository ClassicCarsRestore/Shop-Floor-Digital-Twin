using UnityEngine;
using UI; // <- para encontrar UIManager no namespace UI

public class EnergyPanelToggle : MonoBehaviour
{
    [SerializeField] private GameObject energyPanel;
    [SerializeField] private UIManager uiManager;

    // Se quiseres "bloquear tudo" enquanto o Energy está aberto
    [SerializeField] private bool disableTopButtonsWhileOpen = true;

    public void Open()
    {
        if (energyPanel != null)
            energyPanel.SetActive(true);

        if (disableTopButtonsWhileOpen && uiManager != null)
            uiManager.InteractableOff();
    }

    public void Close()
    {
        if (energyPanel != null)
            energyPanel.SetActive(false);

        if (disableTopButtonsWhileOpen && uiManager != null)
            uiManager.InteractableOn();
    }

    public void Toggle()
    {
        if (energyPanel == null) return;

        bool willOpen = !energyPanel.activeSelf;
        energyPanel.SetActive(willOpen);

        if (!disableTopButtonsWhileOpen || uiManager == null) return;

        if (willOpen) uiManager.InteractableOff();
        else uiManager.InteractableOn();
    }
}
