using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class StorageRepository : MonoBehaviour
{
    // Chamada relativa ao site -> Caddy -> Authentik -> reverse_proxy
    private const string DefaultItemsBaseUrl = "/inventory/items/";
    private const string ItemsCacheKey = "warehouse.storage.rows.cache.v1";

    [Header("Items Query")]
    [SerializeField, Min(1)] private int defaultItemsLimit = 150;

    [Header("Benchmark/Debug (optional)")]
    [SerializeField] private string allStorageUrlOverride = string.Empty;
    [SerializeField] private string itemsByCharterUrlTemplateOverride = string.Empty;

    public bool LastGetAllUsedCacheFallback { get; private set; }
    public bool LastGetAllHadError { get; private set; }

    public void SetRequestUrlOverrides(string allStorageUrl, string itemsByCharterTemplate)
    {
        allStorageUrlOverride = allStorageUrl ?? string.Empty;
        itemsByCharterUrlTemplateOverride = itemsByCharterTemplate ?? string.Empty;
    }

    public void ClearRequestUrlOverrides()
    {
        allStorageUrlOverride = string.Empty;
        itemsByCharterUrlTemplateOverride = string.Empty;
    }

    public IEnumerator GetAllStorage(Action<List<StorageRowDTO>> onSuccess, Action<string> onError)
    {
        LastGetAllUsedCacheFallback = false;
        LastGetAllHadError = false;

        string url = !string.IsNullOrWhiteSpace(allStorageUrlOverride)
            ? allStorageUrlOverride
            : BuildDefaultItemsUrl();
        yield return GetItems(url, onSuccess, onError);
    }

    private string BuildDefaultItemsUrl()
    {
        int safeLimit = Mathf.Max(1, defaultItemsLimit);
        return $"{DefaultItemsBaseUrl}?skip=0&limit={safeLimit}";
    }

    // Swagger: GET /items/?charter_id={carId}
    public IEnumerator GetStorageForCar(string carId, Action<List<StorageRowDTO>> onSuccess, Action<string> onError)
    {
        if (string.IsNullOrWhiteSpace(carId))
        {
            onError?.Invoke("[StorageRepository] carId vazio.");
            yield break;
        }

        string url;
        if (!string.IsNullOrWhiteSpace(itemsByCharterUrlTemplateOverride))
        {
            string template = itemsByCharterUrlTemplateOverride.Trim();
            string encoded = UnityWebRequest.EscapeURL(carId);
            url = template.Replace("{charter_id}", encoded);
        }
        else
        {
            url = $"/inventory/items/?charter_id={UnityWebRequest.EscapeURL(carId)}";
        }

        yield return GetItemsForCar(url, carId, onSuccess, onError);
    }

    // -----------------------------
    // Internals
    // -----------------------------

    private IEnumerator GetItems(string url, Action<List<StorageRowDTO>> onSuccess, Action<string> onError)
    {
        using var req = UnityWebRequest.Get(url);
        req.SetRequestHeader("Accept", "application/json");


        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            LastGetAllHadError = true;
            string body = req.downloadHandler != null ? req.downloadHandler.text : "";
            string msg = $"[StorageRepository] API falhou: {req.error} | HTTP={(int)req.responseCode} | Body={body}";
            Debug.LogWarning(msg);

            if (TryGetCachedRows(out var cachedRows))
            {
                LastGetAllUsedCacheFallback = true;
                Debug.LogWarning($"[StorageRepository] A usar cache local de items (rows={cachedRows.Count}).");
                onSuccess?.Invoke(cachedRows);
                yield break;
            }

            onError?.Invoke(msg);
            yield break;
        }

        string json = req.downloadHandler.text;

        List<StorageRowDTO> rows;
        try
        {
            rows = ParseRowsFromItemsJson(json);
        }
        catch (Exception e)
        {
            LastGetAllHadError = true;
            Debug.LogWarning("[StorageRepository] Erro a fazer parse do JSON (/items/): " + e.Message);
            Debug.LogWarning("JSON recebido: " + json);

            if (TryGetCachedRows(out var cachedRows))
            {
                LastGetAllUsedCacheFallback = true;
                Debug.LogWarning($"[StorageRepository] Parse falhou, a usar cache local (rows={cachedRows.Count}).");
                rows = cachedRows;
            }
            else
            {
                rows = new List<StorageRowDTO>();
            }
        }

        SaveRowsCache(rows);
        onSuccess?.Invoke(rows);
    }

    private IEnumerator GetItemsForCar(
        string url,
        string carId,
        Action<List<StorageRowDTO>> onSuccess,
        Action<string> onError)
    {
        using var req = UnityWebRequest.Get(url);
        req.SetRequestHeader("Accept", "application/json");


        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            string body = req.downloadHandler != null ? req.downloadHandler.text : "";
            string msg = $"[StorageRepository] API falhou: {req.error} | HTTP={(int)req.responseCode} | Body={body}";
            Debug.LogWarning(msg);
            onError?.Invoke(msg);
            yield break;
        }

        string json = req.downloadHandler.text;

        List<StorageRowDTO> rows;
        try
        {
            rows = ParseRowsFromItemsJson(json, carId);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[StorageRepository] Erro a fazer parse do JSON (/items/?charter_id=): " + e.Message);
            Debug.LogWarning("[StorageRepository] A continuar com rows vazias para não bloquear o fluxo.");
            Debug.LogWarning("JSON recebido: " + json);
            rows = new List<StorageRowDTO>();
        }

        onSuccess?.Invoke(rows);
    }

    private List<StorageRowDTO> ParseRowsFromItemsJson(string json, string fallbackCarId = null)
    {
        var rows = new List<StorageRowDTO>();
        int skipped = 0;

        foreach (var item in EnumeratePayloadArray(json, "items", "data", "results", "payload", "value"))
        {
            string carId;
            if (!string.IsNullOrWhiteSpace(fallbackCarId))
            {
                // Quando a lista já vem filtrada por charter_id, garantimos consistência
                // para o match exato no HighlightCarBoxes(row.carId == carId).
                carId = fallbackCarId;
            }
            else
            {
                carId = ReadString(item, "charter_id", "charterId", "project_id", "projectId", "carId", "car_id");
                if (string.IsNullOrWhiteSpace(carId)) carId = "unknown";
            }
            string itemId = ReadString(item, "id", "itemId", "item_id", "barcode", "name");
            if (string.IsNullOrWhiteSpace(itemId)) itemId = "unknown-item";
            string itemName = ReadString(item, "name", "itemName", "title");
            string itemState = ReadString(item, "state", "itemState", "status");
            string itemDescription = ReadString(item, "description", "itemDescription");
            string carModel = ReadString(item, "car_project_name", "car_model_location", "carModel", "car_model");

            bool mappedAny = false;
            foreach (var loc in EnumerateLocationTokens(item, includeItemAsFallback: false))
            {
                string section = ReadString(loc, "section", "sectionId", "section_id");
                string shelf = ReadString(loc, "shelf", "shelfId", "shelf_id");
                string area = ReadString(loc, "area", "areaId", "area_id");

                NormalizeLocationIds(ref section, ref shelf, ref area);

                if (string.IsNullOrWhiteSpace(section) || string.IsNullOrWhiteSpace(shelf) || string.IsNullOrWhiteSpace(area))
                    continue;

                rows.Add(new StorageRowDTO
                {
                    itemId = itemId,
                    itemName = itemName,
                    itemState = itemState,
                    itemDescription = itemDescription,
                    carModel = carModel,
                    carId = carId,
                    location = new StorageLocationDTO
                    {
                        section = section,
                        shelf = shelf,
                        area = area
                    }
                });
                mappedAny = true;
            }

            if (!mappedAny)
                skipped++;
        }

        return rows;
    }

    private void NormalizeLocationIds(ref string section, ref string shelf, ref string area)
    {
        if (string.IsNullOrWhiteSpace(section) || string.IsNullOrWhiteSpace(shelf) || string.IsNullOrWhiteSpace(area))
            return;

        string rawShelf = shelf;

        // backend pode devolver shelf/area "curtos" (ex.: shelf=1, area=1)
        // mas a cena usa ids compostos (ex.: shelf=3-1, area=3-1-1).
        if (!shelf.Contains("-"))
            shelf = $"{section}-{shelf}";

        if (!area.Contains("-"))
        {
            string shelfSegment = !string.IsNullOrWhiteSpace(rawShelf)
                ? rawShelf
                : shelf;
            area = $"{section}-{shelfSegment}-{area}";
        }
    }

    private IEnumerable<JToken> EnumeratePayloadArray(string json, params string[] envelopeKeys)
    {
        if (string.IsNullOrWhiteSpace(json))
            yield break;

        JToken root;
        try
        {
            root = JToken.Parse(json);
        }
        catch
        {
            yield break;
        }

        if (root is JArray arr)
        {
            foreach (var t in arr) yield return t;
            yield break;
        }

        if (root is JObject obj)
        {
            foreach (var key in envelopeKeys)
            {
                var token = obj[key];
                if (token is JArray envArr)
                {
                    foreach (var t in envArr) yield return t;
                    yield break;
                }
            }

            // fallback: objeto único
            yield return obj;
        }
    }

    private JToken ExtractLocation(JToken item)
    {
        if (item == null) return null;
        return item["warehouse_location"]
               ?? item["warehouseLocation"]
               ?? item["location"]
               ?? item["warehouseLocationDto"];
    }

    private IEnumerable<JToken> EnumerateLocationTokens(JToken item, bool includeItemAsFallback)
    {
        var token = ExtractLocation(item);

        if (token is JArray arr)
        {
            foreach (var t in arr)
            {
                if (t != null && t.Type == JTokenType.Object)
                    yield return t;
            }
            yield break;
        }

        if (token != null && token.Type == JTokenType.Object)
        {
            yield return token;
            yield break;
        }

        if (includeItemAsFallback && item != null && item.Type == JTokenType.Object)
            yield return item;
    }

    private string ReadString(JToken token, params string[] keys)
    {
        if (token == null || keys == null) return null;

        if (token.Type != JTokenType.Object)
            return null;

        var obj = token as JObject;
        if (obj == null)
            return null;

        foreach (var key in keys)
        {
            JToken v = null;
            try
            {
                v = obj[key];
            }
            catch
            {
                v = null;
            }

            if (v == null || v.Type == JTokenType.Null) continue;
            string s = v.Type == JTokenType.String ? v.Value<string>() : v.ToString();
            if (!string.IsNullOrWhiteSpace(s)) return s;
        }
        return null;
    }

    private void SaveRowsCache(List<StorageRowDTO> rows)
    {
        if (rows == null) return;

        try
        {
            var wrapper = new StorageRowsResponseDTO { rows = rows };
            string json = JsonConvert.SerializeObject(wrapper);
            PlayerPrefs.SetString(ItemsCacheKey, json);
            PlayerPrefs.Save();
        }
        catch (Exception e)
        {
            Debug.LogWarning("[StorageRepository] Falha a guardar cache local: " + e.Message);
        }
    }

    private bool TryGetCachedRows(out List<StorageRowDTO> rows)
    {
        rows = null;

        if (!PlayerPrefs.HasKey(ItemsCacheKey))
            return false;

        try
        {
            string json = PlayerPrefs.GetString(ItemsCacheKey, string.Empty);
            if (string.IsNullOrWhiteSpace(json))
                return false;

            var wrapper = JsonConvert.DeserializeObject<StorageRowsResponseDTO>(json);
            if (wrapper?.rows == null)
                return false;

            rows = wrapper.rows;
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning("[StorageRepository] Falha a ler cache local: " + e.Message);
            return false;
        }
    }

    // -----------------------------
    // DTOs
    // -----------------------------

    [Serializable]
    private class WarehouseItemDTO
    {
        public string name;
        public string description;
        public string barcode;

        public WarehouseLocationDTO warehouse_location;

        public string car_model_location;
        public string state;
        public string image_url;

        public int id;
        public int project_id;
        public string car_project_name;

        public int segmentation_session_id;
        public List<string> segmentation_mask_ids;

        public string created_at;
        public string updated_at;
    }

    [Serializable]
    private class WarehouseLocationDTO
    {
        public string section;
        public string shelf;
        public string area;
    }
}
