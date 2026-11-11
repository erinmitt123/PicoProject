using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace PaletteRoom
{
    /// <summary>
    /// Button icons from Google's Material Icons (Round), Apache 2.0, shipped as
    /// Resources/Fonts/MaterialIconsRound-Regular.otf. The font is added as a TextMesh Pro fallback, so an
    /// icon is just its private-use character in any label. Codepoints are from the font's codepoints file.
    /// </summary>
    public static class Icons
    {
        public const string Settings = "";
        public const string Palette = "";
        public const string ColorWheel = "";   // color_lens
        public const string Shopping = "";     // shopping_cart
        public const string Lookbook = "";     // auto_stories
        public const string Briefs = "";       // emoji_events
        public const string Tutorial = "";     // school
        public const string Floor = "";        // vertical_align_bottom
        public const string Exit = "";         // logout
        public const string Home = "";
        public const string Language = "";
        public const string Close = "";
        public const string Add = "";
        public const string Remove = "";
        public const string Check = "";
        public const string Reset = "";        // restart_alt
        public const string Size = "";         // aspect_ratio
        public const string Music = "";
        public const string Volume = "";
        public const string Send = "";
        public const string Save = "";
        public const string Delete = "";
        public const string Back = "";
        public const string Next = "";
        public const string Play = "";
        public const string Star = "";
        public const string Eye = "";          // visibility
        public const string Hand = "";         // pan_tool
        public const string Scan = "";         // 360
        public const string Touch = "";        // touch_app

        static TMP_FontAsset s_Asset;

        /// <summary>Adds the icon font as a fallback of the default font. Call once, before labels are made.</summary>
        public static void Ensure()
        {
            if (s_Asset != null)
                return;
            var font = Resources.Load<Font>("Fonts/MaterialIconsRound-Regular");
            var main = TMP_Settings.defaultFontAsset;
            if (font == null || main == null)
            {
                Debug.LogWarning("[PaletteRoom] Icon font not found; buttons show text only.");
                return;
            }
            s_Asset = TMP_FontAsset.CreateFontAsset(font, 64, 6, GlyphRenderMode.SDFAA, 512, 512, AtlasPopulationMode.Dynamic, true);
            if (s_Asset == null)
                return;
            s_Asset.name = "Material Icons Round (dynamic)";
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
