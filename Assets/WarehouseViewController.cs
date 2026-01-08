using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Objects;

public class WarehouseViewController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CameraSystem cameraSystem;
    [SerializeField] private StorageRepository storageRepository;
    //[SerializeField] private WarehouseHUD warehouseHUD;


    // guarda o último ALL para fallback (se endpoint do carro falhar)
    private List<StorageRowDTO> lastAllRows = new List<StorageRowDTO>();

    public void OpenWarehouseAll()
    {
        cameraSystem.EnterWarehouseFirstPerson();
        WarehouseHUD.Instance.Show();


        StartCoroutine(storageRepository.GetAllStorage(
            onSuccess: (rows) =>
            {
                lastAllRows = rows ?? new List<StorageRowDTO>();
                WarehouseManager.Instance.ShowAllStorage(lastAllRows);
                // sem highlights
            },
            onError: (err) =>
            {
                Debug.LogWarning("[WarehouseViewController] GetAllStorage error: " + err);
            }
        ));
    }

    public void OpenWarehouseForCar(string carId)
    {
        if (string.IsNullOrEmpty(carId))
        {
            Debug.LogWarning("[WarehouseViewController] carId vazio");
            return;
        }

        cameraSystem.EnterWarehouseFirstPerson();
        WarehouseHUD.Instance.Show();


        // 1) ALL sem highlight
        StartCoroutine(storageRepository.GetAllStorage(
            onSuccess: (allRows) =>
            {
                lastAllRows = allRows ?? new List<StorageRowDTO>();
                WarehouseManager.Instance.ShowAllStorage(lastAllRows);

                // 2) Só do carro para highlight
                StartCoroutine(storageRepository.GetStorageForCar(
                    carId,
                    onSuccess: (carRows) =>
                    {
                        // Se o endpoint já vier só com rows do carro:
                        WarehouseManager.Instance.HighlightCarBoxes(carId, carRows);
                    },
                    onError: (err2) =>
                    {
                        Debug.LogWarning("[WarehouseViewController] GetStorageForCar error: " + err2);

                        // Fallback: se tivermos ALL, filtramos por carId
                        WarehouseManager.Instance.HighlightCarBoxes(carId, lastAllRows);
                    }
                ));
            },
            onError: (err) =>
            {
                Debug.LogWarning("[WarehouseViewController] GetAllStorage error: " + err);
            }
        ));
    }
}
