using TMPro;
using UnityEngine;

public class WarehouseBoxDetailsPanel : MonoBehaviour
{
    public static WarehouseBoxDetailsPanel Instance { get; private set; }

    [Header("Optional Root")]
    [SerializeField] private GameObject panelRoot;

    [Header("Fields")]
    [SerializeField] private TextMeshProUGUI itemNameValue;
    [SerializeField] private TextMeshProUGUI itemStateValue;
    [SerializeField] private TextMeshProUGUI carModelValue;
    [SerializeField] private TextMeshProUGUI locationValue;
    

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        Hide();
    }

    public void Show(StorageBox box)
    {
        if (box == null)
        {
            Hide();
            return;
        }

        string itemText = FirstNonEmpty(box.ItemName, box.ItemId);
        string carText = FirstNonEmpty(box.CarModel, box.CarId);
        string stateText = FirstNonEmpty(box.ItemState, "test");
        string locationText = FirstNonEmpty(box.LocationKey, BuildLocationFromParentArea(box));

        SetText(itemNameValue, itemText);
        SetText(itemStateValue, stateText);
        SetText(carModelValue, carText);
        SetText(locationValue, locationText);

        SetVisible(true);
    }

    public void Hide()
    {
        SetVisible(false);
    }

    private void SetVisible(bool visible)
    {
        var target = panelRoot != null ? panelRoot : gameObject;
        if (target != null)
        {
            target.SetActive(visible);
        }
    }

    private static void SetText(TextMeshProUGUI text, string value)
    {
        if (text == null) return;
        text.text = string.IsNullOrWhiteSpace(value) ? "--" : value;
    }

    private static string FirstNonEmpty(params string[] values)
    {
        if (values == null) return null;

        for (int i = 0; i < values.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(values[i]))
                return values[i];
        }

        return null;
    }

    private static string BuildLocationFromParentArea(StorageBox box)
    {
        if (box == null) return null;

        var area = box.GetComponentInParent<StorageArea>();
        return area != null ? area.AreaId : null;
    }
}
