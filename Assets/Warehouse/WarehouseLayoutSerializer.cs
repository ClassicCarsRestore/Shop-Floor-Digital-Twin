using System.Collections.Generic;
using UnityEngine;

public static class WarehouseLayoutSerializer
{
    /// <summary>
    /// Constrói o DTO do layout a partir do estado atual do WarehouseManager.
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
                foreach (var shelf in sec.Shelves)
                {
                    if (shelf == null) continue;
                    int areaCount = shelf.Areas != null ? shelf.Areas.Count : 0;

                    secDto.shelves.Add(new ShelfLayoutDTO
                    {
                        shelfId = shelf.ShelfId,
                        areaCount = areaCount
                    });
                }
            }

            dto.sections.Add(secDto);
        }

        return dto;
    }

    /// <summary>
    /// Aplica o layout ao WarehouseManager: limpa sections atuais e recria a partir do DTO.
    /// </summary>
    public static void ApplyLayout(WarehouseLayoutDTO layout, WarehouseManager manager, GameObject sectionPrefab)
    {
        if (manager == null) return;

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
            }

            if (sec.Shelves != null && secDto.shelves != null)
            {
                for (int i = 0; i < sec.Shelves.Count && i < secDto.shelves.Count; i++)
                {
                    var shelf = sec.Shelves[i];
                    var shelfDto = secDto.shelves[i];
                    if (shelf == null) continue;

                    int areaCount = Mathf.Max(1, shelfDto.areaCount);
                    ShelfAreasBuilder.RebuildAreas(shelf, areaCount);
                }
            }
        }

        Physics.SyncTransforms();
    }
}
