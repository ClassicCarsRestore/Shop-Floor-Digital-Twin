using System;
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

    [Header("Month Nav")]                // <-- NOVO
    [SerializeField] private Button prevMonthButton;
    [SerializeField] private Button nextMonthButton;

    private DateTime currentMonth;
    private DateTime? selectedDate;

    public event Action<DateTime> OnDateSelected;
    public event Action OnCancel;

    private void Awake()
    {
        confirmButton?.onClick.AddListener(Confirm);
        cancelButton?.onClick.AddListener(() => OnCancel?.Invoke());
        prevMonthButton?.onClick.AddListener(() => ChangeMonth(-1));  // <-- NOVO
        nextMonthButton?.onClick.AddListener(() => ChangeMonth(+1));  // <-- NOVO

        currentMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        BuildCalendar(currentMonth);
    }

    private void ChangeMonth(int delta)   // <-- NOVO
    {
        currentMonth = currentMonth.AddMonths(delta);
        BuildCalendar(currentMonth);
    }

    private void ClearCalendar()
    {
        if (!calendarContainer) return;
        for (int i = calendarContainer.childCount - 1; i >= 0; i--)
            Destroy(calendarContainer.GetChild(i).gameObject);
    }

    private void BuildCalendar(DateTime month)
    {
        ClearCalendar();

        if (!headerText)
        {
            Debug.LogError("[DatePicker] Sem headerText para atualizar o mês.");
            return;
        }

        headerText.text = month.ToString("MMMM yyyy");

        if (!dayButtonPrefab || !calendarContainer)
        {
            Debug.LogError("[DatePicker] dayButtonPrefab ou calendarContainer não definidos.");
            return;
        }

        var firstDay = new DateTime(month.Year, month.Month, 1);
        int daysInMonth = DateTime.DaysInMonth(month.Year, month.Month);

        for (int day = 1; day <= daysInMonth; day++)
        {
            Button btn = Instantiate(dayButtonPrefab, calendarContainer);
            TMP_Text label = btn.GetComponentInChildren<TMP_Text>(true);
            if (!label)
            {
                Debug.LogError("[DatePicker] O prefab do botão não tem TMP_Text filho.");
                continue;
            }

            label.text = day.ToString();
            int d = day; // captura
            btn.onClick.AddListener(() => SelectDay(new DateTime(month.Year, month.Month, d)));
        }
    }

    private void SelectDay(DateTime date) => selectedDate = date;

    private void Confirm()
    {
        if (selectedDate.HasValue)
            OnDateSelected?.Invoke(selectedDate.Value);
        else
            Debug.LogWarning("[DatePicker] Nenhum dia selecionado.");
    }
}
