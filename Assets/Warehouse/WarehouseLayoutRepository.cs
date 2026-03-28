using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class WarehouseLayoutRepository : MonoBehaviour
{
    private const string LayoutUrl = "/inventory/warehouse/layout";
    private const string LayoutCacheKey = "warehouse.layout.cache.v1";

    public IEnumerator GetLayout(Action<WarehouseLayoutDTO> onSuccess, Action<string> onError)
    {
        using var req = UnityWebRequest.Get(LayoutUrl);
        req.SetRequestHeader("Accept", "application/json");

        yield return req.SendWebRequest();

        string responseBody = req.downloadHandler != null ? req.downloadHandler.text : "";

        if (req.result != UnityWebRequest.Result.Success)
        {
            string msg = $"[WarehouseLayoutRepository] GET falhou: {req.error} | HTTP={(int)req.responseCode} | Body={responseBody}";
            Debug.LogWarning(msg);
            onError?.Invoke(msg);
            yield break;
        }

        try
        {
            var json = responseBody;
            var dto = TryParseLayout(json);
            SaveLayoutCache(json);
            onSuccess?.Invoke(dto);
        }
        catch (Exception e)
        {
            string msg = "[WarehouseLayoutRepository] Erro a fazer parse do layout: " + e.Message;
            Debug.LogError(msg);
            onError?.Invoke(msg);
        }
    }

    public IEnumerator SaveLayout(WarehouseLayoutDTO layout, Action onSuccess, Action<string> onError)
    {
        if (layout == null)
        {
            onError?.Invoke("[WarehouseLayoutRepository] layout é null.");
            yield break;
        }

        string json = JsonConvert.SerializeObject(layout);
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(json);

        using var req = new UnityWebRequest(LayoutUrl, "PUT");
        req.uploadHandler = new UploadHandlerRaw(bytes);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        req.SetRequestHeader("Accept", "application/json");

        yield return req.SendWebRequest();

        string responseBody = req.downloadHandler != null ? req.downloadHandler.text : "";

        if (req.result != UnityWebRequest.Result.Success)
        {
            string msg = $"[WarehouseLayoutRepository] PUT falhou: {req.error} | HTTP={(int)req.responseCode} | Body={responseBody}";
            Debug.LogWarning(msg);
            onError?.Invoke(msg);
            yield break;
        }

        try
        {
            string canonical = JsonConvert.SerializeObject(layout);
            SaveLayoutCache(canonical);
        }
        catch { }

        onSuccess?.Invoke();
    }

    public bool TryGetCachedLayout(out WarehouseLayoutDTO layout)
    {
        layout = null;

        if (!PlayerPrefs.HasKey(LayoutCacheKey))
            return false;

        try
        {
            string json = PlayerPrefs.GetString(LayoutCacheKey, string.Empty);
            if (string.IsNullOrWhiteSpace(json))
                return false;

            layout = TryParseLayout(json);
            return layout != null;
        }
        catch (Exception e)
        {
            Debug.LogWarning("[WarehouseLayoutRepository] Falha ao ler cache de layout: " + e.Message);
            return false;
        }
    }

    private WarehouseLayoutDTO TryParseLayout(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return EmptyLayout();
        }

        // 1) formato direto esperado: { sections: [...] }
        var direct = JsonConvert.DeserializeObject<WarehouseLayoutDTO>(json);
        if (direct != null && direct.sections != null)
        {
            return direct;
        }

        // 2) formatos comuns com envelope: { layout: {...} } / { data: {...} } / { result: {...} }
        var root = JToken.Parse(json);

        if (root is JObject obj)
        {
            if (obj["sections"] is JArray)
            {
                var dto = obj.ToObject<WarehouseLayoutDTO>();
                return dto ?? EmptyLayout();
            }

            JToken wrapped = obj["layout"] ?? obj["data"] ?? obj["payload"] ?? obj["result"];
            if (wrapped != null)
            {
                if (wrapped["sections"] is JArray)
                {
                    var dto = wrapped.ToObject<WarehouseLayoutDTO>();
                    if (dto != null)
                    {
                        if (dto.sections == null) dto.sections = new System.Collections.Generic.List<SectionLayoutDTO>();
                        return dto;
                    }
                }
            }
        }

        // 3) caso o backend devolva só array de sections
        if (root is JArray arr)
        {
            var sections = arr.ToObject<System.Collections.Generic.List<SectionLayoutDTO>>()
                           ?? new System.Collections.Generic.List<SectionLayoutDTO>();
            return new WarehouseLayoutDTO { sections = sections };
        }

        Debug.LogWarning("[WarehouseLayoutRepository] Formato inesperado no GET layout. A usar layout vazio. JSON=" + json);
        return EmptyLayout();
    }

    private static WarehouseLayoutDTO EmptyLayout()
    {
        return new WarehouseLayoutDTO { sections = new System.Collections.Generic.List<SectionLayoutDTO>() };
    }

    private void SaveLayoutCache(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return;

        try
        {
            PlayerPrefs.SetString(LayoutCacheKey, json);
            PlayerPrefs.Save();
        }
        catch (Exception e)
        {
            Debug.LogWarning("[WarehouseLayoutRepository] Falha ao guardar cache de layout: " + e.Message);
        }
    }
}
