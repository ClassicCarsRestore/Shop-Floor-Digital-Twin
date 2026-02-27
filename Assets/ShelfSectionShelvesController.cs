using System.Linq;
using UnityEngine;

public class ShelfSectionShelvesController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Transform shelvesRoot;
    [SerializeField] private GameObject shelfUnitPrefab;

    [Header("Rules")]
    [SerializeField] private int minShelves = 2;

    [Header("Defaults")]
    [SerializeField] private int defaultAreasPerShelf = 6;

    private ShelfSection section;

    private void Awake()
    {
        section = GetComponent<ShelfSection>();
        if (section == null)
            Debug.LogError("[ShelfSectionShelvesController] Não existe ShelfSection no root.");
    }

    public void AddShelf()
    {
        if (shelvesRoot == null)
        {
            Debug.LogError("[ShelfSectionShelvesController] shelvesRoot não atribuído.");
            return;
        }
        if (shelfUnitPrefab == null)
        {
            Debug.LogError("[ShelfSectionShelvesController] shelfUnitPrefab não atribuído.");
            return;
        }
        if (section == null)
        {
            Debug.LogError("[ShelfSectionShelvesController] section é null.");
            return;
        }
        if (string.IsNullOrEmpty(section.SectionId))
        {
            Debug.LogWarning("[ShelfSectionShelvesController] SectionId vazio. Atribui primeiro antes de criar shelves.");
        }

        float topY = GetCurrentTopY(shelvesRoot);

        GameObject unit = Instantiate(shelfUnitPrefab, shelvesRoot);
        unit.name = $"ShelfUnit_{(section.Shelves.Count + 1)}";
        unit.transform.localScale = Vector3.one;

        Bounds unitBounds = CalculateWorldBounds(unit.transform);
        float deltaY = topY - unitBounds.min.y;
        unit.transform.position += new Vector3(0f, deltaY, 0f);

        // reconstruir lista de shelves + IDs
        RebuildShelvesFromHierarchy();

        // criar áreas default na shelf nova
        var newShelf = section.Shelves.Count > 0 ? section.Shelves[section.Shelves.Count - 1] : null;
        if (newShelf != null)
        {
            int shelfIndex = section.Shelves.Count; // última shelf = count
            ShelfAreasBuilder.RebuildAreas(newShelf, defaultAreasPerShelf, section.SectionId, shelfIndex);
        }

        RefreshHighlight();
    }

    public void RemoveShelf()
    {
        if (shelvesRoot == null)
        {
            Debug.LogError("[ShelfSectionShelvesController] shelvesRoot não atribuído.");
            return;
        }

        int shelfCount = CountShelvesInHierarchy();
        if (shelfCount <= minShelves)
        {
            Debug.Log("[ShelfSectionShelvesController] Min shelves atingido, não remove.");
            return;
        }

        if (shelvesRoot.childCount <= 0)
        {
            Debug.LogWarning("[ShelfSectionShelvesController] Não há ShelfUnits para remover.");
            return;
        }

        var shelvesOrdered = shelvesRoot.GetComponentsInChildren<Shelf>(true)
                                      .OrderBy(s => s.transform.position.y)
                                      .ToList();
        if (shelvesOrdered.Count == 0)
        {
            Debug.LogWarning("[ShelfSectionShelvesController] Não foram encontradas shelves válidas para remover.");
            return;
        }

        var topShelf = shelvesOrdered[shelvesOrdered.Count - 1];
        if (IsShelfOccupied(topShelf))
        {
            Debug.LogWarning("[ShelfSectionShelvesController] Não é possível remover shelf com caixas/áreas ocupadas.");
            return;
        }

        Transform lastContainer = shelvesRoot.GetChild(shelvesRoot.childCount - 1);
        Destroy(lastContainer.gameObject);

        StartCoroutine(RebuildEndOfFrame());
    }

    private System.Collections.IEnumerator RebuildEndOfFrame()
    {
        yield return new WaitForEndOfFrame();
        RebuildShelvesFromHierarchy();
    }

    private void RebuildShelvesFromHierarchy()
    {
        if (section == null) return;
        if (shelvesRoot == null) return;

        section.Shelves.Clear();

        var shelves = shelvesRoot.GetComponentsInChildren<Shelf>(true)
                                 .OrderBy(s => s.transform.position.y)
                                 .ToList();

        for (int i = 0; i < shelves.Count; i++)
        {
            var sh = shelves[i];
            if (sh == null) continue;

            int shelfIndex = i + 1;

            sh.ShelfId = $"{section.SectionId}-{shelfIndex}";

            // IMPORTANTE: isto só "renomeia" ids existentes; não cria áreas.
            if (sh.Areas != null)
            {
                for (int a = 0; a < sh.Areas.Count; a++)
                {
                    if (sh.Areas[a] != null)
                        sh.Areas[a].AreaId = $"{section.SectionId}-{shelfIndex}-{a + 1}";
                }
            }

            section.Shelves.Add(sh);
        }

        Debug.Log($"[ShelfSectionShelvesController] Rebuild ok. Shelves={section.Shelves.Count}");
    }

    public void RebuildShelves() => RebuildShelvesFromHierarchy();

    private int CountShelvesInHierarchy()
    {
        if (shelvesRoot == null) return 0;
        return shelvesRoot.GetComponentsInChildren<Shelf>(true).Length;
    }

    private void RefreshHighlight()
    {
        var ht = GetComponent<HighlightTarget>();
        if (ht != null)
            ht.RefreshRenderers();
    }

    private float GetCurrentTopY(Transform root)
    {
        Bounds b = CalculateWorldBounds(root);
        return b.max.y;
    }

    private Bounds CalculateWorldBounds(Transform root)
    {
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
            return new Bounds(root.position, Vector3.zero);

        bool has = false;
        Bounds bounds = default;

        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null) continue;
            if (ShouldIgnoreForStackingBounds(r.transform, root)) continue;

            if (!has)
            {
                bounds = r.bounds;
                has = true;
            }
            else
            {
                bounds.Encapsulate(r.bounds);
            }
        }

        if (!has)
            return new Bounds(root.position, Vector3.zero);

        return bounds;
    }

    private static bool ShouldIgnoreForStackingBounds(Transform candidate, Transform root)
    {
        if (candidate == null || root == null) return false;

        if (candidate.GetComponent<SkinnedMeshRenderer>() != null)
            return true;

        for (Transform t = candidate; t != null; t = t.parent)
        {
            if (t.GetComponent<StorageBox>() != null)
                return true;

            if (t.GetComponent<StorageArea>() != null)
                return true;

            if (t == root)
                break;
        }

        return false;
    }

    private static bool IsShelfOccupied(Shelf shelf)
    {
        if (shelf == null || shelf.Areas == null) return false;

        foreach (var area in shelf.Areas)
        {
            if (area == null) continue;

            if (area.IsOccupied())
                return true;

            var hasBox = area.GetComponentInChildren<StorageBox>(true) != null;
            if (hasBox)
                return true;
        }

        return false;
    }
}