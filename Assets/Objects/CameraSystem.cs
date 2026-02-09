using Cinemachine;
using Models;
using System.Collections;
using System.Collections.Generic;
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
        [SerializeField] private float warehouseFocusHeight = 1.7f;   // altura dos olhos
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

        [Header("Warehouse Focus Occlusion (Hide)")]
        [SerializeField] private LayerMask sectionMask;      // mesma layer das sections
        [SerializeField] private float occluderMargin = 0.05f;

        // --- Occlusion Hide ---
        private readonly HashSet<ShelfSection> hiddenOccluders = new HashSet<ShelfSection>();
        private ShelfSection focusedSection;


        [SerializeField] private AnimationCurve snapTurnEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        private bool isTurning = false;
        private float lastSnapTurnTime = -999f;

        // =========================================================
        // ADDED: Top Placement mode (reusa cam2 existente)
        // - bloqueia Space (não troca cam)
        // - mantém WASD a mover (bounds do armazém)
        // - desliga rotação (Q/E ficam livres para a section)
        // - restaura cam e bounds no fim
        // =========================================================
        private bool inTopPlacementMode = false;
        private bool rotationInputEnabled = true;
        private CinemachineVirtualCamera camBeforeTopPlacement;

        // ===== ADDED: Top View positioning =====
        [Header("Top View Placement Tuning")]
        [SerializeField] private Transform topViewAnchor;      // Empty no centro/teto da warehouse
        [SerializeField] private float topViewHeightOffset = 0f;
        [SerializeField] private bool forceTopViewLookDown = true;

        private Vector3 cam2SavedPos;
        private Quaternion cam2SavedRot;
        private bool cam2PoseSaved = false;
        // ===== ADDED: Top Placement height control (cam2 Transposer) =====
        [Header("Top Placement (cam2 Transposer)")]
        [SerializeField] private float topPlacementHeight = 20f;   // altura (Y) do FollowOffset durante placement
        [SerializeField] private Vector3 topPlacementCenter = new Vector3(513f, 0f, -326f); // centro base do top view (x,z)

        private CinemachineTransposer cam2Transposer;
        private Vector3 cam2FollowOffsetOriginal;
        private bool cam2OffsetSaved = false;
        // ================================================================

        // --- Focus restore ---
        private bool inSectionFocus = false;
        private Vector3 savedRigPos;
        private Quaternion savedRigRot;

        private Vector3 savedFPPos;
        private Quaternion savedFPRot;

        // =======================================
        public bool IsInWarehouseMode => inWarehouseMode;


        private void Update()
        {
            if (!controlsActive) return;

            SwicthCameras();
            HandleCameraMovement();
            HandleCameraRotation();
            HandleCameraZoom();

            // enquanto estiveres em foco, vai ajustando o que tapa a vista
            if (inSectionFocus)
                UpdateOccludersHide();

        }

        public void SwicthCameras()
        {
            if (inWarehouseMode) return;

            // ADDED: durante placement não deixar trocar cams com Space
            if (inTopPlacementMode) return;

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

            // ADDED: cache do transposer da cam2
            if (cam2 != null)
                cam2Transposer = cam2.GetCinemachineComponent<CinemachineTransposer>();

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
            // ADDED: se desligarmos rotação (placement), não mexe aqui
            if (!rotationInputEnabled) return;

            // ADDED: também não rodar em top placement (Q/E são da section)
            if (inTopPlacementMode) return;

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

            // guarda pose atual (uma vez) para poderes restaurar ao sair
            if (!inSectionFocus)
            {
                inSectionFocus = true;
                savedRigPos = transform.position;
                savedRigRot = transform.rotation;

                if (camWarehouseFP != null)
                {
                    savedFPPos = camWarehouseFP.transform.position;
                    savedFPRot = camWarehouseFP.transform.rotation;
                }
            }

            // 1) alvo = centro visual da section (bounds)
            Vector3 target = sectionRoot.position;
            var rends = sectionRoot.GetComponentsInChildren<Renderer>(true);
            if (rends != null && rends.Length > 0)
            {
                Bounds b = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                target = b.center;
            }

            // 2) forward da section projetado no chão
            Vector3 forward = sectionRoot.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
            forward.Normalize();

            // 3) candidata A: "à frente"
            Vector3 camPosA = target - forward * warehouseFocusDistance;
            camPosA.y = warehouseFocusHeight;

            // 4) candidata B: "lado contrário" (se a A ficar fora dos bounds)
            Vector3 camPosB = target + forward * warehouseFocusDistance;
            camPosB.y = warehouseFocusHeight;

            // escolher a melhor posição
            Vector3 camPos = camPosA;
            if (!IsInsideWarehouseBounds(camPosA))
            {
                if (IsInsideWarehouseBounds(camPosB))
                    camPos = camPosB;
                else
                    camPos = ClampToWarehouseBounds(camPosA); // fallback
            }

            // 5) rotação: só YAW (sem inclinação)
            Vector3 lookDir = target - camPos;
            lookDir.y = 0f;
            if (lookDir.sqrMagnitude < 0.0001f) lookDir = forward;

            Quaternion camRot = Quaternion.LookRotation(lookDir.normalized, Vector3.up);

            // força roll=0 e pitch=0
            Vector3 e = camRot.eulerAngles;
            camRot = Quaternion.Euler(0f, e.y, 0f);

            // aplica no rig
            transform.SetPositionAndRotation(camPos, camRot);

            // aplica também na vcam FP (se estiveres a usar)
            if (camWarehouseFP != null)
                camWarehouseFP.transform.SetPositionAndRotation(camPos, camRot);

            focusedSection = sectionRoot.GetComponentInParent<ShelfSection>();
            UpdateOccludersHide();

        }

        private bool IsInsideWarehouseBounds(Vector3 pos)
        {
            // margem para não colar na parede / evitar edge cases
            const float margin = 0.05f;

            return pos.x >= warehouseBoundsX.x + margin &&
                   pos.x <= warehouseBoundsX.y - margin &&
                   pos.z >= warehouseBoundsZ.x + margin &&
                   pos.z <= warehouseBoundsZ.y - margin;
        }

        private Vector3 ClampToWarehouseBounds(Vector3 pos)
        {
            const float margin = 0.05f;

            pos.x = Mathf.Clamp(pos.x, warehouseBoundsX.x + margin, warehouseBoundsX.y - margin);
            pos.z = Mathf.Clamp(pos.z, warehouseBoundsZ.x + margin, warehouseBoundsZ.y - margin);
            return pos;
        }


        public void RestoreAfterSectionFocus()
        {
            if (!inSectionFocus) return;

            inSectionFocus = false;

            transform.SetPositionAndRotation(savedRigPos, savedRigRot);

            if (camWarehouseFP != null)
                camWarehouseFP.transform.SetPositionAndRotation(savedFPPos, savedFPRot);

            RestoreHiddenOccluders();
            focusedSection = null;


            // safety: manter bounds corretos
            if (inWarehouseMode)
            {
                activeBoundsX = warehouseBoundsX;
                activeBoundsZ = warehouseBoundsZ;
            }
        }

        private void UpdateOccludersHide()
        {
            if (focusedSection == null) return;

            Vector3 camPos = camWarehouseFP != null ? camWarehouseFP.transform.position : transform.position;

            // target = centro visual da focused section
            Vector3 target = focusedSection.transform.position;
            var rends = focusedSection.GetComponentsInChildren<Renderer>(true);
            if (rends != null && rends.Length > 0)
            {
                Bounds b = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                target = b.center;
            }

            Vector3 dir = target - camPos;
            float dist = dir.magnitude;
            if (dist < 0.01f) return;
            dir /= dist;

            var hits = Physics.RaycastAll(
                camPos,
                dir,
                Mathf.Max(0f, dist - occluderMargin),
                sectionMask,
                QueryTriggerInteraction.Ignore);

            var newOccluders = new HashSet<ShelfSection>();

            for (int i = 0; i < hits.Length; i++)
            {
                var col = hits[i].collider;
                if (col == null) continue;

                var sec = col.GetComponentInParent<ShelfSection>();
                if (sec == null) continue;
                if (sec == focusedSection) continue;

                newOccluders.Add(sec);
            }

            // reativa os que deixaram de tapar
            foreach (var old in hiddenOccluders)
            {
                if (old == null) continue;
                if (!newOccluders.Contains(old))
                    old.gameObject.SetActive(true);
            }

            // desativa os novos que tapam
            foreach (var sec in newOccluders)
            {
                if (sec == null) continue;
                if (!hiddenOccluders.Contains(sec))
                    sec.gameObject.SetActive(false);
            }

            hiddenOccluders.Clear();
            foreach (var s in newOccluders) hiddenOccluders.Add(s);
        }

        private void RestoreHiddenOccluders()
        {
            foreach (var sec in hiddenOccluders)
            {
                if (sec != null)
                    sec.gameObject.SetActive(true);
            }
            hiddenOccluders.Clear();
        }





        // =========================================================
        // ADDED METHODS (para placement / edit placement)
        // =========================================================

        /// <summary>
        /// Chamar ao clicar no "+" ou "Move Section".
        /// Troca para top view (cam2), ativa bounds do armazém,
        /// e desliga rotação para Q/E ficarem livres para a section.
        /// </summary>
        public void EnterTopPlacementView()
        {
            if (inTopPlacementMode) return;

            camBeforeTopPlacement = cam;

            inTopPlacementMode = true;

            // WASD continua a mexer, mas dentro do armazém
            activeBoundsX = warehouseBoundsX;
            activeBoundsZ = warehouseBoundsZ;

            // guardar pose original da cam2 (uma vez)
            if (cam2 != null && !cam2PoseSaved)
            {
                cam2SavedPos = cam2.transform.position;
                cam2SavedRot = cam2.transform.rotation;
                cam2PoseSaved = true;
            }

            // mover cam2 "para cima" da warehouse
            if (cam2 != null && topViewAnchor != null)
            {
                cam2.transform.position = topViewAnchor.position + Vector3.up * topViewHeightOffset;

                if (forceTopViewLookDown)
                    cam2.transform.rotation = Quaternion.Euler(90f, cam2.transform.eulerAngles.y, 0f);
            }
            // ADDED: posicionar a rig (CameraSystem) em cima da warehouse (x,z)
            // isto controla o “centro” do top view porque cam2 segue este transform
            transform.position = new Vector3(topPlacementCenter.x, transform.position.y, topPlacementCenter.z);

            // ADDED: ajustar altura via Transposer FollowOffset
            if (cam2Transposer != null && !cam2OffsetSaved)
            {
                cam2FollowOffsetOriginal = cam2Transposer.m_FollowOffset;
                cam2OffsetSaved = true;
            }

            if (cam2Transposer != null)
            {
                var off = cam2Transposer.m_FollowOffset;
                off.y = topPlacementHeight;      // controla a “altura” real
                cam2Transposer.m_FollowOffset = off;
            }


            // reusar o top view já existente
            SwitchToTopCam();

            // liberta Q/E para o placement (câmara não roda)
            rotationInputEnabled = false;
        }

        /// <summary>
        /// Chamar ao clicar Save/Cancel do placement.
        /// Restaura a cam anterior e bounds do mundo, e reativa rotação.
        /// </summary>
        public void ExitTopPlacementViewRestore()
        {
            if (!inTopPlacementMode) return;

            inTopPlacementMode = false;

            // volta a permitir rotação normal
            rotationInputEnabled = true;

            // volta aos bounds normais do mundo
            if (inWarehouseMode)
            {
                activeBoundsX = warehouseBoundsX;
                activeBoundsZ = warehouseBoundsZ;
            }
            else
            {
                activeBoundsX = worldBoundsX;
                activeBoundsZ = worldBoundsZ;
            }


            // restaurar pose original da cam2
            if (cam2 != null && cam2PoseSaved)
            {
                cam2.transform.position = cam2SavedPos;
                cam2.transform.rotation = cam2SavedRot;
                cam2PoseSaved = false;
            }
            // ADDED: restaurar FollowOffset original da cam2
            if (cam2Transposer != null && cam2OffsetSaved)
            {
                cam2Transposer.m_FollowOffset = cam2FollowOffsetOriginal;
                cam2OffsetSaved = false;
            }


            // restaurar camera anterior (cam1/cam2/etc)
            if (camBeforeTopPlacement != null)
            {
                CameraSwitcher.switchCamera(camBeforeTopPlacement);
                cam = camBeforeTopPlacement;
            }

            camBeforeTopPlacement = null;
        }

        public bool IsInTopPlacementView() => inTopPlacementMode;
    }
}
