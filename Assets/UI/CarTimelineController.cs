using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ConstrainedExecution;
using System.Threading.Tasks;
using API;
using Models;
using Objects;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Timeline;
using UnityEngine.UI;
using Task = System.Threading.Tasks.Task;

namespace UI
{

    public class CarTimelineController : MonoBehaviour
    {
        [SerializeField] private Slider timelineSlider; // Reference to the slider
        [SerializeField] private GameObject markerPrefab; // Prefab for the timeline markers
        [SerializeField] private RectTransform sliderFillArea;
        [SerializeField] private Text Title;
        [SerializeField] private Button Next;
        [SerializeField] private Button Previous;
        [SerializeField] private Button Last;
        [SerializeField] private Button First;
        [SerializeField] private Button closeButton; // Button to close the timeline
        [SerializeField] private Text ActivityInfo;
        [SerializeField] private Text ActivityNumber;
        [SerializeField] private Text ActivityStatus;
        [SerializeField] private Button PinterestUrl;
        [SerializeField] private RectTransform panelRectTransform; // Reference to the panel's RectTransform
        [SerializeField] private GameObject yellow;
        [SerializeField] private GameObject darkYellow;
        [SerializeField] private GameObject red;
        [SerializeField] private GameObject redMarkerPrefab; // Prefab for the timeline markers
        [SerializeField] private GameObject greenMarkerPrefab; // Prefab for the timeline markers
        [SerializeField] private LineRenderer lineRenderer;
        [SerializeField] private GameObject carMarkerPrefab; // Prefab for the activity markers
        [SerializeField] private GameObject locationMarkerPrefab; // Prefab for the activity markers
        [SerializeField] private GameObject locationPrefab;

        private GameObject currentHover = null;
        [SerializeField] private GameObject hover;

        public event System.Action OnClose; // Event triggered when the timeline is closed

        private ActivityAndLocationHistory activityAndLocationHistory; // This should be set with the car's data
        private APIscript apiManager;
        private GameObject currentCar;
        private int currentActivityIndex = 0;
        private Dictionary<int, (float startPos, float endPos)> taskPositions = new();
        private HighlightAndSelection highlightSelectionCar;
        public Roof workshopRoof;
        private Dictionary<int, GameObject> carInstances = new();
        private Dictionary<string, GameObject> carInstancesByLocation = new();
        private bool topCamera = true;
        private List<GameObject> carSlots = new();
        private List<GameObject> firstFloorAreas = new();
        private float carWidth;
        private float carLength;


        private void Update()
        {

            if (Input.GetKeyUp(KeyCode.Alpha1))
            {
                if (!workshopRoof.FirstFloor.activeSelf)
                {
                    HideFirstFloor();
                }
                else
                {
                    ShowFirstFloor();
                }
            }
        }

        public async void SetUpTimeline(ActivityAndLocationHistory history, GameObject car)
        {
            apiManager = GameObject.Find("APIscript").GetComponent<APIscript>();
            highlightSelectionCar = GameObject.Find("HighlightAndSelection").GetComponent<HighlightAndSelection>();
            highlightSelectionCar.SelectionOff();
            currentCar = car;

            Collider carCollider = car.GetComponent<Collider>();
            Bounds carBounds = carCollider.bounds;
            carWidth = carBounds.size.x;
            carLength = carBounds.size.z;
        
            currentCar.gameObject.SetActive(false);
            activityAndLocationHistory = EliminateActivitiesWithNoLocation(history);
            workshopRoof = GameObject.Find("oficina").GetComponent<Roof>();
            workshopRoof.RoofOff();
            Car carInfo = currentCar.GetComponent<CarObject>().carInfo;
            Title.text = "Process Flow of " + carInfo.make + " " + carInfo.model + " " + carInfo.year;
            timelineSlider.maxValue = 1800f;
            timelineSlider.minValue = 0f;
            await PopulateTimeline();
            timelineSlider.onValueChanged.AddListener(OnSliderValueChanged);
            Next.onClick.AddListener(MoveToNextActivity);
            Previous.onClick.AddListener(MoveToPreviousActivity);
            Last.onClick.AddListener(MoveToLastActivity);
            First.onClick.AddListener(MoveToFirstActivity);
            closeButton.onClick.AddListener(OnCloseButtonClicked);
            ChangeCameraToTop();
            //MoveCarToActivity(0);
            //OnSliderValueChanged(0);
            FocusOnCar(0, 0);
            StartCoroutine(ForceUpdateLayout());
        }

        private async Task PopulateTimeline()
        {
            float sliderWidth = 1800;
            float maxGapDuration = 2 * 86400f; // 2 days in seconds
            float oneDayWidth = sliderWidth / 2; // Width representing one day
            float maxGapWidth = 2 * oneDayWidth; // Maximum gap width in slider units

            Dictionary<(DateTime start, DateTime end), List<TaskDTO>> tasksByTime = new();

            // Group tasks by their start and end times
            foreach (var activity in activityAndLocationHistory.History)
            {
                await apiManager.GetTaskByIdAsync(activity.ActivityId);
                var currentTask = apiManager.task;
                var key = (currentTask.startTime.Date, currentTask.completionTime.Date);

                if (!tasksByTime.ContainsKey(key))
                {
                    tasksByTime[key] = new List<TaskDTO>();
                }
                tasksByTime[key].Add(currentTask);
            }

            Debug.Log("Original tasksByTime:");
            foreach (var kvp in tasksByTime)
            {
                Debug.Log($"Start: {kvp.Key.start}, End: {kvp.Key.end}, Count: {kvp.Value.Count}");
            }

            // Sort tasks by start time
            var sortedTasks = tasksByTime.Keys.OrderBy(x => x.start).ToList();
            Dictionary<(DateTime start, DateTime end), List<TaskDTO>> adjustedTasksByTime = new();

            DateTime startDate = sortedTasks.First().start; // Initialize startDate from the earliest task start
            DateTime lastEndTime = startDate;
            DateTime oldGroupEndTime = lastEndTime;
            foreach (var taskTimeFrame in sortedTasks)
            {
                var (groupStartTime, groupEndTime) = taskTimeFrame;
                List<TaskDTO> tasks = tasksByTime[taskTimeFrame];
                DateTime oldGroupStartTime = groupStartTime;

                var gapBetweenTaskTimeFrames = oldGroupStartTime - oldGroupEndTime;
                if (gapBetweenTaskTimeFrames.TotalSeconds <= 86400f)
                {
                    groupStartTime = lastEndTime;
                }
                else if (gapBetweenTaskTimeFrames.TotalSeconds <= maxGapDuration) // Continuous if within a day
                {
                    groupStartTime = lastEndTime.AddSeconds(maxGapDuration);
                }// Ensure that no task starts more than 2 days after the previous task ends
                else if (gapBetweenTaskTimeFrames.TotalSeconds > maxGapDuration)
                {
                    // Adjust the start time to be no more than 2 days after the last task end
                    groupStartTime = lastEndTime.AddSeconds(maxGapDuration);
                }

                oldGroupEndTime = groupEndTime;
                var gap = groupEndTime - oldGroupStartTime;
                // Adjust the end time if it goes beyond the new start time
                if (gap.TotalSeconds > maxGapDuration)
                {
                    groupEndTime = groupStartTime.AddSeconds(maxGapDuration);
                }
                else
                {
                    groupEndTime = groupStartTime.AddSeconds(gap.TotalSeconds);
                }

                if (groupStartTime == groupEndTime)
                {
                    // Minimum width for zero-duration tasks
                    groupEndTime = groupStartTime.AddSeconds(86400f);
                }

                // Update the adjusted tasks dictionary
                if (!adjustedTasksByTime.ContainsKey((groupStartTime, groupEndTime)))
                {
                    adjustedTasksByTime[(groupStartTime, groupEndTime)] = new List<TaskDTO>();
                }
                adjustedTasksByTime[(groupStartTime, groupEndTime)].AddRange(tasks);

                // Update the last end time to the end time of the current task
                lastEndTime = groupEndTime;
            }

            Debug.Log("Adjusted tasksByTime:");
            foreach (var kvp in adjustedTasksByTime)
            {
                Debug.Log($"Start: {kvp.Key.start}, End: {kvp.Key.end}, Count: {kvp.Value.Count}");
            }

            DateTime endDate = adjustedTasksByTime.Last().Key.end;

            // Prepare for rendering the timeline
            int taskIndex = 0;
            oneDayWidth = (86400f / (float)(endDate - startDate).TotalSeconds) * sliderWidth;

            // Iterate through adjusted tasks and populate timeline
            float lastEndPos = -1;
            bool first = true;

            foreach (var taskGroup in adjustedTasksByTime)
            {
                DateTime groupStartTime = taskGroup.Key.start;
                DateTime groupEndTime = taskGroup.Key.end;
                List<TaskDTO> tasks = taskGroup.Value;

                float totalWidth = (float)(groupEndTime - groupStartTime).TotalSeconds / (float)(endDate - startDate).TotalSeconds * sliderWidth;
                totalWidth = Math.Max(totalWidth, oneDayWidth); // Ensure at least one day width
                float individualWidth = totalWidth / tasks.Count;

                float startPos = (float)(groupStartTime - startDate).TotalSeconds / (float)(endDate - startDate).TotalSeconds * sliderWidth;

                // Adjust start position if there is a gap larger than 2 days
                if (lastEndPos != -1 && startPos > lastEndPos + maxGapWidth)
                {
                    startPos = lastEndPos + maxGapWidth;
                }
                else if (startPos < lastEndPos)
                {
                    startPos = lastEndPos;
                }

                lastEndPos = startPos;

                foreach (var currentTask in tasks)
                {
                    float taskStartPos = lastEndPos;
                    float taskEndPos = taskStartPos + individualWidth;
                    if (taskEndPos > sliderWidth)
                    {
                        taskEndPos = sliderWidth;
                        individualWidth = taskEndPos - taskStartPos;
                    }

                    if ((taskStartPos == lastEndPos) && !first)
                    {
                        taskStartPos += 10f; // Add padding to avoid overlap
                        taskEndPos = taskStartPos + individualWidth; // Adjust end position
                    }
                    first = false;

                    if (individualWidth > 0)
                    {
                        GameObject middleMarker;
                        if (string.IsNullOrEmpty(currentTask.boardSectionUrl)) 
                        {
                             middleMarker = Instantiate(yellow, sliderFillArea);
                        }
                        else
                        {
                             middleMarker = Instantiate(darkYellow, sliderFillArea);
                        }

                        middleMarker.GetComponent<RectTransform>().anchoredPosition = new Vector2(-900 + taskStartPos, 0);
                        middleMarker.GetComponent<RectTransform>().sizeDelta = new Vector2(individualWidth, sliderFillArea.rect.height);
                    }

                    GameObject startMarker = Instantiate(greenMarkerPrefab, sliderFillArea);
                    startMarker.GetComponent<RectTransform>().anchoredPosition = new Vector2(-900 + taskStartPos, 0);

                    GameObject endMarker = Instantiate(redMarkerPrefab, sliderFillArea);
                    endMarker.GetComponent<RectTransform>().anchoredPosition = new Vector2(-900 + taskEndPos, 0);

                    // Add tooltip for start time on start marker
                    EventTrigger startTrigger = startMarker.GetComponent<EventTrigger>() ?? startMarker.AddComponent<EventTrigger>();
                    EventTrigger.Entry pointerEnterStart = new() { eventID = EventTriggerType.PointerEnter };
                    pointerEnterStart.callback.AddListener((data) => { ShowTooltip($"{currentTask.startTime:dd/MM/yyyy}"); });
                    startTrigger.triggers.Add(pointerEnterStart);

                    EventTrigger.Entry pointerExitStart = new() { eventID = EventTriggerType.PointerExit };
                    pointerExitStart.callback.AddListener((data) => { HideTooltip(); });
                    startTrigger.triggers.Add(pointerExitStart);

                    // Add tooltip for completion time on end marker
                    EventTrigger endTrigger = endMarker.GetComponent<EventTrigger>() ?? endMarker.AddComponent<EventTrigger>();
                    EventTrigger.Entry pointerEnterEnd = new() { eventID = EventTriggerType.PointerEnter };
                    pointerEnterEnd.callback.AddListener((data) => { ShowTooltip($"{currentTask.completionTime:dd/MM/yyyy}"); });
                    endTrigger.triggers.Add(pointerEnterEnd);

                    EventTrigger.Entry pointerExitEnd = new() { eventID = EventTriggerType.PointerExit };
                    pointerExitEnd.callback.AddListener((data) => { HideTooltip(); });
                    endTrigger.triggers.Add(pointerExitEnd);

                    var activity = activityAndLocationHistory.History[taskIndex];
                    await apiManager.GetVirtualMapLocationByIdAsync(activity.LocationId);
                    if (!carInstancesByLocation.ContainsKey(apiManager.locationById.id))
                    {
                        GameObject carInstance = null;
                        GameObject carSlot = InstantiateLocation(apiManager.locationById);
                        if (apiManager.locationById.capacity != 0)
                        {
                            carInstance = InstantiateRandomCarInSlot(new Vector3(apiManager.locationById.coordinateX, apiManager.locationById.coordinateY, apiManager.locationById.coordinateZ), apiManager.locationById.vertices, 80, 80);
                            Collider carCollider = carInstance.GetComponent<Collider>();
                            Bounds carBounds = carCollider.bounds;

                            if (ShouldRotateCar(carSlot, carBounds, apiManager.locationById.vertices))
                            {
                                Quaternion currentRotation = carInstance.transform.rotation;
                                Quaternion additionalRotation = Quaternion.Euler(0, 90, 0);
                                Quaternion targetRotation = currentRotation * additionalRotation;
                                carInstance.transform.rotation = targetRotation;
                            }

                            carInstance.SetActive(false);
                            carSlot.name = apiManager.locationById.name;
                            carInstance.name = activityAndLocationHistory.CaseInstanceId;
                        }
                        else
                        {
                            carInstance = carSlot;
                        }

                        carInstancesByLocation[apiManager.locationById.id] = carInstance;
                        carSlots.Add(carSlot);
                    }
                    carInstances[taskIndex] = carInstancesByLocation[apiManager.locationById.id];

                    lastEndPos = taskEndPos;
                    taskPositions[taskIndex] = (taskStartPos, taskEndPos);

                    // Store the task positions
                    taskIndex++;
                }
            }
        }

        
        bool ShouldRotateCar(GameObject carSlot, Bounds carBounds, List<VerticesCoordinates> slotVertices)
        {

            CarSlotObject carSlotOver = carSlot.gameObject.GetComponent<CarSlotObject>();

            // Get the car slot's transform
            Transform carSlotTransform = carSlotOver.transform;

            // Initialize min and max slot boundaries in world space
            Vector3 minSlot = new Vector3(float.MaxValue, 0, float.MaxValue);
            Vector3 maxSlot = new Vector3(float.MinValue, 0, float.MinValue);

            // Calculate the min and max points of the car slot area in world space
            foreach (VerticesCoordinates vertex in slotVertices)
            {
                Vector3 worldPoint = carSlotTransform.TransformPoint(new Vector3(vertex.X, 0, vertex.Z));
                minSlot.x = Mathf.Min(minSlot.x, worldPoint.x);
                minSlot.z = Mathf.Min(minSlot.z, worldPoint.z);
                maxSlot.x = Mathf.Max(maxSlot.x, worldPoint.x);
                maxSlot.z = Mathf.Max(maxSlot.z, worldPoint.z);
            }

            // Slot dimensions
            float slotWidth = maxSlot.x - minSlot.x;
            float slotLength = maxSlot.z - minSlot.z;

            // Car dimensions (current orientation)
            //float carWidth = carBounds.size.x;
            //float carLength = carBounds.size.z;

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

        
        public GameObject InstantiateRandomCarInSlot(Vector3 carSlotPosition, List<VerticesCoordinates> verticesCoordinates, float carLength, float carWidth)
        {

            Car carInfo = currentCar.GetComponent<CarObject>().carInfo;
            GameObject newCar;
            if (carInfo.modelType.ToLower().Contains("carprefabs/car"))
            {
                newCar = Instantiate(currentCar, Vector3.zero, Quaternion.identity);
            }
            else
            {
                newCar = Instantiate(currentCar, Vector3.zero, Quaternion.Euler(0, 90, 0));
            }


            // Instantiate the car at the origin first
           // GameObject newCar = Instantiate(currentCar, Vector3.zero, Quaternion.identity);

            // Initialize min and max slot boundaries in world space
            Vector3 minSlot = new Vector3(float.MaxValue, 0, float.MaxValue);
            Vector3 maxSlot = new Vector3(float.MinValue, 0, float.MinValue);

            List<Vector3> worldVertices = new List<Vector3>();

            // Calculate the min and max points of the car slot area in world space
            foreach (VerticesCoordinates vertex in verticesCoordinates)
            {
                // Convert local vertex position to world position using the carSlotPosition
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

            /**
            Debug.Log(minSlot.x + halfCarWidth);
            Debug.Log(maxSlot.x - halfCarWidth);
            Debug.Log(minSlot.z + halfCarLength);
            Debug.Log(maxSlot.z - halfCarLength);
            **/

            // Generate random X and Z within the slot's boundaries, ensuring the car fits
            float randomX = UnityEngine.Random.Range(minSlot.x + halfCarWidth, maxSlot.x - halfCarWidth);
            float randomZ = UnityEngine.Random.Range(minSlot.z + halfCarLength, maxSlot.z - halfCarLength);

            // Assign the calculated random position, maintaining the car at ground level or specific Y position (e.g., 20)
            Vector3 randomPosition = new Vector3(randomX, 20, randomZ);

            // Calculate orientation based on the first two vertices
            Vector3 edge = worldVertices[1] - worldVertices[0];
            float angle = Mathf.Atan2(edge.z, edge.x) * Mathf.Rad2Deg;

            // Move the instantiated car to the calculated random position
            newCar.transform.position = randomPosition;

            Collider carCollider = newCar.GetComponent<Collider>();
            Bounds carBounds = carCollider.bounds;

            return newCar;
        }

        private GameObject InstantiateLocation(VirtualMapLocation location)
        {
            GameObject carSlot = Instantiate(locationPrefab, new Vector3(location.coordinateX, location.coordinateY, location.coordinateZ), transform.rotation);
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

            if (location.coordinateY > 1)
            {
                firstFloorAreas.Add(carSlot);
            }

            if (!workshopRoof.FirstFloor.active && firstFloorAreas.Count != 0)
            {
                HideFirstFloor();
            }

            return carSlot;

        }

        private void OnSliderValueChanged(float value)
        {
            int nearestIndex = -1;
            Debug.Log("Slider value: " + value);
            bool found = false;

            // Iterate through task positions and check where the value falls
            foreach (var taskPosition in taskPositions)
            {
                float startPos = taskPosition.Value.startPos;
                float endPos = taskPosition.Value.endPos;

                Debug.Log("Checking task index: " + taskPosition.Key + " with startPos: " + startPos + " and endPos: " + endPos);

                if (value >= startPos && value <= endPos)
                {
                    nearestIndex = taskPosition.Key;
                    Debug.Log("Value " + value + " is between " + startPos + " and " + endPos + " for task index: " + taskPosition.Key);
                    found = true;
                    break;
                }
            }

            // If a valid task range is found, update the car's position
            if (nearestIndex != -1 && nearestIndex != currentActivityIndex)
            {
                Debug.Log("Updating to task index: " + nearestIndex);
                var OldIndex = currentActivityIndex;
                currentActivityIndex = nearestIndex;
                FocusOnCar(currentActivityIndex, OldIndex);
            }
            else if (!found)
            {
                currentActivityIndex = -1;
                currentCar.gameObject.SetActive(false);
                PinterestUrl.gameObject.SetActive(false);
                Debug.Log("No activity and no location");
                ActivityNumber.text = "No Activity";
                ActivityInfo.text = "During this gap there´s no tasks and locations associated with the car.";
                ActivityStatus.text = "";
                StartCoroutine(ForceUpdateLayout());
            }
        }

        private void FocusOnCar(int newIndex, int oldIndex)
        {
            if (oldIndex != -1)
            {
                carInstances[oldIndex].gameObject.SetActive(false);
            }
            else
            {
                for (int i = 0; i < taskPositions.Count; i++)
                {
                    carInstances[i].gameObject.SetActive(false);
                }
            }
            if (carInstances.TryGetValue(newIndex, out GameObject carInstance))
            {
                carInstance.gameObject.SetActive(true);
                Vector3 carPos = carInstance.transform.position;
                ChangeCameraToCar(carPos);
                var activity = activityAndLocationHistory.History[newIndex];
                UpdateTaskInfo(activity.ActivityId, newIndex);
                StartCoroutine(ForceUpdateLayout());
            }
        }

        private async void UpdateTaskInfo(string activityId, int index)
        {
            await apiManager.GetTaskByIdAsync(activityId);
            var currentTask = apiManager.task;
            await apiManager.GetCamundaActivityAsync(currentTask.processInstanceId, currentTask.activityId);
            var camundaTask = apiManager.camundaTask;
            string locId = activityAndLocationHistory.History[index].LocationId;
            await apiManager.GetVirtualMapLocationByIdAsync(locId);
            string task = camundaTask.Name;
            string loc = apiManager.locationById.name;
            ActivityNumber.text = "Activity Nº" + (index + 1);
            ActivityInfo.text = task + "\n" + "Location: " + loc + "\n" + "Started on: " + $"{currentTask.startTime: dd/MM/yyyy}" + "\n";
            ActivityInfo.text += "Completed on: " + $"{currentTask.completionTime: dd/MM/yyyy}";

            if (currentTask.boardSectionUrl != null)
            {
                PinterestUrl.gameObject.SetActive(true);
                PinterestUrl.onClick.AddListener(() => OpenURL(currentTask.boardSectionUrl));
            }
            else
            {
                PinterestUrl.gameObject.SetActive(false);
            }
            StartCoroutine(ForceUpdateLayout());
        }

        private void OnCloseButtonClicked()
        {
            highlightSelectionCar.SelectionOn();
            DestroyAllCars();
            DestroyCarSlots();
            firstFloorAreas.Clear();
            OnClose?.Invoke(); // Trigger the OnClose event
            Destroy(gameObject); // Destroy the timeline game object
        }

        private void DestroyCarSlots()
        {
            foreach (var carslot in carSlots)
            {
                Destroy(carslot);
            }
            carSlots.Clear();
        }

        public void DestroyAllCars()
        {
            foreach (var carInstance in carInstancesByLocation.Values)
            {
                Destroy(carInstance);
            }
            carInstancesByLocation.Clear();
        }

        private void MoveToNextActivity()
        {
            int nextIndex = currentActivityIndex + 1;
            if (nextIndex < activityAndLocationHistory.History.Count)
            {
                timelineSlider.value = taskPositions[nextIndex].startPos;
            }
        }

        private void MoveToPreviousActivity()
        {
            int prevIndex = currentActivityIndex - 1;
            if (prevIndex >= 0)
            {
                timelineSlider.value = taskPositions[prevIndex].startPos;
            }
        }

        private void MoveToLastActivity()
        {
            int lastIndex = activityAndLocationHistory.History.Count - 1;
            if (lastIndex >= 0)
            {
                timelineSlider.value = taskPositions[lastIndex].startPos;
            }
        }

        private void MoveToFirstActivity()
        {
            if (activityAndLocationHistory.History.Count > 0)
            {
                timelineSlider.value = taskPositions[0].startPos;
            }
        }

        void OpenURL(string url)
        {
            Application.OpenURL(url);
        }

        private ActivityAndLocationHistory EliminateActivitiesWithNoLocation(ActivityAndLocationHistory activities)
        {
            var filteredActivities = new List<ActivityAndLocation>();

            foreach (var act in activities.History)
            {
                if (act.LocationId != null)
                {
                    filteredActivities.Add(act);
                }
            }

            activities.History = filteredActivities;
            return activities;
        }


        private IEnumerator ForceUpdateLayout()
        {
            yield return new WaitForEndOfFrame();
            LayoutRebuilder.ForceRebuildLayoutImmediate(panelRectTransform);
        }

        private void ShowTooltip(string text)
        {
            if (currentHover == null)
            {
                var c = GameObject.Find("Canvas").GetComponent<Transform>();
                currentHover = Instantiate(hover, c);
            }

            if (text != null)
            {
                Vector3 Position = Input.mousePosition;
                Position.y += 30;
                Position.x += 30;

                currentHover.transform.position = Position;
                currentHover.GetComponent<CarHover>().SetUp(text);
            }
        }

        private void HideTooltip()
        {
            if (currentHover != null)
            {
                Destroy(currentHover);
            }
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

    }
}
