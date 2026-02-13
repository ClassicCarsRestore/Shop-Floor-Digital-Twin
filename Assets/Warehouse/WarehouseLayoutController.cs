using System;
using System.Collections;
using UnityEngine;
using Newtonsoft.Json;

public class WarehouseLayoutController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private WarehouseLayoutRepository layoutRepository;
    [SerializeField] private WarehouseManager warehouseManager;
    [SerializeField] private GameObject sectionPrefabForLayout;

    private WarehouseLayoutDTO lastLoadedLayout;
    private string lastLoadedLayoutJson;

    /// <summary>
    /// Layout carregado da BD (snapshot para Cancel All e deteção de alterações não guardadas).
    /// </summary>
    public WarehouseLayoutDTO LastLoadedLayout => lastLoadedLayout;

    /// <summary>
    /// Indica se o estado atual da cena difere do último layout carregado/guardado (7.3).
    /// </summary>
    public bool HasUnsavedChanges()
    {
        if (warehouseManager == null) return false;
        if (string.IsNullOrEmpty(lastLoadedLayoutJson)) return false;

        var current = WarehouseLayoutSerializer.BuildFromRuntime(warehouseManager);
        try
        {
            string currentJson = JsonConvert.SerializeObject(current);
            return currentJson != lastLoadedLayoutJson;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Carrega o layout da BD, aplica-o e guarda o snapshot. Chama onDone quando terminar (com sucesso ou com layout vazio).
    /// </summary>
    public void LoadLayoutAndThen(Action onDone)
    {
        if (layoutRepository == null || warehouseManager == null)
        {
            Debug.LogWarning("[WarehouseLayoutController] layoutRepository ou warehouseManager não atribuídos.");
            onDone?.Invoke();
            return;
        }

        StartCoroutine(DoLoadLayoutAndThen(onDone));
    }

    private IEnumerator DoLoadLayoutAndThen(Action onDone)
    {
        bool done = false;
        WarehouseLayoutDTO loaded = null;
        string errorMsg = null;

        layoutRepository.GetLayout(
            onSuccess: dto =>
            {
                loaded = dto;
                done = true;
            },
            onError: err =>
            {
                errorMsg = err;
                done = true;
            }
        );

        while (!done)
            yield return null;

        if (errorMsg != null)
        {
            Debug.LogWarning("[WarehouseLayoutController] GetLayout error: " + errorMsg);
            lastLoadedLayout = new WarehouseLayoutDTO { sections = new System.Collections.Generic.List<SectionLayoutDTO>() };
            lastLoadedLayoutJson = JsonConvert.SerializeObject(lastLoadedLayout);
            if (sectionPrefabForLayout != null)
                WarehouseLayoutSerializer.ApplyLayout(lastLoadedLayout, warehouseManager, sectionPrefabForLayout);
        }
        else
        {
            lastLoadedLayout = loaded ?? new WarehouseLayoutDTO { sections = new System.Collections.Generic.List<SectionLayoutDTO>() };
            if (lastLoadedLayout.sections == null)
                lastLoadedLayout.sections = new System.Collections.Generic.List<SectionLayoutDTO>();
            lastLoadedLayoutJson = JsonConvert.SerializeObject(lastLoadedLayout);

            if (sectionPrefabForLayout != null)
                WarehouseLayoutSerializer.ApplyLayout(lastLoadedLayout, warehouseManager, sectionPrefabForLayout);
        }

        onDone?.Invoke();
    }

    public void OnClickSaveToBD()
    {
        if (warehouseManager == null || layoutRepository == null)
        {
            Debug.LogWarning("[WarehouseLayoutController] warehouseManager ou layoutRepository não atribuídos.");
            return;
        }

        var currentLayout = WarehouseLayoutSerializer.BuildFromRuntime(warehouseManager);

        StartCoroutine(layoutRepository.SaveLayout(
            currentLayout,
            onSuccess: () =>
            {
                Debug.Log("[WarehouseLayoutController] Layout guardado na BD.");
                lastLoadedLayout = currentLayout;
                lastLoadedLayoutJson = JsonConvert.SerializeObject(currentLayout);
            },
            onError: err => Debug.LogWarning("[WarehouseLayoutController] SaveLayout error: " + err)
        ));
    }

    public void OnClickCancelAll()
    {
        if (lastLoadedLayout == null)
        {
            Debug.LogWarning("[WarehouseLayoutController] Não há layout carregado para reverter.");
            return;
        }

        if (warehouseManager == null || sectionPrefabForLayout == null)
        {
            Debug.LogWarning("[WarehouseLayoutController] warehouseManager ou sectionPrefabForLayout não atribuídos.");
            return;
        }

        WarehouseLayoutSerializer.ApplyLayout(lastLoadedLayout, warehouseManager, sectionPrefabForLayout);
        Debug.Log("[WarehouseLayoutController] Alterações revertidas (Cancel All).");
    }
}
