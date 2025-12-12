using System.Collections.Generic;
using UnityEngine;

public class ShelfSection : MonoBehaviour
{
    [Header("Section Info")]
    public string SectionId; // Ex: "1", "2", "3"…

    [Header("Shelves in this Section")]
    public List<Shelf> Shelves = new List<Shelf>();
}
