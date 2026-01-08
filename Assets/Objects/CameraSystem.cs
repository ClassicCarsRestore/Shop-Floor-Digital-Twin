using System.Collections;
using Cinemachine;
using Models;
using UnityEngine;

namespace Objects
{
    public class CameraSystem : MonoBehaviour
    {
        [SerializeField] CinemachineVirtualCamera cam1;
        [SerializeField] CinemachineVirtualCamera cam2;
        [SerializeField] private CinemachineVirtualCamera cam;

        [Header("Warehouse First Person")]
        [SerializeField] private CinemachineVirtualCamera camWarehouseFP;
        [SerializeField] private Transform warehouseSpawnPoint;

        [Header("Movement Bounds")]
        [SerializeField] private Vector2 worldBoundsX = new Vector2(-1000, 2000);
        [SerializeField] private Vector2 worldBoundsZ = new Vector2(-1000, 2000);
        [SerializeField] private Vector2 warehouseBoundsX = new Vector2(0, 0);
        [SerializeField] private Vector2 warehouseBoundsZ = new Vector2(0, 0);


        [Header("Warehouse Focus")]
        [SerializeField] private float warehouseFocusHeight = 1.7f;  // altura dos olhos
        [SerializeField] private float warehouseFocusDistance = 3.0f; // distância à frente da estante


        private Vector2 activeBoundsX;
        private Vector2 activeBoundsZ;

        private CinemachineVirtualCamera previousCam;
        private bool inWarehouseMode = false;

        [SerializeField] private float FieldOfViewMin = 0;
        [SerializeField] private float FieldOfViewMax = 70;

        private bool dragPanMoveAction;
        private Vector2 lastMousePosition;
        private float targetFieldOfView = 50;
        private bool controlsActive = true;

        private bool spaceKeyPressed = false;
        private readonly float spaceKeyCooldown = 1f;

        [Header("Warehouse Snap Turn")]
         private float snapTurnAngle = 90f;
         private float snapTurnDuration = 0.10f;
         private float snapTurnCooldown = 0.15f;

        [SerializeField] private AnimationCurve snapTurnEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);


        private bool isTurning = false;
        private float lastSnapTurnTime = -999f;

        private void Update()
        {
            if (!controlsActive) return;

            SwicthCameras();
            HandleCameraMovement();
            HandleCameraRotation();
            HandleCameraZoom();
        }

        public void SwicthCameras()
        {
            if (inWarehouseMode) return;

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

        public void ActiveControls() => controlsActive = true;
        public void DesactiveControls() => controlsActive = false;

        private void OnEnable()
        {
            CameraSwitcher.Register(cam1);
            CameraSwitcher.Register(cam2);
            if (camWarehouseFP != null) CameraSwitcher.Register(camWarehouseFP);

            CameraSwitcher.switchCamera(cam1);
            cam = cam1;

            activeBoundsX = worldBoundsX;
            activeBoundsZ = worldBoundsZ;
        }

        private void OnDisable()
        {
            CameraSwitcher.Unregister(cam1);
            CameraSwitcher.Unregister(cam2);
            if (camWarehouseFP != null) CameraSwitcher.Unregister(camWarehouseFP);
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
            newPosition.x = Mathf.Clamp(newPosition.x, activeBoundsX.x, activeBoundsX.y);
            newPosition.z = Mathf.Clamp(newPosition.z, activeBoundsZ.x, activeBoundsZ.y);

            transform.position = newPosition;
        }

        private void HandleCameraRotation()
        {
            if (inWarehouseMode)
            {
                HandleWarehouseSnapTurn();
                return;
            }

            HandleNormalRotation();
        }

        private void HandleNormalRotation()
        {
            float rotateDir = 0f;
            if (Input.GetKey(KeyCode.Q)) rotateDir = +1f;
            if (Input.GetKey(KeyCode.E)) rotateDir = -1f;

            float rotateSpeed = 50f;
            transform.eulerAngles += new Vector3(0, rotateDir * rotateSpeed * Time.deltaTime, 0);
        }

        private void HandleWarehouseSnapTurn()
        {
            if (isTurning) return;
            if (Time.time - lastSnapTurnTime < snapTurnCooldown) return;

            // Q = esquerda, E = direita
            if (Input.GetKeyDown(KeyCode.Q))
            {
                lastSnapTurnTime = Time.time;
                StartCoroutine(SmoothTurn(-snapTurnAngle));
            }
            else if (Input.GetKeyDown(KeyCode.E))
            {
                lastSnapTurnTime = Time.time;
                StartCoroutine(SmoothTurn(+snapTurnAngle));
            }
        }

        private IEnumerator SmoothTurn(float deltaYaw)
        {
            isTurning = true;

            Quaternion startRot = transform.rotation;
            Quaternion endRot = startRot * Quaternion.Euler(0f, deltaYaw, 0f);

            float elapsed = 0f;
            while (elapsed < snapTurnDuration)
            {
                elapsed += Time.deltaTime;
                float rawT = Mathf.Clamp01(elapsed / snapTurnDuration);
                float easedT = snapTurnEase.Evaluate(rawT);

                transform.rotation = Quaternion.Slerp(startRot, endRot, easedT);
                yield return null;
            }

            transform.rotation = endRot;

            if (camWarehouseFP != null)
                camWarehouseFP.transform.rotation = transform.rotation;

            isTurning = false;
        }


        private void HandleCameraZoom()
        {
            if (Input.mouseScrollDelta.y > 0) targetFieldOfView -= 5;
            if (Input.mouseScrollDelta.y < 0) targetFieldOfView += 5;

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

        public void EnterWarehouseFirstPerson()
        {
            inWarehouseMode = true;
            previousCam = cam;

            ActiveControls();

            activeBoundsX = warehouseBoundsX;
            activeBoundsZ = warehouseBoundsZ;

            if (warehouseSpawnPoint != null)
            {
                transform.position = warehouseSpawnPoint.position;
                transform.rotation = warehouseSpawnPoint.rotation;
            }

            if (camWarehouseFP != null)
            {
                CameraSwitcher.switchCamera(camWarehouseFP);
                cam = camWarehouseFP;
            }

            targetFieldOfView = Mathf.Clamp(60, FieldOfViewMin, FieldOfViewMax);
        }

        public void ExitWarehouseFirstPerson()
        {
            inWarehouseMode = false;

            activeBoundsX = worldBoundsX;
            activeBoundsZ = worldBoundsZ;

            if (previousCam != null)
            {
                CameraSwitcher.switchCamera(previousCam);
                cam = previousCam;
            }
        }

        public void FocusFrontOfSection(Transform sectionRoot)
        {
            if (sectionRoot == null) return;
            
            Vector3 forward = sectionRoot.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
            forward.Normalize();

            Vector3 targetPos = sectionRoot.position - forward * warehouseFocusDistance;
            targetPos.y = warehouseFocusHeight; // altura fixa

            transform.position = targetPos;

            Vector3 lookDir = sectionRoot.position - transform.position;
            lookDir.y = 0f;
            if (lookDir.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(lookDir.normalized, Vector3.up);
        }


    }
}
