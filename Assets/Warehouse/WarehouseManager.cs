using System.Collections.Generic;
using UnityEngine;

public class WarehouseManager : MonoBehaviour
{
    public static WarehouseManager Instance;

    [Header("Warehouse Layout")]
    public List<ShelfSection> Sections = new List<ShelfSection>();

    [Header("Prefabs")]
    public GameObject storageBoxPrefab;

    // Guarda caixas instanciadas por localização (sec-shelf-area)
    private readonly Dictionary<string, StorageBox> boxesByLocation = new Dictionary<string, StorageBox>();

    public Transform WarehouseRoot;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    // ---------------------------
    // PUBLIC API
    // ---------------------------

    /// <summary>
    /// Mostra TODAS as caixas do armazém (todas as locations) sem highlight.
    /// rows: lista com carId + location
    /// </summary>
    public void ShowAllStorage(List<StorageRowDTO> rows)
    {
        ClearAllBoxes();

        if (rows == null) return;

        foreach (var row in rows)
        {
            if (row == null || row.location == null) continue;

            SpawnBoxAt(row.carId, row.location, highlight: false);
        }
    }

    /// <summary>
    /// Faz highlight apenas das caixas desse carro.
    /// </summary>
    public void HighlightCarBoxes(string carId, List<StorageRowDTO> rows)
    {
        if (string.IsNullOrEmpty(carId)) return;

        // 1) desligar highlight de todas
        SetAllHighlights(false);

        if (rows == null) return;

        // 2) ligar highlight só nas locations do carro
        foreach (var row in rows)
        {
            if (row == null || row.location == null) continue;
            if (row.carId != carId) continue;

            string key = MakeKey(row.location.section, row.location.shelf, row.location.area);

            if (boxesByLocation.TryGetValue(key, out var box) && box != null)
            {
                box.Highlight(true);
            }
        }
    }

    /// <summary>
    /// Helper: faz highlight a partir de uma lista de locations (sem precisar de rows).
    /// Útil se o endpoint do carro devolver só location.
    /// </summary>
    public void HighlightCarBoxesByLocations(string carId, List<StorageLocationDTO> locations)
    {
        if (string.IsNullOrEmpty(carId)) return;

        SetAllHighlights(false);

        if (locations == null) return;

        foreach (var loc in locations)
        {
            if (loc == null) continue;

            string key = MakeKey(loc.section, loc.shelf, loc.area);

            if (boxesByLocation.TryGetValue(key, out var box) && box != null)
            {
                
                box.CarId = carId;
                box.Highlight(true);
            }
        }
    }

    /// <summary>
    /// Mostra a(s) caixa(s) para um carro específico nas localizações indicadas.
    /// </summary>
    public void ShowStorageForCar(string carId, List<StorageLocationDTO> locations)
    {
        // Limpa caixas antigas desse carro (se houver)
        ClearBoxesForCar(carId);

        if (locations == null) return;

        foreach (var loc in locations)
        {
            if (loc == null) continue;

            SpawnBoxAt(carId, loc, highlight: true);
        }
    }

    public ShelfSection AddSectionRuntime(GameObject sectionGO)
    {
        if (sectionGO == null) return null;

        // garantir parent
        if (WarehouseRoot != null)
            sectionGO.transform.SetParent(WarehouseRoot, true);

       
        var sec = sectionGO.GetComponent<ShelfSection>();
        if (sec == null)
        {
            Debug.LogError("[WarehouseManager] sectionGO não tem ShelfSection no root.");
            return null;
        }

        string newId = GetNextSectionIdString(Sections);

        sec.SectionId = newId;
        sectionGO.name = $"Section_{newId}";

        if (!Sections.Contains(sec))
            Sections.Add(sec);

        return sec;
    }

    
    private string GetNextSectionIdString(List<ShelfSection> sections)
    {
        int max = 0;

        if (sections != null)
        {
            foreach (var s in sections)
            {
                if (s == null) continue;
                if (string.IsNullOrEmpty(s.SectionId)) continue;

                if (int.TryParse(s.SectionId, out int val))
                {
                    if (val > max) max = val;
                }
            }
        }

        return (max + 1).ToString();
    }


    // ---------------------------
    // INTERNALS
    // ---------------------------

    private void SpawnBoxAt(string carId, StorageLocationDTO loc, bool highlight)
    {
        var area = FindArea(loc.section, loc.shelf, loc.area);
        if (area == null)
        {
            Debug.LogWarning($"[WarehouseManager] Não encontrei area: secção {loc.section}, prateleira {loc.shelf}, área {loc.area}");
            return;
        }

        Transform anchor = area.BoxAnchor != null ? area.BoxAnchor : area.transform;

        // Key única por localização
        string key = MakeKey(loc.section, loc.shelf, loc.area);

        // Se já existe uma caixa nessa location, destrói e substitui (evita duplicados)
        if (boxesByLocation.TryGetValue(key, out var existing) && existing != null)
        {
            Destroy(existing.gameObject);
            boxesByLocation.Remove(key);
        }

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
            boxComp.LocationKey = key;
            boxComp.Highlight(highlight);

            boxesByLocation[key] = boxComp;
        }
    }

    private string MakeKey(string section, string shelf, string area)
    {
        return $"{section}-{shelf}-{area}";
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
    /// Remove todas as caixas desse carro.
    /// </summary>
    private void ClearBoxesForCar(string carId)
    {
        if (string.IsNullOrEmpty(carId)) return;

        var keysToRemove = new List<string>();

        foreach (var kv in boxesByLocation)
        {
            var box = kv.Value;
            if (box != null && box.CarId == carId)
            {
                Destroy(box.gameObject);
                keysToRemove.Add(kv.Key);
            }
        }

        foreach (var key in keysToRemove)
            boxesByLocation.Remove(key);
    }

    /// <summary>
    /// Remove todas as caixas do armazém.
    /// </summary>
    public void ClearAllBoxes()
    {
        foreach (var kv in boxesByLocation)
        {
            if (kv.Value != null)
                Destroy(kv.Value.gameObject);
        }
        boxesByLocation.Clear();
    }

    private void SetAllHighlights(bool on)
    {
        foreach (var kv in boxesByLocation)
        {
            if (kv.Value != null)
                kv.Value.Highlight(on);
        }
    }

 

  
}
