using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace PaletteRoom
{
    public enum Language
    {
        English,
        Chinese,
    }

    /// <summary>
    /// The app's languages: English (the source text, written in the code) and Simplified Chinese (the
    /// table in <see cref="Strings"/>, keyed by the English text). Labels set through
    /// <see cref="LocExtensions.Localize"/> re-render when the language changes, so switching in Settings
    /// is immediate. The choice is kept in <see cref="SaveData"/>; the first launch follows the headset's
    /// system language.
    /// </summary>
    public static class Loc
    {
        static bool s_Loaded;
        static Language s_Language;

        /// <summary>Raised after the language changes (labels have already re-rendered).</summary>
        public static event Action Changed;

        public static Language Current
        {
            get
            {
                Ensure();
                return s_Language;
            }
            set
            {
                Ensure();
                if (s_Language == value)
                    return;
                s_Language = value;
                SaveData.LanguageCode = value == Language.Chinese ? "zh" : "en";
                LocalizedText.RefreshAll();
                Debug.Log($"[PaletteRoom] Language: {value}.");
                Changed?.Invoke();
            }
        }

        public static bool IsEnglish => Current == Language.English;

        static void Ensure()
        {
            if (s_Loaded)
                return;
            s_Loaded = true;
            string saved = SaveData.LanguageCode;
            if (saved == "zh")
                s_Language = Language.Chinese;
            else if (saved == "en")
                s_Language = Language.English;
            else
            {
                var system = Application.systemLanguage;
                s_Language = system == SystemLanguage.ChineseSimplified || system == SystemLanguage.Chinese || system == SystemLanguage.ChineseTraditional
                    ? Language.Chinese
                    : Language.English;
            }
        }

        /// <summary>The text in the current language (the English itself when there's no translation).</summary>
        public static string T(string english)
        {
            if (string.IsNullOrEmpty(english) || Current == Language.English)
                return english;
            if (Strings.Zh.TryGetValue(english, out var zh))
                return zh;
            ReportMissing(english);
            return english;
        }

        /// <summary><see cref="string.Format(string, object[])"/> on the translated format.</summary>
        public static string F(string format, params object[] args) => string.Format(T(format), args);

        /// <summary>A format argument that is itself text to translate (a furniture or palette name).</summary>
        public static LocArg Arg(string english) => new LocArg(english);

        /// <summary>A format argument worked out again whenever the label renders (so it follows a language change).</summary>
        public static LazyArg Lazy(Func<string> text) => new LazyArg(text);

        /// <summary>Translated items joined into a list: "sofa, rug" / "沙发、地毯".</summary>
        public static string Join(IEnumerable<string> english)
        {
            var parts = new List<string>();
            foreach (var e in english)
                parts.Add(T(e));
            return string.Join(IsEnglish ? ", " : "、", parts);
        }

        /// <summary>A plain-words color name from <see cref="ColorNames"/> ("dark navy") in the current language.</summary>
        public static string ColorName(string english)
        {
            if (Current == Language.English || string.IsNullOrEmpty(english))
                return english;
            if (Strings.Zh.TryGetValue(english, out var whole))
                return whole;
            int space = english.IndexOf(' ');
            if (space > 0 && Strings.Zh.TryGetValue(english.Substring(0, space), out var tone) &&
                Strings.Zh.TryGetValue(english.Substring(space + 1), out var hue))
                return tone + hue;
            return english;
        }

        static HashSet<string> s_Missing;

        static void ReportMissing(string english)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            s_Missing ??= new HashSet<string>();
            // Pure numbers and symbols need no translation, and text already in Chinese has been translated.
            bool hasLetter = false;
            foreach (char c in english)
            {
                if (c >= '　')
                    return;
                hasLetter |= char.IsLetter(c);
            }
            if (hasLetter && s_Missing.Add(english))
                Debug.LogWarning($"[PaletteRoom] No Chinese for \"{english}\".");
#endif
        }
    }

    /// <summary>Translatable text passed as a format argument; it translates when the label renders.</summary>
    public readonly struct LocArg
    {
        readonly string m_English;

        public LocArg(string english) => m_English = english;

        public override string ToString() => Loc.T(m_English);
    }

    /// <summary>A format argument computed when the label renders.</summary>
    public sealed class LazyArg
    {
        readonly Func<string> m_Text;

        public LazyArg(Func<string> text) => m_Text = text;

        public override string ToString() => m_Text?.Invoke() ?? string.Empty;
    }

    /// <summary>
    /// Remembers what a label says (an English format and its arguments) so it can be shown again in
    /// another language. Added by <see cref="LocExtensions.Localize"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public class LocalizedText : MonoBehaviour
    {
        static readonly List<LocalizedText> s_All = new List<LocalizedText>();

        TMP_Text m_Label;
        string m_Format;
        object[] m_Args;
        bool m_Registered;

        public void Set(string format, object[] args)
        {
            m_Format = format;
            m_Args = args != null && args.Length > 0 ? args : null;
            if (!m_Registered)
            {
                s_All.Add(this);
                m_Registered = true;
            }
            Render();
        }

        /// <summary>Stops re-rendering: the label's text is now set directly (numbers, user input).</summary>
        public void Release()
        {
            m_Format = null;
            m_Args = null;
        }

        void Render()
        {
            if (m_Format == null)
                return;
            if (m_Label == null)
                m_Label = GetComponent<TMP_Text>();
            if (m_Label == null)
                return;
            m_Label.text = m_Args == null ? Loc.T(m_Format) : string.Format(Loc.T(m_Format), m_Args);
        }

        void OnDestroy() => s_All.Remove(this);

        public static void RefreshAll()
        {
            for (int i = s_All.Count - 1; i >= 0; i--)
            {
                var t = s_All[i];
                if (t == null)
                    s_All.RemoveAt(i);
                else
                    t.Render();
            }
        }
    }

    public static class LocExtensions
    {
        /// <summary>
        /// Shows <paramref name="format"/> (English, with {0}-style arguments) in the current language, and
        /// again in a new language when it changes. Wrap translatable arguments in <see cref="Loc.Arg"/>.
        /// </summary>
        public static void Localize(this TMP_Text label, string format, params object[] args)
        {
            if (label == null)
                return;
            if (string.IsNullOrEmpty(format))
            {
                label.SetPlain(string.Empty);
                return;
            }
            var lt = label.GetComponent<LocalizedText>();
            if (lt == null)
                lt = label.gameObject.AddComponent<LocalizedText>();
            lt.Set(format, args);
        }

        /// <summary>Sets text that isn't translated (counts, names the user typed), so a language change leaves it alone.</summary>
        public static void SetPlain(this TMP_Text label, string text)
        {
            if (label == null)
                return;
            var lt = label.GetComponent<LocalizedText>();
            if (lt != null)
                lt.Release();
            label.text = text;
        }
    }

    /// <summary>
    /// Chinese characters come from Noto Sans SC (SIL Open Font License, in Resources/Fonts), added at
    /// start-up as a dynamic fallback to the UI font, so any label can show either language.
    /// </summary>
    public static class CjkFont
    {
        static TMP_FontAsset s_Asset;

        public static TMP_FontAsset Asset => s_Asset;

        public static void Ensure()
        {
            if (s_Asset != null)
                return;
            var font = Resources.Load<Font>("Fonts/NotoSansSC-Regular");
            var main = TMP_Settings.defaultFontAsset;
            if (font == null || main == null)
            {
                Debug.LogWarning("[PaletteRoom] Chinese font not found; Chinese text will show as boxes.");
                return;
            }
            s_Asset = TMP_FontAsset.CreateFontAsset(font, 72, 8, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, true);
            if (s_Asset == null)
                return;
            s_Asset.name = "Noto Sans SC (dynamic)";
            UIKit.MakeOverlay(s_Asset.material);
            main.fallbackFontAssetTable ??= new List<TMP_FontAsset>();
            if (!main.fallbackFontAssetTable.Contains(s_Asset))
                main.fallbackFontAssetTable.Add(s_Asset);
#if UNITY_EDITOR
            // The default font is a project asset: don't leave a runtime-only fallback in it after Play.
            Application.quitting += () => main.fallbackFontAssetTable.Remove(s_Asset);
#endif
        }
    }
}
