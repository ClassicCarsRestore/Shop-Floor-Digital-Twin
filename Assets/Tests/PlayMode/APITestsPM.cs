using NUnit.Framework;
using System.Collections;
using UnityEngine;
using UnityEngine.TestTools;
using API;
using Models;
using System.Collections.Generic;
using System.Threading.Tasks;


namespace Tests
{
    public class APITestPM
    {
        private APIscript apiScript;
        private GameObject testObject;

        [SetUp]
        public void Setup()
        {
            testObject = new GameObject("TestObject");
            apiScript = testObject.AddComponent<APIscript>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(testObject);
        }

        private IEnumerator Login()
        {
            string email = "admin";
            string password = "antonio2";
            bool loginSuccess = false;
            Task OnSuccess(Task<string> tokenTask)
            {
                loginSuccess = true;
                return Task.CompletedTask;
            }
            Task OnFailure(Task<string> err)
            {
                loginSuccess = false;
                return Task.CompletedTask;
            }
            apiScript.OnLoginSuccess += OnSuccess;
            apiScript.OnLoginFailure += OnFailure;
            _ = apiScript.LoginRequestAsync(email, password);
            yield return new WaitUntil(() => loginSuccess);
            apiScript.OnLoginSuccess -= OnSuccess;
            apiScript.OnLoginFailure -= OnFailure;
            Assert.IsTrue(loginSuccess);
        }

        [UnityTest]
        public IEnumerator Login_WithValidCredentials_ShouldSetSecurityToken()
        {
            string email = "admin";
            string password = "antonio2";
            bool loginCompleted = false;
            bool loginSuccess = false;

            async Task OnSuccess(Task<string> tokenTask)
            {
                loginSuccess = true;
                loginCompleted = true;
                await Task.CompletedTask;
            }

            async Task OnFailure(Task<string> errorTask)
            {
                loginSuccess = false;
                loginCompleted = true;
                await Task.CompletedTask;
            }

            apiScript.OnLoginSuccess += OnSuccess;
            apiScript.OnLoginFailure += OnFailure;

            _ = apiScript.LoginRequestAsync(email, password);

            
            yield return new WaitUntil(() => loginCompleted);

            apiScript.OnLoginSuccess -= OnSuccess;
            apiScript.OnLoginFailure -= OnFailure;

            Assert.IsTrue(loginSuccess);
            Assert.IsNotNull(apiScript.GetRole());
        }

        [UnityTest]
        public IEnumerator GetCars_WithValidToken_ShouldReturnCarList()
        {
            string email = "admin";
            string password = "antonio2";

            bool loginSuccess = false;
            bool carsReceived = false;
            List<Car> receivedCars = null;
            float timeout = 5f;
            float timer = 0f;

            // Login
            Task OnLoginSuccess(Task<string> tokenTask)
            {
                loginSuccess = true;
                return Task.CompletedTask;
            }

            Task OnLoginFailure(Task<string> errorTask)
            {
                loginSuccess = false;
                return Task.CompletedTask;
            }

            apiScript.OnLoginSuccess += OnLoginSuccess;
            apiScript.OnLoginFailure += OnLoginFailure;

            _ = apiScript.LoginRequestAsync(email, password);

            yield return new WaitUntil(() => loginSuccess || (timer += Time.deltaTime) > timeout);
            apiScript.OnLoginSuccess -= OnLoginSuccess;
            apiScript.OnLoginFailure -= OnLoginFailure;

            Assert.IsTrue(loginSuccess, "Login failed before GetCars.");

            //Fetch cars
            timer = 0f;
            void OnCarsReceived(List<Car> cars)
            {
                carsReceived = true;
                receivedCars = cars;
            }

            apiScript.CarsReceived += OnCarsReceived;
            _ = apiScript.GetCarsAsync();

            yield return new WaitUntil(() => carsReceived || (timer += Time.deltaTime) > timeout);
            apiScript.CarsReceived -= OnCarsReceived;

            Assert.IsTrue(carsReceived, "Cars not received.");
            Assert.IsNotNull(receivedCars, "Car list is null.");
        }


        [UnityTest]
        public IEnumerator GetVirtualMapLocation_WithValidId_ShouldReturnLocation()
        {
            string email = "admin";
            string password = "antonio2";
            string testLocationId = "66db346a7e1464d013f6c10b";

            bool loginSuccess = false;
            bool locationReceived = false;
            VirtualMapLocation receivedLocation = null;
            float timeout = 5f;
            float timer = 0f;

            // First login
            Task OnLoginSuccess(Task<string> tokenTask)
            {
                loginSuccess = true;
                return Task.CompletedTask;
            }

            Task OnLoginFailure(Task<string> errorTask)
            {
                loginSuccess = false;
                return Task.CompletedTask;
            }

            apiScript.OnLoginSuccess += OnLoginSuccess;
            apiScript.OnLoginFailure += OnLoginFailure;
            _ = apiScript.LoginRequestAsync(email, password);

            yield return new WaitUntil(() => loginSuccess || (timer += Time.deltaTime) > timeout);
            apiScript.OnLoginSuccess -= OnLoginSuccess;
            apiScript.OnLoginFailure -= OnLoginFailure;

            Assert.IsTrue(loginSuccess, "Login failed");

            // Then fetch the location
            timer = 0f; // Reset timer
            void OnLocationReceived(VirtualMapLocation location)
            {
                locationReceived = true;
                receivedLocation = location;
            }

            apiScript.VirtualMapLocationReceived += OnLocationReceived;
            _ = apiScript.GetVirtualMapLocationByIdAsync(testLocationId);

            yield return new WaitUntil(() => locationReceived || (timer += Time.deltaTime) > timeout);
            apiScript.VirtualMapLocationReceived -= OnLocationReceived;

            Assert.IsTrue(locationReceived, "Location was not received within timeout.");
            Assert.IsNotNull(receivedLocation);
        }


        [UnityTest]
        public IEnumerator GetClosedProjectsAsync_AfterLogin_ReturnsValidData()
        {
            yield return Login();

            bool callbackCalled = false;
            List<Car> closedProjects = null;

            void OnEvent(List<Car> cars)
            {
                callbackCalled = true;
                closedProjects = cars;
            }

            apiScript.ClosedProjectsReceived += OnEvent;
            _ = apiScript.GetClosedProjectsAsync();
            yield return new WaitUntil(() => callbackCalled);
            apiScript.ClosedProjectsReceived -= OnEvent;

            Assert.IsTrue(callbackCalled);
            Assert.IsNotNull(closedProjects);
        }

        [UnityTest]
        public IEnumerator GetVirtualMapLocationsAsync_AfterLogin_ReturnsValidData()
        {
            yield return Login();

            bool callbackCalled = false;
            List<VirtualMapLocation> locations = null;

            void OnEvent(List<VirtualMapLocation> locs)
            {
                callbackCalled = true;
                locations = locs;
            }

            apiScript.VirtualMapLocationsReceived += OnEvent;
            _ = apiScript.GetVirtualMapLocationsAsync();
            yield return new WaitUntil(() => callbackCalled);
            apiScript.VirtualMapLocationsReceived -= OnEvent;

            Assert.IsTrue(callbackCalled);
            Assert.IsNotNull(locations);
        }

        [UnityTest]
        public IEnumerator GetActivityAndLocationHistoryByCarAsync_AfterLogin_ReturnsValidData()
        {
            yield return Login();

            bool callbackCalled = false;
            ActivityAndLocationHistory history = null;

            void OnEvent(ActivityAndLocationHistory h)
            {
                callbackCalled = true;
                history = h;
            }

            apiScript.ActivityAndLocationHistoryReceived += OnEvent;
            _ = apiScript.GetActivityAndLocationHistoryByCarAsync("ferrari_rese_0");
            yield return new WaitUntil(() => callbackCalled);
            apiScript.ActivityAndLocationHistoryReceived -= OnEvent;

            Assert.IsTrue(callbackCalled);
            Assert.IsNotNull(history);
        }

        [UnityTest]
        public IEnumerator GetActivityAndLocationHistoryAsync_AfterLogin_ReturnsValidData()
        {
            yield return Login();

            bool callbackCalled = false;
            List<ActivityAndLocationHistory> histories = null;

            void OnEvent(List<ActivityAndLocationHistory> h)
            {
                callbackCalled = true;
                histories = h;
            }

            apiScript.ActivityAndLocationHistoryRetrieved += OnEvent;
            _ = apiScript.GetActivityAndLocationHistoryAsync();
            yield return new WaitUntil(() => callbackCalled);
            apiScript.ActivityAndLocationHistoryRetrieved -= OnEvent;

            Assert.IsTrue(callbackCalled);
            Assert.IsNotNull(histories);
        }

       

        [UnityTest]
        public IEnumerator GetTaskByIdAsync_AfterLogin_ReturnsValidData()
        {
            yield return Login();

            bool callbackCalled = false;
            TaskDTO receivedTask = null;

            void OnEvent(TaskDTO task)
            {
                callbackCalled = true;
                receivedTask = task;
            }

            apiScript.TaskRetrieved += OnEvent;
            _ = apiScript.GetTaskByIdAsync("66d9b4527e1464d013f6c101");
            yield return new WaitUntil(() => callbackCalled);
            apiScript.TaskRetrieved -= OnEvent;

            Assert.IsTrue(callbackCalled);
            Assert.IsNotNull(receivedTask);
        }



    }


}