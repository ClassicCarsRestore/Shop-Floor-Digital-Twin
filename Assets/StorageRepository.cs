using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;

public class StorageRepository : MonoBehaviour
{
    [Header("API (Reverse Proxy)")]
    [Tooltip("Deixa vazio para usar URL relativa (recomendado em WebGL na VM). Ex: ''")]
    [SerializeField] private string baseUrl = "";

    [Tooltip("Prefixo protegido pelo reverse proxy (Auth via Authentik). Normalmente /inventory")]
    [SerializeField] private string inventoryPrefix = "/inventory";

    [Tooltip("Endpoint do IMS. No Swagger é /items/")]
    [SerializeField] private string itemsPath = "/items/";

    [Header("Paging")]
    [SerializeField] private int skip = 0;
    [SerializeField] private int limit = 50;

    public IEnumerator GetAllStorage(Action<List<StorageRowDTO>> onSuccess, Action<string> onError)
    {
        // exemplo final: /inventory/items/?skip=0&limit=50
        string url = BuildUrl($"{itemsPath}?skip={skip}&limit={limit}");
        Debug.Log("[StorageRepository] GET " + url);

        yield return GetItems(url, onSuccess, onError);
    }

    private string BuildUrl(string pathWithQuery)
    {
        // garante que temos /inventory + /items/... bem formatado
        string prefix = inventoryPrefix ?? "";
        if (!prefix.StartsWith("/")) prefix = "/" + prefix;
        prefix = prefix.TrimEnd('/');

        string path = pathWithQuery ?? "";
        if (!path.StartsWith("/")) path = "/" + path;

        string relative = $"{prefix}{path}"; // /inventory/items/?...

        // se baseUrl estiver vazio -> URL relativa (recomendado)
        if (string.IsNullOrWhiteSpace(baseUrl))
            return relative;

        return baseUrl.TrimEnd('/') + relative;
    }

    private IEnumerator GetItems(string url, Action<List<StorageRowDTO>> onSuccess, Action<string> onError)
    {
        using var req = UnityWebRequest.Get(url);
        req.SetRequestHeader("Accept", "application/json");
        // NÃO meter Authorization (reverse proxy trata da auth)

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            string body = req.downloadHandler != null ? req.downloadHandler.text : "";
            string msg = $"[StorageRepository] API falhou: {req.error} | HTTP={(int)req.responseCode} | Body={body}";
            Debug.LogWarning(msg);
            onError?.Invoke(msg);
            yield break;
        }

        var json = req.downloadHandler.text;

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

    // (mantém como estava)
    public IEnumerator GetStorageForCar(string carId, Action<List<StorageRowDTO>> onSuccess, Action<string> onError)
    {
        return GetAllStorage(
            (allRows) =>
            {
                var filtered = allRows.FindAll(r => r.carId == carId);
                onSuccess?.Invoke(filtered);
            },
            onError
        );
    }

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
