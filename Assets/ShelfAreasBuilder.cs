using System.Collections.Generic;
using UnityEngine;

public static class ShelfAreasBuilder
{
    /// <summary>
    /// Reconstrói as áreas desta shelf criando GameObjects StorageArea + BoxAnchor.
    /// Divide o retângulo da shelf em "areaCount" fatias iguais ao longo do maior eixo (X/Z) do bounds.
    /// </summary>
    public static void RebuildAreas(Shelf shelf, int areaCount)
    {
        if (shelf == null) return;
        areaCount = Mathf.Max(1, areaCount);

        // 1) Apagar áreas antigas (as que estavam no prefab ou criadas antes)
        ClearExistingAreas(shelf);

        // 2) Bounds (world) da shelf (usa Renderers; fallback para Collider)
        if (!TryGetWorldBounds(shelf.transform, out var b))
        {
            Debug.LogWarning($"[ShelfAreasBuilder] Não consegui calcular bounds para shelf {shelf.name}.");
            return;
        }

        // 3) Escolher eixo de divisão (maior entre X e Z)
        bool splitOnX = b.size.x >= b.size.z;

        float len = splitOnX ? b.size.x : b.size.z;
        float step = len / areaCount;

        // centro fixo no outro eixo
        float fixedOther = splitOnX ? b.center.z : b.center.x;

        // Y do spawn (centro do bounds)
        float y = b.center.y;

        // 4) Criar áreas 1..N (ordem consistente)
        for (int i = 0; i < areaCount; i++)
        {
            // posição no eixo principal (world)
            float along = (splitOnX ? b.min.x : b.min.z) + (i + 0.5f) * step;

            Vector3 spawnPos = splitOnX
                ? new Vector3(along, y, fixedOther)
                : new Vector3(fixedOther, y, along);

            // GameObject da área
            var areaGo = new GameObject($"Area_{i + 1}");
            areaGo.transform.SetParent(shelf.transform, worldPositionStays: false);

            // coloca o GO no centro da fatia
            areaGo.transform.position = spawnPos;
            areaGo.transform.rotation = shelf.transform.rotation;

            var area = areaGo.AddComponent<StorageArea>();
            area.AreaId = (i + 1).ToString();

            // anchor (child) — o WarehouseManager usa isto para spawn
            var anchorGo = new GameObject("BoxAnchor");
            anchorGo.transform.SetParent(areaGo.transform, worldPositionStays: false);
            anchorGo.transform.position = spawnPos;
            anchorGo.transform.rotation = shelf.transform.rotation;

            area.BoxAnchor = anchorGo.transform;

            shelf.Areas.Add(area);
        }
    }

    private static void ClearExistingAreas(Shelf shelf)
    {
        if (shelf.Areas != null)
        {
            for (int i = shelf.Areas.Count - 1; i >= 0; i--)
            {
                var a = shelf.Areas[i];
                if (a == null) continue;
                Object.Destroy(a.gameObject);
            }
            shelf.Areas.Clear();
        }

        // Se houver StorageArea "perdidas" (não registadas na lista), apaga também
        var leftovers = shelf.GetComponentsInChildren<StorageArea>(true);
        foreach (var a in leftovers)
        {
            if (a == null) continue;
            // se ainda existir (pode já ter sido destruída acima)
            Object.Destroy(a.gameObject);
        }
    }

    private static bool TryGetWorldBounds(Transform root, out Bounds bounds)
    {
        bounds = default;

        var renderers = root.GetComponentsInChildren<Renderer>(true);
        bool has = false;

        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null) continue;

            if (!has) { bounds = r.bounds; has = true; }
            else bounds.Encapsulate(r.bounds);
        }

        if (has) return true;

        // fallback: colliders
        var cols = root.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < cols.Length; i++)
        {
            var c = cols[i];
            if (c == null) continue;

            if (!has) { bounds = c.bounds; has = true; }
            else bounds.Encapsulate(c.bounds);
        }

        return has;
    }
}
