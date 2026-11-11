using System;
using TMPro;
using UnityEngine;

namespace PaletteRoom
{
    /// <summary>
    /// A small "are you sure?" box: a question, a line of detail, and Cancel / confirm buttons. It opens
    /// straight in front of the user and a little closer than the other panels, so it reads on top of them.
    /// </summary>
    public class ConfirmDialog : MonoBehaviour
    {
        const float W = 400f, H = 196f, Pad = 22f, ButtonH = 42f, Gap = 10f;

        [SerializeField] float distance = 0.6f;
        [SerializeField] float belowEyes = 0.1f;

        Transform m_Head;
        TextMeshProUGUI m_Title, m_Body, m_ConfirmLabel;
        Action m_OnConfirm;

        public void Build(Camera eventCamera)
        {
            m_Head = eventCamera.transform;
            var canvas = UIKit.CreateWorldCanvas("Confirm Canvas", new Vector2(W, H), transform, eventCamera);
            var panel = UIKit.Window(canvas.transform, 18f, modal: true);
            UIKit.Stretch(panel.rectTransform);
            var p = panel.transform;

            m_Title = UIKit.Label(p, "Title", string.Empty, 19f, UIKit.TextColor, style: FontStyles.Bold);
            UIKit.Place(m_Title.rectTransform, Pad, Pad, W - 2 * Pad, 26f);
            m_Body = UIKit.Label(p, "Body", string.Empty, 12f, UIKit.MutedTextColor);
            UIKit.Place(m_Body.rectTransform, Pad, Pad + 32f, W - 2 * Pad, 64f);

            float half = (W - 2 * Pad - Gap) / 2f;
            var cancel = UIKit.Button(p, "Cancel", "Cancel", 14f, false, Cancel);
            UIKit.Place((RectTransform)cancel.transform, Pad, H - Pad - ButtonH, half, ButtonH);
            var confirm = UIKit.Button(p, "Confirm", string.Empty, 14f, true, Confirm);
            UIKit.Place((RectTransform)confirm.transform, Pad + half + Gap, H - Pad - ButtonH, half, ButtonH);
            m_ConfirmLabel = confirm.GetComponentInChildren<TextMeshProUGUI>();
            gameObject.SetActive(false);
        }

        public void Show(string title, string body, string confirmText, Action onConfirm)
        {
            m_Title.Localize(title);
            m_Body.Localize(body);
            m_ConfirmLabel.Localize(confirmText);
            m_OnConfirm = onConfirm;
            gameObject.SetActive(true);
            UIKit.PlaceInFront(transform, m_Head, distance, belowEyes);
        }

        public void Cancel()
        {
            m_OnConfirm = null;
            gameObject.SetActive(false);
        }

        void Confirm()
        {
            var action = m_OnConfirm;
            m_OnConfirm = null;
            gameObject.SetActive(false);
            action?.Invoke();
        }
    }
}
