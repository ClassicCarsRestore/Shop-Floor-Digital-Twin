using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Newtonsoft.Json;

public class WarehouseLayoutController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private WarehouseLayoutRepository layoutRepository;
    [SerializeField] private StorageRepository storageRepository;
    [SerializeField] private WarehouseManager warehouseManager;
    [SerializeField] private GameObject sectionPrefabForLayout;
    [SerializeField] private Button saveChangesButton;
    [SerializeField] private Button cancelChangesButton;

    private WarehouseLayoutDTO lastLoadedLayout;
    private string lastLoadedLayoutJson;
    private bool isLoadingLayout;
    private bool isSavingLayout;
    private bool isCancellingLayout;
    private float nextButtonsRefreshTime;
    private const float ButtonsRefreshInterval = 0.25f;

    private void OnEnable()
    {
        RefreshActionButtons(true);
    }

    private void Update()
    {
        if (Time.unscaledTime < nextButtonsRefreshTime)
            return;

        nextButtonsRefreshTime = Time.unscaledTime + ButtonsRefreshInterval;
        RefreshActionButtons();
    }

    private void RefreshActionButtons(bool force = false)
    {
        bool hasChanges = HasUnsavedChanges();
        bool canClick = hasChanges && !isLoadingLayout && !isSavingLayout && !isCancellingLayout;

        SetButtonInteractable(saveChangesButton, canClick, force);
        SetButtonInteractable(cancelChangesButton, canClick, force);
    }

    private static void SetButtonInteractable(Button button, bool interactable, bool force = false)
    {
        if (button == null)
            return;

        if (force || button.interactable != interactable)
            button.interactable = interactable;
    }

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
        if (layoutRepository == null || warehouseManager == null)
        {
            Debug.LogWarning("[WarehouseLayoutController] layoutRepository ou warehouseManager não atribuídos.");
            RefreshActionButtons();
            onDone?.Invoke();
            return;
        }

        StartCoroutine(DoLoadLayoutAndThen(onDone));
    }

    private IEnumerator DoLoadLayoutAndThen(Action onDone)
    {
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

            if (layoutRepository != null && layoutRepository.TryGetCachedLayout(out var cachedLayout) && cachedLayout != null)
            {
                if (cachedLayout.sections == null)
                    cachedLayout.sections = new System.Collections.Generic.List<SectionLayoutDTO>();

                if (sectionPrefabForLayout != null)
                    WarehouseLayoutSerializer.ApplyLayout(cachedLayout, warehouseManager, sectionPrefabForLayout);

                lastLoadedLayout = WarehouseLayoutSerializer.BuildFromRuntime(warehouseManager);
                lastLoadedLayoutJson = JsonConvert.SerializeObject(lastLoadedLayout, Formatting.None);
            }
            else
            {
                // Se não houver cache, mantém estado atual para evitar limpar a cena.
                lastLoadedLayout = WarehouseLayoutSerializer.BuildFromRuntime(warehouseManager);
                lastLoadedLayoutJson = JsonConvert.SerializeObject(lastLoadedLayout, Formatting.None);
            }
        }
        else
        {
            var safeLoaded = loaded ?? new WarehouseLayoutDTO { sections = new System.Collections.Generic.List<SectionLayoutDTO>() };
            if (safeLoaded.sections == null)
                safeLoaded.sections = new System.Collections.Generic.List<SectionLayoutDTO>();

            if (sectionPrefabForLayout != null)
                WarehouseLayoutSerializer.ApplyLayout(safeLoaded, warehouseManager, sectionPrefabForLayout);

            lastLoadedLayout = WarehouseLayoutSerializer.BuildFromRuntime(warehouseManager);
            lastLoadedLayoutJson = JsonConvert.SerializeObject(lastLoadedLayout, Formatting.None);
        }

        isLoadingLayout = false;
        RefreshActionButtons();

        onDone?.Invoke();
    }

    public void OnClickSaveToBD()
    {
        if (!HasUnsavedChanges())
        {
            RefreshActionButtons();
            return;
        }

        if (isSavingLayout)
        {
            Debug.LogWarning("[WarehouseLayoutController] Save em progresso. Ignorado.");
            RefreshActionButtons();
            return;
        }

        if (warehouseManager == null || layoutRepository == null)
        {
            Debug.LogWarning("[WarehouseLayoutController] warehouseManager ou layoutRepository não atribuídos.");
            RefreshActionButtons();
            return;
        }

        isSavingLayout = true;
        RefreshActionButtons(true);
        StartCoroutine(DoRefreshItemsThenSave());
    }

    private IEnumerator DoRefreshItemsThenSave()
    {
        bool refreshDone = false;
        bool preSyncOk = true;

        if (storageRepository != null)
        {
            yield return StartCoroutine(storageRepository.GetAllStorage(
                onSuccess: rows =>
                {
                    var safeRows = rows ?? new System.Collections.Generic.List<StorageRowDTO>();
                    warehouseManager.ShowAllStorage(safeRows);
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
            RefreshActionButtons();
            yield break;
        }

        var currentLayout = WarehouseLayoutSerializer.BuildFromRuntime(warehouseManager);
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
        RefreshActionButtons();
    }

    public void OnClickCancelAll()
    {
        if (isCancellingLayout)
        {
            Debug.LogWarning("[WarehouseLayoutController] Cancel em progresso. Ignorado.");
            RefreshActionButtons();
            return;
        }

        if (isLoadingLayout)
        {
            Debug.LogWarning("[WarehouseLayoutController] Ainda a carregar layout. Tenta novamente em 1-2 segundos.");
            RefreshActionButtons();
            return;
        }

        if (lastLoadedLayout == null)
        {
            Debug.LogWarning("[WarehouseLayoutController] Não há layout carregado para reverter.");
            RefreshActionButtons();
            return;
        }

        if (!HasUnsavedChanges())
        {
            RefreshActionButtons();
            return;
        }

        if (warehouseManager == null || sectionPrefabForLayout == null)
        {
            Debug.LogWarning("[WarehouseLayoutController] warehouseManager ou sectionPrefabForLayout não atribuídos.");
            RefreshActionButtons();
            return;
        }

        isCancellingLayout = true;
        RefreshActionButtons(true);
        StartCoroutine(DoCancelAllPreserveBoxes());
    }

    private IEnumerator DoCancelAllPreserveBoxes()
    {
        System.Collections.Generic.List<StorageRowDTO> rowsSnapshot = null;
        bool storageDone = false;

        if (storageRepository != null)
        {
            yield return StartCoroutine(storageRepository.GetAllStorage(
                onSuccess: rows =>
                {
                    rowsSnapshot = rows ?? new System.Collections.Generic.List<StorageRowDTO>();
                    storageDone = true;
                },
                onError: err =>
                {
                    Debug.LogWarning("[WarehouseLayoutController][CANCEL][PRESERVE_BOXES][FETCH_ROWS_ERROR] " + err);
                    storageDone = true;
                }
            ));
        }
        else
        {
            storageDone = true;
            Debug.LogWarning("[WarehouseLayoutController][CANCEL][PRESERVE_BOXES] storageRepository não atribuído; sem snapshot de boxes.");
        }

        while (!storageDone)
            yield return null;

        WarehouseLayoutSerializer.ApplyLayout(lastLoadedLayout, warehouseManager, sectionPrefabForLayout);
        lastLoadedLayout = WarehouseLayoutSerializer.BuildFromRuntime(warehouseManager);
        lastLoadedLayoutJson = JsonConvert.SerializeObject(lastLoadedLayout, Formatting.None);

        if (rowsSnapshot != null)
        {
            warehouseManager.ShowAllStorage(rowsSnapshot);
        }

        isCancellingLayout = false;
        RefreshActionButtons();
        Debug.Log("[WarehouseLayoutController] Alterações revertidas (Cancel All).");
    }
}
