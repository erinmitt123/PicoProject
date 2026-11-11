using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PaletteRoom
{
    /// <summary>
    /// The launch intro, about four seconds: five swatches of the palette sweep in from around the user
    /// with a whoosh and snap into a row, the title lands with a chime, the tagline fades up, and the
    /// whole card drifts away to make room for the welcome panel. Floats in front of the user, drawn over
    /// the room like the rest of the interface.
    /// </summary>
    public class IntroSequence : MonoBehaviour
    {
        const float W = 620f, H = 300f, Swatch = 64f, SwatchGap = 14f;
        const float Distance = 1.3f, BelowEyes = 0.05f;

        public IEnumerator Play(Camera eventCamera)
        {
            var canvas = UIKit.CreateWorldCanvas("Intro Canvas", new Vector2(W, H), transform, eventCamera);
            var root = (RectTransform)canvas.transform;
            var group = canvas.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            UIKit.PlaceInFront(transform, eventCamera.transform, Distance, BelowEyes);

            // Swatches: start scattered (off to the sides, tilted, small) and fly to a centred row.
            var swatches = new RectTransform[5];
            var from = new Vector2[5];
            var spin = new float[5];
            var rng = new System.Random(5);
            float rowW = 5 * Swatch + 4 * SwatchGap;
            for (int i = 0; i < 5; i++)
            {
                var s = UIKit.Panel(root, $"Swatch {i + 1}", UIKit.ThemeColor(i), 16f);
                s.raycastTarget = false;
                var rt = s.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(Swatch, Swatch);
                swatches[i] = rt;
                float side = i % 2 == 0 ? -1f : 1f;
                from[i] = new Vector2(side * (420f + (float)rng.NextDouble() * 260f), -160f + (float)rng.NextDouble() * 320f);
                spin[i] = side * (90f + (float)rng.NextDouble() * 160f);
            }
            var title = UIKit.Label(root, "Title", "Palette Room", 64f, UIKit.TextColor, TextAlignmentOptions.Center, FontStyles.Bold);
            UIKit.Stretch(title.rectTransform);
            title.rectTransform.offsetMin = new Vector2(0f, H / 2f - 52f);
            title.rectTransform.offsetMax = new Vector2(0f, -(H / 2f - 92f));
            var tagline = UIKit.Label(root, "Tagline", "Design your room in color", 24f, UIKit.MutedTextColor, TextAlignmentOptions.Center);
            UIKit.Stretch(tagline.rectTransform);
            tagline.rectTransform.offsetMin = new Vector2(0f, 6f);
            tagline.rectTransform.offsetMax = new Vector2(0f, -(H - 40f));
            // A soft card behind the words so they read over a bright room.
            var card = UIKit.Panel(root, "Card", new Color(0.97f, 0.96f, 0.94f, 0.82f), 28f);
            card.raycastTarget = false;
            card.transform.SetAsFirstSibling();
            UIKit.Stretch(card.rectTransform);

            const float fly = 0.85f, stagger = 0.09f, titleAt = 1.05f, taglineAt = 1.5f, holdUntil = 3.3f, fadeOut = 0.6f;
            Sfx.Play(SfxKind.Whoosh);
            bool chimed = false;
            float start = Time.unscaledTime;
            for (float t = 0f; t < holdUntil + fadeOut; t = Time.unscaledTime - start)
            {
                for (int i = 0; i < 5; i++)
                {
                    float k = Mathf.Clamp01((t - i * stagger) / fly);
                    float e = EaseOutBack(k);
                    var to = new Vector2(-rowW / 2f + Swatch / 2f + i * (Swatch + SwatchGap), -H / 2f + 70f + Swatch / 2f);
                    // Settled swatches breathe very slightly, out of step with each other.
                    float breathe = k >= 1f ? 1f + 0.03f * Mathf.Sin((t - fly) * 3f + i) : 1f;
                    swatches[i].anchoredPosition = Vector2.LerpUnclamped(from[i], to, e);
                    swatches[i].localRotation = Quaternion.Euler(0f, 0f, spin[i] * (1f - e));
                    swatches[i].localScale = Vector3.one * Mathf.Lerp(0.3f, 1f, Mathf.Clamp01(e)) * breathe;
                }
                if (!chimed && t >= titleAt)
                {
                    chimed = true;
                    Sfx.Play(SfxKind.Success, 0.8f);
                }
                float tk = Mathf.Clamp01((t - titleAt) / 0.45f);
                title.alpha = tk;
                title.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.85f, 1f, EaseOutBack(tk));
                tagline.alpha = Mathf.Clamp01((t - taglineAt) / 0.5f);
                card.color = new Color(0.97f, 0.96f, 0.94f, 0.82f * Mathf.Clamp01(t / 0.6f));

                float fk = Mathf.Clamp01((t - holdUntil) / fadeOut);
                group.alpha = 1f - fk * fk;
                root.localPosition = Vector3.forward * (0.15f * fk * fk); // drifts back as it fades
                yield return null;
            }
            Destroy(gameObject);
        }

        static float EaseOutBack(float k)
        {
            const float c = 1.5f;
            float u = k - 1f;
            return 1f + (c + 1f) * u * u * u + c * u * u;
        }
    }
}
