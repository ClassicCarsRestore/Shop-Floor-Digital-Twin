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
        Debug.Log("[WarehouseLayoutRepository][GET][START] url=" + LayoutUrl);

        using var req = UnityWebRequest.Get(LayoutUrl);
        req.SetRequestHeader("Accept", "application/json");

        yield return req.SendWebRequest();

        string responseBody = req.downloadHandler != null ? req.downloadHandler.text : "";
        Debug.Log($"[WarehouseLayoutRepository][GET][END] result={req.result} http={(int)req.responseCode} error={req.error}");
        Debug.Log("[WarehouseLayoutRepository][GET][RESPONSE_JSON_RAW] " + responseBody);

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
            int sectionCount = dto?.sections != null ? dto.sections.Count : 0;
            Debug.Log($"[WarehouseLayoutRepository][GET][PARSE_OK] sections={sectionCount}");
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
        int sectionCount = layout.sections != null ? layout.sections.Count : 0;
        Debug.Log($"[WarehouseLayoutRepository][PUT][START] url={LayoutUrl} sections={sectionCount}");
        Debug.Log("[WarehouseLayoutRepository][PUT][REQUEST_JSON_RAW] " + json);
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(json);

        using var req = new UnityWebRequest(LayoutUrl, "PUT");
        req.uploadHandler = new UploadHandlerRaw(bytes);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        req.SetRequestHeader("Accept", "application/json");

        yield return req.SendWebRequest();

        string responseBody = req.downloadHandler != null ? req.downloadHandler.text : "";
        Debug.Log($"[WarehouseLayoutRepository][PUT][END] result={req.result} http={(int)req.responseCode} error={req.error}");
        Debug.Log("[WarehouseLayoutRepository][PUT][RESPONSE_RAW] " + responseBody);

        if (req.result != UnityWebRequest.Result.Success)
        {
            string msg = $"[WarehouseLayoutRepository] PUT falhou: {req.error} | HTTP={(int)req.responseCode} | Body={responseBody}";
            Debug.LogWarning(msg);
            onError?.Invoke(msg);
            yield break;
        }

        Debug.Log("[WarehouseLayoutRepository][PUT][SUCCESS]");

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
            Debug.LogWarning("[WarehouseLayoutRepository][GET][PARSE_EMPTY] body vazio/null -> layout vazio");
            return EmptyLayout();
        }

        // 1) formato direto esperado: { sections: [...] }
        var direct = JsonConvert.DeserializeObject<WarehouseLayoutDTO>(json);
        if (direct != null && direct.sections != null)
        {
            Debug.Log($"[WarehouseLayoutRepository][GET][PARSE_DIRECT] sections={direct.sections.Count}");
            return direct;
        }

        // 2) formatos comuns com envelope: { layout: {...} } / { data: {...} } / { result: {...} }
        var root = JToken.Parse(json);

        if (root is JObject obj)
        {
            if (obj["sections"] is JArray)
            {
                var dto = obj.ToObject<WarehouseLayoutDTO>();
                int c = dto?.sections != null ? dto.sections.Count : 0;
                Debug.Log($"[WarehouseLayoutRepository][GET][PARSE_OBJECT_SECTIONS] sections={c}");
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
                        Debug.Log($"[WarehouseLayoutRepository][GET][PARSE_WRAPPED] sections={dto.sections.Count}");
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
            Debug.Log($"[WarehouseLayoutRepository][GET][PARSE_ARRAY] sections={sections.Count}");
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
