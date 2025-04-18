using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using API;
using Newtonsoft.Json.Serialization;
using Objects;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using UnityEngine.Windows;
using Input = UnityEngine.Input;

namespace UI
{


    public class Login : MonoBehaviour
    {
        [Header("Login Input")]
        public InputField emailInput;
        public InputField passwordInput;
        public Text warningText;
        public Text warningUsernameText;
        public Text warningPasswordText;
        [Header("Acess View")]
        public GameObject loginView;
        public GameObject loggedInView;
        public GameObject locationsView;
        public GameObject simulationView;
        [Header("API Manager")]
        public APIscript apiManager;
        private Roof workshopRoof;

        // Start is called before the first frame update
        void Start()
        {
            Application.targetFrameRate = 60;


            if (apiManager == null)
            {
                Debug.LogError("APIManager not assigned to LoginUI.");
                return;
            }
            workshopRoof = GameObject.Find("oficina").GetComponent<Roof>();
            ShowLoginView();
        }

        void Update()
        {
            // Check if the "Enter" key was pressed
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                // Call the login function when Enter is pressed
                LoginUser();
            }
        }

        public async void LoginUser()
        {
            string email = emailInput.text;
            string password = passwordInput.text;
            warningUsernameText.text = "";
            warningPasswordText.text = "";
            warningText.text = "";

            if (!string.IsNullOrEmpty(email) && !string.IsNullOrEmpty(password))
            {
                await LoginRoutine(email, password);
            }
            else
            {
                if (string.IsNullOrEmpty(email))
                {
                    warningUsernameText.text = "Invalid Username";
                }
                if (string.IsNullOrEmpty(password))
                {
                    warningPasswordText.text = "Invalid Password";
                }
            }

        }

        private async Task LoginRoutine(string email, string password)
        {
            await apiManager.LoginRequestAsync(email, password);

            if (apiManager.lastResponseResult == UnityWebRequest.Result.Success)
            {
                ShowLoggedInView();
                emailInput.text = null;
                passwordInput.text = null;
            }
            else
            {
                warningText.text = "The Username and password do not match";
            }
        }

        private void ShowLoginView()
        {
            workshopRoof.DesactiveRoof();
            loginView.SetActive(true);
            loggedInView.SetActive(false);
        }

        private void ShowLoggedInView()
        {
            switch (apiManager.GetRole())
            {
                case APIscript.ADMIN:
                    AdminView();
                    break;
                case APIscript.MANAGER:
                    ManagerView();
                    break;
                case APIscript.OWNER:
                    OwnerView();
                    break;
            }
            workshopRoof.ActiveRoof();
            loginView.SetActive(false);
        }

        private void AdminView()
        {
            loggedInView.SetActive(true);
        }

        private void ManagerView()
        {
            loggedInView.SetActive(true);
            locationsView.SetActive(false);
        }

        private void OwnerView()
        {
            loggedInView.SetActive(true);
            locationsView.SetActive(false);
            simulationView.SetActive(false);
        }

    }
}
