using System.Collections.Generic;
using UnityEngine;

public class WarehouseManager : MonoBehaviour
{
    public static WarehouseManager Instance;

    [Header("Warehouse Layout")]
    public List<ShelfSection> Sections = new List<ShelfSection>();

    [Header("Prefabs")]
    public GameObject storageBoxPrefab;

    // Guarda caixas instanciadas por localiza��o (sec-shelf-area)
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
    /// Mostra TODAS as caixas do armaz�m (todas as locations) sem highlight.
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

        // 2) ligar highlight s� nas locations do carro
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
    /// �til se o endpoint do carro devolver s� location.
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
    /// Mostra a(s) caixa(s) para um carro espec�fico nas localiza��es indicadas.
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

        if (WarehouseRoot != null)
            sectionGO.transform.SetParent(WarehouseRoot, true);

        var sec = sectionGO.GetComponent<ShelfSection>();
        if (sec == null)
        {
            Debug.LogError("[WarehouseManager] sectionGO n�o tem ShelfSection no root.");
            return null;
        }

        // 1) ID da Section
        string newId = GetNextSectionIdString(Sections);
        sec.SectionId = newId;
        sectionGO.name = $"Section_{newId}";

        if (!Sections.Contains(sec))
            Sections.Add(sec);

        // 2) Rebuild Shelves para aplicar ShelfId = "sec-index"
        var shelvesCtrl = sectionGO.GetComponent<ShelfSectionShelvesController>();
        if (shelvesCtrl != null)
            shelvesCtrl.RebuildShelves(); // IMPORTANT: este m�todo tem de gerar ids novos (ver nota abaixo)

        // 3) Criar �reas default em todas as shelves iniciais
        const int defaultAreas = 6;

        if (sec.Shelves != null)
        {
            for (int i = 0; i < sec.Shelves.Count; i++)
            {
                var shelf = sec.Shelves[i];
                ShelfAreasBuilder.RebuildAreas(shelf, defaultAreas, sec.SectionId, i + 1);
            }
        }

        Physics.SyncTransforms();
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
            Debug.LogWarning($"[WarehouseManager] N�o encontrei area: sec��o {loc.section}, prateleira {loc.shelf}, �rea {loc.area}");
            return;
        }

        Transform anchor = area.BoxAnchor != null ? area.BoxAnchor : area.transform;

        // Key �nica por localiza��o
        string key = MakeKey(loc.section, loc.shelf, loc.area);

        // Se j� existe uma caixa nessa location, destr�i e substitui (evita duplicados)
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

            // garantir tamanho/posição corretos no slot
            boxComp.FitToAreaSlot();

            boxesByLocation[key] = boxComp;
        }
    }

    private string MakeKey(string section, string shelf, string area)
    {
        return area; // "1-2-1"
    }

    /// <summary>
    /// Procura a StorageArea a partir de section/shelf/area.
    /// </summary>
    private StorageArea FindArea(string sectionId, string shelfId, string areaId)
    {
        foreach (var sec in Sections)
        {
            if (sec == null) continue;

            foreach (var sh in sec.Shelves)
            {
                if (sh == null) continue;

                foreach (var ar in sh.Areas)
                {
                    if (ar != null && ar.AreaId == areaId)
                        return ar;
                }
            }
        }
        return null;
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
    /// Remove todas as caixas do armaz�m.
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
