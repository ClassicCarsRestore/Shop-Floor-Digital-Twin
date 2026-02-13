using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Objects;

public class WarehouseViewController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CameraSystem cameraSystem;
    [SerializeField] private StorageRepository storageRepository;
    [SerializeField] private WarehouseLayoutController layoutController;
    [SerializeField] private Roof roof;

    private List<StorageRowDTO> lastAllRows = new List<StorageRowDTO>();

    public void OpenWarehouseAll()
    {
        if (roof != null) roof.EnsureFirstFloorOn();

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
