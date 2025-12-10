using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Models;

public class StepMarkerManager : MonoBehaviour
{

    // NOVOS fields
    [SerializeField] private Color circleLight = new Color(1, 1, 1, 0.9f);  // círculo claro
    [SerializeField] private Color circleDark = new Color(0, 0, 0, 0.85f); // círculo escuro
    [SerializeField] private float cellMargin = 0.08f; // “inset” nas células da grelha (0..0.4)

    [Header("Prefab do número")]
    [SerializeField] private GameObject markerPrefab;
    [SerializeField] private Transform markerParent;

    [Header("Aparência")]
    [SerializeField] private Vector3 markerEuler = new Vector3(0f, 180f, 0f);
    [SerializeField] private Vector3 markerScale = new Vector3(35f, 35f, 35f);   // controla tamanho no prefab/TMP de preferência
    [SerializeField] private float lift = 0.01f;

    [Header("Distribuição por zona")]
    [SerializeField] private int gridSize = 3; // 3x3 por localização

    // controle
    private readonly HashSet<int> droppedForStepIndex = new();   // evita duplicados por índice
    private readonly Dictionary<string, HashSet<Vector2Int>> usedCellsByLoc = new(); // locId -> células usadas
    private int visitCount = 0; // ordem sequencial (1,2,3..)



    public void ClearAll()
    {
        Transform parent = markerParent ? markerParent : transform;
        for (int i = parent.childCount - 1; i >= 0; i--) Destroy(parent.GetChild(i).gameObject);
        droppedForStepIndex.Clear();
        usedCellsByLoc.Clear();
        visitCount = 0;
    }

    public bool HasDroppedForIndex(int stepIndex) => droppedForStepIndex.Contains(stepIndex);

    public void DropNumberAtLocation(int stepIndex, VirtualMapLocation loc, Transform slotTransform)
    {
        if (loc == null || slotTransform == null || markerPrefab == null) { Debug.LogWarning("[PF] Drop: dados nulos"); return; }
        if (droppedForStepIndex.Contains(stepIndex)) return;

        // calcula posição na próxima célula livre da grelha dessa localização
        Vector3 worldPos = PickSpotInside(slotTransform, loc) + Vector3.up * lift;

        var parent = markerParent ? markerParent : transform;
        var go = Instantiate(markerPrefab, worldPos, Quaternion.Euler(markerEuler), parent);
        go.transform.localScale = markerScale;



        // ordem 1..N (sequência global da sessão)
        int order = ++visitCount;

        // setar número
        foreach (var t in go.GetComponentsInChildren<TMPro.TMP_Text>(true))
            t.text = order.ToString();

        // contraste automático com base na cor da zona (hex em loc.color)
        var circle = go.transform.Find("CircleBG")?.GetComponent<UnityEngine.UI.Image>();
        if (circle)
        {
            Color baseColor;
            if (!string.IsNullOrEmpty(loc.color) &&
                ColorUtility.TryParseHtmlString(loc.color, out baseColor))
            {
                float lum = 0.2126f * baseColor.r + 0.7152f * baseColor.g + 0.0722f * baseColor.b;
                bool bgIsLight = lum > 0.55f;
                circle.color = bgIsLight ? circleDark : circleLight;

                // texto inverso
                foreach (var t in go.GetComponentsInChildren<TMPro.TMP_Text>(true))
                    t.color = bgIsLight ? Color.white : Color.black;
            }
            else
            {
                circle.color = circleLight;
                foreach (var t in go.GetComponentsInChildren<TMPro.TMP_Text>(true))
                    t.color = Color.black;
            }
        }


        foreach (var t in go.GetComponentsInChildren<TMP_Text>(true))
            t.text = order.ToString();

        droppedForStepIndex.Add(stepIndex);
        Debug.Log($"[PF] Drop #{order} em {loc.name} @ {worldPos}");
    }

    private Vector3 PickSpotInside(Transform slot, VirtualMapLocation loc)
    {
        var verts = loc.vertices;
        int n = verts?.Count ?? 0;
        if (n < 4) // fallback seguro
        {
            // centroide
            float cx = 0f, cz = 0f;
            for (int i = 0; i < n; i++) { cx += verts[i].X; cz += verts[i].Z; }
            if (n > 0) { cx /= n; cz /= n; }
            return slot.TransformPoint(new Vector3(cx, 0f, cz));
        }

        // --- ordenar 4 vértices em sentido horário/anti-horário ---
        var pts = new List<Vector2>(4);
        for (int i = 0; i < 4; i++) pts.Add(new Vector2(verts[i].X, verts[i].Z));

        // centroide p/ ordenar
        Vector2 c = Vector2.zero;
        foreach (var p in pts) c += p;
        c /= pts.Count;

        pts.Sort((a, b) =>
        {
            float aa = Mathf.Atan2(a.y - c.y, a.x - c.x);
            float bb = Mathf.Atan2(b.y - c.y, b.x - c.x);
            return aa.CompareTo(bb);
        });

        // agora pts[0..3] estão em volta do polígono (convexo)
        // bilinear nos 4 cantos do quad
        Vector3 v0 = new Vector3(pts[0].x, 0f, pts[0].y);
        Vector3 v1 = new Vector3(pts[1].x, 0f, pts[1].y);
        Vector3 v2 = new Vector3(pts[2].x, 0f, pts[2].y);
        Vector3 v3 = new Vector3(pts[3].x, 0f, pts[3].y);

        // grelha por localização (célula livre seguinte)
        if (!usedCellsByLoc.TryGetValue(loc.id, out var used))
        {
            used = new HashSet<Vector2Int>();
            usedCellsByLoc[loc.id] = used;
        }

        for (int gz = 0; gz < gridSize; gz++)
        {
            for (int gx = 0; gx < gridSize; gx++)
            {
                var cell = new Vector2Int(gx, gz);
                if (used.Contains(cell)) continue;
                used.Add(cell);

                // centro da célula
                float uRaw = (gx + 0.5f) / gridSize;
                float vRaw = (gz + 0.5f) / gridSize;
                float u = Mathf.Lerp(cellMargin, 1f - cellMargin, uRaw);
                float v = Mathf.Lerp(cellMargin, 1f - cellMargin, vRaw);


                // interpolação bilinear (fica SEMPRE dentro do quad)
                Vector3 local =
                    (1 - u) * (1 - v) * v0 +
                    u * (1 - v) * v1 +
                    u * v * v2 +
                    (1 - u) * v * v3;

                return slot.TransformPoint(local);
            }
        }

        // fallback (lotado): centroide
        Vector3 centroid = (v0 + v1 + v2 + v3) / 4f;
        return slot.TransformPoint(centroid);
    }

}
