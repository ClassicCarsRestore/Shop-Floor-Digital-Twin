using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;

public class WarehouseLayoutRepository : MonoBehaviour
{
    private const string LayoutUrl = "/inventory/warehouse/layout";

    public IEnumerator GetLayout(Action<WarehouseLayoutDTO> onSuccess, Action<string> onError)
    {
        using var req = UnityWebRequest.Get(LayoutUrl);
        req.SetRequestHeader("Accept", "application/json");

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            string body = req.downloadHandler != null ? req.downloadHandler.text : "";
            string msg = $"[WarehouseLayoutRepository] GET falhou: {req.error} | HTTP={(int)req.responseCode} | Body={body}";
            Debug.LogWarning(msg);
            onError?.Invoke(msg);
            yield break;
        }

        try
        {
            var json = req.downloadHandler.text;
            var dto = JsonConvert.DeserializeObject<WarehouseLayoutDTO>(json);
            if (dto == null)
                dto = new WarehouseLayoutDTO { sections = new System.Collections.Generic.List<SectionLayoutDTO>() };
            if (dto.sections == null)
                dto.sections = new System.Collections.Generic.List<SectionLayoutDTO>();
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

        if (req.result != UnityWebRequest.Result.Success)
        {
            string body = req.downloadHandler != null ? req.downloadHandler.text : "";
            string msg = $"[WarehouseLayoutRepository] PUT falhou: {req.error} | HTTP={(int)req.responseCode} | Body={body}";
            Debug.LogWarning(msg);
            onError?.Invoke(msg);
            yield break;
        }

        onSuccess?.Invoke();
    }
}
