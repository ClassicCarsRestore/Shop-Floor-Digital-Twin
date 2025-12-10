using UnityEngine;
using UnityEngine.UI;
using UI;

[RequireComponent(typeof(Button))]
public class HeatmapButtonLauncher : MonoBehaviour
{
    private Button btn;
    private HeatmapModeController heatmap;

    private void Awake()
    {
        btn = GetComponent<Button>();
        heatmap = FindObjectOfType<HeatmapModeController>(true);
        btn.onClick.AddListener(OnClick);
    }

    private void OnDestroy()
    {
        btn.onClick.RemoveListener(OnClick);
    }

    private void OnClick()
    {
        if (heatmap == null)
        {
            heatmap = FindObjectOfType<HeatmapModeController>(true);
            if (heatmap == null)
            {
                Debug.LogError("[Heatmap] HeatmapView não encontrado na cena.");
                return;
            }
        }
        heatmap.EnterHeatmapMode();
    }
}
