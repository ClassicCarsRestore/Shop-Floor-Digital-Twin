using System.Collections.Generic;
using UnityEngine;

public class WarehouseManager : MonoBehaviour
{
    public static WarehouseManager Instance;

    [Header("Warehouse Layout")]
    public List<ShelfSection> Sections = new List<ShelfSection>();

    [Header("Prefabs")]
    public GameObject storageBoxPrefab;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    /// <summary>
    /// Mostra a(s) caixa(s) para um carro específico nas localizações indicadas.
    /// </summary>
    public void ShowStorageForCar(string carId, List<StorageLocationDTO> locations)
    {
        // Limpa caixas antigas desse carro (se houver)
        ClearBoxesForCar(carId);

        foreach (var loc in locations)
        {
            var area = FindArea(loc.section, loc.shelf, loc.area);
            if (area == null)
            {
                Debug.LogWarning($"[WarehouseManager] Não encontrei area: secção {loc.section}, prateleira {loc.shelf}, área {loc.area}");
                continue;
            }

            // Instanciar a caixa na posição do BoxAnchor
            Transform anchor = area.BoxAnchor != null ? area.BoxAnchor : area.transform;

            GameObject boxGO = Instantiate(
                storageBoxPrefab,
                anchor.position,
                storageBoxPrefab.transform.rotation, 
                area.transform
            );


            var boxComp = boxGO.GetComponent<StorageBox>();
            if (boxComp != null)
            {
                boxComp.CarId = carId;
                boxComp.Highlight(true);
            }
        }
    }

    /// <summary>
    /// Procura a StorageArea a partir de section/shelf/area.
    /// </summary>
    private StorageArea FindArea(string sectionId, string shelfId, string areaId)
    {
        ShelfSection section = null;
        foreach (var sec in Sections)
        {
            if (sec != null && sec.SectionId == sectionId)
            {
                section = sec;
                break;
            }
        }
        if (section == null) return null;

        Shelf shelf = null;
        foreach (var sh in section.Shelves)
        {
            if (sh != null && sh.ShelfId == shelfId)
            {
                shelf = sh;
                break;
            }
        }
        if (shelf == null) return null;

        StorageArea area = null;
        foreach (var ar in shelf.Areas)
        {
            if (ar != null && ar.AreaId == areaId)
            {
                area = ar;
                break;
            }
        }
        return area;
    }

    /// <summary>
    /// Remove todas as caixas daquele carro.
    /// </summary>
    private void ClearBoxesForCar(string carId)
    {
        var allBoxes = FindObjectsOfType<StorageBox>();
        foreach (var box in allBoxes)
        {
            if (box.CarId == carId)
            {
                Destroy(box.gameObject);
            }
        }
    }
    [ContextMenu("Test Spawn Boxes")]
    private void TestSpawn()
    {
        var locations = new List<StorageLocationDTO>
    {
        new StorageLocationDTO
        {
            section = "1",
            shelf = "1",
            area  = "1"
        },
        new StorageLocationDTO
        {
            section = "5",
            shelf = "2",
            area  = "2"
        },
        new StorageLocationDTO
        {
            section = "8",
            shelf = "3",
            area  = "1"
        },
        new StorageLocationDTO
        {
            section = "4",
            shelf = "1",
            area  = "2"
        },
         new StorageLocationDTO
        {
            section = "2",
            shelf = "1",
            area  = "3"
        }
    };

        
        ShowStorageForCar("TEST_CAR", locations);
    }


}
