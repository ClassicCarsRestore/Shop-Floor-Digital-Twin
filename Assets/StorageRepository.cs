using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class StorageRepository : MonoBehaviour
{
    // Chamada relativa ao teu site -> Caddy -> Authentik -> reverse_proxy
    private const string ItemsUrl = "/inventory/items/?skip=0&limit=50";
    private const string ItemsCacheKey = "warehouse.storage.rows.cache.v1";

    public IEnumerator GetAllStorage(Action<List<StorageRowDTO>> onSuccess, Action<string> onError)
    {
        Debug.Log("[StorageRepository] GET " + ItemsUrl);
        yield return GetItems(ItemsUrl, onSuccess, onError);
    }

    // ✅ AGORA: endpoint leve por project_id (carro)
    // Swagger: GET /projects/{project_id}/locations
    public IEnumerator GetStorageForCar(string carId, Action<List<StorageRowDTO>> onSuccess, Action<string> onError)
    {
        if (string.IsNullOrWhiteSpace(carId))
        {
            onError?.Invoke("[StorageRepository] carId vazio.");
            yield break;
        }

        string url = $"/inventory/projects/{UnityWebRequest.EscapeURL(carId)}/locations";
        Debug.Log("[StorageRepository] GET " + url);

        yield return GetProjectLocations(url, carId, onSuccess, onError);
    }

    // -----------------------------
    // Internals
    // -----------------------------

    private IEnumerator GetItems(string url, Action<List<StorageRowDTO>> onSuccess, Action<string> onError)
    {
        Debug.Log("[StorageRepository][GetItems][START] " + url);

        using var req = UnityWebRequest.Get(url);
        req.SetRequestHeader("Accept", "application/json");


        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            string body = req.downloadHandler != null ? req.downloadHandler.text : "";
            string msg = $"[StorageRepository] API falhou: {req.error} | HTTP={(int)req.responseCode} | Body={body}";
            Debug.LogWarning(msg);

            if (TryGetCachedRows(out var cachedRows))
            {
                Debug.LogWarning($"[StorageRepository] A usar cache local de items (rows={cachedRows.Count}).");
                onSuccess?.Invoke(cachedRows);
                yield break;
            }

            onError?.Invoke(msg);
            yield break;
        }

        string json = req.downloadHandler.text;
        Debug.Log($"[StorageRepository][GetItems][END] result={req.result} http={(int)req.responseCode}");
        Debug.Log("[StorageRepository][GetItems][RESPONSE_JSON_RAW] " + json);

        List<StorageRowDTO> rows;
        try
        {
            rows = ParseRowsFromItemsJson(json);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[StorageRepository] Erro a fazer parse do JSON (/items/): " + e.Message);
            Debug.LogWarning("JSON recebido: " + json);

            if (TryGetCachedRows(out var cachedRows))
            {
                Debug.LogWarning($"[StorageRepository] Parse falhou, a usar cache local (rows={cachedRows.Count}).");
                rows = cachedRows;
            }
            else
            {
                rows = new List<StorageRowDTO>();
            }
        }

        Debug.Log($"[StorageRepository] Parsed rows = {rows.Count}");
        SaveRowsCache(rows);
        onSuccess?.Invoke(rows);
    }

    private IEnumerator GetProjectLocations(
        string url,
        string projectId,
        Action<List<StorageRowDTO>> onSuccess,
        Action<string> onError)
    {
        Debug.Log("[StorageRepository][GetProjectLocations][START] " + url + " projectId=" + projectId);

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
        Debug.Log($"[StorageRepository][GetProjectLocations][END] result={req.result} http={(int)req.responseCode}");
        Debug.Log("[StorageRepository][GetProjectLocations][RESPONSE_JSON_RAW] " + json);

        List<StorageRowDTO> rows;
        try
        {
            rows = ParseRowsFromProjectLocationsJson(json, projectId);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[StorageRepository] Erro a fazer parse do JSON (/projects/{id}/locations): " + e.Message);
            Debug.LogWarning("[StorageRepository] A continuar com rows vazias para não bloquear o fluxo.");
            Debug.LogWarning("JSON recebido: " + json);
            rows = new List<StorageRowDTO>();
        }

        Debug.Log($"[StorageRepository] Parsed car locations rows = {rows.Count} (project_id={projectId})");
        onSuccess?.Invoke(rows);
    }

    private List<StorageRowDTO> ParseRowsFromItemsJson(string json)
    {
        var rows = new List<StorageRowDTO>();
        int skipped = 0;

        foreach (var item in EnumeratePayloadArray(json, "items", "data", "results", "payload", "value"))
        {
            string carId = ReadString(item, "project_id", "projectId", "carId", "car_id");
            if (string.IsNullOrWhiteSpace(carId)) carId = "unknown";
            string itemId = ReadString(item, "id", "itemId", "item_id", "barcode", "name");
            if (string.IsNullOrWhiteSpace(itemId)) itemId = "unknown-item";

            bool mappedAny = false;
            foreach (var loc in EnumerateLocationTokens(item, includeItemAsFallback: false))
            {
                string section = ReadString(loc, "section", "sectionId", "section_id");
                string shelf = ReadString(loc, "shelf", "shelfId", "shelf_id");
                string area = ReadString(loc, "area", "areaId", "area_id");

                if (string.IsNullOrWhiteSpace(section) || string.IsNullOrWhiteSpace(shelf) || string.IsNullOrWhiteSpace(area))
                    continue;

                rows.Add(new StorageRowDTO
                {
                    itemId = itemId,
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

        Debug.Log($"[StorageRepository][GetItems][MAP] mapped={rows.Count} skipped={skipped}");
        return rows;
    }

    private List<StorageRowDTO> ParseRowsFromProjectLocationsJson(string json, string projectId)
    {
        var rows = new List<StorageRowDTO>();
        int skipped = 0;

        foreach (var item in EnumeratePayloadArray(json, "locations", "items", "data", "results", "payload", "value"))
        {
            bool mappedAny = false;
            string itemId = ReadString(item, "id", "itemId", "item_id", "barcode", "name");
            if (string.IsNullOrWhiteSpace(itemId)) itemId = "unknown-item";
            foreach (var loc in EnumerateLocationTokens(item, includeItemAsFallback: true))
            {
                string section = ReadString(loc, "section", "sectionId", "section_id");
                string shelf = ReadString(loc, "shelf", "shelfId", "shelf_id");
                string area = ReadString(loc, "area", "areaId", "area_id");

                if (string.IsNullOrWhiteSpace(section) || string.IsNullOrWhiteSpace(shelf) || string.IsNullOrWhiteSpace(area))
                    continue;

                rows.Add(new StorageRowDTO
                {
                    itemId = itemId,
                    carId = projectId,
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

        Debug.Log($"[StorageRepository][GetProjectLocations][MAP] mapped={rows.Count} skipped={skipped}");
        return rows;
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

    // DTO do endpoint /projects/{id}/locations (exemplo do swagger)
    [Serializable]
    private class ProjectLocationDTO
    {
        public int id;
        public string name;
        public WarehouseLocationDTO warehouse_location;
    }

    [Serializable]
    private class WarehouseLocationDTO
    {
        public string section;
        public string shelf;
        public string area;
    }
}
