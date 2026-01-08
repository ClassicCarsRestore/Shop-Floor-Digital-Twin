using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class StorageRepository : MonoBehaviour
{
    [Header("API")]
    [SerializeField] private string baseUrl = "https://api-teu-endpoint";

    [Header("Dev / Offline")]
    [SerializeField] private bool useMockData = false;

    public IEnumerator GetAllStorage(Action<List<StorageRowDTO>> onSuccess, Action<string> onError)
    {
        if (useMockData)
        {
            onSuccess?.Invoke(Mock_AllRows());
            yield break;
        }

        string url = $"{baseUrl}/storage"; // endpoint ALL
        yield return GetRows(url, onSuccess, onError, fallback: Mock_AllRows);
    }

    public IEnumerator GetStorageForCar(string carId, Action<List<StorageRowDTO>> onSuccess, Action<string> onError)
    {
        if (useMockData)
        {
            onSuccess?.Invoke(Mock_RowsForCar(carId));
            yield break;
        }

        string url = $"{baseUrl}/storage?carId={carId}"; // endpoint por carro
        yield return GetRows(url, onSuccess, onError, fallback: () => Mock_RowsForCar(carId));
    }

    private IEnumerator GetRows(
        string url,
        Action<List<StorageRowDTO>> onSuccess,
        Action<string> onError,
        Func<List<StorageRowDTO>> fallback)
    {
        using var req = UnityWebRequest.Get(url);
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning($"[StorageRepository] API falhou: {req.error}. A usar mock.");
            onError?.Invoke(req.error);

            // fallback para não bloquear desenvolvimento
            onSuccess?.Invoke(fallback != null ? fallback() : new List<StorageRowDTO>());
            yield break;
        }

        var json = req.downloadHandler.text;

        // Espera: { "rows": [...] }
        var response = JsonUtility.FromJson<StorageRowsResponseDTO>(json);
        onSuccess?.Invoke(response?.rows ?? new List<StorageRowDTO>());
    }

    // -----------------------
    // MOCK DATA (sem BD)
    // -----------------------
    private List<StorageRowDTO> Mock_AllRows()
    {
        // Ajusta para bater com o teu armazém real:
        // 10 sections, 3 shelves, 4 areas
        var rows = new List<StorageRowDTO>();
        string[] carIds = { "CAR_A", "CAR_B", "CAR_C" };

        for (int section = 1; section <= 10; section++)
        {
            for (int shelf = 1; shelf <= 3; shelf++)
            {
                for (int area = 1; area <= 4; area++)
                {
                    rows.Add(new StorageRowDTO
                    {
                        carId = carIds[UnityEngine.Random.Range(0, carIds.Length)],
                        location = new StorageLocationDTO
                        {
                            section = section.ToString(),
                            shelf = shelf.ToString(),
                            area = area.ToString()
                        }
                    });
                }
            }
        }

        return rows;
    }

    private List<StorageRowDTO> Mock_RowsForCar(string carId)
    {
        // Usa o ALL e filtra, para garantir coerência
        var all = Mock_AllRows();
        var filtered = new List<StorageRowDTO>();
        foreach (var r in all)
            if (r != null && r.carId == carId)
                filtered.Add(r);

        // se por acaso vier vazio, força umas quantas
        if (filtered.Count == 0 && all.Count > 0)
        {
            for (int i = 0; i < 5 && i < all.Count; i++)
            {
                all[i].carId = carId;
                filtered.Add(all[i]);
            }
        }

        return filtered;
    }
}
