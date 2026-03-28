using TMPro;
using UnityEngine;

public class HeatmapTooltipUI : MonoBehaviour
{
    [SerializeField] private TMP_Text titleText; 
    [SerializeField] private TMP_Text bodyText;

    public void SetText(string title, string body)
    {
        if (titleText) titleText.text = title;
        if (bodyText) bodyText.text = body;
    }

    public void SetPosition(Vector2 screenPosition)
    {
        RectTransform rt = (RectTransform)transform;
        rt.position = screenPosition;
    }

    public void Show() => gameObject.SetActive(true);
    public void Hide() => gameObject.SetActive(false);
}
