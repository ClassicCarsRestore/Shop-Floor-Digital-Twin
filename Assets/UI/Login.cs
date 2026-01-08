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

        //NOVO Tirar depois
        [Header("Temporary Dev Mode")]
        [SerializeField] private bool bypassLoginAsAdmin = true; // <-- mete true enquanto o server está em baixo

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

            // BYPASS TEMPORÁRIO
            if (bypassLoginAsAdmin)
            {
                EnterAsAdmin_NoServer();
                return;
            }
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

        private void EnterAsAdmin_NoServer()
        {
            // Não precisamos de loginView
            workshopRoof.ActiveRoof();
            loginView.SetActive(false);

            // Força Admin view sem depender do apiManager
            loggedInView.SetActive(true);

            // Se quiseres manter consistência visual:
            locationsView.SetActive(false);
            simulationView.SetActive(true); // ou false, dependendo do que queres testar

            Debug.Log("[DEV] Bypass login ativo: a entrar como ADMIN (temporário).");
        }


        public async void LoginUser()
        {

            //  BYPASS TEMPORÁRIO
            if (bypassLoginAsAdmin)
            {
                EnterAsAdmin_NoServer();
                return;
            }
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
