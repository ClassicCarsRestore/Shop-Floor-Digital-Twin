using System;
using System.Collections;
using UnityEngine;
using Newtonsoft.Json;

public class WarehouseLayoutController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private WarehouseLayoutRepository layoutRepository;
    [SerializeField] private StorageRepository storageRepository;
    [SerializeField] private WarehouseManager warehouseManager;
    [SerializeField] private GameObject sectionPrefabForLayout;

    private WarehouseLayoutDTO lastLoadedLayout;
    private string lastLoadedLayoutJson;
    private bool isLoadingLayout;
    private bool isSavingLayout;

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
            string currentJson = JsonConvert.SerializeObject(current, Formatting.None);
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
        Debug.Log("[WarehouseLayoutController][LOAD][REQUESTED]");

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
        Debug.Log("[WarehouseLayoutController][LOAD][START]");
        isLoadingLayout = true;

        bool done = false;
        WarehouseLayoutDTO loaded = null;
        string errorMsg = null;

        StartCoroutine(layoutRepository.GetLayout(
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
        ));

        while (!done)
            yield return null;

        if (errorMsg != null)
        {
            Debug.LogWarning("[WarehouseLayoutController] GetLayout error: " + errorMsg);
            // Em caso de erro de rede/auth no GET, não limpamos a cena à força.
            lastLoadedLayout = WarehouseLayoutSerializer.BuildFromRuntime(warehouseManager);
            lastLoadedLayoutJson = JsonConvert.SerializeObject(lastLoadedLayout, Formatting.None);
            Debug.Log("[WarehouseLayoutController][LOAD][END_WITH_ERROR] snapshot mantido do runtime atual");
        }
        else
        {
            var safeLoaded = loaded ?? new WarehouseLayoutDTO { sections = new System.Collections.Generic.List<SectionLayoutDTO>() };
            if (safeLoaded.sections == null)
                safeLoaded.sections = new System.Collections.Generic.List<SectionLayoutDTO>();

            Debug.Log($"[WarehouseLayoutController][LOAD][DTO] sections={safeLoaded.sections.Count}");

            if (sectionPrefabForLayout != null)
                WarehouseLayoutSerializer.ApplyLayout(safeLoaded, warehouseManager, sectionPrefabForLayout);

            lastLoadedLayout = WarehouseLayoutSerializer.BuildFromRuntime(warehouseManager);
            lastLoadedLayoutJson = JsonConvert.SerializeObject(lastLoadedLayout, Formatting.None);
            int finalSections = (lastLoadedLayout != null && lastLoadedLayout.sections != null) ? lastLoadedLayout.sections.Count : 0;
            Debug.Log($"[WarehouseLayoutController][LOAD][END_OK] sections_aplicadas={finalSections}");
        }

        isLoadingLayout = false;

        onDone?.Invoke();
    }

    public void OnClickSaveToBD()
    {
        Debug.Log("[WarehouseLayoutController][PUT][REQUESTED]");

        if (isSavingLayout)
        {
            Debug.LogWarning("[WarehouseLayoutController] Save em progresso. Ignorado.");
            return;
        }

        if (warehouseManager == null || layoutRepository == null)
        {
            Debug.LogWarning("[WarehouseLayoutController] warehouseManager ou layoutRepository não atribuídos.");
            return;
        }

        StartCoroutine(DoRefreshItemsThenSave());
    }

    private IEnumerator DoRefreshItemsThenSave()
    {
        isSavingLayout = true;

        bool refreshDone = false;
        bool preSyncOk = true;

        if (storageRepository != null)
        {
            Debug.Log("[WarehouseLayoutController][PUT][PRE_SYNC_ITEMS][START]");

            yield return StartCoroutine(storageRepository.GetAllStorage(
                onSuccess: rows =>
                {
                    var safeRows = rows ?? new System.Collections.Generic.List<StorageRowDTO>();
                    warehouseManager.ShowAllStorage(safeRows);
                    Debug.Log($"[WarehouseLayoutController][PUT][PRE_SYNC_ITEMS][OK] rows={safeRows.Count}");
                    refreshDone = true;
                },
                onError: err =>
                {
                    Debug.LogWarning("[WarehouseLayoutController][PUT][PRE_SYNC_ITEMS][ERROR] " + err);
                    preSyncOk = false;
                    refreshDone = true;
                }
            ));
        }
        else
        {
            Debug.LogWarning("[WarehouseLayoutController] storageRepository não atribuído. Save segue sem pré-sync de items.");
            refreshDone = true;
        }

        while (!refreshDone)
            yield return null;

        if (!preSyncOk)
        {
            Debug.LogWarning("[WarehouseLayoutController][PUT][ABORTED] Save cancelado: pré-sync de items falhou.");
            isSavingLayout = false;
            yield break;
        }

        var currentLayout = WarehouseLayoutSerializer.BuildFromRuntime(warehouseManager);
        int sectionCount = currentLayout != null && currentLayout.sections != null ? currentLayout.sections.Count : 0;
        Debug.Log($"[WarehouseLayoutController][PUT][BUILD] sections={sectionCount}");

        bool saveDone = false;

        yield return StartCoroutine(layoutRepository.SaveLayout(
            currentLayout,
            onSuccess: () =>
            {
                Debug.Log("[WarehouseLayoutController] Layout guardado na BD.");

                // snapshot canónico do estado REAL da cena para comparação/cancel estáveis
                lastLoadedLayout = WarehouseLayoutSerializer.BuildFromRuntime(warehouseManager);
                lastLoadedLayoutJson = JsonConvert.SerializeObject(lastLoadedLayout, Formatting.None);
                saveDone = true;
            },
            onError: err =>
            {
                Debug.LogWarning("[WarehouseLayoutController] SaveLayout error: " + err);
                saveDone = true;
            }
        ));

        while (!saveDone)
            yield return null;

        isSavingLayout = false;
    }

    public void OnClickCancelAll()
    {
        if (isLoadingLayout)
        {
            Debug.LogWarning("[WarehouseLayoutController] Ainda a carregar layout. Tenta novamente em 1-2 segundos.");
            return;
        }

        if (lastLoadedLayout == null)
        {
            Debug.LogWarning("[WarehouseLayoutController] Não há layout carregado para reverter.");
            return;
        }

        if (!HasUnsavedChanges())
        {
            Debug.Log("[WarehouseLayoutController] Sem alterações por guardar. Cancel All ignorado.");
            return;
        }

        if (warehouseManager == null || sectionPrefabForLayout == null)
        {
            Debug.LogWarning("[WarehouseLayoutController] warehouseManager ou sectionPrefabForLayout não atribuídos.");
            return;
        }

        WarehouseLayoutSerializer.ApplyLayout(lastLoadedLayout, warehouseManager, sectionPrefabForLayout);
        lastLoadedLayout = WarehouseLayoutSerializer.BuildFromRuntime(warehouseManager);
        lastLoadedLayoutJson = JsonConvert.SerializeObject(lastLoadedLayout, Formatting.None);
        Debug.Log("[WarehouseLayoutController] Alterações revertidas (Cancel All).");
    }
}
