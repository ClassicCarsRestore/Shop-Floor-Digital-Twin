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
        [SerializeField] private Button HeatmapButton;

        void Start()
        {
            ControlsPanel.SetActive(false);
            CarSlotsToggle.isOn = false;

            if (CarSlotsToggle != null)
            {
                CarSlotsToggle.onValueChanged.AddListener(HandleCarSlotsToggleChanged);
                HandleCarSlotsToggleChanged(CarSlotsToggle.isOn);
            }

            
            if (ProjectsToggle != null)
            {
                ProjectsToggle.onValueChanged.AddListener(HandleProjectsToggleChanged);
                HandleProjectsToggleChanged(ProjectsToggle.isOn);
            }
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
            HeatmapButton.interactable = false;
            DisableAllInteractables();
        }

        public void SimulationMode_InteractableOff()
        {
            ControlsButton.interactable = false;
            CarSlotsToggle.interactable = false;
            CharterTurinButton.interactable = false;
            ProjectsToggle.interactable = false;
            ProjectsSlider.interactable = false;
            HeatmapButton.interactable = false;
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
            HeatmapButton.interactable = true;
            EnableAllInteractables();

           
            if (CarSlotsToggle != null)
                HandleCarSlotsToggleChanged(CarSlotsToggle.isOn);

            if (ProjectsToggle != null)
                HandleProjectsToggleChanged(ProjectsToggle.isOn);
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
            Button[] buttons = LocationsPanel.GetComponentsInChildren<Button>(true);
            foreach (Button button in buttons)
            {
                button.interactable = false;
            }

            Toggle[] toggles = LocationsPanel.GetComponentsInChildren<Toggle>(true);
            foreach (Toggle toggle in toggles)
            {
                toggle.interactable = false;
            }

            Slider[] sliders = LocationsPanel.GetComponentsInChildren<Slider>(true);
            foreach (Slider slider in sliders)
            {
                slider.interactable = false;
            }
        }

        public void EnableAllInteractables()
        {
            Button[] buttons = LocationsPanel.GetComponentsInChildren<Button>(true);
            foreach (Button button in buttons)
            {
                button.interactable = true;
            }

            Toggle[] toggles = LocationsPanel.GetComponentsInChildren<Toggle>(true);
            foreach (Toggle toggle in toggles)
            {
                toggle.interactable = true;
            }

            Slider[] sliders = LocationsPanel.GetComponentsInChildren<Slider>(true);
            foreach (Slider slider in sliders)
            {
                slider.interactable = true;
            }
        }

        public void VirtualGridOff()
        {
            CarSlotGridSlider.value = 0;
        }

        public bool CarSlotToggleIsOn { get { return CarSlotsToggle.isOn; } }

       
        private void HandleCarSlotsToggleChanged(bool isOn)
        {
            
            bool canUseHeatmapAndSim = !isOn;

            if (HeatmapButton != null)
                HeatmapButton.interactable = canUseHeatmapAndSim;

            if (CharterTurinButton != null)
                CharterTurinButton.interactable = canUseHeatmapAndSim;

            if (SimulationSlider != null)
                SimulationSlider.interactable = canUseHeatmapAndSim;
        }

        private void HandleProjectsToggleChanged(bool isOn)
        {
            bool canUseHeatmapAndSim = !isOn;

            if (HeatmapButton != null)
                HeatmapButton.interactable = canUseHeatmapAndSim;

            if (CharterTurinButton != null)
                CharterTurinButton.interactable = canUseHeatmapAndSim;

            if (SimulationSlider != null)
                SimulationSlider.interactable = canUseHeatmapAndSim;
        }

    }
}
