using System.Collections;
using System.Collections.Generic;
using API;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UI
{

    public class Logout : MonoBehaviour
    {

        [Header("Acess View")]
        public GameObject loginView;
        public GameObject loggedInView;
        [Header("API Manager")]
        public APIscript apiManager;

        void Start()
        {
            if (apiManager == null)
            {
                Debug.LogError("APIManager not assigned to LogoutUI.");
            }
        }

        public void LogoutUser()
        {
            PlayerPrefs.DeleteAll();
            apiManager.ClearSecurityToken();
            ShowLoginView();
            ReloadCurrentScene();
        }

        private void ShowLoginView()
        {
            loginView.SetActive(true);
            loggedInView.SetActive(false);
        }

        public void ReloadCurrentScene()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }
}
