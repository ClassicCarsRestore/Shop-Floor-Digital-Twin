using System.Collections;
using System.Collections.Generic;
using UI;
using UnityEngine;

namespace Objects
{

    public class Roof : MonoBehaviour
    {

        public GameObject roof;
        public GameObject FirstFloor;

        private bool isRoofActive = true;
        private VirtualMapScrollView virtualMap;

        void Update()
        {
            if (Input.GetKeyUp(KeyCode.T) && isRoofActive)
            {
                roof.SetActive(!roof.activeSelf);
            }

            if (Input.GetKeyUp(KeyCode.Alpha1))
            {
                FirstFloor.SetActive(!FirstFloor.activeSelf);
                FirstFloorOff(FirstFloor.activeSelf);
            }

        }

        public void RoofOff()
        {
            roof.SetActive(false);
        }

        public void ActiveRoof()
        {
            isRoofActive = true;
        }

        public void DesactiveRoof()
        {
            isRoofActive = false;
        }

        public void FirstFloorOff(bool active)
        {
            virtualMap = GameObject.Find("virtualMap").GetComponent<VirtualMapScrollView>();

            if (!active)
            {
                virtualMap.HideFirstFloor();
            }
            else
            {
                virtualMap.ShowFirstFloor();
            }

        }

    }

}