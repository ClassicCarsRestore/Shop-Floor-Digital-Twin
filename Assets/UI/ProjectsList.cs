using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.ConstrainedExecution;
using System.Threading.Tasks;
using API;
using Models;
using Objects;
using TS.ColorPicker;
using UI;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class ProjectsList : MonoBehaviour
{
    [SerializeField] private GameObject Panel;
    [SerializeField] Button TemplateProjectButton;
    [SerializeField] Button TemplateClosedProjectButton;
    [SerializeField] private Transform contentParent;
    [SerializeField] private Toggle toggleButton;
    [Header("API Manager")]
    public APIscript apiManager;
    [SerializeField] private GameObject prefabCar;
    [SerializeField] public Slider carsSlider;
    [SerializeField] public Slider projectsTypeSlider;
    [SerializeField] public Text projectsTitle;
    [SerializeField] private Text carsstatusText;
    [SerializeField] Button liveIconOn;
    [SerializeField] Button liveIconOff;
    [SerializeField] Button ProjectsButton;
    [SerializeField] Button ClosedProjectsButton;
    [SerializeField] Text NoClosedProjectsText;
    [SerializeField] private GameObject CarDetails;
    [SerializeField] private Transform canvas;
    [SerializeField] private Text pendingActionsText;

    public GameObject currentInfoPanel = null;
    private Car currentCarClass;
    public List<GameObject> gameObjectsCar;
    private Color normalColor = Color.white;
    private Color pressedColor = Color.red;
    private Color normalTextColor = Color.black;
    private Color pressedTextColor = Color.white;
    private bool OnlyOneCar = false;
    private readonly List<string> locationsOccupied = new();
    private Dictionary<string, List<Vector3>> occupiedPositions = new Dictionary<string, List<Vector3>>();
    private float minDistanceBetweenCars = 50f;
    private Dictionary<int, string> carPrefabPaths;

    // Start is called before the first frame update
    void Start()
    {
        gameObjectsCar = new List<GameObject>();

        if (apiManager == null)
        {
            Debug.LogError("APIManager not assigned to VirtualMapScrollView.");
            return;
        }
        //apiManager.ClosedProjectsReceived += OnClosedProjectsReceived;

        liveIconOn.gameObject.SetActive(false);
        liveIconOff.gameObject.SetActive(true);

        toggleButton.onValueChanged.AddListener(OnProjectsToggleValueChanged);
        toggleButton.isOn = false;
        OnProjectsToggleValueChanged(false);
        carsSlider.onValueChanged.AddListener(OnCarsSliderValueChanged);
        projectsTypeSlider.onValueChanged.AddListener(OnProjectsTypeSliderValueChanged);

        ProjectsButton.onClick.AddListener(() => OnButtonClicked(ProjectsButton));
        ClosedProjectsButton.onClick.AddListener(() => OnButtonClicked(ClosedProjectsButton));

        SetButtonColor(ProjectsButton, pressedColor, pressedTextColor);
        SetButtonColor(ClosedProjectsButton, normalColor, normalTextColor);

        carPrefabPaths = new Dictionary<int, string>()
        {
            { 0, "CarPrefabs/Car1" }, // Path relative to the Resources folder
            { 1, "CarPrefabs/Car2" },
            { 2, "CarPrefabs/Car3" },
            { 3, "CarPrefabs/Car4" },
            { 4, "CarPrefabs/Car5" },
            { 5, "CarPrefabs/Car6" },
            { 6, "CarPrefabs/carDino" },
            { 7, "CarPrefabs/DinoRed" },
            { 8, "CarPrefabs/landrover_defender" },
            { 9, "CarPrefabs/landrover_serie2" },
            { 10, "CarPrefabs/berlineta" },
        };
    }

    private void OnButtonClicked(Button clickedButton)
    {
        // Reset all buttons to normal color
        SetButtonColor(ProjectsButton, normalColor, normalTextColor);
        SetButtonColor(ClosedProjectsButton, normalColor, normalTextColor);

        // Set the clicked button to the pressed color
        SetButtonColor(clickedButton, pressedColor, pressedTextColor);
    }

    private void SetButtonColor(Button button, Color color, Color textColor)
    {
        ColorBlock colorBlock = button.colors;
        colorBlock.normalColor = color;
        colorBlock.selectedColor = color;
        colorBlock.highlightedColor = color;
        colorBlock.pressedColor = color;
        button.colors = colorBlock;

        Text buttonText = button.GetComponentInChildren<Text>();
        if (buttonText != null)
        {
            buttonText.color = textColor;
        }
    }

    async void OnCarsSliderValueChanged(float v)
    {
        if (v == 1.0f)
        {
            liveIconOn.gameObject.SetActive(true);
            liveIconOff.gameObject.SetActive(false);
            carsstatusText.text = "On";
            await GetAndPopulateProjectsOnWorkshop();
        }
        else
        {
            liveIconOn.gameObject.SetActive(false);
            liveIconOff.gameObject.SetActive(true);
            carsstatusText.text = "Off";
            DestroyCars();
            occupiedPositions.Clear();
        }
        if (currentInfoPanel != null)
        {
            Destroy(currentInfoPanel);
            currentInfoPanel = null;
        }
    }

    async void OnProjectsTypeSliderValueChanged(float v)
    {
        if (v == 1.0f)
        {
            ClosedProjectButtonClick();
            projectsTitle.text = "Closed Projects";
        }
        else
        {
            OpenProjectsButtonClick();
            projectsTitle.text = "Active Projects";
        }

    }

    async void OnProjectsToggleValueChanged(bool isOn)
    {
        // Check if the toggle is checked
        if (isOn)
        {
            // Get and populate locations from the API
            await GetAndPopulateProjectButtons();
            Panel.SetActive(true);
            SetButtonColor(ProjectsButton, pressedColor, pressedTextColor);
            SetButtonColor(ClosedProjectsButton, normalColor, normalTextColor);
        }
        else
        {
            ClearButtons();
            Panel.SetActive(false);
            if (OnlyOneCar)
            {
                DestroyCars();
                OnlyOneCar = false;
            }
            if (currentInfoPanel != null)
            {
                Destroy(currentInfoPanel);
                currentInfoPanel = null;
            }
            projectsTypeSlider.value = 0;
        }
    }

    void ClearButtons()
    {
        foreach (Transform child in contentParent)
        {
            child.gameObject.SetActive(false);
        }
    }

    public async Task GetAndPopulateProjectButtons()
    {
        ClearButtons();
        await apiManager.GetCarsAsync();
        foreach (var car in apiManager.cars)
        {
            Button newCar = Instantiate(TemplateProjectButton, contentParent);
            newCar.gameObject.name = car.id;
            newCar.transform.GetChild(0).GetComponent<Text>().text = car.make + " " + car.model + " " + car.year;
            newCar.transform.GetChild(1).GetComponent<Text>().text = car.licencePlate;
            int numberPendingActions = await PendingActions(car.caseInstanceId);

            if (apiManager.GetRole().Equals(APIscript.OWNER))
            {
                pendingActionsText.gameObject.SetActive(false);
                newCar.transform.GetChild(2).GetComponent<Text>().text = "";
            }
            else if (numberPendingActions != 0)
            {
                newCar.transform.GetChild(2).GetComponent<Text>().text = "Yes (" + numberPendingActions + ")";
            }
            else
            {
                newCar.transform.GetChild(2).GetComponent<Text>().text = "No";
            }

            newCar.onClick.AddListener(() => ShowCarInfo(car));
            newCar.gameObject.SetActive(true);
        }
    }

    public async void ShowCarInfo(Car car)
    {
        currentCarClass = car;

        if (currentInfoPanel != null)
        {
            Destroy(currentInfoPanel);
            currentInfoPanel = null; // Clear the reference
        }

        currentInfoPanel = Instantiate(CarDetails, canvas);
        RectTransform rec = currentInfoPanel.GetComponent<RectTransform>();
        rec.anchoredPosition = new Vector2(-600, -270);

        ColorPickerCar colorPicker = null;
        GameObject currentObject = null;

        if (OnlyOneCar)
        {
            DestroyCars();
            occupiedPositions.Clear();
            await InstatiateCar(car);
        }
        else if (gameObjectsCar.Count == 0)
        {
            await InstatiateCar(car);
            OnlyOneCar = true;
        }
        else if (!existGameObjectCar(car.caseInstanceId))
        {
            DestroyCars();
            occupiedPositions.Clear();
            await InstatiateCar(car);
        }

        foreach (GameObject carobject in gameObjectsCar)
        {
            if (carobject.name.Contains(car.caseInstanceId))
            {
                colorPicker = carobject.GetComponent<ColorPickerCar>();
                currentObject = carobject;
            }
        }
        currentInfoPanel.GetComponent<CarInfoDetailPanel>().SetUp(car, colorPicker, currentObject);
        var cameraSystem = GameObject.Find("CameraSystem").GetComponent<CameraSystem>();
        if (cameraSystem != null && currentObject != null)
        {
            cameraSystem.FocusOnCar(currentObject.transform.position);
        }
    }

    private bool existGameObjectCar(string caseInstanceId)
    {
        foreach (GameObject carobject in gameObjectsCar)
        {
            if (carobject.name.Equals(caseInstanceId))
            {
                return true;
            }
        }
        return false;
    }

    private async Task<int> PendingActions(string caseInstanceId)
    {
        int numberPendingActions = 0;
        await apiManager.GetActivityAndLocationHistoryByCarAsync(caseInstanceId);
        var activityAndLocationHistoryOfNewCar = apiManager.activityAndLocationByCar;
        foreach (var currentActivityAndLocation in activityAndLocationHistoryOfNewCar.History)
        {
            if (currentActivityAndLocation.LocationId == null)
            {
                numberPendingActions++;
            }
        }
        return numberPendingActions;
    }

    public void DestroyCars()
    {
        for (int i = gameObjectsCar.Count - 1; i >= 0; i--)
        {
            GameObject carClone = gameObjectsCar[i];
            if (carClone != null)
            {
                carClone.SetActive(false);
                Destroy(carClone);
            }
            gameObjectsCar.RemoveAt(i);
        }
        locationsOccupied.Clear();
    }

    public async Task GetAndPopulateProjectsOnWorkshop()
    {
        DestroyCars();
        occupiedPositions.Clear();
        await apiManager.GetCarsAsync();
        await InstantiateCars(apiManager.cars);

        OnlyOneCar = false;
    }

    private async Task InstatiateCar(Car car)
    {
        await apiManager.GetActivityAndLocationHistoryByCarAsync(car.caseInstanceId);
        var activityAndLocationHistoryOfNewCar = apiManager.activityAndLocationByCar;
        ActivityAndLocation lastActivityWithLocation = await FoundLastActivity(activityAndLocationHistoryOfNewCar);
        if (lastActivityWithLocation == null)
        {
            return;
        }
        await apiManager.GetVirtualMapLocationByIdAsync(lastActivityWithLocation.LocationId);
        GameObject newCarPrefab = null;
        int index = 0;

        if (car.caseInstanceId.ToLower().Contains("rese"))
        {
            index = 7;
            carPrefabPaths.TryGetValue(index, out string prefabPath);
            car.modelType = prefabPath;
            newCarPrefab = Resources.Load<GameObject>(prefabPath);
        }
        else if (car.caseInstanceId.ToLower().Contains("defender"))
        {
            index = 8;
            carPrefabPaths.TryGetValue(index, out string prefabPath);
            car.modelType = prefabPath;
            newCarPrefab = Resources.Load<GameObject>(prefabPath);
        }
        else if (car.caseInstanceId.ToLower().Contains("serie 2"))
        {
            index = 9;
            carPrefabPaths.TryGetValue(index, out string prefabPath);
            car.modelType = prefabPath;
            newCarPrefab = Resources.Load<GameObject>(prefabPath);
        }
        else if (car.caseInstanceId.ToLower().Contains("berlinetta"))
        {
            index = 10;
            carPrefabPaths.TryGetValue(index, out string prefabPath);
            car.modelType = prefabPath;
            newCarPrefab = Resources.Load<GameObject>(prefabPath);
        }
        else
        {
            index = 2;
            carPrefabPaths.TryGetValue(index, out string prefabPath);
            car.modelType = prefabPath;
            newCarPrefab = Resources.Load<GameObject>(prefabPath);
        }
        
        GameObject carClone = InstantiateRandomCarInSlot(newCarPrefab, new Vector3(apiManager.locationById.coordinateX, apiManager.locationById.coordinateY, apiManager.locationById.coordinateZ), apiManager.locationById.vertices, 80, 80, apiManager.locationById.id);
        Collider carCollider = carClone.GetComponent<Collider>();
        Bounds carBounds = carCollider.bounds;

        if (ShouldRotateCar(new Vector3(apiManager.locationById.coordinateX, apiManager.locationById.coordinateY, apiManager.locationById.coordinateZ), carBounds, apiManager.locationById.vertices))
        {
            Quaternion currentRotation = carClone.transform.rotation;
            Quaternion additionalRotation = Quaternion.Euler(0, 90, 0);
            Quaternion targetRotation = currentRotation * additionalRotation;

            carClone.transform.rotation = targetRotation;
            Debug.Log(carClone.transform.rotation);
        }

        carClone.GetComponent<CarObject>().carInfo = car;
        carClone.name = car.caseInstanceId;
        ColorPickerCar colorCar = carClone.GetComponent<ColorPickerCar>();
        
        if (!string.IsNullOrEmpty(car.paintRecordNumber) && index < 7 )
        {
            colorCar.SetColorFromHex(car.paintRecordNumber);   // TODO
        }
        
        gameObjectsCar.Add(carClone);
        
    }

    bool ShouldRotateCar(Vector3 carSlotPosition, Bounds carBounds, List<VerticesCoordinates> slotVertices)
    {

        // Initialize min and max slot boundaries in world space
        Vector3 minSlot = new Vector3(float.MaxValue, 0, float.MaxValue);
        Vector3 maxSlot = new Vector3(float.MinValue, 0, float.MinValue);

        // Calculate the min and max points of the car slot area in world space
        foreach (VerticesCoordinates vertex in slotVertices)
        {
            Vector3 worldPoint = carSlotPosition + new Vector3(vertex.X, 0, vertex.Z);
            minSlot.x = Mathf.Min(minSlot.x, worldPoint.x);
            minSlot.z = Mathf.Min(minSlot.z, worldPoint.z);
            maxSlot.x = Mathf.Max(maxSlot.x, worldPoint.x);
            maxSlot.z = Mathf.Max(maxSlot.z, worldPoint.z);
        }

        // Slot dimensions
        float slotWidth = maxSlot.x - minSlot.x;
        float slotLength = maxSlot.z - minSlot.z;

        // Car dimensions (current orientation)
        float carWidth = carBounds.size.x;
        float carLength = carBounds.size.z;

        Debug.Log("carWidth " + carWidth);
        Debug.Log("carLength " + carLength);

        // Car dimensions if rotated by 90 degrees
        float rotatedCarWidth = carLength;
        float rotatedCarLength = carWidth;

        // Check if the car fits better without rotation
        bool fitsWithoutRotation = (carWidth <= slotWidth && carLength <= slotLength);

        // Check if the car fits better with rotation
        bool fitsWithRotation = (rotatedCarWidth <= slotWidth && rotatedCarLength <= slotLength);

        // Decide whether to rotate the car or not
        return fitsWithRotation && !fitsWithoutRotation; // Rotate if it fits better with rotation
    }

    public GameObject InstantiateRandomCarInSlot(GameObject newprefabcar, Vector3 carSlotPosition, List<VerticesCoordinates> verticesCoordinates, float carLength, float carWidth, string carSlotId)
    {
        // Instantiate the car at the origin first
        GameObject newCar;
        if (newprefabcar.name.ToLower().Contains("car"))
        {
             newCar = Instantiate(newprefabcar, Vector3.zero, Quaternion.identity);
        }
        else
        {
             newCar = Instantiate(newprefabcar, Vector3.zero, Quaternion.Euler(0, 90, 0));
        }

        // Initialize min and max slot boundaries in world space
        Vector3 minSlot = new Vector3(float.MaxValue, 0, float.MaxValue);
        Vector3 maxSlot = new Vector3(float.MinValue, 0, float.MinValue);

        List<Vector3> worldVertices = new List<Vector3>();

        // Calculate the min and max points of the car slot area in world space
        foreach (VerticesCoordinates vertex in verticesCoordinates)
        {
            Vector3 worldPoint = carSlotPosition + new Vector3(vertex.X, 0, vertex.Z);
            worldVertices.Add(worldPoint);

            minSlot.x = Mathf.Min(minSlot.x, worldPoint.x);
            minSlot.z = Mathf.Min(minSlot.z, worldPoint.z);
            maxSlot.x = Mathf.Max(maxSlot.x, worldPoint.x);
            maxSlot.z = Mathf.Max(maxSlot.z, worldPoint.z);
        }

        // Calculate the half extents of the car based on its dimensions
        float halfCarLength = carLength / 2;
        float halfCarWidth = carWidth / 2;

        Vector3 randomPosition;
        bool validPosition = false;
        int maxAttempts = 10; // Limit the number of attempts to find a non-overlapping position
        int attempts = 0;

        do
        {
            // Generate random X and Z within the slot's boundaries, ensuring the car fits
            float randomX = UnityEngine.Random.Range(minSlot.x + halfCarWidth, maxSlot.x - halfCarWidth);
            float randomZ = UnityEngine.Random.Range(minSlot.z + halfCarLength, maxSlot.z - halfCarLength);

            randomPosition = new Vector3(randomX, 20, randomZ);

            // Check if the random position is too close to any already occupied positions
            validPosition = !IsPositionOccupied(randomPosition, carSlotId);

            attempts++;

        } while (!validPosition && attempts < maxAttempts);

        if (validPosition)
        {
            // Move the instantiated car to the calculated random position
            newCar.transform.position = randomPosition;

            // Add the new position to the list of occupied positions for this slot
            if (!occupiedPositions.ContainsKey(carSlotId))
            {
                occupiedPositions[carSlotId] = new List<Vector3>();
            }

            occupiedPositions[carSlotId].Add(randomPosition);
        }
        else
        {
            Debug.LogWarning("Could not find a valid position for the car.");
        }

        return newCar;
    }

    private bool IsPositionOccupied(Vector3 position, string carSlotId)
    {
        if (!occupiedPositions.ContainsKey(carSlotId)) return false;

        foreach (Vector3 occupiedPos in occupiedPositions[carSlotId])
        {
            if (Vector3.Distance(position, occupiedPos) < minDistanceBetweenCars)
            {
                return true;
            }
        }

        return false;
    }
    private async Task InstantiateCars(IEnumerable<Car> cars)
    {
        List<Task> tasks = new();
        foreach (var car in cars)
        {
            tasks.Add(InstatiateCar(car));
        }
        await Task.WhenAll(tasks);
    }

    private async Task<ActivityAndLocation> FoundLastActivity(ActivityAndLocationHistory activities)
    {
        ActivityAndLocation lastActivityWithLocation = null;
        foreach (var activity in activities.History)
        {
            if (activity.LocationId != null)
            {
                string locId = activity.LocationId;
                await apiManager.GetVirtualMapLocationByIdAsync(locId);
                var location = apiManager.locationById;
                if (location.capacity != 0)
                {
                    lastActivityWithLocation = activity;
                }
            }
        }
        return lastActivityWithLocation;
    }

    public async void OpenProjectsButtonClick()
    {
        await GetAndPopulateProjectButtons();
    }

    public async void ClosedProjectButtonClick()
    {
        //await PopulateClosedCarsButtons();
        ClearButtons();
        await OnClosedProjectsReceived();
    }

    public async Task PopulateClosedCarsButtons()
    {
        ClearButtons();
        await apiManager.GetClosedProjectsAsync();

    }

    private async Task OnClosedProjectsReceived()
    {
        await apiManager.GetClosedProjectsAsync();
        var closedProjects = apiManager.closedProjects;
        if (closedProjects.Count == 0)
        {
            NoClosedProjectsText.gameObject.SetActive(true);
        }
        else
        {
            foreach (var closedProject in closedProjects)
            {
                Button newClosedProject = Instantiate(TemplateClosedProjectButton, contentParent);
                newClosedProject.gameObject.name = closedProject.id;
                newClosedProject.transform.GetChild(0).GetComponent<Text>().text = closedProject.make + " " + closedProject.model + " " + closedProject.year;
                newClosedProject.transform.GetChild(1).GetComponent<Text>().text = closedProject.licencePlate;
                int numberPendingActions = await PendingActions(closedProject.caseInstanceId);

                if (apiManager.GetRole().Equals(APIscript.OWNER))
                {
                    pendingActionsText.gameObject.SetActive(false);
                    newClosedProject.transform.GetChild(2).GetComponent<Text>().text = "";
                }
                else if (numberPendingActions != 0)
                {
                    newClosedProject.transform.GetChild(2).GetComponent<Text>().text = "Yes (" + numberPendingActions + ")";
                }
                else
                {
                    newClosedProject.transform.GetChild(2).GetComponent<Text>().text = "No";
                }
                newClosedProject.onClick.AddListener(() => ShowClosedCarInfo(closedProject));
                newClosedProject.gameObject.SetActive(true);
            }
        }
    }

    public async void ShowClosedCarInfo(Car car)
    {
        liveIconOn.gameObject.SetActive(false);
        liveIconOff.gameObject.SetActive(true);
        carsstatusText.text = "Off";
        DestroyCars();
        occupiedPositions.Clear(); currentCarClass = car;

        if (currentInfoPanel != null)
        {
            Destroy(currentInfoPanel);
            currentInfoPanel = null; // Clear the reference
        }

        currentInfoPanel = Instantiate(CarDetails, canvas);
        RectTransform rec = currentInfoPanel.GetComponent<RectTransform>();
        rec.anchoredPosition = new Vector2(-600, -270);

        ColorPickerCar colorPicker = null;
        GameObject currentObject = null;

        await InstatiateCar(car);
        OnlyOneCar = true;

        foreach (GameObject carobject in gameObjectsCar)
        {
            Debug.Log(carobject.name);
            if (carobject.name.Contains(car.caseInstanceId))
            {
                Debug.Log("entrei");
                colorPicker = carobject.GetComponent<ColorPickerCar>();
                currentObject = carobject;
            }
        }

        currentInfoPanel.GetComponent<CarInfoDetailPanel>().SetUp(car, colorPicker, currentObject);
        var cameraSystem = GameObject.Find("CameraSystem").GetComponent<CameraSystem>();
        if (cameraSystem != null && currentObject != null)
        {
            cameraSystem.FocusOnCar(currentObject.transform.position);
        }
    }

    public async Task refreshProjectsList()
    {
        if (!OnlyOneCar)
        {
            await GetAndPopulateProjectsOnWorkshop();
        }
        await GetAndPopulateProjectButtons();

        if (currentCarClass != null)
        {
            ShowCarInfo(currentCarClass);
        }
    }

    public List<GameObject> GetGameObjectsCar()
    {
        return gameObjectsCar;
    }

    public void HideOtherCars(string caseInstanceId)
    {
        List<GameObject> carsToRemove = new();

        foreach (var car in gameObjectsCar)
        {
            if (!car.name.Contains(caseInstanceId))
            {
                car.gameObject.SetActive(false);
                carsToRemove.Add(car);
            }
        }

        foreach (var car in carsToRemove)
        {
            Destroy(car);
            gameObjectsCar.Remove(car);
        }
    }

    public void Initialize()
    {
        ClearButtons();
        DestroyCars();
        OnlyOneCar = false;
        currentCarClass = null;
        NoClosedProjectsText.gameObject.SetActive(false);

        if (currentInfoPanel != null)
        {
            Destroy(currentInfoPanel);
            currentInfoPanel = null;
        }

        // Set up UI elements to their default states
        liveIconOn.gameObject.SetActive(false);
        liveIconOff.gameObject.SetActive(true);
        carsstatusText.text = "Off";

        // Reset any other relevant states
        toggleButton.isOn = false;
        SetButtonColor(ProjectsButton, normalColor, normalTextColor);
        SetButtonColor(ClosedProjectsButton, normalColor, normalTextColor);
    }


}
