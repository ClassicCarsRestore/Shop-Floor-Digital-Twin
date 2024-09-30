
using System.Runtime.InteropServices.WindowsRuntime;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{

    public class UIManager : MonoBehaviour
    {

        [SerializeField] private Button ControlsButton;
        [SerializeField] private Toggle CarSlotsToggle;
        [SerializeField] private Button CharterTurinButton;
        [SerializeField] private Slider SimulationSlider;
        [SerializeField] private Slider CarSlotGridSlider;
        [SerializeField] private GameObject ControlsPanel;
        [SerializeField] private GameObject LocationsPanel;
        [SerializeField] private Toggle ProjectsToggle;
        [SerializeField] private Slider ProjectsSlider;

        void Start()
        {
            ControlsPanel.SetActive(false);
            CarSlotsToggle.isOn = false;
        }

        public void InteractableOff()
        {
            ControlsButton.interactable = false;
            CarSlotsToggle.interactable = false;
            CharterTurinButton.interactable = false;
            SimulationSlider.interactable = false;
            CarSlotGridSlider.interactable = false;
            ProjectsToggle.interactable = false;
            ProjectsSlider.interactable = false;
            DisableAllInteractables();
        }

        public void SimulationMode_InteractableOff()
        {
            ControlsButton.interactable = false;
            CarSlotsToggle.interactable = false;
            CharterTurinButton.interactable = false;
            ProjectsToggle.interactable = false;
            ProjectsSlider.interactable = false;
            DisableAllInteractables();
        }

        public void InteractableOn()
        {
            ControlsButton.interactable = true;
            CarSlotsToggle.interactable = true;
            CharterTurinButton.interactable = true;
            SimulationSlider.interactable = true;
            CarSlotGridSlider.interactable = true;
            ProjectsToggle.interactable = true;
            ProjectsSlider.interactable = true;
            EnableAllInteractables();
        }

        private void OnControlsToggleValueChanged(bool isOn)
        {
            if (isOn)
            {
                ControlsPanel.SetActive(true);
            }
            else
            {
                ControlsPanel.SetActive(false);
            }

        }

        public void CloseControlsPage()
        {
            ControlsPanel.SetActive(false);
        }

        public void OpenControlsPage()
        {
            ControlsPanel.SetActive(true);
        }

        public void CloseProjects()
        {
            ProjectsToggle.isOn = false;
            ProjectsSlider.value = 0;
        }

        public void CloseCarLocations()
        {
            CarSlotsToggle.isOn = false;
            CarSlotGridSlider.value = 0;
        }

        public void DisableAllInteractables()
        {
            // Find all Button components in the panel and disable them
            Button[] buttons = LocationsPanel.GetComponentsInChildren<Button>(true);
            foreach (Button button in buttons)
            {
                button.interactable = false;
            }

            // Find all Toggle components in the panel and disable them
            Toggle[] toggles = LocationsPanel.GetComponentsInChildren<Toggle>(true);
            foreach (Toggle toggle in toggles)
            {
                toggle.interactable = false;
            }

            // Find all Slider components in the panel and disable them
            Slider[] sliders = LocationsPanel.GetComponentsInChildren<Slider>(true);
            foreach (Slider slider in sliders)
            {
                slider.interactable = false;
            }

            // Repeat for other interactable components as needed
        }

        public void EnableAllInteractables()
        {
            // Find all Button components in the panel and enable them
            Button[] buttons = LocationsPanel.GetComponentsInChildren<Button>(true);
            foreach (Button button in buttons)
            {
                button.interactable = true;
            }

            // Find all Toggle components in the panel and enable them
            Toggle[] toggles = LocationsPanel.GetComponentsInChildren<Toggle>(true);
            foreach (Toggle toggle in toggles)
            {
                toggle.interactable = true;
            }

            // Find all Slider components in the panel and enable them
            Slider[] sliders = LocationsPanel.GetComponentsInChildren<Slider>(true);
            foreach (Slider slider in sliders)
            {
                slider.interactable = true;
            }

            // Repeat for other interactable components as needed
        }

        public bool CarSlotToggleIsOn { get { return CarSlotsToggle.isOn; } }


    }
}
