using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LegendItemUI : MonoBehaviour
{
    [SerializeField] private TMP_Text numberText;   // filho do Circle
    [SerializeField] private Image circleImage;     // o Circle
    [SerializeField] private TMP_Text zoneText;
    [SerializeField] private TMP_Text datesText;

    public void SetNumber(int n) => numberText.text = n.ToString();

    public void SetZone(string zone) => zoneText.text = zone;

    public void SetDates(System.DateTime start, System.DateTime? end)
    {
        string s = start.ToString("dd/MM/yyyy");
        string e = end.HasValue ? end.Value.ToString("dd/MM/yyyy") : "—";
        datesText.text = $"in: {s}   out: {e}";
    }

    public void SetCircleColor(Color c)
    {
        if (circleImage) circleImage.color = c;
    }
}
