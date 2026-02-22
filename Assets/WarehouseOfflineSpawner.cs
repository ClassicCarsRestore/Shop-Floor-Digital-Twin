using System.Collections.Generic;
using UnityEngine;

public class WarehouseDebugSpawner : MonoBehaviour
{
    [SerializeField] private WarehouseManager warehouseManager;
    [SerializeField] private int boxesToSpawn = 1;
    [SerializeField] private string carIdPrefix = "CAR";

    [ContextMenu("Spawn Sequential Boxes")]
    public void SpawnSequential()
    {
        if (warehouseManager == null)
        {
            Debug.LogError("[WarehouseDebugSpawner] warehouseManager não atribuído.");
            return;
        }

        int spawned = 0;
        int carCounter = 1;

        foreach (var sec in warehouseManager.Sections)
        {
            if (sec == null || sec.Shelves == null) continue;

            foreach (var shelf in sec.Shelves)
            {
                if (shelf == null || shelf.Areas == null) continue;

                // garante ordem por index (assumindo que Areas já estão criadas em ordem)
                for (int i = 0; i < shelf.Areas.Count; i++)
                {
                    var area = shelf.Areas[i];
                    if (area == null) continue;

                    if (area.Status == "free" || string.IsNullOrEmpty(area.ItemId))
                    {
                        string carId = $"{carIdPrefix}-{carCounter:00}";
                        string itemId = $"BOX-{carCounter:000}";

                        area.Status = "occupied";
                        area.ItemId = itemId;
                        area.UpdateVisual();

                        // construir row/loc para o teu manager
                        var row = new StorageRowDTO
                        {
                            carId = carId,
                            location = new StorageLocationDTO
                            {
                                section = sec.SectionId,
                                shelf = shelf.ShelfId,      // "sec-shelfIndex"
                                area = area.AreaId          // "sec-shelfIndex-areaIndex"
                            }
                        };

                        warehouseManager.ShowAllStorage(new List<StorageRowDTO> { row });
                        // ^ se preferires não limpar tudo, cria um método SpawnSingleRow no WarehouseManager.
                        // alternativa: chamar um método público que só faça SpawnBoxAt sem ClearAllBoxes.

                        spawned++;
                        carCounter++;

                        if (spawned >= boxesToSpawn)
                        {
                            Debug.Log($"[WarehouseDebugSpawner] Spawned {spawned} boxes.");
                            return;
                        }
                    }
                }
            }
        }

        Debug.Log($"[WarehouseDebugSpawner] Spawned {spawned} boxes (acabaram áreas livres).");
    }
}