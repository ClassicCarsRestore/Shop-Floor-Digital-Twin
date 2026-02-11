using System.Linq;
using UnityEngine;

public class ShelfSectionShelvesController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Transform shelvesRoot;
    [SerializeField] private GameObject shelfUnitPrefab;

    [Header("Rules")]
    [SerializeField] private int minShelves = 2;

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

        // 1) topo atual (bounds de tudo dentro do ShelvesRoot)
        float topY = GetCurrentTopY(shelvesRoot);

        // 2) instanciar unit como filha (vai herdar escala da section)
        GameObject unit = Instantiate(shelfUnitPrefab, shelvesRoot);
        // renomeia a unit
        unit.name = $"ShelfUnit_{(section.Shelves.Count + 1)}";
        // segurança para não trazer scale marada dentro da unit
        unit.transform.localScale = Vector3.one;

        // 3) alinhar a base da unit com o topo atual
        Bounds unitBounds = CalculateWorldBounds(unit.transform);
        float deltaY = topY - unitBounds.min.y;
        unit.transform.position += new Vector3(0f, deltaY, 0f);

      


        // 4) reconstruir lista de shelves + IDs
        RebuildShelvesFromHierarchy();

        // 4.5) criar áreas default na shelf nova
        var newShelf = section.Shelves.Count > 0 ? section.Shelves[section.Shelves.Count - 1] : null;
        if (newShelf != null)
        {
            // escolhe o default que queres (ex.: 6)
            ShelfAreasBuilder.RebuildAreas(newShelf, 6);
        }


        // 5) garantir highlight inclui novos renderers
        RefreshHighlight();
    }

    public void RemoveShelf()
    {
        if (shelvesRoot == null)
        {
            Debug.LogError("[ShelfSectionShelvesController] shelvesRoot não atribuído.");
            return;
        }

        // Conta as shelves na hierarquia
        int shelfCount = CountShelvesInHierarchy();
        if (shelfCount <= minShelves)
        {
            Debug.Log("[ShelfSectionShelvesController] Min shelves atingido, não remove.");
            return;
        }

        //remover o último container
        if (shelvesRoot.childCount <= 0)
        {
            Debug.LogWarning("[ShelfSectionShelvesController] Não há ShelfUnits para remover, mas shelfCount > minShelves. Confirma a hierarquia.");
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


    // -------------------------
    // REBUILD LIST
    // -------------------------
    private void RebuildShelvesFromHierarchy()
    {
        if (section == null) return;
        if (shelvesRoot == null) return;

        section.Shelves.Clear();

        // pega em todas as shelves dentro do ShelvesRoot (incluindo as novas)
        var shelves = shelvesRoot.GetComponentsInChildren<Shelf>(true)
                                 .OrderBy(s => s.transform.position.y) // de baixo para cima
                                 .ToList();

        for (int i = 0; i < shelves.Count; i++)
        {
            var sh = shelves[i];
            if (sh == null) continue;

            sh.ShelfId = (i + 1).ToString();

            if (sh.Areas != null)
            {
                for (int a = 0; a < sh.Areas.Count; a++)
                {
                    if (sh.Areas[a] != null)
                        sh.Areas[a].AreaId = (a + 1).ToString();
                }
            }

            section.Shelves.Add(sh);
        }

        Debug.Log($"[ShelfSectionShelvesController] Rebuild ok. Shelves={section.Shelves.Count}");
    }
    public void RebuildShelves()
    {
        RebuildShelvesFromHierarchy();
    }

    // -------------------------
    // HELPERS
    // -------------------------
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

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        return bounds;
    }
}
