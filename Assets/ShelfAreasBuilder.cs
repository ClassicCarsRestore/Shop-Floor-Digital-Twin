using System.Collections.Generic;
using UnityEngine;

public static class ShelfAreasBuilder
{
    public static void RebuildAreas(Shelf shelf, int areaCount, string sectionId, int shelfIndex)
    {
        if (shelf == null) return;
        areaCount = Mathf.Max(1, areaCount);

        // 1) snapshot de estado
        var oldState = new Dictionary<int, (string status, string itemId)>();
        if (shelf.Areas != null)
        {
            for (int i = 0; i < shelf.Areas.Count; i++)
            {
                var a = shelf.Areas[i];
                if (a == null) continue;
                oldState[i + 1] = (a.Status, a.ItemId);
            }
        }

        // 2) snapshot boxes por índice + detach para não morrerem
        var oldBoxesByIndex = new Dictionary<int, StorageBox>();
        var oldBoxes = shelf.GetComponentsInChildren<StorageBox>(true);

        foreach (var box in oldBoxes)
        {
            if (box == null) continue;

            int idx = TryParseAreaIndex(box.LocationKey);
            if (idx <= 0)
            {
                var parentArea = box.GetComponentInParent<StorageArea>();
                idx = parentArea != null ? TryParseAreaIndex(parentArea.AreaId) : -1;
            }

            if (idx > 0 && !oldBoxesByIndex.ContainsKey(idx))
                oldBoxesByIndex[idx] = box;

            // detach mantendo world transform
            box.transform.SetParent(shelf.transform, true);
        }

        // 3) limpar áreas antigas (já sem boxes)
        ClearExistingAreas(shelf);

        // 4) bounds da shelf em LOCAL (para não explodir com scale/rotação)
        if (!TryGetLocalBounds(shelf.transform, out var localB))
        {
            Debug.LogWarning($"[ShelfAreasBuilder] Não consegui bounds locais para shelf {shelf.name}.");
            return;
        }

        // dividir pelo maior eixo local X/Z
        bool splitOnX = localB.size.x >= localB.size.z;
        float len = splitOnX ? localB.size.x : localB.size.z;
        float step = len / areaCount;

        // para o “plano de cima” da shelf (local)
        float topY = localB.max.y + 0.002f;
        float fixedOther = splitOnX ? localB.center.z : localB.center.x;

        // IMPORTANTÍSSIMO: definir direção para “área 1” ficar na ESQUERDA
        // Se estiver ao contrário no teu modelo, troca o sinal aqui.
        bool leftToRight = true; // se estiver invertido no teu caso, mete false

        for (int i = 0; i < areaCount; i++)
        {
            int areaIndex = i + 1;

            int logicalI = leftToRight ? i : (areaCount - 1 - i);

            float along = (splitOnX ? localB.min.x : localB.min.z) + (logicalI + 0.5f) * step;

            Vector3 localCenter = splitOnX
                ? new Vector3(along, topY, fixedOther)
                : new Vector3(fixedOther, topY, along);

            // root area
            var areaGo = new GameObject($"Area_{sectionId}-{shelfIndex}-{areaIndex}");
            areaGo.transform.SetParent(shelf.transform, false);
            areaGo.transform.localPosition = localCenter;
            areaGo.transform.localRotation = Quaternion.identity;

            var area = areaGo.AddComponent<StorageArea>();
            area.AreaId = $"{sectionId}-{shelfIndex}-{areaIndex}";

            if (oldState.TryGetValue(areaIndex, out var st))
            {
                area.Status = string.IsNullOrWhiteSpace(st.status) ? "free" : st.status;
                area.ItemId = st.itemId;
            }
            else
            {
                area.Status = "free";
                area.ItemId = null;
            }

            // criar slot visual (cube fino)
            var slotGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slotGo.name = "Slot";
            slotGo.transform.SetParent(areaGo.transform, false);
            slotGo.transform.localRotation = Quaternion.identity;

            // tamanho LOCAL do slot (encaixa na fatia)
            float slotX = splitOnX ? step : localB.size.x;
            float slotZ = splitOnX ? localB.size.z : step;
            float slotY = 0.01f;

            slotGo.transform.localScale = new Vector3(slotX, slotY, slotZ);
            slotGo.transform.localPosition = new Vector3(0f, 0f, 0f);

            // collider do slot (vem no primitive)
            var slotCol = slotGo.GetComponent<BoxCollider>();
            // garantir que o collider acompanha o slot
            slotCol.size = Vector3.one;
            slotCol.center = Vector3.zero;

            area.SlotVisual = slotGo.transform;
            area.SlotCollider = slotCol;

            // divisória visível entre slots (uma "linha" em cima do slot)
            // Só criamos a divisória depois desta fatia se ela NÃO for a última fatia física.
            if (logicalI < areaCount - 1)
            {
                var dividerGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                dividerGo.name = "SlotDivider";
                dividerGo.transform.SetParent(areaGo.transform, false);
                dividerGo.transform.localRotation = Quaternion.identity;

                float thickness = Mathf.Clamp(step * 0.02f, 0.002f, 0.02f);
                float dividerY = 0.002f;
                float yPos = (slotY * 0.5f) + (dividerY * 0.5f) + 0.0005f;

                if (splitOnX)
                {
                    dividerGo.transform.localPosition = new Vector3(step * 0.5f, yPos, 0f);
                    dividerGo.transform.localScale = new Vector3(thickness, dividerY, slotZ);
                }
                else
                {
                    dividerGo.transform.localPosition = new Vector3(0f, yPos, step * 0.5f);
                    dividerGo.transform.localScale = new Vector3(slotX, dividerY, thickness);
                }

                var divCol = dividerGo.GetComponent<Collider>();
                if (divCol != null) Object.Destroy(divCol);

                var divRenderer = dividerGo.GetComponent<Renderer>();
                if (divRenderer != null)
                {
                    // cor preta opaca para máxima visibilidade
                    var divMpb = new MaterialPropertyBlock();
                    var c = new Color(0f, 0f, 0f, 1f);
                    divRenderer.GetPropertyBlock(divMpb);
                    divMpb.SetColor("_BaseColor", c);
                    divMpb.SetColor("_Color", c);
                    divRenderer.SetPropertyBlock(divMpb);
                }
            }

            // anchor para box (no centro do slot)
            var anchorGo = new GameObject("BoxAnchor");
            anchorGo.transform.SetParent(areaGo.transform, false);
            anchorGo.transform.localPosition = Vector3.zero;
            anchorGo.transform.localRotation = Quaternion.identity;
            area.BoxAnchor = anchorGo.transform;

            // regista na shelf
            shelf.Areas.Add(area);

            // update visual (verde/vermelho)
            area.UpdateVisual();

            // recolocar box se existia aqui
            if (oldBoxesByIndex.TryGetValue(areaIndex, out var box) && box != null)
            {
                box.LocationKey = area.AreaId;
                box.transform.SetParent(areaGo.transform, true);

                // alinha no anchor e refaz fit
                if (area.BoxAnchor != null)
                {
                    box.transform.position = area.BoxAnchor.position;
                    box.transform.rotation = shelf.transform.rotation;
                }
                box.FitToAreaSlot();

                // marca área como ocupada (se quiseres forçar consistência)
                area.Status = "occupied";
                if (string.IsNullOrWhiteSpace(area.ItemId)) area.ItemId = box.CarId;
                area.UpdateVisual();
            }
        }
    }

    private static int TryParseAreaIndex(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return -1;
        var parts = key.Split('-');
        if (parts.Length < 3) return -1;
        return int.TryParse(parts[2], out int idx) ? idx : -1;
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

        var leftovers = shelf.GetComponentsInChildren<StorageArea>(true);
        foreach (var a in leftovers)
        {
            if (a == null) continue;
            Object.Destroy(a.gameObject);
        }
    }

    // bounds locais: encapsula geometria em espaço local da shelf
    // prioridade para Renderers (mais estável quando há colliders com escala negativa)
    private static bool TryGetLocalBounds(Transform root, out Bounds localBounds)
    {
        localBounds = default;
        bool has = false;

        var rends = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < rends.Length; i++)
        {
            var r = rends[i];
            if (r == null) continue;
            EncapsulateWorldBoundsIntoLocal(root, r.bounds, ref localBounds, ref has);
        }

        if (has) return true;

        // fallback para colliders (ignorando os casos problemáticos)
        var cols = root.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < cols.Length; i++)
        {
            var c = cols[i];
            if (c == null) continue;
            if (!c.enabled) continue;
            if (HasNegativeLossyScale(c.transform)) continue;

            EncapsulateWorldBoundsIntoLocal(root, c.bounds, ref localBounds, ref has);
        }

        return has;
    }

    private static bool HasNegativeLossyScale(Transform t)
    {
        if (t == null) return false;
        var s = t.lossyScale;
        return s.x < 0f || s.y < 0f || s.z < 0f;
    }

    private static void EncapsulateWorldBoundsIntoLocal(
        Transform root,
        Bounds worldBounds,
        ref Bounds localBounds,
        ref bool has)
    {
        Vector3 c = worldBounds.center;
        Vector3 e = worldBounds.extents;

        Vector3[] corners =
        {
        c + new Vector3( e.x,  e.y,  e.z),
        c + new Vector3( e.x,  e.y, -e.z),
        c + new Vector3( e.x, -e.y,  e.z),
        c + new Vector3( e.x, -e.y, -e.z),
        c + new Vector3(-e.x,  e.y,  e.z),
        c + new Vector3(-e.x,  e.y, -e.z),
        c + new Vector3(-e.x, -e.y,  e.z),
        c + new Vector3(-e.x, -e.y, -e.z),
    };

        for (int i = 0; i < corners.Length; i++)
        {
            Vector3 lp = root.InverseTransformPoint(corners[i]);
            if (!has)
            {
                localBounds = new Bounds(lp, Vector3.zero);
                has = true;
            }
            else
            {
                localBounds.Encapsulate(lp);
            }
        }
    }
}



