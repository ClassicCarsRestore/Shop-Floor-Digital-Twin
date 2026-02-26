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
            string msg = "[StorageRepository] Erro a fazer parse do JSON (/items/): " + e.Message;
            Debug.LogError(msg);
            Debug.LogError("JSON recebido: " + json);
            onError?.Invoke(msg);
            yield break;
        }

        Debug.Log($"[StorageRepository] Parsed rows = {rows.Count}");
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
            string msg = "[StorageRepository] Erro a fazer parse do JSON (/projects/{id}/locations): " + e.Message;
            Debug.LogError(msg);
            Debug.LogError("JSON recebido: " + json);
            onError?.Invoke(msg);
            yield break;
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
            var loc = ExtractLocation(item);
            if (loc == null)
            {
                skipped++;
                continue;
            }

            string section = ReadString(loc, "section", "sectionId", "section_id");
            string shelf = ReadString(loc, "shelf", "shelfId", "shelf_id");
            string area = ReadString(loc, "area", "areaId", "area_id");

            if (string.IsNullOrWhiteSpace(section) || string.IsNullOrWhiteSpace(shelf) || string.IsNullOrWhiteSpace(area))
            {
                skipped++;
                continue;
            }

            string carId = ReadString(item, "project_id", "projectId", "carId", "car_id");
            if (string.IsNullOrWhiteSpace(carId)) carId = "unknown";

            rows.Add(new StorageRowDTO
            {
                carId = carId,
                location = new StorageLocationDTO
                {
                    section = section,
                    shelf = shelf,
                    area = area
                }
            });
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
            var loc = ExtractLocation(item) ?? item;

            string section = ReadString(loc, "section", "sectionId", "section_id");
            string shelf = ReadString(loc, "shelf", "shelfId", "shelf_id");
            string area = ReadString(loc, "area", "areaId", "area_id");

            if (string.IsNullOrWhiteSpace(section) || string.IsNullOrWhiteSpace(shelf) || string.IsNullOrWhiteSpace(area))
            {
                skipped++;
                continue;
            }

            rows.Add(new StorageRowDTO
            {
                carId = projectId,
                location = new StorageLocationDTO
                {
                    section = section,
                    shelf = shelf,
                    area = area
                }
            });
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

    private string ReadString(JToken token, params string[] keys)
    {
        if (token == null || keys == null) return null;
        foreach (var key in keys)
        {
            var v = token[key];
            if (v == null || v.Type == JTokenType.Null) continue;
            string s = v.Type == JTokenType.String ? v.Value<string>() : v.ToString();
            if (!string.IsNullOrWhiteSpace(s)) return s;
        }
        return null;
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
