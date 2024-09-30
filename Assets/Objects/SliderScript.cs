using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Objects
{

    public class SliderScript : MonoBehaviour
    {
        [SerializeField] private Slider slider;
        [SerializeField] private Text statusText;
        [SerializeField] private GameObject addCarButton;

        void Start()
        {
            addCarButton.SetActive(false);

            slider.onValueChanged.AddListener((v) =>
            {
                if (v == 1.0f)
                {
                    statusText.text = "On";
                    addCarButton.SetActive(true);
                }
                else
                {
                    statusText.text = "Off";
                    addCarButton.SetActive(false);
                }
            });
        }

    }
}
