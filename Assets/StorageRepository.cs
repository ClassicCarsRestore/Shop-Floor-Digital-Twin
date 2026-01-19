using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;

public class StorageRepository : MonoBehaviour
{
    private string baseUrl = "https://ims-server.raimundobranco.com";

    [Header("Auth")]
    [SerializeField] private string jwtToken;

    public IEnumerator GetAllStorage(Action<List<StorageRowDTO>> onSuccess, Action<string> onError)
    {
        string url = $"{baseUrl}/items/?skip=0&limit=50";
        Debug.Log("[StorageRepository] Token len=" + (jwtToken != null ? jwtToken.Length : 0));
        Debug.Log("[StorageRepository] GET " + url);

        yield return GetItems(url, onSuccess, onError);
    }

    private IEnumerator GetItems(string url, Action<List<StorageRowDTO>> onSuccess, Action<string> onError)
    {
        using var req = UnityWebRequest.Get(url);

        if (string.IsNullOrEmpty(jwtToken))
        {
            string msg = "[StorageRepository] jwtToken vazio.";
            Debug.LogError(msg);
            onError?.Invoke(msg);
            yield break;
        }

        req.SetRequestHeader("Authorization", "Bearer " + jwtToken);
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
        if (rows.Count > 0)
            Debug.Log($"[StorageRepository] First row: sec={rows[0].location.section} shelf={rows[0].location.shelf} area={rows[0].location.area} carId={rows[0].carId}");

        onSuccess?.Invoke(rows);
    }

    // Ignorar por enquanto
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
