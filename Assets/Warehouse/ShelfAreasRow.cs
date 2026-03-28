using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ShelfAreasRow : MonoBehaviour
{
    [SerializeField] private TMP_Text labelText;
    [SerializeField] private TMP_Text countText;
    [SerializeField] private Button minusButton;
    [SerializeField] private Button plusButton;

    public Action OnMinus;
    public Action OnPlus;

    private void Awake()
    {
        if (minusButton != null) minusButton.onClick.AddListener(() => OnMinus?.Invoke());
        if (plusButton != null) plusButton.onClick.AddListener(() => OnPlus?.Invoke());
    }

    public void SetLabel(string s)
    {
        if (labelText != null) labelText.text = s;
    }

    public void SetCount(int v)
    {
        if (countText != null) countText.text = v.ToString();
    }

    public void SetMinusInteractable(bool on)
    {
        if (minusButton != null) minusButton.interactable = on;
    }

    public void SetPlusInteractable(bool on)
    {
        if (plusButton != null) plusButton.interactable = on;
    }
}
