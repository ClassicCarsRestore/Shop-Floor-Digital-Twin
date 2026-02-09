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

        [Header("Refs")]
        [SerializeField] private CameraSystem cameraSystem; // arrasta o CameraSystem no Inspector
        [SerializeField] private VirtualMapScrollView virtualMap; // opcional: arrasta no Inspector

        private bool isRoofActive = true;

        private void Awake()
        {
            // fallback: tenta achar 1x se não estiver no inspector
            if (virtualMap == null)
            {
                var vm = GameObject.Find("virtualMap");
                if (vm != null) virtualMap = vm.GetComponent<VirtualMapScrollView>();
            }
        }

        void Update()
        {
            if (Input.GetKeyUp(KeyCode.T) && isRoofActive)
            {
                roof.SetActive(!roof.activeSelf);
            }

            if (Input.GetKeyUp(KeyCode.Alpha1))
            {
                //  Bloqueia toggle do 1.º andar enquanto estiver na warehouse
                if (cameraSystem != null && cameraSystem.IsInWarehouseMode)
                    return;

                ToggleFirstFloor();
            }
        }

        private void ToggleFirstFloor()
        {
            if (FirstFloor == null) return;

            FirstFloor.SetActive(!FirstFloor.activeSelf);
            FirstFloorOff(FirstFloor.activeSelf);
        }

        //  Para o WarehouseViewController poder garantir que está ON antes de entrar
        public bool IsFirstFloorActive()
        {
            return FirstFloor != null && FirstFloor.activeSelf;
        }

        public void EnsureFirstFloorOn()
        {
            if (FirstFloor == null) return;

            if (!FirstFloor.activeSelf)
            {
                FirstFloor.SetActive(true);
                FirstFloorOff(true);
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
            if (virtualMap == null) return;

            if (!active) virtualMap.HideFirstFloor();
            else virtualMap.ShowFirstFloor();
        }
    }
}
