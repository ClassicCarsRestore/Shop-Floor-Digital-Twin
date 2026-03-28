using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;


public enum HeatmapMetric
{
    Frequency,
    Occupancy
}

public enum HeatmapScale
{
    Relative,
    Absolute
}

public enum HeatmapCurve
{
    Linear,
    Logarithmic
}


public class HeatmapHUDController : MonoBehaviour
{
    [Header("Date Picker Prefab")]
    [SerializeField] private GameObject datePickerPrefab;

    [Header("Refs")]
    [SerializeField] private Button buttonFrom;
    [SerializeField] private Button buttonTo;
    [SerializeField] private TMP_Text fromLabel;
    [SerializeField] private TMP_Text toLabel;

    [Header("Metric Toggles")]
    [SerializeField] private Toggle positionToggle;   // Frequency
    [SerializeField] private Toggle rotationToggle;   // Occupancy

    [Header("Scale Toggles")]
    [SerializeField] private Toggle relativeToggle;
    [SerializeField] private Toggle absoluteToggle;

    [Header("Curve Toggles")]
    [SerializeField] private Toggle linearToggle;
    [SerializeField] private Toggle logToggle;

    [SerializeField] private Button exitButton;
    [SerializeField] private ToggleGroup modeGroup;

    public event Action<DateTime?, DateTime?> OnDateRangeChanged;
    public event Action<bool> OnMetricChanged;
    public event Action<HeatmapMetric> OnMetricChangedEnum;
    public event Action OnExitRequested;

    // NOVO
    public event Action<HeatmapScale> OnScaleChanged;
    public event Action<HeatmapCurve> OnCurveChanged;

    private DateTime? fromDate, toDate;
    private Canvas parentCanvas;
    private bool isOpeningPicker = false;

    private void Awake()
    {
        parentCanvas = GetComponentInParent<Canvas>();

        if (buttonFrom) buttonFrom.onClick.AddListener(() => OpenPicker(true));
        if (buttonTo) buttonTo.onClick.AddListener(() => OpenPicker(false));

        if (positionToggle) positionToggle.onValueChanged.AddListener(on =>
        {
            if (on) EmitMetric(HeatmapMetric.Frequency);
        });

        if (rotationToggle) rotationToggle.onValueChanged.AddListener(on =>
        {
            if (on) EmitMetric(HeatmapMetric.Occupancy);
        });

        // Escala
        if (relativeToggle) relativeToggle.onValueChanged.AddListener(on =>
        {
            if (on) OnScaleChanged?.Invoke(HeatmapScale.Relative);
        });

        if (absoluteToggle) absoluteToggle.onValueChanged.AddListener(on =>
        {
            if (on) OnScaleChanged?.Invoke(HeatmapScale.Absolute);
        });

        // Curva
        if (linearToggle) linearToggle.onValueChanged.AddListener(on =>
        {
            if (on) OnCurveChanged?.Invoke(HeatmapCurve.Linear);
        });

        if (logToggle) logToggle.onValueChanged.AddListener(on =>
        {
            if (on) OnCurveChanged?.Invoke(HeatmapCurve.Logarithmic);
        });

        if (exitButton) exitButton.onClick.AddListener(() => OnExitRequested?.Invoke());
    }

    public void SetDefaults(DateTime from, DateTime to, bool usePosition)
    {
        fromDate = from;
        toDate = to;

        if (fromLabel) fromLabel.text = from.ToString("yyyy-MM-dd");
        if (toLabel) toLabel.text = to.ToString("yyyy-MM-dd");

        if (positionToggle) positionToggle.isOn = usePosition;
        if (rotationToggle) rotationToggle.isOn = !usePosition;

        // Escala default: Relative
        if (relativeToggle) relativeToggle.isOn = true;
        if (absoluteToggle) absoluteToggle.isOn = false;

        // Curva default: Linear
        if (linearToggle) linearToggle.isOn = true;
        if (logToggle) logToggle.isOn = false;

        EmitDates();
        EmitMetric(usePosition ? HeatmapMetric.Frequency : HeatmapMetric.Occupancy);

        OnScaleChanged?.Invoke(HeatmapScale.Relative);
        OnCurveChanged?.Invoke(HeatmapCurve.Linear);
    }

    private void OpenPicker(bool isFrom)
    {
        if (isOpeningPicker || !datePickerPrefab || parentCanvas == null)
            return;

        isOpeningPicker = true;

        var pickerGO = Instantiate(datePickerPrefab, parentCanvas.transform);
        var picker = pickerGO.GetComponent<SimpleDatePicker>();
        if (picker == null)
        {
            Debug.LogError("[HUD] SimpleDatePicker n�o encontrado no prefab de date picker.");
            Destroy(pickerGO);
            isOpeningPicker = false;
            return;
        }

        DateTime seedDate = DateTime.Today;
        if (isFrom)
            seedDate = fromDate ?? toDate ?? DateTime.Today;
        else
            seedDate = toDate ?? fromDate ?? DateTime.Today;

        picker.Initialize(seedDate);

        picker.OnDateSelected += date =>
        {
            if (isFrom)
            {
                fromDate = date;
                if (fromLabel) fromLabel.text = date.ToString("yyyy-MM-dd");
            }
            else
            {
                toDate = date;
                if (toLabel) toLabel.text = date.ToString("yyyy-MM-dd");
            }

            if (fromDate.HasValue && toDate.HasValue && fromDate.Value.Date > toDate.Value.Date)
            {
                DateTime temp = fromDate.Value;
                fromDate = toDate;
                toDate = temp;

                if (fromLabel) fromLabel.text = fromDate.Value.ToString("yyyy-MM-dd");
                if (toLabel) toLabel.text = toDate.Value.ToString("yyyy-MM-dd");
            }

            EmitDates();

            pickerGO.SetActive(false);
            Destroy(pickerGO);
            isOpeningPicker = false;
        };

        picker.OnCancel += () =>
        {
            Destroy(pickerGO);
            isOpeningPicker = false;
        };
    }

    private void EmitDates() => OnDateRangeChanged?.Invoke(fromDate, toDate);


    private void EmitMetric(HeatmapMetric metric)
    {
        bool isFrequency = (metric == HeatmapMetric.Frequency);
        OnMetricChanged?.Invoke(isFrequency);
        OnMetricChangedEnum?.Invoke(metric);
    }

    private void EmitMetric()
    {
        bool isFrequency = positionToggle && positionToggle.isOn;
        EmitMetric(isFrequency ? HeatmapMetric.Frequency : HeatmapMetric.Occupancy);
    }

    public void ForceDateFields(DateTime from, DateTime to)
    {
        fromDate = from; toDate = to;
        if (fromLabel) fromLabel.text = from.ToString("yyyy-MM-dd");
        if (toLabel) toLabel.text = to.ToString("yyyy-MM-dd");
        EmitDates();
    }
}
