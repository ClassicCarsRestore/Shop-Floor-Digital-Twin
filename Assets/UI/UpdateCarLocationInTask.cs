using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using API;
using Models;
using Objects;
using UnityEngine;
using UnityEngine.UI;
using Task = System.Threading.Tasks.Task;

namespace UI
{

    public class UpdateCarLocationInTask : MonoBehaviour
    {
        [SerializeField] private GameObject carSlotPrefab;
        [SerializeField] private GameObject locationMarkerPrefab;
        [SerializeField] private GameObject ActivityInfoPrefab;
        [SerializeField] public GameObject CarPrefab;
        [SerializeField] private GameObject ActivityLocationInfo;
        [SerializeField] public Text errorText;
        [SerializeField] private Button confirmLocationButton;
        [SerializeField] private Button exitGameObjectButton;

        private APIscript apiManager;
        private List<GameObject> carSlots;
        private List<GameObject> cars;
        private ActivityAndLocation currentActivityAndLocation;
        private string currentCaseInstanceId;
        private GameObject currentCar;
        private Transform Canvas;
        private GameObject currentActivityInfo;
        private GameObject currentLocationInfo;
        private UIManager uiManager;
        private List<Tuple<string, TaskDTO>> TasksAndLocs;
        private List<GameObject> locationMarkers;
        private HighlightAndSelection highlightSelectionCar;
        public event System.Action OnClose;
        private readonly List<string> occupiedLocs = new();
        private List<string> locationsIds;
        private Color originalColor;
        private List<GameObject> firstFloorAreas = new();
        private Roof workshopRoof;

        void Start()
        {
            apiManager = GameObject.Find("APIscript").GetComponent<APIscript>();
            Canvas = GameObject.Find("Canvas").GetComponent<Transform>();
            uiManager = GameObject.Find("UIManager").GetComponent<UIManager>();
            highlightSelectionCar = GameObject.Find("HighlightAndSelection").GetComponent<HighlightAndSelection>();
            carSlots = new List<GameObject>();
            errorText.gameObject.SetActive(false); // Hide the text
            cars = new List<GameObject>();
            TasksAndLocs = new List<Tuple<string, TaskDTO>>();
        }

        void Update()
        {
            if (currentCar == null || currentActivityInfo == null)
            {
                return;
            }
            CarObject car = currentCar.GetComponent<CarObject>();

            if (Input.GetKeyUp(KeyCode.Alpha1))
            {
                // FirstFloor.SetActive(!FirstFloor.activeSelf);
                // workshopRoof.FirstFloor.activeSelf
                if (!workshopRoof.FirstFloor.activeSelf)
                {
                    HideFirstFloor();
                }
                else
                {
                    ShowFirstFloor();
                }
            }

            if (Input.GetMouseButtonDown(0))
            {
                // Perform a raycast to check if a car slot was clicked
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                RaycastHit hit;

                if (Physics.Raycast(ray, out hit))
                {
                    GameObject clickedObject = hit.collider.gameObject;

                    // Check if the clicked object has the tag "carslot"
                    if (clickedObject.CompareTag("carSlot"))
                    {
                        HandleCarSlotClick(clickedObject);
                    }
                }
            }

            if (car.GetCarSlot() != null)
            {
                
                Text[] textComponent = currentActivityInfo.GetComponentsInChildren<Text>();
                CarSlotObject carLocation = car.GetCarSlot();
                VirtualMapLocation loc = carLocation.info;
                string locationText = "\nLocation associated: ";

                if (textComponent[2].text.Contains(locationText))
                {
                    int index = textComponent[2].text.IndexOf(locationText);
                    textComponent[2].text = textComponent[2].text.Remove(index);
                }
                locationText += loc.name;
                textComponent[2].text += locationText;

                LayoutRebuilder.ForceRebuildLayoutImmediate(currentActivityInfo.GetComponent<RectTransform>());
            }
            else
            {
               
                Text[] textComponent = currentActivityInfo.GetComponentsInChildren<Text>();
                string locationText = "\nLocation associated: ";

                // Remove the location text if it exists
                if (textComponent[2].text.Contains(locationText))
                {
                    int index = textComponent[2].text.IndexOf(locationText);
                    textComponent[2].text = textComponent[2].text.Remove(index);
                }
                LayoutRebuilder.ForceRebuildLayoutImmediate(currentActivityInfo.GetComponent<RectTransform>());

            }
        }

        private void StartVariables()
        {
            apiManager = GameObject.Find("APIscript").GetComponent<APIscript>();
            Canvas = GameObject.Find("Canvas").GetComponent<Transform>();
            uiManager = GameObject.Find("UIManager").GetComponent<UIManager>();
            carSlots = new List<GameObject>();
            errorText.gameObject.SetActive(false); 
            cars = new List<GameObject>();
            TasksAndLocs = new List<Tuple<string, TaskDTO>>();
            highlightSelectionCar = GameObject.Find("HighlightAndSelection").GetComponent<HighlightAndSelection>();
            highlightSelectionCar.SelectionOff();
            exitGameObjectButton.onClick.AddListener(Exit);
            confirmLocationButton.onClick.AddListener(ConfirmCarLocation);
            workshopRoof = GameObject.Find("oficina").GetComponent<Roof>();
            workshopRoof.RoofOff();
            ChangeCameraToTop();
        }

        public async void Setup(ActivityAndLocation activity, string caseInstanceId, GameObject car)
        {
            StartVariables();
            uiManager.InteractableOff();
            currentCaseInstanceId = caseInstanceId;
            currentActivityAndLocation = activity;
            if (car == null)
            {
                car = Instantiate(CarPrefab, new Vector3(0, 20, 0), transform.rotation);
            }
            car.GetComponent<CarObject>().SetDraggable(true);
            currentCar = car;
            await apiManager.GetTaskByIdAsync(currentActivityAndLocation.ActivityId);
            var currentTask = apiManager.task;
            await apiManager.GetListOfLocationWithActivityAsync(currentTask.activityId);
            locationsIds = apiManager.locationIds;
            await apiManager.GetCamundaActivityAsync(currentTask.processInstanceId, currentTask.activityId);
            var ct = apiManager.camundaTask;
            currentActivityInfo = Instantiate(ActivityInfoPrefab, Canvas);
            Text[] textComponent = currentActivityInfo.GetComponentsInChildren<Text>();
            textComponent[1].text = ct.Name;
            textComponent[2].text = "Started on: " + $"{currentTask.startTime: dd/MM/yyyy}" + "\n" + "Ended on: " + $"{currentTask.completionTime: dd/MM/yyyy}";
            InstatiatePossibleLocations();
            foreach (var locationId in locationsIds)
            {
                await GetTasksByLocation(locationId);
            }
            if (TasksAndLocs != null)
            {
                foreach (var locationId in locationsIds)
                {
                    // if (locationOccupied(taskAndLoc.Item2, currentTask.startTime, currentTask.completionTime))
                    bool isFull = await IsLocationFull(locationId, currentTask.startTime, currentTask.completionTime);
                    if (isFull)
                    {
                        occupiedLocs.Add(locationId);
                    }
                }

            }
            string occupiedLocsString = "Locations with full capacity: ";
            foreach (var locId in occupiedLocs)
            {
                occupiedLocsString += "\n";
                await apiManager.GetVirtualMapLocationByIdAsync(locId);
                occupiedLocsString += apiManager.locationById.name;
                locationsIds.Remove(locId);
            }
            string availableLocsString = "Locations available: ";
            foreach (var locId in locationsIds)
            {
                availableLocsString += "\n";
                await apiManager.GetVirtualMapLocationByIdAsync(locId);
                availableLocsString += apiManager.locationById.name;
            }
            currentLocationInfo = Instantiate(ActivityLocationInfo, Canvas);
            Text[] textComponentLocationInfo = currentLocationInfo.GetComponentsInChildren<Text>();
            if (occupiedLocs.Count == 0)
            {
                textComponentLocationInfo[1].text = availableLocsString;
            }
            else
            {
                textComponentLocationInfo[1].text = availableLocsString + "\n" + occupiedLocsString;
            }

        }

        private async Task InstatiatePossibleLocations()
        {
            DestroyPossibleLocations();
            foreach (var locationId in locationsIds)
            {
                await apiManager.GetVirtualMapLocationByIdAsync(locationId);
                var location = apiManager.locationById;
                GameObject carSlot = InstantiateLocation(location);

                carSlot.GetComponent<CarSlotObject>().info = location;
                carSlot.name = location.id;
                carSlots.Add(carSlot);
                if (location.coordinateY > 1)
                {
                    firstFloorAreas.Add(carSlot);
                }

                if (!workshopRoof.FirstFloor.active && firstFloorAreas.Count != 0)
                {
                    HideFirstFloor();
                }
            }

        }

        private GameObject GetCarSlotById(string locationId)
        {
            foreach (GameObject carSlot in carSlots)
            {
                CarSlotObject carSlotObject = carSlot.GetComponent<CarSlotObject>();

                if (carSlotObject != null && carSlotObject.info.id == locationId)
                {
                    return carSlot; // Return the car slot if the location ID matches
                }
            }

            return null; // Return null if no matching car slot is found
        }

        private GameObject InstantiateLocation(VirtualMapLocation location)
        {
            GameObject carSlot = Instantiate(carSlotPrefab, new Vector3(location.coordinateX, location.coordinateY, location.coordinateZ), transform.rotation);
            CreatePolygon area = carSlot.GetComponent<CreatePolygon>();

            Vector3[] spherePositions = new Vector3[4];
            for (int i = 0; i < location.vertices.Count; i++)
            {
                spherePositions[i].x = location.vertices[i].X;
                spherePositions[i].z = location.vertices[i].Z;
            }
            area.initialize(spherePositions);
            CarSlotMeshCreator currentCarSlotMeshCreator = carSlot.GetComponent<CarSlotMeshCreator>();

            area.HideAllSpheres();

            ColorPickerLocation colorLocation = carSlot.GetComponent<ColorPickerLocation>();
            colorLocation._renderer = currentCarSlotMeshCreator.meshRenderer;
            if (!string.IsNullOrEmpty(location.color))
            {
                colorLocation.SetColorFromHex(location.color);
            }

            return carSlot;
        }

        public void DestroyPossibleLocations()
        {
            foreach (GameObject carSlot in carSlots)
            {
                carSlot.SetActive(false);
                Destroy(carSlot);
            }
            carSlots.Clear();
        }

        public async void ConfirmCarLocation()
        {
            CarObject car = currentCar.GetComponent<CarObject>();
            if (car.GetCarSlot() != null)
            {
                CarSlotObject carLocation = car.GetCarSlot();
                VirtualMapLocation loc = carLocation.info;
                bool canConfirmLocation = true;
                if (occupiedLocs.Count > 0)
                {
                    foreach (var carSlotName in occupiedLocs)
                    {
                        if (carSlotName.Equals(loc.id))
                        {
                            canConfirmLocation = false;
                        }
                    }
                }

                if (canConfirmLocation)
                {
                    ActivityAndLocation updated = new();
                    updated.Id = currentActivityAndLocation.Id;
                    updated.ActivityId = currentActivityAndLocation.ActivityId;
                    updated.LocationId = loc.id;
                    await apiManager.UpdateActivityAndLocationAsync(currentCaseInstanceId, currentActivityAndLocation.Id, updated);
                    confirmLocationButton.gameObject.SetActive(false);
                    exitGameObjectButton.gameObject.SetActive(false);
                    DestroyPossibleLocations();
                    Destroy(currentActivityInfo);
                    Destroy(currentLocationInfo);
                    Destroy(currentCar);
                    uiManager.InteractableOn();
                    DestroyCarSlots();
                    DestroyCars();
                    TasksAndLocs.Clear();
                    highlightSelectionCar.SelectionOn();
                    OnClose?.Invoke(); // Trigger the OnClose event
                    Destroy(gameObject);// Trigger the OnClose event
                    return;
                }

            }

            StartCoroutine(DisplayTextForDuration());

        }

        public void Exit()
        {
            Destroy(currentActivityInfo);
            Destroy(currentLocationInfo);
            DestroyPossibleLocations();
            Destroy(currentCar);
            uiManager.InteractableOn();
            DestroyCarSlots();
            DestroyCars();
            highlightSelectionCar.SelectionOn();
            OnClose?.Invoke();
            Destroy(gameObject);// Trigger the OnClose event
        }

        private void DestroyCarSlots()
        {
            foreach (var item in carSlots)
            {
                item.gameObject.SetActive(false);
                Destroy(item);
            }
            carSlots.Clear();
        }
        private void DestroyCars()
        {
            foreach (var item in cars)
            {
                item.gameObject.SetActive(false);
                Destroy(item);
            }
            cars.Clear();
        }

        private async Task GetTasksByLocation(string locId)
        {
            await apiManager.GetActivityAndLocationHistoryAsync();
            var allCars = apiManager.activities;
            foreach (var activityHistoryByCar in allCars)
            {
                foreach (var activityAndLocation in activityHistoryByCar.History)
                {
                    if (activityAndLocation.LocationId == locId)
                    {
                        await apiManager.GetTaskByIdAsync(activityAndLocation.ActivityId);
                        var task = apiManager.task;
                        var tuple = new Tuple<string, TaskDTO>(locId, task);
                        TasksAndLocs.Add(tuple);
                    }
                }
            }
        }

        private async Task<bool> IsLocationFull(string locId, DateTime currentTaskStartTime, DateTime currentTaskEndTime)
        {
            await apiManager.GetVirtualMapLocationByIdAsync(locId);

            int maxCapacity = apiManager.locationById.capacity;

            if (maxCapacity != 0)
            {

                // Count how many cars are in the location during the specified time range
                int carCount = 0;

                foreach (var tuple in TasksAndLocs)
                {
                    string taskLocationId = tuple.Item1;
                    TaskDTO task = tuple.Item2;

                    if (taskLocationId == locId)
                    {

                        DateTime taskStartTime = task.startTime;
                        DateTime taskEndTime = task.completionTime;

                        // Check if the time ranges overlap (indicating that the car occupies the location)
                        if (taskStartTime < currentTaskEndTime && taskEndTime > currentTaskStartTime)
                        {
                       
                            carCount++;

                            // If the number of cars reaches the max capacity, the location is full
                            if (carCount >= maxCapacity)
                            {
                                GameObject carslot = GetCarSlotById(locId);
                                ColorPickerLocation colorLocation = carslot.GetComponent<ColorPickerLocation>();
                                CarSlotMeshCreator currentCarSlotMeshCreator = carslot.GetComponent<CarSlotMeshCreator>();
                                colorLocation._renderer = currentCarSlotMeshCreator.meshRenderer;
                                currentCarSlotMeshCreator.MeshColliderTriggerFalse();

                                colorLocation.SetColor(Color.black);

                                return true; // Location is full
                            }
                        }
                    }
                }
            }

            return false; // Location is not full
        }

        private IEnumerator DisplayTextForDuration()
        {
            errorText.gameObject.SetActive(true); // Show the text
            yield return new WaitForSeconds(5f); // Wait for the specified duration
            errorText.gameObject.SetActive(false); // Hide the text
        }

        private void ChangeCameraToCar(Vector3 newPos)
        {
            var cameraSystem = GameObject.Find("CameraSystem").GetComponent<CameraSystem>();

            if (cameraSystem != null)
            {
                cameraSystem.FocusOnCar(newPos);
            }

        }

        private void ChangeCameraToTop()
        {
            var cameraSystem = GameObject.Find("CameraSystem").GetComponent<CameraSystem>();

            if (cameraSystem != null)
            {
                cameraSystem.SwitchToTopCam();
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

        void OnMouseDown()
        {

            // Perform a raycast to check if a car slot was clicked
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit))
            {

                GameObject clickedObject = hit.collider.gameObject;

                // Check if the clicked object has the tag "carslot"
                if (clickedObject.CompareTag("carSlot"))
                {
                    HandleCarSlotClick(clickedObject);
                }
            }
        }

        private void HandleCarSlotClick(GameObject carSlot)
        {
            CarSlotObject carSlotObject = carSlot.GetComponent<CarSlotObject>();

            if (carSlotObject != null)
            {

                CarObject car = currentCar.GetComponent<CarObject>();
                car.carSlotOver = carSlotObject;
            }
        }

    }
}
