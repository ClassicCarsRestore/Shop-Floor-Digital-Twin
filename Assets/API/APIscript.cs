using Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using static System.Net.WebRequestMethods;

namespace API
{

    public class APIscript : MonoBehaviour
    {
        public const string ADMIN = "admin";
        public const string MANAGER = "manager";
        public const string OWNER = "owner";

        //private const string apiUrl = "http://194.210.120.34:5000/api";
        private const string apiUrl = "https://charterturinmonitor.raimundobranco.com/api";
        //private const string apiUrl = "/api";
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
        public List<TaskDTO> tasks;   // <--- NOVO


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

        /// <summary>
        /// Sends a login request to the backend API using the provided email and password.
        /// On success, stores the returned security token and user role.
        /// </summary>
        /// <param name="email">The user's email address used for authentication.</param>
        /// <param name="password">The user's password used for authentication.</param>
        /// <returns>A task that completes when the login request finishes processing.</returns> 
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

        /// <summary>
        /// Delegate for handling lists of retrieved cars.
        /// </summary>
        /// <param name="cars">A list of <see cref="Car"/> objects received from the API.</param>
        public delegate void OnCarsReceived(List<Car> cars);

        /// <summary>
        /// Event triggered when a list of cars is successfully retrieved from the API.
        /// </summary>
        public event OnCarsReceived CarsReceived;

        /// <summary>
        /// Fetches a list of all cars from the backend API.
        /// Requires a valid security token.
        /// Triggers the <see cref="CarsReceived"/> event upon successful response.
        /// </summary>
        /// <returns>A task that completes once the car data has been received and processed.</returns>
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

        /// <summary>
        /// Delegate for handling lists of closed projects.
        /// </summary>
        /// <param name="closedProjects">A list of closed <see cref="Car"/> project entries.</param>
        public delegate void OnClosedProjectsReceived(List<Car> closedProjects);

        /// <summary>
        /// Event triggered when a list of closed car projects is successfully retrieved from the API.
        /// </summary>
        public event OnClosedProjectsReceived ClosedProjectsReceived;


        /// <summary>
        /// Retrieves a list of all closed car restoration projects from the backend.
        /// Requires user to be authenticated.
        /// Triggers the <see cref="ClosedProjectsReceived"/> event on success.
        /// </summary>
        /// <returns>A task representing the asynchronous operation of fetching closed projects.</returns>
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

        /// <summary>
        /// Delegate triggered when a virtual map location is retrieved by its ID.
        /// </summary>
        /// <param name="location">The retrieved virtual map location.</param>
        public delegate void OnVirtualMapLocationReceived(VirtualMapLocation location);

        /// <summary>
        /// Event fired after successfully retrieving a virtual map location by ID.
        /// </summary>
        public event OnVirtualMapLocationReceived VirtualMapLocationReceived;


        /// <summary>
        /// Retrieves a virtual map location from the backend by its unique identifier.
        /// Triggers <see cref="VirtualMapLocationReceived"/> upon success.
        /// </summary>
        /// <param name="id">The ID of the virtual map location to retrieve.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
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

        /// <summary>
        /// Delegate triggered when the activity and location history of a car is received.
        /// </summary>
        /// <param name="history">The activity and location history for a car.</param>
        public delegate void OnActivityAndLocationHistoryReceived(ActivityAndLocationHistory history);

        /// <summary>
        /// Event fired when the activity and location history for a car is fetched successfully.
        /// </summary>
        public event OnActivityAndLocationHistoryReceived ActivityAndLocationHistoryReceived;


        /// <summary>
        /// Retrieves the activity and location history for a given car instance from the backend.
        /// Triggers <see cref="ActivityAndLocationHistoryReceived"/> upon success.
        /// </summary>
        /// <param name="caseInstanceId">The case instance ID of the car.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
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

        /// <summary>
        /// Delegate triggered when a list of virtual map locations is received.
        /// </summary>
        /// <param name="locations">The list of available virtual map locations.</param>
        public delegate void OnVirtualMapLocationsReceived(List<VirtualMapLocation> locations);

        /// <summary>
        /// Event fired when virtual map locations are successfully retrieved.
        /// </summary>
        public event OnVirtualMapLocationsReceived VirtualMapLocationsReceived;


        /// <summary>
        /// Fetches all virtual map locations from the backend.
        /// Triggers <see cref="VirtualMapLocationsReceived"/> upon success.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
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

        /// <summary>
        /// Delegate triggered after a virtual map location is successfully deleted.
        /// </summary>
        public delegate void OnVirtualMapLocationDeleted();

        /// <summary>
        /// Event fired after a virtual map location is deleted from the backend.
        /// </summary>
        public event OnVirtualMapLocationDeleted VirtualMapLocationDeleted;


        /// <summary>
        /// Deletes a virtual map location by its ID from the backend.
        /// Triggers <see cref="VirtualMapLocationDeleted"/> upon success.
        /// </summary>
        /// <param name="locationId">The ID of the location to delete.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
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

        /// <summary>
        /// Delegate triggered after a virtual map location is successfully updated.
        /// </summary>
        public delegate void OnVirtualMapLocationUpdated();

        /// <summary>
        /// Event fired when a virtual map location is updated in the backend.
        /// </summary>
        public event OnVirtualMapLocationUpdated VirtualMapLocationUpdated;

        /// <summary>
        /// Updates an existing virtual map location with new data.
        /// Triggers <see cref="VirtualMapLocationUpdated"/> upon success.
        /// </summary>
        /// <param name="locationId">The ID of the location to update.</param>
        /// <param name="locationName">The new name of the location.</param>
        /// <param name="coordinateX">The new X coordinate.</param>
        /// <param name="coordinateY">The new Y coordinate.</param>
        /// <param name="coordinateZ">The new Z coordinate.</param>
        /// <param name="activityIds">List of activity IDs associated with the location.</param>
        /// <param name="vertices">List of vertex coordinates defining the location's shape.</param>
        /// <param name="color">Display color of the location.</param>
        /// <param name="capacity">Maximum capacity of the location.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
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

        /// <summary>
        /// Delegate triggered after a virtual map location is successfully created.
        /// </summary>
        public delegate void OnVirtualMapLocationCreated();

        /// <summary>
        /// Event fired when a new virtual map location is created in the backend.
        /// </summary>
        public event OnVirtualMapLocationCreated VirtualMapLocationCreated;

        /// <summary>
        /// Creates a new virtual map location in the backend system.
        /// Triggers <see cref="VirtualMapLocationCreated"/> upon success.
        /// </summary>
        /// <param name="locationName">The name of the new location.</param>
        /// <param name="coordinateX">The X coordinate.</param>
        /// <param name="coordinateY">The Y coordinate.</param>
        /// <param name="coordinateZ">The Z coordinate.</param>
        /// <param name="activityIds">List of activity IDs to associate with this location.</param>
        /// <param name="vertices">List of vertices defining the shape of the location.</param>
        /// <param name="color">The display color of the location.</param>
        /// <param name="capacity">The maximum number of entities the location can hold.</param>
        /// <returns>A task representing the asynchronous creation operation.</returns>
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

        /// <summary>
        /// Delegate triggered when a list of location IDs is retrieved for a specific activity.
        /// </summary>
        /// <param name="locationIds">The list of location IDs that contain the specified activity.</param>
        public delegate void OnListOfLocationWithActivityRetrieved(List<string> locationIds);

        /// <summary>
        /// Event fired after successfully retrieving location IDs by activity.
        /// </summary>
        public event OnListOfLocationWithActivityRetrieved ListOfLocationWithActivityRetrieved;


        /// <summary>
        /// Retrieves a list of location IDs associated with a specific activity from the backend.
        /// Triggers <see cref="ListOfLocationWithActivityRetrieved"/> on success.
        /// </summary>
        /// <param name="activityId">The ID of the activity to filter locations by.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
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

        /// <summary>
        /// Delegate triggered when the complete activity and location history is retrieved.
        /// </summary>
        /// <param name="activities">A list of activity and location history entries.</param>
        public delegate void OnActivityAndLocationHistoryRetrieved(List<ActivityAndLocationHistory> activities);

        /// <summary>
        /// Event fired after successfully retrieving all activity and location history records.
        /// </summary>
        public event OnActivityAndLocationHistoryRetrieved ActivityAndLocationHistoryRetrieved;


        /// <summary>
        /// Retrieves all activity and location history records from the backend.
        /// Triggers <see cref="ActivityAndLocationHistoryRetrieved"/> on success.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
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

        /// <summary>
        /// Delegate triggered after attempting to update a specific activity and location.
        /// </summary>
        /// <param name="success">Indicates whether the update was successful.</param>
        public delegate void OnActivityAndLocationUpdated(bool success);

        /// <summary>
        /// Event fired after an activity and location update request completes.
        /// </summary>
        public event OnActivityAndLocationUpdated ActivityAndLocationUpdated;


        /// <summary>
        /// Updates a specific activity and location entry in the backend.
        /// Triggers <see cref="ActivityAndLocationUpdated"/> to indicate success or failure.
        /// </summary>
        /// <param name="caseInstanceId">The ID of the case instance containing the activity.</param>
        /// <param name="activityAndLocationId">The ID of the activity and location entry to update.</param>
        /// <param name="updated">The new data to apply to the activity and location entry.</param>
        /// <returns>A task representing the asynchronous update operation.</returns>
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

        /// <summary>
        /// Delegate triggered when Camunda activity details are retrieved from the backend.
        /// </summary>
        /// <param name="camundaTask">The retrieved Camunda task data.</param>
        public delegate void OnCamundaActivityRetrieved(CamundaTaskDto camundaTask);

        /// <summary>
        /// Event fired after successfully fetching a Camunda activity.
        /// </summary>
        public event OnCamundaActivityRetrieved CamundaActivityRetrieved;


        /// <summary>
        /// Retrieves a Camunda task from the backend based on process and activity ID.
        /// Triggers <see cref="CamundaActivityRetrieved"/> with the result.
        /// </summary>
        /// <param name="processInstanceId">The process instance identifier.</param>
        /// <param name="activityId">The specific activity ID within the process.</param>
        /// <returns>A task representing the asynchronous fetch operation.</returns>
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

        public async Task GetTasksAsync()
        {
            Debug.Log("Fetching tasks...");
            string url = $"{apiUrl}/Tasks";

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

            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    tasks = JsonConvert.DeserializeObject<List<TaskDTO>>(request.downloadHandler.text);
                    Debug.Log($"Tasks retrieved successfully. Count = {tasks?.Count ?? 0}");
                }
                catch (JsonReaderException ex)
                {
                    Debug.LogError("JSON parsing error (tasks): " + ex.Message);
                    Debug.LogError("Response content: " + request.downloadHandler.text);
                    tasks = null;
                }
            }
            else
            {
                Debug.LogError("Get Tasks failed: " + request.error);
                Debug.LogError("Response content: " + request.downloadHandler.text);
                tasks = null;
            }

            lastResponseResult = request.result;
        }


        /// <summary>
        /// Delegate triggered when a specific task is retrieved from the backend.
        /// </summary>
        /// <param name="task">The task details retrieved.</param>
        public delegate void OnTaskRetrieved(TaskDTO task);

        /// <summary>
        /// Event fired after successfully retrieving a task by its ID.
        /// </summary>
        public event OnTaskRetrieved TaskRetrieved;


        /// <summary>
        /// Retrieves a task from the backend using its unique task ID.
        /// Triggers <see cref="TaskRetrieved"/> with the result, or null on failure.
        /// </summary>
        /// <param name="taskId">The identifier of the task to retrieve.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
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

        /// <summary>
        /// Delegate triggered after attempting to update a car's information.
        /// </summary>
        /// <param name="success">Indicates whether the update was successful.</param>
        public delegate void OnCarUpdated(bool success);

        /// <summary>
        /// Event fired when a car update operation completes.
        /// </summary>
        public event OnCarUpdated CarUpdated;


        /// <summary>
        /// Updates the information of a car in the backend system.
        /// Triggers <see cref="CarUpdated"/> upon completion.
        /// </summary>
        /// <param name="car">The car object containing updated information.</param>
        /// <returns>A task representing the asynchronous update operation.</returns>
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



