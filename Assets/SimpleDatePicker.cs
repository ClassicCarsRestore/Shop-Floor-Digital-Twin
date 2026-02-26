using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SimpleDatePicker : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text headerText;
    [SerializeField] private Transform calendarContainer;
    [SerializeField] private Button dayButtonPrefab;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;

    [Header("Month Navigation")]
    [SerializeField] private Button prevMonthButton;
    [SerializeField] private Button nextMonthButton;
    [SerializeField] private TMP_Dropdown monthDropdown;
    [SerializeField] private TMP_Dropdown yearDropdown;

    [Header("Selection")]
    [SerializeField] private Color selectedDayColor = new Color(0.23f, 0.57f, 0.95f, 1f);
    [SerializeField] private Color selectedDayTextColor = Color.white;
    [SerializeField] private int yearRangePast = 30;
    [SerializeField] private int yearRangeFuture = 10;

    private static readonly string[] WeekdayHeaders = { "S", "M", "T", "W", "T", "F", "S" };

    private DateTime currentMonth;
    private DateTime? selectedDate;
    private readonly List<Button> generatedButtons = new List<Button>();
    private bool suppressDropdownCallbacks;
    private DateTime initialDate = DateTime.Today;

    public event Action<DateTime> OnDateSelected;
    public event Action OnCancel;

    private void Awake()
    {
        confirmButton?.onClick.AddListener(Confirm);
        cancelButton?.onClick.AddListener(() => OnCancel?.Invoke());
        prevMonthButton?.onClick.AddListener(() => ChangeMonth(-1));
        nextMonthButton?.onClick.AddListener(() => ChangeMonth(+1));
        if (monthDropdown != null) monthDropdown.onValueChanged.AddListener(OnMonthDropdownChanged);
        if (yearDropdown != null) yearDropdown.onValueChanged.AddListener(OnYearDropdownChanged);

        selectedDate = initialDate.Date;
        currentMonth = new DateTime(initialDate.Year, initialDate.Month, 1);

        SetupMonthDropdown();
        SetupYearDropdown();
        BuildCalendar(currentMonth);
    }

    public void Initialize(DateTime initial)
    {
        initialDate = initial.Date;
        selectedDate = initialDate;
        currentMonth = new DateTime(initialDate.Year, initialDate.Month, 1);

        SetupYearDropdown();
        BuildCalendar(currentMonth);
    }

    private void SetupMonthDropdown()
    {
        if (monthDropdown == null)
            return;

        suppressDropdownCallbacks = true;
        monthDropdown.ClearOptions();

        var options = new List<string>();
        for (int month = 1; month <= 12; month++)
            options.Add(new DateTime(2000, month, 1).ToString("MMM"));

        monthDropdown.AddOptions(options);
        monthDropdown.value = Mathf.Clamp(currentMonth.Month - 1, 0, 11);
        monthDropdown.RefreshShownValue();
        suppressDropdownCallbacks = false;
    }

    private void SetupYearDropdown()
    {
        if (yearDropdown == null)
            return;

        int centerYear = selectedDate?.Year ?? DateTime.Today.Year;
        int minYear = centerYear - Mathf.Max(1, yearRangePast);
        int maxYear = centerYear + Mathf.Max(1, yearRangeFuture);

        suppressDropdownCallbacks = true;
        yearDropdown.ClearOptions();

        var options = new List<string>();
        int selectedIndex = 0;
        for (int y = minYear; y <= maxYear; y++)
        {
            if (y == currentMonth.Year)
                selectedIndex = options.Count;
            options.Add(y.ToString());
        }

        yearDropdown.AddOptions(options);
        yearDropdown.value = selectedIndex;
        yearDropdown.RefreshShownValue();
        suppressDropdownCallbacks = false;
    }

    private void OnMonthDropdownChanged(int monthIndex)
    {
        if (suppressDropdownCallbacks)
            return;

        currentMonth = new DateTime(currentMonth.Year, monthIndex + 1, 1);
        BuildCalendar(currentMonth);
    }

    private void OnYearDropdownChanged(int yearIndex)
    {
        if (suppressDropdownCallbacks || yearDropdown == null)
            return;

        if (!int.TryParse(yearDropdown.options[yearIndex].text, out int selectedYear))
            return;

        currentMonth = new DateTime(selectedYear, currentMonth.Month, 1);
        BuildCalendar(currentMonth);
    }

    private void ChangeMonth(int delta)
    {
        currentMonth = currentMonth.AddMonths(delta);
        BuildCalendar(currentMonth);
    }

    private void ClearCalendar()
    {
        if (!calendarContainer)
            return;

        for (int i = calendarContainer.childCount - 1; i >= 0; i--)
            Destroy(calendarContainer.GetChild(i).gameObject);

        generatedButtons.Clear();
    }

    private void BuildCalendar(DateTime month)
    {
        ClearCalendar();

        if (headerText != null)
            headerText.text = month.ToString("MMMM yyyy");

        SyncDropdownsFromMonth(month);

        if (!dayButtonPrefab || !calendarContainer)
        {
            Debug.LogError("[DatePicker] dayButtonPrefab ou calendarContainer não definidos.");
            return;
        }

        BuildWeekdayHeaders();

        var firstDay = new DateTime(month.Year, month.Month, 1);
        int daysInMonth = DateTime.DaysInMonth(month.Year, month.Month);
        int firstWeekdayCol = (int)firstDay.DayOfWeek; // Sunday=0

        for (int i = 0; i < firstWeekdayCol; i++)
            CreateSpacerCell();

        for (int day = 1; day <= daysInMonth; day++)
        {
            Button btn = Instantiate(dayButtonPrefab, calendarContainer);
            generatedButtons.Add(btn);

            TMP_Text label = btn.GetComponentInChildren<TMP_Text>(true);
            if (!label)
            {
                Debug.LogError("[DatePicker] O prefab do botão não tem TMP_Text filho.");
                continue;
            }

            label.text = day.ToString();

            DateTime dateForButton = new DateTime(month.Year, month.Month, day);
            int d = day;
            btn.onClick.AddListener(() => SelectDay(new DateTime(month.Year, month.Month, d), btn));

            bool isSelected = selectedDate.HasValue && selectedDate.Value.Date == dateForButton.Date;
            ApplyButtonSelectedVisual(btn, isSelected);
        }
    }

    private void BuildWeekdayHeaders()
    {
        for (int i = 0; i < WeekdayHeaders.Length; i++)
        {
            Button headerBtn = Instantiate(dayButtonPrefab, calendarContainer);
            generatedButtons.Add(headerBtn);
            headerBtn.interactable = false;

            TMP_Text label = headerBtn.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
                label.text = WeekdayHeaders[i];
        }
    }

    private void CreateSpacerCell()
    {
        Button spacer = Instantiate(dayButtonPrefab, calendarContainer);
        generatedButtons.Add(spacer);
        spacer.interactable = false;

        TMP_Text label = spacer.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
            label.text = string.Empty;
    }

    private void SyncDropdownsFromMonth(DateTime month)
    {
        suppressDropdownCallbacks = true;

        if (monthDropdown != null)
        {
            monthDropdown.value = Mathf.Clamp(month.Month - 1, 0, 11);
            monthDropdown.RefreshShownValue();
        }

        if (yearDropdown != null)
        {
            int idx = yearDropdown.options.FindIndex(o => o.text == month.Year.ToString());
            if (idx < 0)
            {
                suppressDropdownCallbacks = false;
                SetupYearDropdown();
                suppressDropdownCallbacks = true;
                idx = yearDropdown.options.FindIndex(o => o.text == month.Year.ToString());
            }

            if (idx >= 0)
            {
                yearDropdown.value = idx;
                yearDropdown.RefreshShownValue();
            }
        }

        suppressDropdownCallbacks = false;
    }

    private void SelectDay(DateTime date, Button clickedButton)
    {
        selectedDate = date.Date;

        foreach (var btn in generatedButtons)
            ApplyButtonSelectedVisual(btn, btn == clickedButton);
    }

    private void ApplyButtonSelectedVisual(Button btn, bool selected)
    {
        if (btn == null)
            return;

        var colors = btn.colors;
        if (selected)
        {
            colors.normalColor = selectedDayColor;
            colors.highlightedColor = selectedDayColor;
            colors.pressedColor = selectedDayColor;
            colors.selectedColor = selectedDayColor;
        }
        btn.colors = colors;

        TMP_Text text = btn.GetComponentInChildren<TMP_Text>(true);
        if (text != null)
            text.color = selected ? selectedDayTextColor : Color.black;
    }

    private void Confirm()
    {
        if (selectedDate.HasValue)
            OnDateSelected?.Invoke(selectedDate.Value);
        else
            Debug.LogWarning("[DatePicker] Nenhum dia selecionado.");
    }
}
