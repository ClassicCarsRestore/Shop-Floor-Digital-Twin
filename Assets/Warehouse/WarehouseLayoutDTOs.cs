using System;
using System.Collections.Generic;

[Serializable]
public class WarehouseLayoutDTO
{
    public List<SectionLayoutDTO> sections;
}

[Serializable]
public class SectionLayoutDTO
{
    public string sectionId;
    public float positionX, positionY, positionZ;
    public float rotationY;
    public float scaleX, scaleY, scaleZ;
    public List<ShelfLayoutDTO> shelves;
}

[Serializable]
public class ShelfLayoutDTO
{
    public string shelfId;
    public List<AreaLayoutDTO> areas;
}

[Serializable]
public class AreaLayoutDTO
{
    public string areaId;
    public int index;
    public string status;
    public string itemId;
}
