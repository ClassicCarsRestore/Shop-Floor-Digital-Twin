using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using Objects;
using UI;

public class WarehouseViewController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CameraSystem cameraSystem;
    [SerializeField] private StorageRepository storageRepository;
    [SerializeField] private WarehouseLayoutController layoutController;
    [SerializeField] private Roof roof;
    [SerializeField] private UIManager uiManager;

    private List<StorageRowDTO> lastAllRows = new List<StorageRowDTO>();
    private bool isRefreshingAllStorage;

    public bool TryRefreshAllStorage(Action onCompleted = null)
    {
        if (isRefreshingAllStorage)
            return false;

        if (storageRepository == null)
        {
            Debug.LogWarning("[WarehouseViewController] StorageRepository não atribuído para refresh.");
            onCompleted?.Invoke();
            return false;
        }

        StartCoroutine(RefreshAllStorageRoutine(onCompleted));
        return true;
    }

    private IEnumerator RefreshAllStorageRoutine(Action onCompleted)
    {
        isRefreshingAllStorage = true;

        yield return StartCoroutine(storageRepository.GetAllStorage(
            onSuccess: (rows) =>
            {
                lastAllRows = rows ?? new List<StorageRowDTO>();
                if (WarehouseManager.Instance != null)
                    WarehouseManager.Instance.ShowAllStorage(lastAllRows);
            },
            onError: (err) => Debug.LogWarning("[WarehouseViewController] Refresh GetAllStorage error: " + err)
        ));

        isRefreshingAllStorage = false;
        onCompleted?.Invoke();
    }

    public void OpenWarehouseAll()
    {
        if (roof != null) roof.EnsureFirstFloorOn();

        if (WarehouseManager.Instance != null)
            WarehouseManager.Instance.SetWarehouseRootVisible(true);

        cameraSystem.EnterWarehouseFirstPerson();
        WarehouseHUD.Instance.Show();

        if (layoutController != null)
        {
            layoutController.LoadLayoutAndThen(() =>
            {
                StartCoroutine(storageRepository.GetAllStorage(
                    onSuccess: (rows) =>
                    {
                        lastAllRows = rows ?? new List<StorageRowDTO>();
                        WarehouseManager.Instance.ShowAllStorage(lastAllRows);
                    },
                    onError: (err) => Debug.LogWarning("[WarehouseViewController] GetAllStorage error: " + err)
                ));
            });
        }
        else
        {
            StartCoroutine(storageRepository.GetAllStorage(
                onSuccess: (rows) =>
                {
                    lastAllRows = rows ?? new List<StorageRowDTO>();
                    WarehouseManager.Instance.ShowAllStorage(lastAllRows);
                },
                onError: (err) => Debug.LogWarning("[WarehouseViewController] GetAllStorage error: " + err)
            ));
        }
    }

    public void OpenWarehouseForCar(string carId)
    {
        if (roof != null) roof.EnsureFirstFloorOn();

        if (string.IsNullOrEmpty(carId))
        {
            Debug.LogWarning("[WarehouseViewController] carId vazio");
            return;
        }

        if (uiManager != null)
        {
            // Entrada pela ficha de projeto: fechar/deselecionar Projects.
            uiManager.CloseProjects();
        }

        if (WarehouseManager.Instance != null)
            WarehouseManager.Instance.SetWarehouseRootVisible(true);

        cameraSystem.EnterWarehouseFirstPerson();
        WarehouseHUD.Instance.Show();

        if (layoutController != null)
        {
            layoutController.LoadLayoutAndThen(() =>
            {
                StartCoroutine(storageRepository.GetAllStorage(
                    onSuccess: (allRows) =>
                    {
                        lastAllRows = allRows ?? new List<StorageRowDTO>();
                        WarehouseManager.Instance.ShowAllStorage(lastAllRows);

                        StartCoroutine(storageRepository.GetStorageForCar(
                            carId,
                            onSuccess: (carRows) =>
                            {
                                WarehouseManager.Instance.HighlightCarBoxes(carId, carRows);
                            },
                            onError: (err2) =>
                            {
                                Debug.LogWarning("[WarehouseViewController] GetStorageForCar error: " + err2);
                                WarehouseManager.Instance.HighlightCarBoxes(carId, lastAllRows);
                            }
                        ));
                    },
                    onError: (err) => Debug.LogWarning("[WarehouseViewController] GetAllStorage error: " + err)
                ));
            });
        }
        else
        {
            StartCoroutine(storageRepository.GetAllStorage(
                onSuccess: (allRows) =>
                {
                    lastAllRows = allRows ?? new List<StorageRowDTO>();
                    WarehouseManager.Instance.ShowAllStorage(lastAllRows);

                    StartCoroutine(storageRepository.GetStorageForCar(
                        carId,
                        onSuccess: (carRows) =>
                        {
                            WarehouseManager.Instance.HighlightCarBoxes(carId, carRows);
                        },
                        onError: (err2) =>
                        {
                            Debug.LogWarning("[WarehouseViewController] GetStorageForCar error: " + err2);
                            WarehouseManager.Instance.HighlightCarBoxes(carId, lastAllRows);
                        }
                    ));
                },
                onError: (err) => Debug.LogWarning("[WarehouseViewController] GetAllStorage error: " + err)
            ));
        }
    }
}
