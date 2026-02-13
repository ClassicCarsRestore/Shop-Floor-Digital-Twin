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
    public int areaCount;
}
