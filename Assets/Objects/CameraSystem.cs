using System.Collections;
using System.Collections.Generic;
using Cinemachine;
using Models;
using Unity.VisualScripting;
using UnityEngine;

namespace Objects
{

    public class CameraSystem : MonoBehaviour
    {

        [SerializeField] CinemachineVirtualCamera cam1;
        [SerializeField] CinemachineVirtualCamera cam2;
        [SerializeField] private CinemachineVirtualCamera cam;
        [SerializeField] private float FieldOfViewMin = 0;
        [SerializeField] private float FieldOfViewMax = 70;

        private bool dragPanMoveAction;
        private Vector2 lastMousePosition;
        private float targetFieldOfView = 50;
        private GameObject camobj;
        private bool controlsActive = true;
        private bool spaceKeyPressed = false;
        private readonly float spaceKeyCooldown = 1f;

        private void Update()
        {
            if (controlsActive)
            {
                SwicthCameras();
                HandleCameraMovement();
                HandleCameraRotation();
                HandleCameraZoom();
            }
        }

        public void SwicthCameras()
        {
            if (Input.GetKey(KeyCode.Space) && !spaceKeyPressed)
            {
                spaceKeyPressed = true;
                StartCoroutine(ResetSpaceKeyCooldown());
                if (CameraSwitcher.isActiveCamera(cam1))
                {
                    CameraSwitcher.switchCamera(cam2);
                    cam = cam2;
                }
                else
                {
                    CameraSwitcher.switchCamera(cam1);
                    cam = cam1;
                }
            }
        }

        private IEnumerator ResetSpaceKeyCooldown()
        {
            yield return new WaitForSeconds(spaceKeyCooldown);
            spaceKeyPressed = false;
        }

        public void ActiveControls()
        {
            controlsActive = true;
        }

        public void DesactiveControls()
        {
            controlsActive = false;
        }

        private void OnEnable()
        {
            CameraSwitcher.Register(cam1);
            CameraSwitcher.Register(cam2);
            CameraSwitcher.switchCamera(cam1);
            cam = cam1;
        }

        private void OnDisable()
        {
            CameraSwitcher.Unregister(cam1);
            CameraSwitcher.Unregister(cam2);
        }

        private void HandleCameraMovement()
        {
            Vector3 inputDir = new(0, 0, 0);
            if (Input.GetKey(KeyCode.W)) inputDir.z = +1f;
            if (Input.GetKey(KeyCode.S)) inputDir.z = -1f;
            if (Input.GetKey(KeyCode.A)) inputDir.x = -1f;
            if (Input.GetKey(KeyCode.D)) inputDir.x = +1f;

            if (Input.GetMouseButtonDown(1))
            {
                dragPanMoveAction = true;
                lastMousePosition = Input.mousePosition;
            }

            if (Input.GetMouseButtonUp(1))
            {
                dragPanMoveAction = false;
            }

            if (dragPanMoveAction)
            {
                Vector2 mouseMovementDelta = (Vector2)Input.mousePosition - lastMousePosition;
                float dragPanSpeedZ = 0.2f;
                float dragPanSpeedX = 0.05f;
                inputDir.x = mouseMovementDelta.x * dragPanSpeedX;
                inputDir.z = mouseMovementDelta.y * dragPanSpeedZ;

                lastMousePosition = Input.mousePosition;
            }

            Vector3 moveDir = transform.forward * inputDir.z + transform.right * inputDir.x;

            float moveSpeed = 200f;


            Vector3 newPosition = transform.position + moveDir * moveSpeed * Time.deltaTime;

            newPosition.x = Mathf.Clamp(newPosition.x, -1000, 2000);
            newPosition.z = Mathf.Clamp(newPosition.z, -1000, 2000);

            transform.position = newPosition;
        }

        private void HandleCameraRotation()
        {
            float rotateDir = 0f;
            if (Input.GetKey(KeyCode.Q)) rotateDir = +1f;
            if (Input.GetKey(KeyCode.E)) rotateDir = -1f;

            float rotateSpeed = 50f;
            transform.eulerAngles += new Vector3(0, rotateDir * rotateSpeed * Time.deltaTime, 0);
        }

        private void HandleCameraZoom()
        {
            if (Input.mouseScrollDelta.y > 0)
            {
                targetFieldOfView -= 5;
            }
            if (Input.mouseScrollDelta.y < 0)
            {
                targetFieldOfView += 5;
            }

            targetFieldOfView = Mathf.Clamp(targetFieldOfView, FieldOfViewMin, FieldOfViewMax);
            float zoomSpeed = 10f;

            cam.m_Lens.FieldOfView = Mathf.Lerp(cam.m_Lens.FieldOfView, targetFieldOfView, Time.deltaTime * zoomSpeed);

        }

        public void FocusOnCar(Vector3 carPosition)
        {
            Vector3 offset = new(0, 10, -10); 
            Vector3 newPos = new(carPosition.x, carPosition.y, carPosition.z);

            targetFieldOfView = Mathf.Clamp(40, FieldOfViewMin, FieldOfViewMax);
            float zoomSpeed = 10f;

            cam.m_Lens.FieldOfView = Mathf.Lerp(cam.m_Lens.FieldOfView, targetFieldOfView, Time.deltaTime * zoomSpeed);

            transform.position = newPos + offset;
        }

        public void SwitchToTopCam()
        {
            CameraSwitcher.switchCamera(cam2);
            cam = cam2;
        }

    }
}