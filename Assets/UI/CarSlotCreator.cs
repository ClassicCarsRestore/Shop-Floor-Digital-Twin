using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using API;
using Models;
using Objects;
using TS.ColorPicker;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Windows;

namespace UI
{

    public class CarSlotCreator : MonoBehaviour
    {
        [SerializeField] private GameObject carSlotPrefab;
        [SerializeField] private GameObject locationMarkerPrefab;
        [SerializeField] private InputField nameInputField;
        [SerializeField] private GameObject CreateCarSlotPanel;
        [SerializeField] private GameObject ConfirmCarSlotLocationButton;
        [SerializeField] private GameObject PreviousLocationButton;
        [SerializeField] private GameObject ConfirmCarSlotCreationButton;
        [SerializeField] private GameObject PreviousProcButton;
        [SerializeField] private GameObject LocationButton;
        [SerializeField] private CameraSystem cameraSystem;
        [SerializeField] private Text title;
        [SerializeField] private Text descriptionName;
        [SerializeField] private Text warningNameText;
        [SerializeField] private ProcessesAndActivitiesList processesAndActivitiesPanel;
        [SerializeField] private Transform canvas;
        [SerializeField] private Dropdown Floor;
        [SerializeField] private ColorPicker colorPicker;
        [SerializeField] private Button colorPickerButton;
        [SerializeField] private Image imageColor;
        [SerializeField] private GameObject LocationDetails;
        [SerializeField] private Toggle requiresCarToggle;
        [SerializeField] private InputField capacityInput;
        [SerializeField] private Text errorMessage;
        [SerializeField] private GameObject infoPanel;

        [Header("API Manager")]
        public APIscript apiManager;
        public UIManager uiManager;
        private GameObject currentCarSlot;
        private CarSlotObject currentCarSlotDragDrop;
        public VirtualMapScrollView scrollView;
        private ProcessesAndActivitiesList currentProcessesAndActivitiesPanel;
        private bool carSlotType = true;
        private Roof workshopRoof;
        private CreatePolygon newArea;
        private ColorPickerLocation currentColorPickerLocation;
        private CarSlotMeshCreator currentCarSlotMeshCreator;

        private void Start()
        {
            if (uiManager == null)
            {
                Debug.LogError("UIManager not found in the scene!");
            }
            CreateCarSlotPanel.SetActive(false);
            ConfirmCarSlotLocationButton.SetActive(false);
            PreviousLocationButton.SetActive(false);
            title.gameObject.SetActive(false);
            descriptionName.gameObject.SetActive(false);
            PreviousProcButton.SetActive(false);
            ConfirmCarSlotCreationButton.SetActive(false);
            // LocationTypePanel.SetActive(false);
            Floor.onValueChanged.AddListener(delegate { OnFloorSelected(Floor); });
            Floor.gameObject.SetActive(false);
            workshopRoof = GameObject.Find("oficina").GetComponent<Roof>();
            LocationDetails.gameObject.SetActive(false);
            requiresCarToggle.onValueChanged.AddListener(RequiresCarChanged);
            RequiresCarChanged(requiresCarToggle.isOn);
            infoPanel.SetActive(false);
        }

        void RequiresCarChanged(bool isOn)
        {
            // Enable or disable the InputField based on the Toggle state
            capacityInput.interactable = isOn;

            if (!isOn)
            {
                // Clear the input if toggle is unchecked
                capacityInput.text = "";
            }
        }

        public void CreateCarSlotButtonClicked()
        {
            CreateCarSlotPanel.SetActive(true);
            scrollView.carSlotSlider.value = 1.0f;
            cameraSystem.DesactiveControls();
            uiManager.InteractableOff();
            uiManager.CloseProjects();
            workshopRoof.DesactiveRoof();
        }

        void OnFloorSelected(Dropdown dropdown)
        {
            int index = dropdown.value;

            // Assuming Ground Floor is at index 0 and First Floor is at index 1
            switch (index)
            {
                case 0:
                    currentCarSlot.transform.position = new Vector3(0f, 1f, 0f);

                    break;
                case 1:
                    currentCarSlot.transform.position = new Vector3(540f, 86f, -280f);
                    break;
            }
        }

        public void CancelButton()
        {
            CreateCarSlotPanel.SetActive(false);
            cameraSystem.ActiveControls();
            warningNameText.text = "";
            uiManager.InteractableOn();
            nameInputField.text = "";
            workshopRoof.ActiveRoof();
        }

        public void PreviousButtonCarSlot()
        {
            CreateCarSlotPanel.SetActive(true);
            ConfirmCarSlotLocationButton.SetActive(false);
            Destroy(currentCarSlot);
            PreviousLocationButton.SetActive(false);
            cameraSystem.DesactiveControls();
            title.gameObject.SetActive(false);
            descriptionName.gameObject.SetActive(false);
            // LocationTypePanel.SetActive(false);
            Floor.gameObject.SetActive(false);
            workshopRoof.DesactiveRoof();
            infoPanel.SetActive(false);
        }

        public void PreviousButtonProc()
        {
            currentProcessesAndActivitiesPanel.gameObject.SetActive(false);
            currentCarSlotDragDrop.SetDraggable(true);
            ConfirmCarSlotCreationButton.SetActive(false);
            PreviousProcButton.SetActive(false);
            ConfirmCarSlotLocationButton.SetActive(true);
            PreviousLocationButton.SetActive(true);
            //  LocationTypePanel.SetActive(true);
            Floor.gameObject.SetActive(true);
            LocationDetails.gameObject.SetActive(false);
            newArea.ShowAllSpheres();
            infoPanel.SetActive(true);
        }

        public void ConfirmNameButtonClicked()
        {
            warningNameText.text = "";

            string carSlotName = nameInputField.text;
            if (!string.IsNullOrEmpty(carSlotName))
            {
                currentCarSlot = Instantiate(carSlotPrefab, new Vector3(0f, 1f, 0f), transform.rotation);
                carSlotType = true;
                currentCarSlot.name = carSlotName;
                currentCarSlot.GetComponent<Renderer>().material.color = Color.red;

                newArea = currentCarSlot.GetComponent<CreatePolygon>();
                Vector3[] spherePositions = new Vector3[4];
                for (int i = 0; i < currentCarSlot.transform.childCount; i++)
                {
                    spherePositions[i] = currentCarSlot.transform.GetChild(i).position; // Get positions of vertex spheres
                }
                newArea.initialize(spherePositions);

                currentCarSlotMeshCreator = currentCarSlot.GetComponent<CarSlotMeshCreator>();
                currentCarSlotMeshCreator.MeshColliderTriggerFalse();
                // currentCarSlotMeshCreator.CreateMesh(spherePositions);
                ColorPickerLocation colorPickerLocation = currentCarSlot.GetComponent<ColorPickerLocation>();
                colorPickerLocation._renderer = currentCarSlotMeshCreator.meshRenderer;
                InitializeColorPickerLocation(colorPickerLocation);

                foreach (Transform vertex in currentCarSlot.transform)
                {
                    VertexDragger dragger = vertex.gameObject.AddComponent<VertexDragger>();
                    dragger.Initialize(newArea); // Pass the PolygonCreator reference
                }

                currentCarSlotDragDrop = currentCarSlot.AddComponent<CarSlotObject>();
                currentCarSlotDragDrop.SetDraggable(true);
                CreateCarSlotPanel.SetActive(false);
                ConfirmCarSlotLocationButton.SetActive(true);
                PreviousLocationButton.SetActive(true);
                cameraSystem.ActiveControls();
                title.gameObject.SetActive(true);
                descriptionName.gameObject.SetActive(true);
                descriptionName.text = carSlotName;
                //  LocationTypePanel.SetActive(true);
                Floor.gameObject.SetActive(true);
                Floor.value = 0;
                workshopRoof.ActiveRoof();
                infoPanel.SetActive(true);
            }
            else
            {
                warningNameText.text = "Invalid Name";
            }

        }

        private void InitializeColorPickerLocation(ColorPickerLocation colorPickerLocation)
        {
            this.currentColorPickerLocation = colorPickerLocation;

            currentColorPickerLocation.SetColor(Color.red);
            imageColor.color = currentColorPickerLocation.GetCurrentColor();

            colorPicker.gameObject.SetActive(false);

            // Add listener to the color picker button
            colorPickerButton.onClick.AddListener(OpenColorPicker);

            // Set up color picker events
            colorPicker.OnChanged.AddListener(ColorPicker_OnChanged);
            colorPicker.OnSubmit.AddListener(ColorPicker_OnSubmit);
            colorPicker.OnCancel.AddListener(ColorPicker_OnCancel);

        }

        private void OpenColorPicker()
        {
            if (currentColorPickerLocation != null)
            {
                colorPicker.Open(currentColorPickerLocation.GetCurrentColor());
                colorPicker.gameObject.SetActive(true);
            }
        }

        private void ColorPicker_OnChanged(Color color)
        {
            if (currentColorPickerLocation != null)
            {
                currentColorPickerLocation.SetColor(color);
                imageColor.color = color;
            }
        }

        private async void ColorPicker_OnSubmit(Color color)
        {
            if (currentColorPickerLocation != null)
            {
                currentColorPickerLocation.SetColor(color);
                imageColor.color = color;
            }
            colorPicker.gameObject.SetActive(false);
        }

        private void ColorPicker_OnCancel()
        {
            if (currentColorPickerLocation != null)
            {
                currentColorPickerLocation.ResetColor();
                imageColor.color = currentColorPickerLocation._originalColor;
            }
            colorPicker.gameObject.SetActive(false); // Hide the color picker after submission
        }

        public void LocationMarkerType()
        {
            string tmpName = currentCarSlot.name;
            Destroy(currentCarSlot);
            Destroy(currentCarSlotDragDrop);
            currentCarSlot = null;
            if (Floor.value == 0)
            {
                currentCarSlot = Instantiate(locationMarkerPrefab, new Vector3(0f, 20f, 0f), transform.rotation);
            }
            else
            {
                currentCarSlot = Instantiate(locationMarkerPrefab, new Vector3(580f, 105f, -460f), transform.rotation);
            }
            carSlotType = false;
            currentCarSlot.name = tmpName;
            currentCarSlot.GetComponent<Renderer>().material.color = Color.red;
            currentCarSlotDragDrop = currentCarSlot.AddComponent<CarSlotObject>();
            currentCarSlotDragDrop.SetDraggable(true);
        }

        public void CarSlotType()
        {
            string tmpName = currentCarSlot.name;
            Destroy(currentCarSlot);
            Destroy(currentCarSlotDragDrop);
            currentCarSlot = null;
            if (Floor.value == 0)
            {
                currentCarSlot = Instantiate(carSlotPrefab, new Vector3(0f, 1f, 0f), transform.rotation);
            }
            else
            {
                currentCarSlot = Instantiate(carSlotPrefab, new Vector3(580f, 86f, -460f), transform.rotation);
            }
            carSlotType = true;
            currentCarSlot.name = tmpName;
            currentCarSlot.GetComponent<Renderer>().material.color = Color.red;
            currentCarSlotDragDrop = currentCarSlot.AddComponent<CarSlotObject>();
            currentCarSlotDragDrop.SetDraggable(true);
        }

        public void ConfirmCarSlotLocation()
        {
            LocationDetails.gameObject.SetActive(true);
            newArea.HideAllSpheres();
            currentProcessesAndActivitiesPanel = Instantiate(processesAndActivitiesPanel, canvas);
            currentProcessesAndActivitiesPanel.GetComponent<ProcessesAndActivitiesList>().PopulateProcessesAndActivitiesNew();
            currentCarSlotDragDrop.SetDraggable(false);
            ConfirmCarSlotLocationButton.SetActive(false);
            PreviousLocationButton.SetActive(false);
            ConfirmCarSlotCreationButton.SetActive(true);
            PreviousProcButton.SetActive(true);
            // LocationTypePanel.SetActive(false);
            Floor.gameObject.SetActive(false);
            cameraSystem.DesactiveControls();
            workshopRoof.DesactiveRoof();
            infoPanel.SetActive(false);
        }

        public async void ConfirmCreationCarSlot()
        {
            int capacityIntValue = 0;
            if (requiresCarToggle.isOn)
            {
                if (!ValidateCapacity(capacityInput.text))
                {
                    return;
                }

                int.TryParse(capacityInput.text, out capacityIntValue);
            }

            Vector3 position;
            float rotation;
            position = currentCarSlot.transform.position;
            rotation = Mathf.Round(currentCarSlot.transform.eulerAngles.y * 100f) / 100f;
            List<string> activityIds = currentProcessesAndActivitiesPanel.GetComponent<ProcessesAndActivitiesList>().GetCheckedActivities();
            currentCarSlotMeshCreator.MeshColliderTriggerTrue();
            List<VerticesCoordinates> verticesCoordinates = currentCarSlotMeshCreator.GetVertices();
            string color = currentColorPickerLocation.GetCurrentColor().ToHexString();
            await apiManager.CreateVirtualMapLocationAsync(currentCarSlot.name, position.x, position.y, position.z, activityIds, verticesCoordinates, "#" + color, capacityIntValue);
            ConfirmCarSlotCreationButton.SetActive(false);
            uiManager.InteractableOn();
            scrollView.PopulateLocations();
            scrollView.InstantiateLocations();
            Destroy(currentCarSlot);
            nameInputField.text = "";
            PreviousProcButton.SetActive(false);
            title.gameObject.SetActive(false);
            descriptionName.gameObject.SetActive(false);
            LocationDetails.gameObject.SetActive(false);
            cameraSystem.ActiveControls();
            Destroy(currentProcessesAndActivitiesPanel.gameObject);
        }


        bool ValidateCapacity(string input)
        {
            // Clear any previous error messages
            errorMessage.text = "";
            if (input != null)
            {


                // Try parsing the input as a number
                if (int.TryParse(input, out int capacity))
                {
                    if (capacity > 0)
                    {
                        // Valid capacity
                        errorMessage.text = "";  // Clear any error messages
                        return true;
                    }
                    else
                    {
                        // Invalid: capacity must be greater than 0
                        errorMessage.text = "Capacity must be greater than 0.";
                        return false;

                    }
                }
                else
                {
                    // Invalid: not a valid number
                    errorMessage.text = "Please enter a valid number.";
                    return false;

                }
            }
            return false;
        }

    }
}