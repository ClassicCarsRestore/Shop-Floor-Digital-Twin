using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.ConstrainedExecution;
using System.Threading.Tasks;
using Models;
using TS.ColorPicker;
using UnityEngine;
using UnityEngine.UI;
using API;
using Objects;

namespace UI
{
    public class CarInfoDetailPanel : MonoBehaviour
    {
        [SerializeField] private Text carName;
        [SerializeField] private Text licensePlate;
        [SerializeField] private Text startDate;
        [SerializeField] private Text CurrentTask;
        [SerializeField] private Text Engine;
        [SerializeField] private Text Chassis;
        [SerializeField] private Text Status;
        [SerializeField] private Button PinterestURL;
        [SerializeField] private Button CharterOfTurinLink;
        [SerializeField] private Button colorPickerButton;
        [SerializeField] private Button closePage;
        [SerializeField] private Image carColor;
        [SerializeField] private Text carColorTextHex;
        [SerializeField] private ColorPicker colorPicker;
        [SerializeField] private Button TemplateListButton; // Reference to the template Text element
        [SerializeField] private Transform contentParent; // Reference to the content parent within ScrollView
        [SerializeField] private Sprite confirmedIcon; // Icon for activities with location
        [SerializeField] private Sprite notConfirmedIcon; // Icon for activities without location
        [SerializeField] private Dropdown carModelDropdown; // Reference to the Dropdown
        [SerializeField] private Button ProcessFlowButton;
        [SerializeField] private GameObject carTimeline;
        [SerializeField] private GameObject UpdateLocation;

        private ActivityAndLocationHistory activityAndLocationHistoryOfCar;
        private APIscript apiManager;
        private List<GameObject> gameObjectsCars;
        private GameObject carsList;
        private ColorPickerCar currentColorPickerCar;
        private GameObject currentCar;
        private readonly string Url = "http://194.210.120.34:5000/projects/details/";
        private Dictionary<int, string> carPrefabPaths;
        private UpdateCarLocationInTask UpdateCarLocation;
        private Car currentCarClass;
        private UIManager uiManager;
        private string currentCarTypeModel;

        // Start is called before the first frame update
        void Start()
        {
            closePage.onClick.AddListener(ClosePanel);
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

            carModelDropdown.onValueChanged.AddListener(delegate
            {
                OnCarModelChanged(carModelDropdown.value); // Assuming currentCar is a reference to the current car GameObject
            });

            ProcessFlowButton.onClick.AddListener(StartProcessFlow);
            carsList = GameObject.Find("CarsList");
            uiManager = GameObject.Find("UIManager").GetComponent<UIManager>();
        }

        public void SetUp(Car car, ColorPickerCar colorPickerCar, GameObject currentGameObjectCar)
        {
            apiManager = GameObject.Find("APIscript").GetComponent<APIscript>();
            gameObject.SetActive(false);
            currentCarClass = car;
            PopulateActivitiesButton(car);
            currentCar = currentGameObjectCar;
            carName.text = car.make + " " + car.model + " " + car.year;
            licensePlate.text = car.licencePlate;
            currentCarTypeModel = car.modelType;
            DateTime startDateParse;
            if (DateTime.TryParse(car.startDate, out startDateParse))
            {
                startDate.text = $"{startDateParse: dd/MM/yyyy HH:mm}";
            }
            Engine.text = car.engineNo;
            Chassis.text = car.chassisNo;
            if (car.isComplete)
            {
                Color red = new(100 / 255f, 0 / 255f, 0 / 255f);
                Status.color = red;
                Status.text = "Closed";
            }
            else
            {
                Color darkGreen = new(0 / 255f, 100 / 255f, 0 / 255f);
                Status.color = darkGreen;
                Status.text = "Active";
            }

            if (PinterestURL != null)
            {
                PinterestURL.onClick.AddListener(() => OpenURL(car.pinterestBoardUrl));
            }
            if (CharterOfTurinLink != null)
            {
                CharterOfTurinLink.onClick.AddListener(() => OpenURL(Url + car.id));
            }


            InitializeColorPickerCar(colorPickerCar);


            gameObjectsCars = GameObject.Find("ProjectsList").GetComponent<ProjectsList>().GetGameObjectsCar();
            /**

            if (currentCar != null && !string.IsNullOrEmpty(currentCar.name))
            {

                switch (currentCar.name)
                {
                    case string name when name.Contains("Car1"):
                        SelectDropdownValue(0);
                        break;
                    case string name when name.Contains("Car2"):
                        SelectDropdownValue(1);
                        break;
                    case string name when name.Contains("Car3"):
                        SelectDropdownValue(2);
                        break;
                    case string name when name.Contains("Car4"):
                        SelectDropdownValue(3);
                        break;
                    case string name when name.Contains("Car5"):
                        SelectDropdownValue(4);
                        break;
                    case string name when name.Contains("Car6"):
                        SelectDropdownValue(5);
                        break;
                    case string name when name.Contains("Car6"):
                        SelectDropdownValue(5);
                        break;
                    case string name when name.Contains("Car6"):
                        SelectDropdownValue(5);
                        break;
                    default:
                        SelectDropdownValue(1); // Optional: Handle other cases
                        break;
                }
            }
            **/

            gameObject.SetActive(true);

        }

        private void InitializeColorPickerCar(ColorPickerCar colorPickerCar)
        {
            this.currentColorPickerCar = colorPickerCar;

            bool allowColor = currentCarTypeModel.ToLower().Contains("carprefabs/car");

            if (currentCarClass.paintRecordNumber != null && currentCarClass.paintRecordNumber != "")
            {
                if (allowColor)
                {
                    currentColorPickerCar.SetColorFromHex(currentCarClass.paintRecordNumber);
                }
                carColorTextHex.text = currentCarClass.paintRecordNumber.ToString();
                ColorUtility.TryParseHtmlString(currentCarClass.paintRecordNumber, out Color color);
                carColor.color = color;
            }

            //carColor.color = currentColorPickerCar.GetCurrentColor();


            colorPicker.gameObject.SetActive(false);

            // Add listener to the color picker button
            colorPickerButton.onClick.AddListener(OpenColorPicker);

            // Set up color picker events
            colorPicker.OnChanged.AddListener(ColorPicker_OnChanged);
            colorPicker.OnSubmit.AddListener(ColorPicker_OnSubmit);
            colorPicker.OnCancel.AddListener(ColorPicker_OnCancel);

            if (apiManager.GetRole().Equals(APIscript.OWNER))
            {
                colorPickerButton.gameObject.SetActive(false);
            }
        }

        private void OpenColorPicker()
        {
            if (currentColorPickerCar != null)
            {
                colorPicker.Open(currentColorPickerCar.GetCurrentColor());
                colorPicker.gameObject.SetActive(true);
            }
        }

        private void ColorPicker_OnChanged(Color color)
        {
            if (currentColorPickerCar != null)
            {
                bool allowColor = currentCarTypeModel.ToLower().Contains("carprefabs/car");
                if (allowColor)
                {
                    currentColorPickerCar.SetColor(color);
                }

                carColor.color = color;
            }
        }

        private async void ColorPicker_OnSubmit(Color color)
        {
            Debug.Log(currentColorPickerCar != null);
            if (currentColorPickerCar != null)
            {
                bool allowColor = currentCarTypeModel.ToLower().Contains("carprefabs/car");
                Debug.Log(allowColor);
                if (allowColor)
                {
                    currentColorPickerCar.SetColor(color);
                }
                carColor.color = color;
                string hexColor = ColorUtility.ToHtmlStringRGBA(color);
                currentCarClass.paintRecordNumber = "#" + hexColor;
                carColorTextHex.text = currentCarClass.paintRecordNumber.ToString();
                await apiManager.UpdateCarAsync(currentCarClass);
            }
            colorPicker.gameObject.SetActive(false);
        }

        private void ColorPicker_OnCancel()
        {
            if (currentColorPickerCar != null)
            {
                currentColorPickerCar.ResetColor();
                carColor.color = currentColorPickerCar._originalColor;
            }
            colorPicker.gameObject.SetActive(false); // Hide the color picker after submission
        }

        private void OnDestroy()
        {
            // Remove listeners to avoid memory leaks
            colorPicker.OnChanged.RemoveListener(ColorPicker_OnChanged);
            colorPicker.OnSubmit.RemoveListener(ColorPicker_OnSubmit);
            colorPicker.OnCancel.RemoveListener(ColorPicker_OnCancel);
            colorPickerButton.onClick.RemoveListener(OpenColorPicker);
        }

        private void ClosePanel()
        {
            Destroy(gameObject);
        }

        void OpenURL(string url)
        {
            Application.OpenURL(url);
        }

        public async Task PopulateActivitiesButton(Car car)
        {
            ClearButtons();
            await apiManager.GetActivityAndLocationHistoryByCarAsync(car.caseInstanceId);
            activityAndLocationHistoryOfCar = apiManager.activityAndLocationByCar;
            foreach (var activity in activityAndLocationHistoryOfCar.History)
            {
                Button activityToAdd = Instantiate(TemplateListButton, contentParent);
                activityToAdd.gameObject.name = car.id;
                await apiManager.GetTaskByIdAsync(activity.ActivityId);
                var currentTask = apiManager.task;
                await apiManager.GetCamundaActivityAsync(currentTask.processInstanceId, currentTask.activityId);
                var camundaTask = apiManager.camundaTask;
                activityToAdd.transform.GetChild(0).GetComponent<Text>().text = camundaTask.Name;
                if (activity.LocationId != null)
                {
                    await apiManager.GetVirtualMapLocationByIdAsync(activity.LocationId);
                    activityToAdd.transform.GetChild(1).GetComponent<Text>().text = apiManager.locationById.name;
                    //ColorBlock colorBlock = activityToAdd.colors;
                    //colorBlock.highlightedColor = Color.green;
                    //colorBlock.normalColor = Color.green;
                    //colorBlock.pressedColor = Color.yellow;
                    //activityToAdd.colors = colorBlock;
                    activityToAdd.transform.GetChild(2).GetComponent<Text>().text = currentTask.startTime.ToString("dd/MM/yyyy");

                    activityToAdd.transform.GetChild(3).GetComponent<Text>().text = currentTask.completionTime.ToString("dd/MM/yyyy");

                    Button innerButton = activityToAdd.transform.Find("StatusButton").GetComponent<Button>();
                    Image iconImage = innerButton.transform.GetChild(0).GetComponent<Image>();
                    innerButton.colors = activityToAdd.colors;
                    if (activity.LocationId != null)
                    {
                        iconImage.sprite = confirmedIcon;
                    }
                    else
                    {
                        iconImage.sprite = notConfirmedIcon;
                    }

                    activityToAdd.gameObject.SetActive(true);
                }
                else if (apiManager.GetRole().Equals(APIscript.ADMIN) || apiManager.GetRole().Equals(APIscript.MANAGER))
                {
                    activityToAdd.transform.GetChild(1).GetComponent<Text>().text = "Awaiting Assignment";
                    ColorBlock colorBlock = activityToAdd.colors;
                    colorBlock.highlightedColor = Color.yellow;
                    colorBlock.normalColor = Color.yellow;
                    colorBlock.pressedColor = Color.yellow;
                    activityToAdd.colors = colorBlock;
                    activityToAdd.onClick.AddListener(() => OnAwaitingAssignmentButtonClick(activity, car.caseInstanceId));
                    activityToAdd.transform.GetChild(2).GetComponent<Text>().text = currentTask.startTime.ToString("dd/MM/yyyy");

                    activityToAdd.transform.GetChild(3).GetComponent<Text>().text = currentTask.completionTime.ToString("dd/MM/yyyy");

                    Button innerButton = activityToAdd.transform.Find("StatusButton").GetComponent<Button>();
                    Image iconImage = innerButton.transform.GetChild(0).GetComponent<Image>();
                    innerButton.colors = activityToAdd.colors;
                    if (activity.LocationId != null)
                    {
                        iconImage.sprite = confirmedIcon;
                    }
                    else
                    {
                        iconImage.sprite = notConfirmedIcon;
                    }

                    activityToAdd.gameObject.SetActive(true);
                }

            }
        }

        void ClearButtons()
        {
            foreach (Transform child in contentParent)
            {
                child.gameObject.SetActive(false);
            }
        }

        private void OnCarModelChanged(int index)
        {
            if (carPrefabPaths.TryGetValue(index, out string prefabPath))
            {
                GameObject newCarPrefab = Resources.Load<GameObject>(prefabPath);
                if (newCarPrefab != null)
                {

                    GameObject tempNewCar;
                    if (index == 3)
                    {
                        currentCar.transform.position = new Vector3(currentCar.transform.position.x, 23f, currentCar.transform.position.z);
                        tempNewCar = Instantiate(newCarPrefab, currentCar.transform.position, Quaternion.Euler(0, currentCar.transform.eulerAngles.y, 0));
                    }
                    else if (index == 4)
                    {
                        currentCar.transform.position = new Vector3(currentCar.transform.position.x, 20f, currentCar.transform.position.z);
                        tempNewCar = Instantiate(newCarPrefab, currentCar.transform.position, Quaternion.Euler(0, currentCar.transform.eulerAngles.y, 0));
                    }
                    else
                    {
                        tempNewCar = Instantiate(newCarPrefab);
                    }

                    // Copy components and properties to the current car
                    CopyComponents(tempNewCar, currentCar);
                    tempNewCar.GetComponent<CarObject>().carInfo = currentCar.GetComponent<CarObject>().carInfo;
                    tempNewCar.name = currentCar.GetComponent<CarObject>().carInfo.caseInstanceId + "_" + prefabPath;
                    currentCarTypeModel = prefabPath;
                    InitializeColorPickerCar(tempNewCar.GetComponent<ColorPickerCar>());

                    // Destroy the temporary new car
                    Destroy(currentCar);

                    gameObjectsCars.Remove(currentCar);
                    gameObjectsCars.Add(tempNewCar);

                    currentCar = tempNewCar;
                }
                else
                {
                    Debug.LogError("Prefab not found at path: " + prefabPath);
                }
            }
        }

        private void CopyComponents(GameObject target, GameObject source)
        {
            // Copy Transform properties
            target.transform.position = source.transform.position;
            target.transform.rotation = source.transform.rotation;
            target.transform.localScale = source.transform.localScale;

            // Copy other components (this is an example, modify based on your needs)
            var sourceRenderers = source.GetComponentsInChildren<Renderer>();
            var targetRenderers = target.GetComponentsInChildren<Renderer>();

            for (int i = 0; i < sourceRenderers.Length; i++)
            {
                if (i < targetRenderers.Length)
                {
                    targetRenderers[i].material = sourceRenderers[i].material;
                }
            }
        }

        private void SelectDropdownValue(int index)
        {
            if (carModelDropdown != null && index >= 0 && index < carModelDropdown.options.Count)
            {
                carModelDropdown.value = index;
                carModelDropdown.RefreshShownValue(); // Refresh the displayed value
            }
        }

        private void StartProcessFlow()
        {
            gameObject.SetActive(false);
            uiManager.CloseCarLocations();
            uiManager.InteractableOff();
            ProjectsList projectsList = GameObject.Find("ProjectsList").GetComponent<ProjectsList>();
            projectsList.HideOtherCars(currentCarClass.caseInstanceId);
            if (carsList != null)
            {
                carsList.gameObject.SetActive(false);
            }
            var t = GameObject.Find("Canvas").GetComponent<Transform>();
            var newTimeline = Instantiate(carTimeline, t);
            RectTransform rec = newTimeline.GetComponent<RectTransform>();
            rec.anchoredPosition = new Vector2(0, 360);
            newTimeline.GetComponent<CarTimelineController>().SetUpTimeline(activityAndLocationHistoryOfCar, currentCar);
            newTimeline.GetComponent<CarTimelineController>().OnClose += () =>
            {
                projectsList.refreshProjectsList();
                if (carsList != null)
                {
                    carsList.gameObject.SetActive(true);
                }
                uiManager.InteractableOn();
            };
        }

        private void HideOtherCars(string caseInstanceId)
        {
            List<GameObject> carsToRemove = new();

            foreach (var car in gameObjectsCars)
            {
                if (car.name != caseInstanceId)
                {
                    car.gameObject.SetActive(false);
                    carsToRemove.Add(car);
                }
            }

            foreach (var car in carsToRemove)
            {
                Destroy(car);
                gameObjectsCars.Remove(car);
            }
        }

        private void OnAwaitingAssignmentButtonClick(ActivityAndLocation activity, string caseInstanceId)
        {
            carsList.gameObject.SetActive(false);
            var c = GameObject.Find("Canvas").GetComponent<Transform>();
            GameObject UpdateCarLocationManager = Instantiate(UpdateLocation, c);
            ProjectsList projectsList = GameObject.Find("ProjectsList").GetComponent<ProjectsList>();
            projectsList.HideOtherCars(caseInstanceId);
            GameObject currentCarGameObject = null;
            foreach (var car in gameObjectsCars)
            {
                if (car.name.Contains(caseInstanceId))
                {
                    currentCarGameObject = car.gameObject;
                    currentCar = car;
                    break;
                }
            }
            uiManager.InteractableOff();
            uiManager.CloseCarLocations();
            UpdateCarLocationManager.GetComponent<UpdateCarLocationInTask>().Setup(activity, caseInstanceId, currentCarGameObject);
            gameObject.SetActive(false);
            UpdateCarLocationManager.GetComponent<UpdateCarLocationInTask>().OnClose += () =>
            {
                if (projectsList.currentInfoPanel != null)
                {
                    Destroy(projectsList.currentInfoPanel);
                    projectsList.currentInfoPanel = null; // Clear the reference
                }
                projectsList.refreshProjectsList();
                carsList.gameObject.SetActive(true);
                uiManager.InteractableOn();
            };
        }

    }
}