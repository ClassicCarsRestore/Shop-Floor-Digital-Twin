using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;

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

        List<WarehouseItemDTO> items;
        try
        {
            items = JsonConvert.DeserializeObject<List<WarehouseItemDTO>>(json);
        }
        catch (Exception e)
        {
            string msg = "[StorageRepository] Erro a fazer parse do JSON (/items/): " + e.Message;
            Debug.LogError(msg);
            Debug.LogError("JSON recebido: " + json);
            onError?.Invoke(msg);
            yield break;
        }

        var rows = new List<StorageRowDTO>();
        if (items != null)
        {
            foreach (var it in items)
            {
                if (it == null || it.warehouse_location == null) continue;

                rows.Add(new StorageRowDTO
                {
                    carId = it.project_id.ToString(),
                    location = new StorageLocationDTO
                    {
                        section = it.warehouse_location.section,
                        shelf = it.warehouse_location.shelf,
                        area = it.warehouse_location.area
                    }
                });
            }
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

        List<ProjectLocationDTO> locs;
        try
        {
            locs = JsonConvert.DeserializeObject<List<ProjectLocationDTO>>(json);
        }
        catch (Exception e)
        {
            string msg = "[StorageRepository] Erro a fazer parse do JSON (/projects/{id}/locations): " + e.Message;
            Debug.LogError(msg);
            Debug.LogError("JSON recebido: " + json);
            onError?.Invoke(msg);
            yield break;
        }

        var rows = new List<StorageRowDTO>();
        if (locs != null)
        {
            foreach (var l in locs)
            {
                if (l == null || l.warehouse_location == null) continue;

                rows.Add(new StorageRowDTO
                {
                    // highlight usa carId para bater com o que foi pedido
                    carId = projectId,
                    location = new StorageLocationDTO
                    {
                        section = l.warehouse_location.section,
                        shelf = l.warehouse_location.shelf,
                        area = l.warehouse_location.area
                    }
                });
            }
        }

        Debug.Log($"[StorageRepository] Parsed car locations rows = {rows.Count} (project_id={projectId})");
        onSuccess?.Invoke(rows);
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
