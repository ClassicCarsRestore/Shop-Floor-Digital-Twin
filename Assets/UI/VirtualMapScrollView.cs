using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.Design.Serialization;
using System.Runtime.ConstrainedExecution;
using System.Threading.Tasks;
using TS.ColorPicker;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using API;
using Models;
using Unity.VisualScripting;
using Objects;

namespace UI
{

    public class VirtualMapScrollView : MonoBehaviour
    {
        [SerializeField] private GameObject LocationsPanel;
        [SerializeField] private Toggle toggleButton;
        [SerializeField] Button locationTemplate;
        [SerializeField] private Transform contentParent;
        [Header("API Manager")]
        public APIscript apiManager;
        [Header("Slider")]
        [SerializeField] public Slider carSlotSlider;
        [SerializeField] private Text carSlotstatusText;
        [SerializeField] public GameObject CarSlot;
        [SerializeField] public GameObject location_marker;
        [SerializeField] public Material carMaterial;
        [SerializeField] private InputField updateName;
        [SerializeField] public GameObject updateCarLocationPanel;
        [SerializeField] private GameObject PreviousUpdateLocationButton;
        [SerializeField] private GameObject confirmUpdateLocationButton;
        [SerializeField] private UIManager uiManager;
        [SerializeField] private CameraSystem cameraSystem;
        [SerializeField] private Text title;
        [SerializeField] private Text descriptionName;
        [SerializeField] private Text warningNameText;
        [SerializeField] private GameObject updateCarPanelGeral;
        [SerializeField] private ProcessesAndActivitiesList processesAndActivitiesPanel;
        [SerializeField] private Transform canvas;
        [SerializeField] private GameObject PreviousUpdateGeral;
        [SerializeField] private GameObject confirmUpdateTasks;
        [SerializeField] private GameObject exitUpdateTasks;
        [SerializeField] private GameObject LocationDetails;
        [SerializeField] private Toggle requiresCarToggle;
        [SerializeField] private InputField capacityInput;
        [SerializeField] private Text errorMessage;
        [SerializeField] private Image imageColor;
        [SerializeField] private ColorPicker colorPicker;
        [SerializeField] private Button colorPickerButton;

        private List<GameObject> gameObjects;
        private List<GameObject> firstFloorAreas = new();
        private GameObject currentlyHighlightedCarSlot;
        private GameObject currentlyBorder;
        private CarSlotObject currentCarSlotObject;
        private string currentLocationId;
        private string currentLocationName;
        private ProcessesAndActivitiesList currentProcessesAndActivitiesPanel;
        private Roof workshopRoof;
        private bool carSlotType = true;
        private Vector3 oldPos;
        private string currentCarSlotFloor;
        private ColorPickerLocation currentColorPickerLocation;
        private CarSlotMeshCreator currentCarSlotMeshCreator;
        private CreatePolygon currentPolygon;


        void Start()
        {
            gameObjects = new List<GameObject>();

            if (apiManager == null)
            {
                Debug.LogError("APIManager not assigned to VirtualMapScrollView.");
                return;
            }

            if (uiManager == null)
            {
                Debug.LogError("UIManager not found in the scene!");
            }
            apiManager.VirtualMapLocationUpdated += OnVirtualMapLocationUpdated;
            toggleButton.onValueChanged.AddListener(OnCarSlotToggleValueChanged);

            toggleButton.isOn = false;

            OnCarSlotToggleValueChanged(false);

            carSlotSlider.onValueChanged.AddListener(OnCarSlotSliderValueChanged);
            updateCarPanelGeral.SetActive(false);
            updateCarLocationPanel.SetActive(false);
            PreviousUpdateLocationButton.SetActive(false);
            confirmUpdateLocationButton.SetActive(false);
            PreviousUpdateGeral.SetActive(false);
            confirmUpdateTasks.SetActive(false);
            exitUpdateTasks.SetActive(false);
            title.gameObject.SetActive(false);
            descriptionName.gameObject.SetActive(false);
            LocationDetails.gameObject.SetActive(false);
            requiresCarToggle.onValueChanged.AddListener(RequiresCarChanged);
            workshopRoof = GameObject.Find("oficina").GetComponent<Roof>();
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

        void OnCarSlotToggleValueChanged(bool isOn)
        {
            if (isOn)
            {
                LocationsPanel.SetActive(true);
                carSlotSlider.value = 1.0f;
                PopulateLocations();
            }
            else
            {
                LocationsPanel.SetActive(false);
                carSlotSlider.value = 0.0f;
                ClearLocations();
            }
        }

        void OnCarSlotSliderValueChanged(float v)
        {
            if (v == 1.0f)
            {
                carSlotstatusText.text = "On";
                InstantiateLocations();
            }
            else
            {
                carSlotstatusText.text = "Off";
                DestroyLocations();
                firstFloorAreas.Clear();
            }
        }

        void ClearLocations()
        {
            foreach (Transform child in contentParent)
            {
                if (child.gameObject.name != locationTemplate.gameObject.name)
                {
                    Destroy(child.gameObject);
                }
                else
                {
                    child.gameObject.SetActive(false);
                }
            }
        }

        public async void PopulateLocations()
        {
            ClearLocations();
            await apiManager.GetVirtualMapLocationsAsync();
            // Populate with fetched locations
            foreach (var location in apiManager.locations)
            {
                string floorText;
                string locationNameUI = location.name;
                /**
                if (location.name.StartsWith("FF_"))
                {
                    floorText = "First Floor";
                    locationNameUI = locationNameUI.Replace("FF_", "");
                }
                else
                {
                    floorText = "Ground Floor";
                    locationNameUI = locationNameUI.Replace("GF_", "");
                }
                **/
                /**
                string locationText = string.Format("{0}: ({1} , {2}) , rotation -> {3}",
                                                 location.name, location.coordinateX, location.coordinateY, location.rotation);**/
                string locationText = string.Format("{0}",
                                                 locationNameUI);
                Button newLocation = Instantiate(locationTemplate, contentParent);
                newLocation.gameObject.name = location.id;

                newLocation.transform.GetChild(0).GetComponent<Text>().text = locationText;
                newLocation.transform.GetChild(1).GetComponent<Button>().onClick.AddListener(() => DeleteLocation(location.id));
                newLocation.transform.GetChild(2).GetComponent<Button>().onClick.AddListener(() => OpenUpdateCarSlotPanelGeral(location.id, location.name));
                newLocation.transform.GetChild(3).GetComponent<Toggle>().onValueChanged.AddListener((value) => ToggleCarSlotVisibility(location.id, value));

                UnityEngine.ColorUtility.TryParseHtmlString(location.color, out Color color);


                newLocation.transform.GetChild(4).GetComponent<Image>().color = color;
                newLocation.gameObject.SetActive(true);
                /**
                ColorBlock colorBlock = newLocation.colors;
                colorBlock.highlightedColor = Color.blue;
                colorBlock.pressedColor = Color.blue;
                newLocation.colors = colorBlock;
                **/
                // newLocation.onClick.AddListener(() => HighlightCarSlot(location.id));

            }
        }

        public async Task InstantiateLocations()
        {
            DestroyLocations();
            firstFloorAreas.Clear();
            await apiManager.GetVirtualMapLocationsAsync();
            foreach (var location in apiManager.locations)
            {
                GameObject carSlot;
                carSlot = Instantiate(CarSlot, new Vector3(location.coordinateX, location.coordinateY, location.coordinateZ), transform.rotation);
                CreatePolygon area = carSlot.GetComponent<CreatePolygon>();

                Vector3[] spherePositions = new Vector3[4];
                for (int i = 0; i < location.vertices.Count; i++)
                {
                    spherePositions[i].x = location.vertices[i].X;
                    spherePositions[i].z = location.vertices[i].Z;
                }
                area.initialize(spherePositions);
                //  area.UpdateVertex(0, spherePositions[0]);

                CarSlotMeshCreator currentCarSlotMeshCreator = carSlot.GetComponent<CarSlotMeshCreator>();
                //CarSlotMeshCreator newcr = area.gameObject.GetComponent<CarSlotMeshCreator>();
                // newcr.CreateMesh(spherePositions);


                //urrentCarSlotMeshCreator.CreateMesh(spherePositions);
                //   currentCarSlotMeshCreator.CreateMesh(spherePositions);
                // currentCarSlotMeshCreator.CreateMesh(spherePositions);


                area.HideAllSpheres();

                ColorPickerLocation colorLocation = carSlot.GetComponent<ColorPickerLocation>();
                colorLocation._renderer = currentCarSlotMeshCreator.meshRenderer;
                if (!string.IsNullOrEmpty(location.color))
                {
                    Debug.Log(location.color);
                    colorLocation.SetColorFromHex(location.color);
                    Debug.Log(colorLocation.GetCurrentColor());
                }

                carSlot.GetComponent<CarSlotObject>().info = location;
                carSlot.name = location.id;
                if (location.coordinateY > 1)
                {
                    firstFloorAreas.Add(carSlot);
                }
                gameObjects.Add(carSlot);
            }

            if (!workshopRoof.FirstFloor.active && firstFloorAreas.Count != 0)
            {
                HideFirstFloor();
            }
        }

        public void DestroyLocations()
        {
            foreach (GameObject carSlot in gameObjects)
            {
                carSlot.SetActive(false);
                Destroy(carSlot);
            }
            gameObjects.Clear();
        }

        public async Task DeleteLocation(string id)
        {
            await apiManager.DeleteVirtualMapLocationAsync(id);
            foreach (Transform child in contentParent)
            {
                if (child.gameObject.name == id)
                {
                    Destroy(child.gameObject);
                    break; 
                }
            }
            for (int i = gameObjects.Count - 1; i >= 0; i--)
            {
                GameObject carSlot = gameObjects[i];
                if (carSlot.gameObject.name == id)
                {
                    Destroy(carSlot);
                    gameObjects.RemoveAt(i);
                }
            }
        }

        public void OpenUpdateCarSlotPanelGeral(string locationId, string locationName)
        {
            carSlotSlider.value = 1.0f;
            uiManager.CloseProjects();
            uiManager.InteractableOff();
            currentLocationId = locationId;
            currentLocationName = locationName;
            currentCarSlotFloor = null;
            if (currentLocationName.StartsWith("FF_"))
            {
                currentLocationName = currentLocationName.Replace("FF_", "");
                currentCarSlotFloor = "FF_";
            }
            else
            {
                currentLocationName = currentLocationName.Replace("GF_", "");
                currentCarSlotFloor = "GF_";
            }
            if (currentLocationName.Contains("_location"))
            {
                carSlotType = false;
                currentLocationName = currentLocationName.Replace("_location", "");
            }

            updateCarPanelGeral.SetActive(true);
            PreviousUpdateGeral.SetActive(true);
            foreach (GameObject carSlot in gameObjects)
            {
                if (carSlot.gameObject.name == currentLocationId)
                {
                    currentCarSlotObject = carSlot.GetComponent<CarSlotObject>();
                    currentCarSlotObject.GetComponent<Renderer>().material.color = Color.blue;
                    oldPos = currentCarSlotObject.transform.position;
                }
                else
                {
                    var notCarSlotSelected = carSlot.GetComponent<CarSlotObject>();
                    notCarSlotSelected.GetComponent<Renderer>().material = carMaterial;
                }
            }
        }

        public void OpenUpdateCarSlotPanelLocation()
        {
            PreviousUpdateGeral.SetActive(false);
            updateCarPanelGeral.SetActive(false);
            updateCarLocationPanel.SetActive(true);
            uiManager.InteractableOff();
            updateName.text = currentLocationName;
            cameraSystem.DesactiveControls();
            workshopRoof.DesactiveRoof();
        }
        public async void OpenUpdateCarSlotPanelProcAndTasks()
        {
            currentProcessesAndActivitiesPanel = Instantiate(processesAndActivitiesPanel, canvas);
            await currentProcessesAndActivitiesPanel.GetComponent<ProcessesAndActivitiesList>().PopulateProcessesAndActivitiesUpdate(currentLocationId);
            uiManager.InteractableOff();
            updateName.text = currentLocationName;
            updateCarPanelGeral.SetActive(false);
            PreviousUpdateGeral.SetActive(false);
            confirmUpdateTasks.SetActive(true);
            exitUpdateTasks.SetActive(true);
            title.gameObject.SetActive(true);
            descriptionName.gameObject.SetActive(true);
            descriptionName.text = currentLocationName;
        }

        public void CancelUpdateLocation()
        {
            updateCarLocationPanel.SetActive(false);
            uiManager.InteractableOn();
            cameraSystem.ActiveControls();
            currentCarSlotObject.gameObject.GetComponent<Renderer>().material = carMaterial;
            warningNameText.text = "";
            title.gameObject.SetActive(false);
            descriptionName.gameObject.SetActive(false);
            workshopRoof.ActiveRoof();

        }

        public async void ConfirmNameUpdateCarSlot()
        {
            string newName = updateName.text;
            if (!string.IsNullOrEmpty(newName))
            {
                currentCarSlotObject.name = newName;
                if (currentCarSlotFloor == "FF_")
                {
                    currentCarSlotObject.name = "FF_" + currentCarSlotObject.name;
                }
                if (!carSlotType)
                {
                    currentCarSlotObject.name += "_location";
                }
                currentCarSlotObject.GetComponent<Renderer>().material.color = Color.red;
                currentCarSlotObject.SetDraggable(true);
                currentCarSlotMeshCreator = currentCarSlotObject.GetComponent<CarSlotMeshCreator>();
                currentCarSlotMeshCreator.MeshColliderTriggerFalse();
                currentPolygon = currentCarSlotMeshCreator.GetComponent<CreatePolygon>();
                currentPolygon.ShowAllSpheres();
                ColorPickerLocation colorPickerLocation = currentCarSlotObject.GetComponent<ColorPickerLocation>();
                colorPickerLocation._renderer = currentCarSlotMeshCreator.meshRenderer;
                InitializeColorPickerLocation(colorPickerLocation);
                foreach (Transform vertex in currentCarSlotObject.transform)
                {
                    VertexDragger dragger = vertex.gameObject.AddComponent<VertexDragger>();
                    dragger.Initialize(currentPolygon); 
                }

                await apiManager.GetVirtualMapLocationByIdAsync(currentLocationId);
                if (apiManager.locationById.capacity == 0)
                {
                    requiresCarToggle.isOn = false;
                }
                else
                {
                    requiresCarToggle.isOn = true;
                    capacityInput.text = apiManager.locationById.capacity.ToString();
                }

                PreviousUpdateLocationButton.SetActive(true);
                confirmUpdateLocationButton.SetActive(true);
                updateCarLocationPanel.SetActive(false);
                cameraSystem.ActiveControls();
                title.gameObject.SetActive(true);
                descriptionName.gameObject.SetActive(true);
                LocationDetails.gameObject.SetActive(true);
                descriptionName.text = newName;
                workshopRoof.ActiveRoof();
            }
            else
            {
                warningNameText.text = "Invalid Name";
            }
        }


        private void InitializeColorPickerLocation(ColorPickerLocation colorPickerLocation)
        {
            this.currentColorPickerLocation = colorPickerLocation;

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
            colorPicker.gameObject.SetActive(false); 
        }

        public async void ConfirmCarSlotUpdateLocation()
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
            warningNameText.text = "";
            Vector3 position = currentCarSlotObject.transform.position;
            float rotation = currentCarSlotObject.transform.eulerAngles.y;
            await apiManager.GetVirtualMapLocationByIdAsync(currentLocationId);
            var location = apiManager.locationById;
            ColorPickerLocation currentColorPickerLocation = currentCarSlotObject.GetComponent<ColorPickerLocation>();
            string color = currentColorPickerLocation.GetCurrentColor().ToHexString();
            List<VerticesCoordinates> verticesCoordinates = currentCarSlotMeshCreator.GetVertices();
            await apiManager.UpdateVirtualMapLocationAsync(currentCarSlotObject.info.id, currentCarSlotObject.name, position.x, position.y, position.z, location.activityIds, verticesCoordinates, "#" + color, capacityIntValue);

            currentCarSlotObject.SetDraggable(false);
            PreviousUpdateLocationButton.SetActive(false);
            confirmUpdateLocationButton.SetActive(false);
            uiManager.InteractableOn();
            cameraSystem.ActiveControls();
            title.gameObject.SetActive(false);
            descriptionName.gameObject.SetActive(false);
            LocationDetails.gameObject.SetActive(false);
            currentCarSlotObject.gameObject.GetComponent<Renderer>().material = carMaterial;
        }

        public async void ConfirmTasksOfCarSlot()
        {
            await apiManager.GetVirtualMapLocationByIdAsync(currentLocationId);
            var location = apiManager.locationById;
            List<string> activityIds = currentProcessesAndActivitiesPanel.GetComponent<ProcessesAndActivitiesList>().GetCheckedActivities();
            await apiManager.UpdateVirtualMapLocationAsync(location.id, location.name, location.coordinateX, location.coordinateY, location.coordinateZ, activityIds, location.vertices, location.color, location.capacity);
            confirmUpdateTasks.SetActive(false);
            currentProcessesAndActivitiesPanel.gameObject.SetActive(false);
            Destroy(currentProcessesAndActivitiesPanel.gameObject);
            uiManager.InteractableOn();
            title.gameObject.SetActive(false);
            descriptionName.gameObject.SetActive(false);
            exitUpdateTasks.SetActive(false);
            currentCarSlotObject.gameObject.GetComponent<Renderer>().material = carMaterial;
        }

        public void ExitUpdateTasks()
        {
            confirmUpdateTasks.SetActive(false);
            currentProcessesAndActivitiesPanel.gameObject.SetActive(false);
            Destroy(currentProcessesAndActivitiesPanel.gameObject);
            uiManager.InteractableOn();
            cameraSystem.ActiveControls();
            title.gameObject.SetActive(false);
            descriptionName.gameObject.SetActive(false);
            exitUpdateTasks.SetActive(false);
            foreach (GameObject carSlot in gameObjects)
            {
                currentCarSlotObject = carSlot.GetComponent<CarSlotObject>();
                currentCarSlotObject.GetComponent<Renderer>().material = carMaterial;
            }
        }

        private async void OnVirtualMapLocationUpdated()
        {
            PopulateLocations();
            await InstantiateLocations();
        }

        public void PreviousButton()
        {
            currentCarSlotObject.gameObject.GetComponent<Renderer>().material.color = Color.blue;
            currentCarSlotObject.SetDraggable(false);
            PreviousUpdateLocationButton.SetActive(false);
            confirmUpdateLocationButton.SetActive(false);
            updateCarLocationPanel.SetActive(true);
            cameraSystem.DesactiveControls();
            title.gameObject.SetActive(false);
            descriptionName.gameObject.SetActive(false);
            workshopRoof.DesactiveRoof();
            currentCarSlotObject.transform.position = oldPos;
            LocationDetails.gameObject.SetActive(false);
            currentPolygon.HideAllSpheres();
        }

        public void PreviousButtonGeral()
        {
            updateCarPanelGeral.SetActive(false);
            PreviousUpdateGeral.SetActive(false);
            uiManager.InteractableOn();
            currentCarSlotObject.gameObject.GetComponent<Renderer>().material = carMaterial;
            foreach (GameObject carSlot in gameObjects)
            {
                currentCarSlotObject = carSlot.GetComponent<CarSlotObject>();
                currentCarSlotObject.GetComponent<Renderer>().material = carMaterial;
            }
        }

        private void ToggleCarSlotVisibility(string locationId, bool value)
        {
            foreach (GameObject carSlot in gameObjects)
            {
                if (carSlot.gameObject.name == locationId)
                {
                    carSlot.gameObject.SetActive(value);
                }
            }

        }

        bool ValidateCapacity(string input)
        {
            // Clear any previous error messages
            errorMessage.text = "";

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

        public void HideFirstFloor()
        {
            foreach (GameObject area in firstFloorAreas)
            {
                area.gameObject.SetActive(false);
            }
        }

        public void ShowFirstFloor()
        {
            foreach (GameObject area in firstFloorAreas)
            {
                area.gameObject.SetActive(true);
            }
        }

    }
}
