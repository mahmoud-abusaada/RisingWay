using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ConfirmationDialog : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI messageText;
    [SerializeField] private RectTransform yesNoRect;
    [SerializeField] private RectTransform confirmRect;
    private Action onPositive = null;
    private Action onNegative = null;

    public void setConfirmationDialog(string title, string message, bool isYesNo, Action onPositive = null, Action onNegative = null)
    {
        titleText.text = title;
        messageText.text = message;
        this.onPositive = onPositive;
        this.onNegative = onNegative;
        yesNoRect.gameObject.SetActive(isYesNo);
        confirmRect.gameObject.SetActive(!isYesNo);
        gameObject.SetActive(true);
    }

    public void PositiveButton()
    {
        onPositive?.Invoke();
        gameObject.SetActive(false);
    }

    public void NegativeButton()
    {
        onNegative?.Invoke();
        gameObject.SetActive(false);
    }

    public bool isShowing()
    {
        return gameObject.activeSelf;
    }
}
