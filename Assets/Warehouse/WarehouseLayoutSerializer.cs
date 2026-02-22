using System.Collections.Generic;
using UnityEngine;

public static class WarehouseLayoutSerializer
{
    /// <summary>
    /// Constrói o DTO do layout a partir do estado atual do WarehouseManager.
    /// Agora ShelfLayoutDTO tem "areas: List<AreaLayoutDTO>" (não há areaCount).
    /// </summary>
    public static WarehouseLayoutDTO BuildFromRuntime(WarehouseManager manager)
    {
        var dto = new WarehouseLayoutDTO { sections = new List<SectionLayoutDTO>() };
        if (manager == null || manager.Sections == null) return dto;

        foreach (var sec in manager.Sections)
        {
            if (sec == null) continue;

            var shelvesCtrl = sec.GetComponent<ShelfSectionShelvesController>();
            if (shelvesCtrl != null)
                shelvesCtrl.RebuildShelves();

            var t = sec.transform;
            var rot = t.rotation.eulerAngles;

            var secDto = new SectionLayoutDTO
            {
                sectionId = sec.SectionId,
                positionX = t.position.x,
                positionY = t.position.y,
                positionZ = t.position.z,
                rotationY = rot.y,
                scaleX = t.localScale.x,
                scaleY = t.localScale.y,
                scaleZ = t.localScale.z,
                shelves = new List<ShelfLayoutDTO>()
            };

            if (sec.Shelves != null)
            {
                for (int i = 0; i < sec.Shelves.Count; i++)
                {
                    var shelf = sec.Shelves[i];
                    if (shelf == null) continue;

                    var shelfDto = new ShelfLayoutDTO
                    {
                        shelfId = shelf.ShelfId, // "sec-index"
                        areas = new List<AreaLayoutDTO>()
                    };

                    // Se não tens status/itemId no Unity (ainda), manda defaults.
                    // Se tiveres esses campos no StorageArea, troca aqui para mapear.
                    if (shelf.Areas != null)
                    {
                        for (int a = 0; a < shelf.Areas.Count; a++)
                        {
                            var ar = shelf.Areas[a];
                            if (ar == null) continue;

                            shelfDto.areas.Add(new AreaLayoutDTO
                            {
                                areaId = ar.AreaId,      // "sec-shelfIndex-areaIndex"
                                index = a + 1,           // índice 1..N
                                status = string.IsNullOrEmpty(ar.Status) ? "free" : ar.Status,
                                itemId = string.IsNullOrEmpty(ar.ItemId) ? null : ar.ItemId
                            });
                        }
                    }

                    secDto.shelves.Add(shelfDto);
                }
            }

            dto.sections.Add(secDto);
        }

        return dto;
    }

    /// <summary>
    /// Aplica o layout ao WarehouseManager: limpa sections atuais e recria a partir do DTO.
    /// Cria as áreas em runtime com base em shelfDto.areas.Count.
    /// </summary>
    public static void ApplyLayout(WarehouseLayoutDTO layout, WarehouseManager manager, GameObject sectionPrefab)
    {
        if (manager == null) return;

        // limpar runtime atual
        if (manager.Sections != null)
        {
            foreach (var old in manager.Sections)
            {
                if (old != null)
                    Object.Destroy(old.gameObject);
            }
            manager.Sections.Clear();
        }

        if (layout == null || layout.sections == null || layout.sections.Count == 0) return;

        if (sectionPrefab == null)
        {
            Debug.LogError("[WarehouseLayoutSerializer] sectionPrefab é null.");
            return;
        }

        foreach (var secDto in layout.sections)
        {
            var go = Object.Instantiate(sectionPrefab);
            var sec = go.GetComponent<ShelfSection>();
            if (sec == null)
            {
                Debug.LogError("[WarehouseLayoutSerializer] Prefab não tem ShelfSection.");
                Object.Destroy(go);
                continue;
            }

            if (manager.WarehouseRoot != null)
                go.transform.SetParent(manager.WarehouseRoot, true);

            go.transform.position = new Vector3(secDto.positionX, secDto.positionY, secDto.positionZ);
            go.transform.rotation = Quaternion.Euler(0f, secDto.rotationY, 0f);
            go.transform.localScale = new Vector3(secDto.scaleX, secDto.scaleY, secDto.scaleZ);

            sec.SectionId = secDto.sectionId ?? "";
            go.name = "Section_" + sec.SectionId;

            if (!manager.Sections.Contains(sec))
                manager.Sections.Add(sec);

            var shelvesCtrl = go.GetComponent<ShelfSectionShelvesController>();

            // 1) garantir shelves suficientes
            if (shelvesCtrl != null)
            {
                shelvesCtrl.RebuildShelves();

                int current = sec.Shelves != null ? sec.Shelves.Count : 0;
                int desired = secDto.shelves != null ? secDto.shelves.Count : 0;

                while (current < desired)
                {
                    shelvesCtrl.AddShelf();
                    current = sec.Shelves != null ? sec.Shelves.Count : 0;
                }

                shelvesCtrl.RebuildShelves(); // reforço ids "sec-index"
            }

            // 2) criar áreas por shelf a partir do DTO (A': precisa sectionId + shelfIndex)
            if (sec.Shelves != null && secDto.shelves != null)
            {
                for (int i = 0; i < sec.Shelves.Count && i < secDto.shelves.Count; i++)
                {
                    var shelf = sec.Shelves[i];
                    var shelfDto = secDto.shelves[i];
                    if (shelf == null) continue;

                    int shelfIndex = i + 1;
                    int areaCount = (shelfDto.areas != null) ? shelfDto.areas.Count : 0;
                    areaCount = Mathf.Max(1, areaCount);

                    ShelfAreasBuilder.RebuildAreas(shelf, areaCount, sec.SectionId, shelfIndex);

                    // (Opcional mas recomendado)
                    // Se quiseres aplicar status/itemId às StorageArea criadas,
                    // precisas que StorageArea tenha esses campos. Ex:
                    // area.Status = ...
                    // area.ItemId = ...
                    // (Se ainda não tens isso no componente, ignora este bloco.)
                    if (shelfDto.areas != null && shelf.Areas != null)
                    {
                        for (int a = 0; a < shelf.Areas.Count && a < shelfDto.areas.Count; a++)
                        {
                            var areaComp = shelf.Areas[a];
                            var areaDto = shelfDto.areas[a];
                            if (areaComp == null || areaDto == null) continue;

                            // Garante AreaId exatamente igual ao DTO (mesmo que o builder já o faça)
                            areaComp.AreaId = areaDto.areaId;

                            // Se adicionares estes campos ao StorageArea no futuro:
                            areaComp.Status = areaDto.status;
                            areaComp.ItemId = areaDto.itemId;
                            areaComp.UpdateVisual();
                        }
                    }
                }

                shelvesCtrl?.RebuildShelves(); // ids finais consistentes
            }
        }

        Physics.SyncTransforms();
    }
}