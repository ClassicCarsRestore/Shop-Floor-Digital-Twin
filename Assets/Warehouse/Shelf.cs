using System.Collections.Generic;
using UnityEngine;

public class Shelf : MonoBehaviour
{
    [Header("Shelf Info")]
    public string ShelfId; // Ex: "1", "2", "3"…

    [Header("Areas in this Shelf")]
    public List<StorageArea> Areas = new List<StorageArea>();
}
