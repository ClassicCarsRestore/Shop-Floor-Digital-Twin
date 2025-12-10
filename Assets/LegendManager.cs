using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class LegendManager : MonoBehaviour
{
    [SerializeField] private Transform listParent;     // LegendList (o content)
    [SerializeField] private GameObject itemPrefab;    // prefab LegendItem


    private int orderCounter = 0;

    // “Estado” do segmento atual
    private class OpenStay
    {
        public int startIndex;
        public string locId;
        public string locName;
        public DateTime entry;
        public LegendItemUI row;
        public int order;
    }
    private OpenStay current;   // segmento aberto (desde que o carro entrou numa zona até sair)

    public void Clear()
    {
        for (int i = listParent.childCount - 1; i >= 0; i--)
            Destroy(listParent.GetChild(i).gameObject);
        orderCounter = 0;
        current = null;
    }

    public void BeginStay(int startIndex, string locId, string locName, DateTime entry, Color? circle = null)
    {
        EndStayIfOpen(null); // segurança: fecha o anterior se estava aberto sem saída

        orderCounter++;
        var go = Instantiate(itemPrefab, listParent);
        var ui = go.GetComponent<LegendItemUI>();
        ui.SetNumber(orderCounter);
        ui.SetZone(locName);
        ui.SetDates(entry, null);
        if (circle.HasValue) ui.SetCircleColor(circle.Value);

        current = new OpenStay
        {
            startIndex = startIndex,
            locId = locId,
            locName = locName,
            entry = entry,
            row = ui,
            order = orderCounter
        };
    }

    public void EndStay(DateTime exit)
    {
        if (current == null) return;
        current.row.SetDates(current.entry, exit);
        current = null;
    }

    public void EndStayIfOpen(DateTime? exit)
    {
        if (current == null) return;
        current.row.SetDates(current.entry, exit ?? current.entry); // fallback
        current = null;
    }

    // útil se precisares recomeçar com um novo percurso
    public void ResetSession()
    {
        Clear();
    }
}
