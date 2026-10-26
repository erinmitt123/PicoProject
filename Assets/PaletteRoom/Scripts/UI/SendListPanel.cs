using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PaletteRoom
{
    /// <summary>
    /// Send a saved room's shopping list (pieces, sizes, dimensions, colors) to a phone number or email
    /// address, typed on the panel's own keys. Send hands the list to the messaging
    /// or email app, Share opens Android's share sheet. The list is also copied and saved as a file.
    /// </summary>
    public class SendListPanel : MonoBehaviour
    {
        const float W = 460f, Pad = 16f, ButtonH = 30f;

        Transform m_Head;
        TextMeshProUGUI m_Title, m_Preview, m_Status;
        TMP_InputField m_To;
        Lookbook.Entry m_Entry;

        /// <summary>Raised when the user closes the panel (so the lookbook can come back).</summary>
        public event System.Action Closed;

        public void Build(Camera eventCamera)
        {
            m_Head = eventCamera.transform;
            const float previewH = 150f;
            float height = Pad + 24f + 8f + previewH + 10f + 18f + 34f + 8f + KeyboardHeight + 10f + ButtonH + 8f + 30f + Pad;
            var canvas = UIKit.CreateWorldCanvas("Send List Canvas", new Vector2(W, height), transform, eventCamera);
            var panel = UIKit.Panel(canvas.transform, "Panel", UIKit.PanelColor, 16f);
            UIKit.Stretch(panel.rectTransform);
            var p = panel.transform;

            m_Title = UIKit.Label(p, "Title", string.Empty, 14f, UIKit.TextColor, style: FontStyles.Bold);
            UIKit.Place(m_Title.rectTransform, Pad, Pad, W - 2 * Pad, 22f);
            float y = Pad + 24f + 8f;

            var previewBg = UIKit.Panel(p, "Preview", new Color(1f, 1f, 1f, 0.7f), 8f);
            UIKit.Place(previewBg.rectTransform, Pad, y, W - 2 * Pad, previewH);
            m_Preview = UIKit.Label(previewBg.transform, "Text", string.Empty, 8.5f, UIKit.TextColor);
            m_Preview.overflowMode = TextOverflowModes.Ellipsis;
            UIKit.Stretch(m_Preview.rectTransform, 8f);
            y += previewH + 10f;

            var label = UIKit.Label(p, "To Label", "Send to a phone number or email address", 10f, UIKit.MutedTextColor);
            UIKit.Place(label.rectTransform, Pad, y, W - 2 * Pad, 16f);
            y += 18f;
            m_To = InputField(p, Pad, y, W - 2 * Pad, 32f);
            y += 34f + 8f;
            BuildKeyboard(p, Pad, y, W - 2 * Pad);
            y += KeyboardHeight + 10f;

            float third = (W - 2 * Pad - 16f) / 3f;
            var send = UIKit.Button(p, "Send", "Send", 12f, true, Send);
            UIKit.Place((RectTransform)send.transform, Pad, y, third, ButtonH);
            var share = UIKit.Button(p, "Share", "Share…", 11f, false, Share);
            UIKit.Place((RectTransform)share.transform, Pad + third + 8f, y, third, ButtonH);
            var close = UIKit.Button(p, "Close", "Close", 11f, false, () => { gameObject.SetActive(false); Closed?.Invoke(); });
            UIKit.Place((RectTransform)close.transform, Pad + 2f * (third + 8f), y, third, ButtonH);
            y += ButtonH + 8f;

            m_Status = UIKit.Label(p, "Status", string.Empty, 9.5f, UIKit.MutedTextColor);
            UIKit.Place(m_Status.rectTransform, Pad, y, W - 2 * Pad, 30f);
            gameObject.SetActive(false);
        }

        /// <summary>A text field for the address; selecting it opens the system keyboard on the headset.</summary>
        static TMP_InputField InputField(Transform parent, float x, float y, float w, float h)
        {
            var bg = UIKit.Panel(parent, "To", Color.white, 8f);
            UIKit.Place(bg.rectTransform, x, y, w, h);
            var viewport = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
            viewport.SetParent(bg.transform, false);
            UIKit.Stretch(viewport, 8f);
            var text = UIKit.Label(viewport, "Text", string.Empty, 12f, UIKit.TextColor, TextAlignmentOptions.MidlineLeft);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            UIKit.Stretch(text.rectTransform);
            var placeholder = UIKit.Label(viewport, "Placeholder", "e.g. you@example.com or 555 123 4567", 11f, UIKit.MutedTextColor,
                TextAlignmentOptions.MidlineLeft, FontStyles.Italic);
            UIKit.Stretch(placeholder.rectTransform);
            var field = bg.gameObject.AddComponent<TMP_InputField>();
            field.targetGraphic = bg;
            field.textViewport = viewport;
            field.textComponent = text;
            field.placeholder = placeholder;
            field.contentType = TMP_InputField.ContentType.EmailAddress; // keyboard with @ and digits
            field.lineType = TMP_InputField.LineType.SingleLine;
            field.shouldHideMobileInput = false;
            // The panel has its own keys: the system keyboard doesn't come up for every headset or the
            // emulator, and when it does it takes focus away from the app.
            field.shouldHideSoftKeyboard = true;
            return field;
        }

        // ---- On-panel keyboard -----------------------------------------------------------------

        const float KeyH = 28f, KeyGap = 4f;
        const float KeyboardHeight = 5 * KeyH + 4 * KeyGap;
        static readonly string[] KeyRows = { "1234567890", "qwertyuiop", "asdfghjkl@", "zxcvbnm.-_" };

        void BuildKeyboard(Transform parent, float x, float y, float width)
        {
            float key = (width - 9 * KeyGap) / 10f;
            for (int r = 0; r < KeyRows.Length; r++)
            {
                for (int i = 0; i < KeyRows[r].Length; i++)
                {
                    string ch = KeyRows[r][i].ToString();
                    var b = UIKit.Button(parent, "Key " + ch, ch, 12f, false, () => Type(ch));
                    UIKit.Place((RectTransform)b.transform, x + i * (key + KeyGap), y + r * (KeyH + KeyGap), key, KeyH);
                }
            }
            // Bottom row: .com, +, space, delete, clear.
            float rowY = y + 4 * (KeyH + KeyGap);
            float cx = x;
            void Wide(string name, string label, float keys, UnityEngine.Events.UnityAction onClick)
            {
                float w = keys * key + (keys - 1f) * KeyGap;
                var b = UIKit.Button(parent, name, label, 11f, false, onClick);
                UIKit.Place((RectTransform)b.transform, cx, rowY, w, KeyH);
                cx += w + KeyGap;
            }
            Wide("Key .com", ".com", 2f, () => Type(".com"));
            Wide("Key +", "+", 1f, () => Type("+"));
            Wide("Key Space", "space", 3f, () => Type(" "));
            Wide("Key Delete", "Delete", 2f, Backspace);
            Wide("Key Clear", "Clear", 2f, () => SetAddress(string.Empty));
        }

        void Type(string s) => SetAddress(m_To.text + s);

        void Backspace()
        {
            var t = m_To.text;
            if (t.Length > 0)
                SetAddress(t.Substring(0, t.Length - 1));
        }

        void SetAddress(string text)
        {
            m_To.text = text;
            m_To.caretPosition = text.Length;
            m_Status.text = string.Empty;
        }

        public void Open(Lookbook.Entry entry, Transform anchor)
        {
            m_Entry = entry;
            m_Title.text = $"Send the list for \"{entry.title}\"";
            m_Preview.text = RoomList.Build(entry);
            m_Status.text = string.Empty;
            gameObject.SetActive(true);
            if (anchor != null)
            {
                // Take the place of the panel it was opened from (that panel hides meanwhile).
                transform.SetPositionAndRotation(anchor.position, anchor.rotation);
            }
            else
            {
                UIKit.PlaceInFront(transform, m_Head, 0.85f, 0.15f);
            }
        }

        void Send()
        {
            if (m_Entry == null)
                return;
            var to = m_To.text;
            var outcome = ListSender.Send(to, RoomList.Subject(m_Entry), RoomList.Build(m_Entry), out var saved);
            Report(outcome, saved, ListSender.LooksLikeEmail(to) ? "email" : "messages");
        }

        void Share()
        {
            if (m_Entry == null)
                return;
            var outcome = ListSender.Share(RoomList.Subject(m_Entry), RoomList.Build(m_Entry), out var saved);
            Report(outcome, saved, "share");
        }

        void Report(ListSender.Outcome outcome, string saved, string app)
        {
            switch (outcome)
            {
                case ListSender.Outcome.Opened:
                    m_Status.text = app == "share"
                        ? "Pick an app to send it with. (The list is also copied.)"
                        : $"Opened your {app} app with the list filled in — press send there. (Also copied.)";
                    break;
                case ListSender.Outcome.Invalid:
                    m_Status.text = "Enter a phone number (at least 7 digits) or an email address.";
                    break;
                default:
                    m_Status.text = $"No app on this headset can send it, so it's copied to the clipboard and saved to {saved}.";
                    break;
            }
            Debug.Log($"[PaletteRoom] Room list: {outcome} ({app}); saved to {saved}.");
        }
    }
}
