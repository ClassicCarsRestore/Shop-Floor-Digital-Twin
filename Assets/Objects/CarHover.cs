using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

namespace Objects
{

    public class CarHover : MonoBehaviour
    {
        public Text caseInstanceId;

        public void SetUp(string carName)
        {
            caseInstanceId.text = carName;
        }

    }
}