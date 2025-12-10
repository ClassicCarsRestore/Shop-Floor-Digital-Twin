using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API;
using Models;
using Objects;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Task = System.Threading.Tasks.Task;
using ColorUtility = UnityEngine.ColorUtility;

namespace UI
{
    public class CarTimelineController : MonoBehaviour
    {
        [SerializeField] private Slider timelineSlider;
        [SerializeField] private GameObject markerPrefab;
        [SerializeField] private RectTransform sliderFillArea;
        [SerializeField] private Text Title;
        [SerializeField] private Button Next;
        [SerializeField] private Button Previous;
        [SerializeField] private Button Last;
        [SerializeField] private Button First;
        [SerializeField] private Button closeButton;
        [SerializeField] private Text ActivityInfo;
        [SerializeField] private Text ActivityNumber;
        [SerializeField] private Text ActivityStatus;
        [SerializeField] private Button PinterestUrl;
        [SerializeField] private RectTransform panelRectTransform;

        [SerializeField] private GameObject yellow;
        [SerializeField] private GameObject darkYellow;
        [SerializeField] private GameObject red;
        [SerializeField] private GameObject redMarkerPrefab;
        [SerializeField] private GameObject greenMarkerPrefab;
        [SerializeField] private LineRenderer lineRenderer;
        [SerializeField] private GameObject carMarkerPrefab;
        [SerializeField] private GameObject locationMarkerPrefab;
        [SerializeField] private GameObject locationPrefab;

        // Números no chão
        [SerializeField] private StepMarkerManager markerManager;

        // LEGEND
        [SerializeField] private GameObject legendPanelRoot;   // GO do painel (fica INATIVO por defeito)
        [SerializeField] private LegendManager legendManager;    // componente do painel
        private bool legendStarted = false;                      // já abrimos a 1ª linha?
        private bool legendVisible = false;                      // painel ativo?

        // mapas runtime
        private readonly Dictionary<string, GameObject> slotByLocationId = new();
        private readonly Dictionary<int, TaskDTO> taskByIndex = new();
        private readonly Dictionary<int, VirtualMapLocation> locByIndex = new();

        private GameObject currentHover = null;
        [SerializeField] private GameObject hover;

        public event System.Action OnClose;

        private ActivityAndLocationHistory activityAndLocationHistory;
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
                if (!workshopRoof.FirstFloor.activeSelf) HideFirstFloor();
                else ShowFirstFloor();
            }
        }

        // ---------- helpers legenda ----------
        private void EnsureLegendRefs()
        {
            if (legendManager == null || legendPanelRoot == null)
            {
                if (legendPanelRoot == null)
                {
                    var go = GameObject.Find("LegendPanel");
                    if (go) legendPanelRoot = go;
                }
                if (legendPanelRoot && legendManager == null)
                    legendManager = legendPanelRoot.GetComponent<LegendManager>();
            }
        }

   

        private bool TryEnsureLegend()
        {
            if (legendManager != null) return true;

            if (legendPanelRoot == null)
            {
                Debug.LogError("[Legend] LegendPanelRoot está NULL no CarTimelineController.");
                return false;
            }

          
            var canvas = GameObject.Find("Canvas");
            if (canvas == null)
            {
                Debug.LogError("[Legend] Canvas não encontrado na cena.");
                return false;
            }

            var inst = Instantiate(legendPanelRoot, canvas.transform); 
            inst.name = "LegendPanel (Runtime)";
            legendManager = inst.GetComponentInChildren<LegendManager>(true);
            if (legendManager == null)
            {
                Debug.LogError("[Legend] LegendManager não existe dentro do prefab LegendPanel.");
                Destroy(inst);
                return false;
            }

            var rt = inst.GetComponent<RectTransform>();
            if (rt != null)
            {
              
                rt.localScale = Vector3.one;
                rt.gameObject.SetActive(true);
            }

            Debug.Log("[Legend] Painel instanciado.");
            return true;
        }


        private void ActivateLegend()
        {
            EnsureLegendRefs();
            if (legendPanelRoot && !legendPanelRoot.activeSelf)
            {
                legendPanelRoot.SetActive(true);
            }
            legendVisible = legendPanelRoot && legendPanelRoot.activeSelf;
        }

        private void DeactivateLegend()
        {
            if (legendPanelRoot) legendPanelRoot.SetActive(false);
            legendVisible = false;
        }

        // ---------- HELPER ----------
        private void RebuildVisualsUpTo(int targetIndex)
        {
            markerManager?.ClearAll();
            TryEnsureLegend();
            legendManager?.ResetSession();
            legendStarted = false;

            if (targetIndex < 0)
            {
                DeactivateLegend();
                return;
            }

            ActivateLegend();

            if (locByIndex.TryGetValue(0, out var firstLoc) && taskByIndex.TryGetValue(0, out var firstTask))
            {
                legendManager?.BeginStay(0, firstLoc.id, firstLoc.name, firstTask.startTime);

                if (slotByLocationId.TryGetValue(firstLoc.id, out var firstSlot) && firstSlot != null)
                    markerManager.DropNumberAtLocation(0, firstLoc, firstSlot.transform);

                legendStarted = true;
            }

            for (int i = 1; i <= targetIndex; i++)
            {
                var prevLoc = locByIndex[i - 1];
                var curLoc = locByIndex[i];
                if (prevLoc.id == curLoc.id) continue;

                var prevTask = taskByIndex[i - 1];
                legendManager?.EndStay(prevTask.completionTime);

                var curTask = taskByIndex[i];
                legendManager?.BeginStay(i, curLoc.id, curLoc.name, curTask.startTime);

                if (slotByLocationId.TryGetValue(curLoc.id, out var curSlot) && curSlot != null)
                    markerManager.DropNumberAtLocation(i, curLoc, curSlot.transform);
            }
        }

        // -------------------------------------

        public async void SetUpTimeline(ActivityAndLocationHistory history, GameObject car)
        {
            apiManager = GameObject.Find("APIscript").GetComponent<APIscript>();
            markerManager = GameObject.Find("StepMarkers")?.GetComponent<StepMarkerManager>();
            EnsureLegendRefs();             
            DeactivateLegend();              

        
            markerManager?.ClearAll();
            slotByLocationId.Clear();
            taskByIndex.Clear();
            locByIndex.Clear();
            TryEnsureLegend();
            legendManager?.ResetSession();
            legendStarted = false;

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

            // listeners
            timelineSlider.onValueChanged.AddListener(OnSliderValueChanged);
            Next.onClick.AddListener(MoveToNextActivity);
            Previous.onClick.AddListener(MoveToPreviousActivity);
            Last.onClick.AddListener(MoveToLastActivity);
            First.onClick.AddListener(MoveToFirstActivity);
            closeButton.onClick.AddListener(OnCloseButtonClicked);

            ChangeCameraToTop();
            FocusOnCar(0, 0);

            StartCoroutine(ForceUpdateLayout());
        }

        private async Task PopulateTimeline()
        {
            float sliderWidth = 1800;
            float maxGapDuration = 2 * 86400f; // 2 days
            float oneDayWidth = sliderWidth / 2;
            float maxGapWidth = 2 * oneDayWidth;

            Dictionary<(DateTime start, DateTime end), List<TaskDTO>> tasksByTime = new();

            foreach (var activity in activityAndLocationHistory.History)
            {
                await apiManager.GetTaskByIdAsync(activity.ActivityId);
                var currentTask = apiManager.task;
                var key = (currentTask.startTime.Date, currentTask.completionTime.Date);
                if (!tasksByTime.ContainsKey(key)) tasksByTime[key] = new List<TaskDTO>();
                tasksByTime[key].Add(currentTask);
            }

            var sortedTasks = tasksByTime.Keys.OrderBy(x => x.start).ToList();
            Dictionary<(DateTime start, DateTime end), List<TaskDTO>> adjustedTasksByTime = new();

            DateTime startDate = sortedTasks.First().start;
            DateTime lastEndTime = startDate;
            DateTime oldGroupEndTime = lastEndTime;

            foreach (var taskTimeFrame in sortedTasks)
            {
                var (groupStartTime, groupEndTime) = taskTimeFrame;
                List<TaskDTO> tasks = tasksByTime[taskTimeFrame];
                DateTime oldGroupStartTime = groupStartTime;

                var gapBetweenTaskTimeFrames = oldGroupStartTime - oldGroupEndTime;
                if (gapBetweenTaskTimeFrames.TotalSeconds <= 86400f)
                    groupStartTime = lastEndTime;
                else if (gapBetweenTaskTimeFrames.TotalSeconds <= maxGapDuration)
                    groupStartTime = lastEndTime.AddSeconds(maxGapDuration);
                else if (gapBetweenTaskTimeFrames.TotalSeconds > maxGapDuration)
                    groupStartTime = lastEndTime.AddSeconds(maxGapDuration);

                oldGroupEndTime = groupEndTime;
                var gap = groupEndTime - oldGroupStartTime;
                if (gap.TotalSeconds > maxGapDuration)
                    groupEndTime = groupStartTime.AddSeconds(maxGapDuration);
                else
                    groupEndTime = groupStartTime.AddSeconds(gap.TotalSeconds);

                if (groupStartTime == groupEndTime)
                    groupEndTime = groupStartTime.AddSeconds(86400f);

                if (!adjustedTasksByTime.ContainsKey((groupStartTime, groupEndTime)))
                    adjustedTasksByTime[(groupStartTime, groupEndTime)] = new List<TaskDTO>();
                adjustedTasksByTime[(groupStartTime, groupEndTime)].AddRange(tasks);

                lastEndTime = groupEndTime;
            }

            DateTime endDate = adjustedTasksByTime.Last().Key.end;

            int taskIndex = 0;
            oneDayWidth = (86400f / (float)(endDate - startDate).TotalSeconds) * sliderWidth;

            float lastEndPos = -1;
            bool first = true;

            foreach (var taskGroup in adjustedTasksByTime)
            {
                DateTime groupStartTime = taskGroup.Key.start;
                DateTime groupEndTime = taskGroup.Key.end;
                List<TaskDTO> tasks = taskGroup.Value;

                float totalWidth = (float)(groupEndTime - groupStartTime).TotalSeconds / (float)(endDate - startDate).TotalSeconds * sliderWidth;
                totalWidth = Math.Max(totalWidth, oneDayWidth);
                float individualWidth = totalWidth / tasks.Count;

                float startPos = (float)(groupStartTime - startDate).TotalSeconds / (float)(endDate - startDate).TotalSeconds * sliderWidth;

                if (lastEndPos != -1 && startPos > lastEndPos + maxGapWidth) startPos = lastEndPos + maxGapWidth;
                else if (startPos < lastEndPos) startPos = lastEndPos;

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
                        taskStartPos += 10f;
                        taskEndPos = taskStartPos + individualWidth;
                    }
                    first = false;

                    if (individualWidth > 0)
                    {
                        GameObject middleMarker = string.IsNullOrEmpty(currentTask.boardSectionUrl)
                            ? Instantiate(yellow, sliderFillArea)
                            : Instantiate(darkYellow, sliderFillArea);

                        middleMarker.GetComponent<RectTransform>().anchoredPosition = new Vector2(-900 + taskStartPos, 0);
                        middleMarker.GetComponent<RectTransform>().sizeDelta = new Vector2(individualWidth, sliderFillArea.rect.height);
                    }

                    GameObject startMarker = Instantiate(greenMarkerPrefab, sliderFillArea);
                    startMarker.GetComponent<RectTransform>().anchoredPosition = new Vector2(-900 + taskStartPos, 0);

                    GameObject endMarker = Instantiate(redMarkerPrefab, sliderFillArea);
                    endMarker.GetComponent<RectTransform>().anchoredPosition = new Vector2(-900 + taskEndPos, 0);

                    // tooltips
                    EventTrigger startTrigger = startMarker.GetComponent<EventTrigger>() ?? startMarker.AddComponent<EventTrigger>();
                    var peS = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
                    peS.callback.AddListener((_) => { ShowTooltip($"{currentTask.startTime:dd/MM/yyyy}"); });
                    startTrigger.triggers.Add(peS);
                    var pxS = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
                    pxS.callback.AddListener((_) => { HideTooltip(); });
                    startTrigger.triggers.Add(pxS);

                    EventTrigger endTrigger = endMarker.GetComponent<EventTrigger>() ?? endMarker.AddComponent<EventTrigger>();
                    var peE = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
                    peE.callback.AddListener((_) => { ShowTooltip($"{currentTask.completionTime:dd/MM/yyyy}"); });
                    endTrigger.triggers.Add(peE);
                    var pxE = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
                    pxE.callback.AddListener((_) => { HideTooltip(); });
                    endTrigger.triggers.Add(pxE);

                    var activity = activityAndLocationHistory.History[taskIndex];
                    await apiManager.GetVirtualMapLocationByIdAsync(activity.LocationId);
                    if (!carInstancesByLocation.ContainsKey(apiManager.locationById.id))
                    {
                        GameObject carInstance = null;
                        GameObject carSlot = InstantiateLocation(apiManager.locationById);

                        // map slot para drop dos números
                        slotByLocationId[apiManager.locationById.id] = carSlot;

                        if (apiManager.locationById.capacity != 0)
                        {
                            carInstance = InstantiateRandomCarInSlot(
                                new Vector3(apiManager.locationById.coordinateX, apiManager.locationById.coordinateY, apiManager.locationById.coordinateZ),
                                apiManager.locationById.vertices, 80, 80);

                            Collider ccol = carInstance.GetComponent<Collider>();
                            Bounds cb = ccol.bounds;

                            if (ShouldRotateCar(carSlot, cb, apiManager.locationById.vertices))
                            {
                                Quaternion currentRotation = carInstance.transform.rotation;
                                Quaternion additionalRotation = Quaternion.Euler(0, 90, 0);
                                carInstance.transform.rotation = currentRotation * additionalRotation;
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

                    taskByIndex[taskIndex] = currentTask;
                    locByIndex[taskIndex] = apiManager.locationById;

                    taskIndex++;
                }
            }
        }

        bool ShouldRotateCar(GameObject carSlot, Bounds carBounds, List<VerticesCoordinates> slotVertices)
        {
            CarSlotObject carSlotOver = carSlot.gameObject.GetComponent<CarSlotObject>();
            Transform carSlotTransform = carSlotOver.transform;

            Vector3 minSlot = new Vector3(float.MaxValue, 0, float.MaxValue);
            Vector3 maxSlot = new Vector3(float.MinValue, 0, float.MinValue);

            foreach (VerticesCoordinates vertex in slotVertices)
            {
                Vector3 worldPoint = carSlotTransform.TransformPoint(new Vector3(vertex.X, 0, vertex.Z));
                minSlot.x = Mathf.Min(minSlot.x, worldPoint.x);
                minSlot.z = Mathf.Min(minSlot.z, worldPoint.z);
                maxSlot.x = Mathf.Max(maxSlot.x, worldPoint.x);
                maxSlot.z = Mathf.Max(maxSlot.z, worldPoint.z);
            }

            float slotWidth = maxSlot.x - minSlot.x;
            float slotLength = maxSlot.z - minSlot.z;

            float rotatedCarWidth = carLength;
            float rotatedCarLength = carWidth;

            bool fitsWithoutRotation = (carWidth <= slotWidth && carLength <= slotLength);
            bool fitsWithRotation = (rotatedCarWidth <= slotWidth && rotatedCarLength <= slotLength);

            return fitsWithRotation && !fitsWithoutRotation;
        }

        public GameObject InstantiateRandomCarInSlot(Vector3 carSlotPosition, List<VerticesCoordinates> verticesCoordinates, float carLength, float carWidth)
        {
            Car carInfo = currentCar.GetComponent<CarObject>().carInfo;
            GameObject newCar = carInfo.modelType.ToLower().Contains("carprefabs/car")
                ? Instantiate(currentCar, Vector3.zero, Quaternion.identity)
                : Instantiate(currentCar, Vector3.zero, Quaternion.Euler(0, 90, 0));

            Vector3 minSlot = new Vector3(float.MaxValue, 0, float.MaxValue);
            Vector3 maxSlot = new Vector3(float.MinValue, 0, float.MinValue);

            List<Vector3> worldVertices = new List<Vector3>();

            foreach (VerticesCoordinates vertex in verticesCoordinates)
            {
                Vector3 worldPoint = carSlotPosition + new Vector3(vertex.X, 0, vertex.Z);
                worldVertices.Add(worldPoint);
                minSlot.x = Mathf.Min(minSlot.x, worldPoint.x);
                minSlot.z = Mathf.Min(minSlot.z, worldPoint.z);
                maxSlot.x = Mathf.Max(maxSlot.x, worldPoint.x);
                maxSlot.z = Mathf.Max(maxSlot.z, worldPoint.z);
            }

            float halfCarLength = carLength / 2;
            float halfCarWidth = carWidth / 2;

            float randomX = UnityEngine.Random.Range(minSlot.x + halfCarWidth, maxSlot.x - halfCarWidth);
            float randomZ = UnityEngine.Random.Range(minSlot.z + halfCarLength, maxSlot.z - halfCarLength);

            Vector3 randomPosition = new Vector3(randomX, 20, randomZ);
            newCar.transform.position = randomPosition;

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

            if (location.coordinateY > 1) firstFloorAreas.Add(carSlot);
            if (!workshopRoof.FirstFloor.active && firstFloorAreas.Count != 0) HideFirstFloor();

            return carSlot;
        }

        // ---------- SLIDER CHANGE: ao avançar e mudar de zona -> fecha anterior, abre nova e DROP na NOVA ----------
        private void OnSliderValueChanged(float value)
        {
            int nearestIndex = -1;
            bool found = false;

            foreach (var kv in taskPositions)
            {
                float startPos = kv.Value.startPos;
                float endPos = kv.Value.endPos;

                if (value >= startPos && value <= endPos)
                {
                    nearestIndex = kv.Key;
                    found = true;
                    break;
                }
            }

            if (nearestIndex != -1 && nearestIndex != currentActivityIndex)
            {
                int oldIndex = currentActivityIndex;
                currentActivityIndex = nearestIndex;

                // Só tratamos números/legenda se for avanço e tiver havido mudança de zona
                if (oldIndex != -1 && markerManager != null && legendManager != null)
                {
                    bool isForward = nearestIndex > oldIndex;
                    if (isForward &&
                        locByIndex.TryGetValue(oldIndex, out var prevLoc) &&
                        locByIndex.TryGetValue(currentActivityIndex, out var newLoc) &&
                        prevLoc != null && newLoc != null &&
                        prevLoc.id != newLoc.id)
                    {
                        TryEnsureLegend();
                        ActivateLegend();

                        // Se ainda não iniciámos, cria a 1ª linha e DROP na primeira zona
                        if (!legendStarted &&
                            locByIndex.TryGetValue(0, out var startLoc) &&
                            taskByIndex.TryGetValue(0, out var startTask))
                        {
                            legendManager.BeginStay(0, startLoc.id, startLoc.name, startTask.startTime);
                            if (slotByLocationId.TryGetValue(startLoc.id, out var startSlot) && startSlot != null)
                                markerManager.DropNumberAtLocation(0, startLoc, startSlot.transform);
                            legendStarted = true;
                        }

                        // Fecha estadia anterior
                        if (taskByIndex.TryGetValue(oldIndex, out var prevTask))
                            legendManager.EndStay(prevTask.completionTime);

                        // Abre nova estadia
                        if (taskByIndex.TryGetValue(currentActivityIndex, out var newTask))
                            legendManager.BeginStay(currentActivityIndex, newLoc.id, newLoc.name, newTask.startTime);

                        // DROP número na NOVA zona (ao ENTRAR)
                        if (slotByLocationId.TryGetValue(newLoc.id, out var slotGo) && slotGo != null)
                            markerManager.DropNumberAtLocation(currentActivityIndex, newLoc, slotGo.transform);
                    }
                }

                FocusOnCar(currentActivityIndex, oldIndex);
            }
            else if (!found)
            {
                currentActivityIndex = -1;
                currentCar.gameObject.SetActive(false);
                PinterestUrl.gameObject.SetActive(false);
                ActivityNumber.text = "No Activity";
                ActivityInfo.text = "During this gap there´s no tasks and locations associated with the car.";
                ActivityStatus.text = "";
                StartCoroutine(ForceUpdateLayout());
            }
        }


        private void FocusOnCar(int newIndex, int oldIndex)
        {
            if (oldIndex != -1) carInstances[oldIndex].gameObject.SetActive(false);
            else
            {
                for (int i = 0; i < taskPositions.Count; i++)
                    carInstances[i].gameObject.SetActive(false);
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
            // Remove listeners para evitar chamadas em GOs destruídos
            timelineSlider.onValueChanged.RemoveListener(OnSliderValueChanged);
            Next.onClick.RemoveListener(MoveToNextActivity);
            Previous.onClick.RemoveListener(MoveToPreviousActivity);
            Last.onClick.RemoveListener(MoveToLastActivity);
            First.onClick.RemoveListener(MoveToFirstActivity);
            closeButton.onClick.RemoveListener(OnCloseButtonClicked);

            StopAllCoroutines();

            highlightSelectionCar.SelectionOn();
            DestroyAllCars();
            DestroyCarSlots();
            firstFloorAreas.Clear();

            markerManager?.ClearAll();
            slotByLocationId.Clear();
            taskByIndex.Clear();
            locByIndex.Clear();

            legendManager?.Clear();
            legendPanelRoot?.SetActive(false); // esconde a legenda

            OnClose?.Invoke();
            Destroy(gameObject); // destrói só o prefab do timeline
        }

        
        private void OnDestroy()
        {
            // Tentar remover mesmo que já tenha sido removido antes (safe)
            timelineSlider.onValueChanged.RemoveListener(OnSliderValueChanged);
            Next.onClick.RemoveListener(MoveToNextActivity);
            Previous.onClick.RemoveListener(MoveToPreviousActivity);
            Last.onClick.RemoveListener(MoveToLastActivity);
            First.onClick.RemoveListener(MoveToFirstActivity);
            closeButton.onClick.RemoveListener(OnCloseButtonClicked);
        }


        private void DestroyCarSlots()
        {
            foreach (var carslot in carSlots) Destroy(carslot);
            carSlots.Clear();
        }

        public void DestroyAllCars()
        {
            foreach (var carInstance in carInstancesByLocation.Values) Destroy(carInstance);
            carInstancesByLocation.Clear();
        }

        // ---------- NEXT: avança 1; no fim não reinicia ----------
        private void MoveToNextActivity()
        {
            int lastIndex = activityAndLocationHistory.History.Count - 1;
            int nextIndex = currentActivityIndex + 1;

            if (nextIndex <= lastIndex)
            {
                timelineSlider.value = taskPositions[nextIndex].startPos;
            }
            // Se já está no fim, não faz nada (mantém números/legenda).
        }


        // ---------- PREVIOUS: desfaz 1 passo (sem remover nº se continuar na mesma zona) ----------
        private void MoveToPreviousActivity()
        {
            int prevIndex = currentActivityIndex - 1;

            if (prevIndex >= 0)
            {
                // Reconstrói números/legenda até ao prevIndex (drop em ENTRADAS)
                RebuildVisualsUpTo(prevIndex);

                var old = currentActivityIndex;
                currentActivityIndex = prevIndex;
                timelineSlider.value = taskPositions[prevIndex].startPos;
                FocusOnCar(currentActivityIndex, old);
            }
            else
            {
                // Estava no primeiro: limpa tudo e fica no início sem legenda/números
                markerManager?.ClearAll();
                legendManager?.ResetSession();
                legendStarted = false;
                DeactivateLegend();

                var old = currentActivityIndex;
                currentActivityIndex = 0;
                if (taskPositions.TryGetValue(0, out var p))
                    timelineSlider.value = p.startPos;
                FocusOnCar(currentActivityIndex, old);
            }
        }


        // ---------- LAST: constrói a história toda de uma vez (drop em ENTRADAS) ----------
        private void MoveToLastActivity()
        {
            int lastIndex = activityAndLocationHistory.History.Count - 1;
            if (lastIndex < 0) return;

            markerManager?.ClearAll();
            TryEnsureLegend();
            legendManager?.ResetSession();
            legendStarted = true;
            ActivateLegend();

            if (!locByIndex.TryGetValue(0, out var firstLoc)) return;
            if (!taskByIndex.TryGetValue(0, out var firstTask)) return;

            // Abre 1ª estadia e dropa nº na 1ª zona
            legendManager?.BeginStay(0, firstLoc.id, firstLoc.name, firstTask.startTime);
            if (slotByLocationId.TryGetValue(firstLoc.id, out var firstSlot) && firstSlot != null)
                markerManager.DropNumberAtLocation(0, firstLoc, firstSlot.transform);

            // Percorre todas as mudanças de zona: fecha anterior, abre nova e DROP na nova
            for (int i = 1; i <= lastIndex; i++)
            {
                var prevLoc = locByIndex[i - 1];
                var curLoc = locByIndex[i];
                if (prevLoc.id == curLoc.id) continue;

                var prevTask = taskByIndex[i - 1];
                legendManager?.EndStay(prevTask.completionTime);

                var curTask = taskByIndex[i];
                legendManager?.BeginStay(i, curLoc.id, curLoc.name, curTask.startTime);

                if (slotByLocationId.TryGetValue(curLoc.id, out var curSlot) && curSlot != null)
                    markerManager.DropNumberAtLocation(i, curLoc, curSlot.transform);
            }

            // Fecha a última estadia com a última task
            var lastTask = taskByIndex[lastIndex];
            legendManager?.EndStay(lastTask.completionTime);

            timelineSlider.value = taskPositions[lastIndex].startPos;
            var oldIdx = currentActivityIndex;
            currentActivityIndex = lastIndex;
            FocusOnCar(currentActivityIndex, oldIdx);
        }


        // ---------- FIRST: limpa tudo e volta ao início ----------
        private void MoveToFirstActivity()
        {
            if (activityAndLocationHistory.History.Count == 0) return;

            markerManager?.ClearAll();
            legendManager?.ResetSession();
            legendStarted = false;
            DeactivateLegend();

            var old = currentActivityIndex;
            currentActivityIndex = 0;

            if (taskPositions.TryGetValue(0, out var p))
                timelineSlider.value = p.startPos;

            FocusOnCar(currentActivityIndex, old);
        }


        void OpenURL(string url) => Application.OpenURL(url);

        private ActivityAndLocationHistory EliminateActivitiesWithNoLocation(ActivityAndLocationHistory activities)
        {
            var filteredActivities = new List<ActivityAndLocation>();
            foreach (var act in activities.History)
                if (act.LocationId != null) filteredActivities.Add(act);

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
            if (currentHover != null) Destroy(currentHover);
        }

        private void ChangeCameraToCar(Vector3 newPos)
        {
            var cameraSystem = GameObject.Find("CameraSystem").GetComponent<CameraSystem>();
            if (cameraSystem != null) cameraSystem.FocusOnCar(newPos);
        }

        private void ChangeCameraToTop()
        {
            var cameraSystem = GameObject.Find("CameraSystem").GetComponent<CameraSystem>();
            if (cameraSystem != null) cameraSystem.SwitchToTopCam();
        }

        public void HideFirstFloor()
        {
            foreach (GameObject area in firstFloorAreas) area.gameObject.SetActive(false);
        }

        public void ShowFirstFloor()
        {
            foreach (GameObject area in firstFloorAreas) area.gameObject.SetActive(true);
        }
    }
}
