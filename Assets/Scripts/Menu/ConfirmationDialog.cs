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

    // The layout as authored, captured once, so a long message can grow the dialog and a short one
    // put it back.
    private bool layoutCaptured;
    private float baseFontSize, baseMessageHeight, baseMessageTop, baseDialogHeight;
    private TextAlignmentOptions baseAlignment;

    public void setConfirmationDialog(string title, string message, bool isYesNo, Action onPositive = null, Action onNegative = null)
    {
        titleText.text = title;
        setMessage(message, -1f, false);
        this.onPositive = onPositive;
        this.onNegative = onNegative;
        yesNoRect.gameObject.SetActive(isYesNo);
        confirmRect.gameObject.SetActive(!isYesNo);
        gameObject.SetActive(true);
    }

    /// <summary>An information dialog (one OK button) for longer text, left-aligned, like the mystery box odds.</summary>
    public void setInfoDialog(string title, string message, float fontSize)
    {
        setConfirmationDialog(title, message, false);
        setMessage(message, fontSize, true);
    }

    // The dialog used to be a fixed size: a message longer than about three lines ran out of it.
    // Now the message box grows to fit its text, and the dialog grows by the same amount; the
    // buttons are anchored to the bottom, so they move down with it.
    private void setMessage(string message, float fontSize, bool alignLeft)
    {
        RectTransform msg = messageText.rectTransform;
        RectTransform dialog = (RectTransform)msg.parent;
        if (!layoutCaptured)
        {
            layoutCaptured = true;
            baseFontSize = messageText.fontSize;
            baseAlignment = messageText.alignment;
            baseMessageHeight = msg.sizeDelta.y;
            baseMessageTop = msg.anchoredPosition.y + (1f - msg.pivot.y) * baseMessageHeight;
            baseDialogHeight = dialog.sizeDelta.y;
        }
        messageText.fontSize = fontSize > 0 ? fontSize : baseFontSize;
        messageText.alignment = alignLeft ? TextAlignmentOptions.TopLeft : baseAlignment;
        messageText.text = message;

        float width = msg.rect.width > 0 ? msg.rect.width : dialog.sizeDelta.x + msg.sizeDelta.x; // stretched: parent width + delta
        float needed = messageText.GetPreferredValues(message, width, 0f).y;
        float height = Mathf.Max(baseMessageHeight, needed);
        msg.sizeDelta = new Vector2(msg.sizeDelta.x, height);
        msg.anchoredPosition = new Vector2(msg.anchoredPosition.x, baseMessageTop - (1f - msg.pivot.y) * height);
        dialog.sizeDelta = new Vector2(dialog.sizeDelta.x, baseDialogHeight + (height - baseMessageHeight));
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
