using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;
using System.Text;
using System.Threading.Tasks;
using System;
using Models;

namespace API
{

    public class APIscript : MonoBehaviour
    {
        public const string ADMIN = "admin";
        public const string MANAGER = "manager";
        public const string OWNER = "owner";

        private const string apiUrl = "http://194.210.120.34:5000/api";
        private string role;
        private string securityToken;
        public string lastCarLocation;
        public List<Car> cars;
        public List<Car> closedProjects;
        public List<VirtualMapLocation> locations;
        public List<ActivityAndLocationHistory> activities;
        public ActivityAndLocationHistory activityAndLocationById;
        public ActivityAndLocationHistory activityAndLocationByCar;
        public VirtualMapLocation locationById;
        public CamundaTaskDto camundaTask;
        public TaskDTO task;
        public List<string> locationIds;

        public UnityWebRequest.Result lastResponseResult;

        public void ClearSecurityToken()
        {
            securityToken = null;
            Debug.Log(securityToken);
        }

        public event Func<Task<string>, Task> OnLoginSuccess;
        public event Func<Task<string>, Task> OnLoginFailure;

        public string GetRole()
        {
            return role;
        }

        public async Task LoginRequestAsync(string email, string password)
        {
            UnityWebRequest request = new(apiUrl + "/Account/Login", "POST");
            request.SetRequestHeader("Content-Type", "application/json");
            string jsonRequestBody = "{\"email\":\"" + email + "\",\"password\":\"" + password + "\"}";
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonRequestBody);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.uploadHandler.contentType = "application/json";
            request.downloadHandler = new DownloadHandlerBuffer();

            var tcs = new TaskCompletionSource<bool>();
            var asyncOperation = request.SendWebRequest();

            asyncOperation.completed += _ => tcs.SetResult(true);

            await tcs.Task;

            lastResponseResult = request.result;

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("Error: " + request.error);
                if (OnLoginFailure != null) await OnLoginFailure(Task.FromResult(request.error));
            }
            else
            {
                Debug.Log("Response: " + request.result);
                Debug.Log("Response: " + request.downloadHandler.text);
                ApiResponseData responseData = JsonUtility.FromJson<ApiResponseData>(request.downloadHandler.text);

                if (responseData != null && !string.IsNullOrEmpty(responseData.token))
                {
                    securityToken = responseData.token;
                    role = responseData.role;
                    if (OnLoginSuccess != null) await OnLoginSuccess(Task.FromResult(securityToken));
                }
                else
                {
                    if (OnLoginFailure != null) await OnLoginFailure(Task.FromResult("Failed to get token from response"));
                }
                Debug.Log(securityToken);
            }
        }

        public delegate void OnCarsReceived(List<Car> cars);
        public event OnCarsReceived CarsReceived;

        public async Task GetCarsAsync()
        {
            Debug.Log("Fetching cars...");
            string url = $"{apiUrl}/Projects";

            UnityWebRequest request = UnityWebRequest.Get(url);

            if (!string.IsNullOrEmpty(securityToken))
            {
                request.SetRequestHeader("Authorization", "Bearer " + securityToken);
            }

            var tcs = new TaskCompletionSource<bool>();
            var asyncOperation = request.SendWebRequest();
            asyncOperation.completed += _ => tcs.SetResult(true);

            await tcs.Task;

            if (request.result == UnityWebRequest.Result.Success)
            {
                cars = JsonConvert.DeserializeObject<List<Car>>(request.downloadHandler.text);
                CarsReceived?.Invoke(cars);

                foreach (Car car in cars)
                {
                    Debug.Log($"Car ID: {car.id}, Make: {car.make}, Year: {car.year}");
                }
            }
            else
            {
                Debug.LogError("Get cars failed: " + request.error);
            }
        }

        public delegate void OnClosedProjectsReceived(List<Car> closedProjects);
        public event OnClosedProjectsReceived ClosedProjectsReceived;

        public async Task GetClosedProjectsAsync()
        {
            Debug.Log("Fetching closed projects...");
            UnityWebRequest request = new(apiUrl + "/Projects/Closed", "GET");

            if (!string.IsNullOrEmpty(securityToken))
            {
                request.SetRequestHeader("Authorization", "Bearer " + securityToken);
            }

            request.downloadHandler = new DownloadHandlerBuffer();

            var tcs = new TaskCompletionSource<bool>();
            var asyncOperation = request.SendWebRequest();

            asyncOperation.completed += _ => tcs.SetResult(true);
            await tcs.Task;

            if (request.result == UnityWebRequest.Result.Success)
            {
                closedProjects = JsonConvert.DeserializeObject<List<Car>>(request.downloadHandler.text);
                Debug.Log("Closed projects fetched successfully.");

                if (closedProjects.Count == 0)
                {
                    Debug.Log("No closed projects found.");
                }
                else
                {
                    foreach (Car car in closedProjects)
                    {
                        Debug.Log($"Car ID: {car.id}, Make: {car.make}, Year: {car.year}");
                    }
                }

                ClosedProjectsReceived?.Invoke(closedProjects);
            }
            else
            {
                Debug.LogError("Get closed projects failed: " + request.error);
            }

            lastResponseResult = request.result;
        }

        public delegate void OnVirtualMapLocationReceived(VirtualMapLocation location);
        public event OnVirtualMapLocationReceived VirtualMapLocationReceived;

        public async Task GetVirtualMapLocationByIdAsync(string id)
        {
            Debug.Log($"Fetching virtual map location with ID: {id}");
            UnityWebRequest request = new(apiUrl + "/VirtualMapLocations/" + id, "GET");

            if (!string.IsNullOrEmpty(securityToken))
            {
                request.SetRequestHeader("Authorization", "Bearer " + securityToken);
            }

            request.downloadHandler = new DownloadHandlerBuffer();

            var tcs = new TaskCompletionSource<bool>();
            var asyncOperation = request.SendWebRequest();

            asyncOperation.completed += _ => tcs.SetResult(true);
            await tcs.Task;

            if (request.result == UnityWebRequest.Result.Success)
            {
                locationById = JsonConvert.DeserializeObject<VirtualMapLocation>(request.downloadHandler.text);
                Debug.Log($"Location ID: {locationById.id}, Name: {locationById.name}");

                // Invoke the event to notify subscribers
                VirtualMapLocationReceived?.Invoke(locationById);
            }
            else
            {
                Debug.LogError("Get Location by ID failed: " + request.error);
            }

            lastResponseResult = request.result;
        }

        public delegate void OnActivityAndLocationHistoryReceived(ActivityAndLocationHistory history);
        public event OnActivityAndLocationHistoryReceived ActivityAndLocationHistoryReceived;

        public async Task GetActivityAndLocationHistoryByCarAsync(string caseInstanceId)
        {
            Debug.Log("Fetching activity and location history...");
            UnityWebRequest request = new(apiUrl + "/ActivityAndLocationHistory/Car/" + caseInstanceId, "GET");

            if (!string.IsNullOrEmpty(securityToken))
            {
                request.SetRequestHeader("Authorization", "Bearer " + securityToken);
            }

            request.downloadHandler = new DownloadHandlerBuffer();

            var tcs = new TaskCompletionSource<bool>();
            var asyncOperation = request.SendWebRequest();

            asyncOperation.completed += _ => tcs.SetResult(true);
            await tcs.Task;

            if (request.result == UnityWebRequest.Result.Success)
            {
                activityAndLocationByCar = JsonConvert.DeserializeObject<ActivityAndLocationHistory>(request.downloadHandler.text);
                Debug.Log("Activity and location history fetched successfully.");
                ActivityAndLocationHistoryReceived?.Invoke(activityAndLocationByCar);
            }
            else
            {
                Debug.LogError("Get Activities failed: " + request.error);
            }

            lastResponseResult = request.result;
        }

        public delegate void OnVirtualMapLocationsReceived(List<VirtualMapLocation> locations);
        public event OnVirtualMapLocationsReceived VirtualMapLocationsReceived;

        public async Task GetVirtualMapLocationsAsync()
        {
            Debug.Log("Fetching virtual map locations...");
            UnityWebRequest request = new(apiUrl + "/VirtualMapLocations", "GET");

            if (!string.IsNullOrEmpty(securityToken))
            {
                request.SetRequestHeader("Authorization", "Bearer " + securityToken);
            }

            request.downloadHandler = new DownloadHandlerBuffer();

            var tcs = new TaskCompletionSource<bool>();
            var asyncOperation = request.SendWebRequest();

            asyncOperation.completed += _ => tcs.SetResult(true);
            await tcs.Task;

            if (request.result == UnityWebRequest.Result.Success)
            {
                locations = JsonConvert.DeserializeObject<List<VirtualMapLocation>>(request.downloadHandler.text);
                Debug.Log("Virtual map locations fetched successfully.");
                VirtualMapLocationsReceived?.Invoke(locations);
            }
            else
            {
                Debug.LogError("Get Locations failed: " + request.error);
            }

            lastResponseResult = request.result;
        }

        public delegate void OnVirtualMapLocationDeleted();
        public event OnVirtualMapLocationDeleted VirtualMapLocationDeleted;

        public async Task DeleteVirtualMapLocationAsync(string locationId)
        {
            Debug.Log("Deleting virtual map location...");
            UnityWebRequest request = new(apiUrl + "/VirtualMapLocations/" + locationId, "DELETE");

            if (!string.IsNullOrEmpty(securityToken))
            {
                request.SetRequestHeader("Authorization", "Bearer " + securityToken);
            }

            var tcs = new TaskCompletionSource<bool>();
            var asyncOperation = request.SendWebRequest();

            asyncOperation.completed += _ => tcs.SetResult(true);
            await tcs.Task;

            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("Location deleted successfully.");
                VirtualMapLocationDeleted?.Invoke();
            }
            else
            {
                Debug.LogError("Delete Location failed: " + request.error);
            }

            lastResponseResult = request.result;
        }

        public delegate void OnVirtualMapLocationUpdated();
        public event OnVirtualMapLocationUpdated VirtualMapLocationUpdated;

        public async Task UpdateVirtualMapLocationAsync(
            string locationId, string locationName, float coordinateX, float coordinateY, float coordinateZ, List<string> activityIds, List<VerticesCoordinates> vertices, string color, int capacity)
        {
            Debug.Log("Updating virtual map location...");
            UnityWebRequest request = new(apiUrl + "/VirtualMapLocations/" + locationId, "PUT");

            // Set headers
            request.SetRequestHeader("Content-Type", "application/json");

            // Create JSON request body
            string jsonRequestBody = JsonConvert.SerializeObject(new
            {
                name = locationName,
                coordinateX,
                coordinateY,
                coordinateZ,
                activityIds,
                vertices,
                color,
                capacity
            });

            // Convert the JSON string to a byte array
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonRequestBody);

            // Attach the byte array to the request
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.uploadHandler.contentType = "application/json";

            // Set the download handler to handle the response
            request.downloadHandler = new DownloadHandlerBuffer();

            if (!string.IsNullOrEmpty(securityToken))
            {
                request.SetRequestHeader("Authorization", "Bearer " + securityToken);
            }

            var tcs = new TaskCompletionSource<bool>();
            var asyncOperation = request.SendWebRequest();

            asyncOperation.completed += _ => tcs.SetResult(true);
            await tcs.Task;

            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("Location updated successfully.");
                VirtualMapLocationUpdated?.Invoke();
            }
            else
            {
                Debug.LogError("Update Location failed: " + request.error);
            }

            lastResponseResult = request.result;
        }

        public delegate void OnVirtualMapLocationCreated();
        public event OnVirtualMapLocationCreated VirtualMapLocationCreated;

        public async Task CreateVirtualMapLocationAsync(
            string locationName, float coordinateX, float coordinateY, float coordinateZ, List<string> activityIds, List<VerticesCoordinates> vertices, string color, int capacity)
        {
            Debug.Log("Creating virtual map location...");
            UnityWebRequest request = new(apiUrl + "/VirtualMapLocations", "POST");

            // Set headers
            request.SetRequestHeader("Content-Type", "application/json");

            // Create JSON request body
            string jsonRequestBody = JsonConvert.SerializeObject(new
            {
                name = locationName,
                coordinateX,
                coordinateY,
                coordinateZ,
                activityIds,
                vertices,
                color,
                capacity
            });

            // Convert the JSON string to a byte array
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonRequestBody);

            // Attach the byte array to the request
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.uploadHandler.contentType = "application/json";

            // Set the download handler to handle the response
            request.downloadHandler = new DownloadHandlerBuffer();

            if (!string.IsNullOrEmpty(securityToken))
            {
                request.SetRequestHeader("Authorization", "Bearer " + securityToken);
            }

            var tcs = new TaskCompletionSource<bool>();
            var asyncOperation = request.SendWebRequest();

            asyncOperation.completed += _ => tcs.SetResult(true);
            await tcs.Task;

            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("Location created successfully.");
                VirtualMapLocationCreated?.Invoke();
            }
            else
            {
                Debug.LogError("Create Location failed: " + request.error);
            }
        }

        public delegate void OnListOfLocationWithActivityRetrieved(List<string> locationIds);
        public event OnListOfLocationWithActivityRetrieved ListOfLocationWithActivityRetrieved;

        public async Task GetListOfLocationWithActivityAsync(string activityId)
        {
            Debug.Log("Fetching locations with activity...");
            UnityWebRequest request = new(apiUrl + "/VirtualMapLocations/activity/" + activityId, "GET");

            if (!string.IsNullOrEmpty(securityToken))
            {
                request.SetRequestHeader("Authorization", "Bearer " + securityToken);
            }

            request.downloadHandler = new DownloadHandlerBuffer();

            var tcs = new TaskCompletionSource<bool>();
            var asyncOperation = request.SendWebRequest();

            asyncOperation.completed += _ => tcs.SetResult(true);
            await tcs.Task;

            if (request.result == UnityWebRequest.Result.Success)
            {
                locationIds = JsonConvert.DeserializeObject<List<string>>(request.downloadHandler.text);
                Debug.Log("Locations retrieved successfully.");
                ListOfLocationWithActivityRetrieved?.Invoke(locationIds);
            }
            else
            {
                Debug.LogError("Get locationsIds by activity failed: " + request.error);
            }
        }

        public delegate void OnActivityAndLocationHistoryRetrieved(List<ActivityAndLocationHistory> activities);
        public event OnActivityAndLocationHistoryRetrieved ActivityAndLocationHistoryRetrieved;

        public async Task GetActivityAndLocationHistoryAsync()
        {
            Debug.Log("Fetching activity and location history...");
            UnityWebRequest request = new(apiUrl + "/ActivityAndLocationHistory", "GET");

            if (!string.IsNullOrEmpty(securityToken))
            {
                request.SetRequestHeader("Authorization", "Bearer " + securityToken);
            }

            request.downloadHandler = new DownloadHandlerBuffer();

            var tcs = new TaskCompletionSource<bool>();
            var asyncOperation = request.SendWebRequest();

            asyncOperation.completed += _ => tcs.SetResult(true);
            await tcs.Task;

            if (request.result == UnityWebRequest.Result.Success)
            {
                activities = JsonConvert.DeserializeObject<List<ActivityAndLocationHistory>>(request.downloadHandler.text);
                Debug.Log("Activities retrieved successfully.");
                ActivityAndLocationHistoryRetrieved?.Invoke(activities);
            }
            else
            {
                Debug.LogError("Get Activities failed: " + request.error);
            }
        }

        public delegate void OnActivityAndLocationUpdated(bool success);
        public event OnActivityAndLocationUpdated ActivityAndLocationUpdated;

        public async Task UpdateActivityAndLocationAsync(string caseInstanceId, string activityAndLocationId, ActivityAndLocation updated)
        {
            Debug.Log("Updating activity and location...");
            UnityWebRequest request = new(apiUrl + "/ActivityAndLocationHistory/" + caseInstanceId + "/activities/" + activityAndLocationId, "PUT");

            // Set headers
            request.SetRequestHeader("Content-Type", "application/json");

            string jsonRequestBody = JsonConvert.SerializeObject(new
            {
                id = activityAndLocationId,
                activityId = updated.ActivityId,
                locationId = updated.LocationId
            });

            // Convert the JSON string to a byte array
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonRequestBody);

            // Attach the byte array to the request
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.uploadHandler.contentType = "application/json"; // Set content type explicitly

            // Set the download handler to handle the response
            request.downloadHandler = new DownloadHandlerBuffer();

            if (!string.IsNullOrEmpty(securityToken))
            {
                request.SetRequestHeader("Authorization", "Bearer " + securityToken);
            }

            var tcs = new TaskCompletionSource<bool>();
            var asyncOperation = request.SendWebRequest();

            asyncOperation.completed += _ => tcs.SetResult(true);
            await tcs.Task;

            // Check if the request was successful
            bool success = request.result == UnityWebRequest.Result.Success;
            if (success)
            {
                Debug.Log("Activity and Location updated successfully.");
            }
            else
            {
                Debug.LogError("Update Activity and Location failed: " + request.error);
            }

            // Trigger the event to notify that the update is complete
            ActivityAndLocationUpdated?.Invoke(success);
        }

        public delegate void OnCamundaActivityRetrieved(CamundaTaskDto camundaTask);
        public event OnCamundaActivityRetrieved CamundaActivityRetrieved;

        public async Task GetCamundaActivityAsync(string processInstanceId, string activityId)
        {
            Debug.Log("Fetching Camunda activity...");
            UnityWebRequest request = new(apiUrl + "/Tasks/getBC/" + processInstanceId + "/" + activityId, "GET");

            if (!string.IsNullOrEmpty(securityToken))
            {
                request.SetRequestHeader("Authorization", "Bearer " + securityToken);
            }

            request.downloadHandler = new DownloadHandlerBuffer();

            var tcs = new TaskCompletionSource<bool>();
            var asyncOperation = request.SendWebRequest();

            asyncOperation.completed += _ => tcs.SetResult(true);
            await tcs.Task;

            // Check if the request was successful
            if (request.result == UnityWebRequest.Result.Success)
            {
                camundaTask = JsonConvert.DeserializeObject<CamundaTaskDto>(request.downloadHandler.text);
                Debug.Log("Camunda activity retrieved successfully.");

                // Trigger the event with the retrieved data
                CamundaActivityRetrieved?.Invoke(camundaTask);
            }
            else
            {
                Debug.LogError("Get Camunda Activity failed: " + request.error);

                // Trigger the event with null to indicate failure
                CamundaActivityRetrieved?.Invoke(null);
            }
        }

        public delegate void OnTaskRetrieved(TaskDTO task);
        public event OnTaskRetrieved TaskRetrieved;

        public async Task GetTaskByIdAsync(string taskId)
        {
            Debug.Log("Fetching task by ID...");
            string url = $"{apiUrl}/Tasks/{taskId}";

            UnityWebRequest request = UnityWebRequest.Get(url);

            if (!string.IsNullOrEmpty(securityToken))
            {
                request.SetRequestHeader("Authorization", "Bearer " + securityToken);
            }

            request.downloadHandler = new DownloadHandlerBuffer();

            var tcs = new TaskCompletionSource<bool>();
            var asyncOperation = request.SendWebRequest();

            asyncOperation.completed += _ => tcs.SetResult(true);
            await tcs.Task;

            // Check if the request was successful
            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    task = JsonConvert.DeserializeObject<TaskDTO>(request.downloadHandler.text);
                    Debug.Log("Task retrieved successfully.");

                    // Trigger the event with the retrieved data
                    TaskRetrieved?.Invoke(task);
                }
                catch (JsonReaderException ex)
                {
                    Debug.LogError("JSON parsing error: " + ex.Message);
                    Debug.LogError("Response content: " + request.downloadHandler.text);

                    // Trigger the event with null to indicate failure
                    TaskRetrieved?.Invoke(null);
                }
            }
            else
            {
                Debug.LogError("Get Task failed: " + request.error);
                Debug.LogError("Response content: " + request.downloadHandler.text);

                // Trigger the event with null to indicate failure
                TaskRetrieved?.Invoke(null);
            }
        }

        public delegate void OnCarUpdated(bool success);
        public event OnCarUpdated CarUpdated;

        public async Task UpdateCarAsync(Car car)
        {
            Debug.Log("Updating car...");
            string url = $"{apiUrl}/Projects/{car.id}";

            string jsonData = JsonConvert.SerializeObject(car);
            byte[] jsonToSend = new System.Text.UTF8Encoding().GetBytes(jsonData);

            UnityWebRequest request = new(url, "PUT");
            request.uploadHandler = new UploadHandlerRaw(jsonToSend);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            if (!string.IsNullOrEmpty(securityToken))
            {
                request.SetRequestHeader("Authorization", "Bearer " + securityToken);
            }

            var tcs = new TaskCompletionSource<bool>();
            var asyncOperation = request.SendWebRequest();

            asyncOperation.completed += _ => tcs.SetResult(true);
            await tcs.Task;

            // Check if the request was successful
            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("Car updated successfully.");
                CarUpdated?.Invoke(true);
            }
            else
            {
                Debug.LogError("Update Car failed: " + request.error);
                Debug.LogError("Response content: " + request.downloadHandler.text);
                CarUpdated?.Invoke(false);
            }
        }


    }
}



