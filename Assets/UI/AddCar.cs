using System.Collections.Generic;
using API;
using Models;
using Objects;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{

    public class AddCar : MonoBehaviour
    {
        [SerializeField] public GameObject carObject;
        [SerializeField] private Slider simulationSlider;
        [SerializeField] private Text statusText;
        [SerializeField] private GameObject simulationPanel;
        [Header("API Manager")]
        public APIscript apiManager;
        private List<GameObject> simulationCars;
        private UIManager uiManager;
        public bool simulationModeOn = false;

        public void Start()
        {
            if (apiManager == null)
            {
                Debug.LogError("APIManager not assigned to LoginUI.");
                return;
            }

            simulationPanel.SetActive(false);
            simulationSlider.onValueChanged.AddListener((v) =>
            {
                if (v == 1.0f)
                {
                    statusText.text = "On";
                    simulationPanel.SetActive(true);
                    uiManager.CloseProjects();
                    uiManager.SimulationMode_InteractableOff();
                    simulationModeOn = true;
                }
                else
                {
                    statusText.text = "Off";
                    simulationPanel.SetActive(false);
                    uiManager.InteractableOn();
                    simulationModeOn = false;
                }
            });
            simulationCars = new List<GameObject>();
            uiManager = GameObject.Find("UIManager").GetComponent<UIManager>();

        }

        public void addSimulationCar()
        {
            GameObject carClone = Instantiate(carObject, new Vector3(0, 20, 0), transform.rotation);
            carClone.GetComponent<CarObject>().SetDraggable(true);
            simulationCars.Add(carClone);
        }

        public void removeAllCars()
        {
            foreach (GameObject carClone in simulationCars)
            {
                carClone.SetActive(false);
                Destroy(carClone);
            }
            simulationCars.Clear();
        }

    }
}
