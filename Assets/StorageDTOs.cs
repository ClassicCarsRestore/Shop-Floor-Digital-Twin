using System;
using System.Collections.Generic;

[Serializable]
public class StorageLocationDTO
{
    public string section;
    public string shelf;
    public string area;
}

[Serializable]
public class StorageRowDTO
{
    public string itemId;
    public string carId;
    public StorageLocationDTO location;
}

// Resposta t�pica: { "rows": [ ... ] }
[Serializable]
public class StorageRowsResponseDTO
{
    public List<StorageRowDTO> rows;
}
