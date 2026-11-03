using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace PaletteRoom
{
    /// <summary>
    /// What carries over from one session to the next, kept on the headset as JSON in the app's
    /// persistent data folder: the palettes the user made, the color wheel's set colors, the palette in
    /// use, the room they were last working on, and whether they've been offered the tutorial.
    /// Revealed rooms are kept separately, in the <see cref="Lookbook"/>.
    /// </summary>
    public static class SaveData
    {
        [Serializable]
        class SavedPalette
        {
            public string name;
            public int harmony; // HarmonyRule
            public Color[] colors; // in PaletteSlot order
        }

        [Serializable]
        class SavedSetColor
        {
            public int slot; // PaletteSlot
            public Color color;
            public int order;
        }

        [Serializable]
        class Store
        {
            public List<SavedPalette> customPalettes = new List<SavedPalette>();
            public string currentPalette;
            public List<SavedSetColor> wheelColors = new List<SavedSetColor>();
            public bool hasLastRoom;
            public Lookbook.Entry lastRoom = new Lookbook.Entry();
            public bool tutorialSeen;
        }

        static readonly PaletteSlot[] Slots = (PaletteSlot[])Enum.GetValues(typeof(PaletteSlot));

        static Store s_Store;

        static string FilePath => Path.Combine(Application.persistentDataPath, "palette-room.json");

        static Store Load()
        {
            if (s_Store != null)
                return s_Store;
            s_Store = new Store();
            try
            {
                if (File.Exists(FilePath))
                    s_Store = JsonUtility.FromJson<Store>(File.ReadAllText(FilePath)) ?? new Store();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PaletteRoom] Couldn't read saved data: {e.Message}");
            }
            return s_Store;
        }

        static void Write()
        {
            try
            {
                File.WriteAllText(FilePath, JsonUtility.ToJson(Load(), true));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PaletteRoom] Couldn't save: {e.Message}");
            }
        }

        // ---- palettes ------------------------------------------------------------------------

        /// <summary>The palettes the user made on the color wheel, oldest first.</summary>
        public static List<ColorPalette> CustomPalettes()
        {
            var list = new List<ColorPalette>();
            foreach (var saved in Load().customPalettes)
            {
                if (saved.colors == null || saved.colors.Length < Slots.Length)
                    continue;
                var c = saved.colors;
                list.Add(new ColorPalette(saved.name, (HarmonyRule)saved.harmony, c[0], c[1], c[2], c[3], c[4]));
            }
            return list;
        }

        public static void SaveCustomPalettes(IEnumerable<ColorPalette> palettes)
        {
            var store = Load();
            store.customPalettes.Clear();
            foreach (var palette in palettes)
            {
                var colors = new Color[Slots.Length];
                for (int i = 0; i < Slots.Length; i++)
                    colors[i] = palette.Get(Slots[i]);
                store.customPalettes.Add(new SavedPalette { name = palette.name, harmony = (int)palette.harmony, colors = colors });
            }
            Write();
        }

        /// <summary>The name of the palette the room was last styled with.</summary>
        public static string CurrentPalette
        {
            get => Load().currentPalette;
            set
            {
                if (Load().currentPalette == value)
                    return;
                s_Store.currentPalette = value;
                Write();
            }
        }

        /// <summary>The colors pinned on the color wheel, so the user can keep refining them next time.</summary>
        public static List<ColorHarmony.SetColor> WheelColors()
        {
            var list = new List<ColorHarmony.SetColor>();
            foreach (var saved in Load().wheelColors)
                list.Add(new ColorHarmony.SetColor((PaletteSlot)saved.slot, saved.color, saved.order));
            return list;
        }

        public static void SaveWheelColors(IEnumerable<ColorHarmony.SetColor> colors)
        {
            var store = Load();
            store.wheelColors.Clear();
            foreach (var c in colors)
                store.wheelColors.Add(new SavedSetColor { slot = (int)c.slot, color = c.color, order = c.order });
            Write();
        }

        // ---- the room in progress ------------------------------------------------------------

        public static bool HasLastRoom => Load().hasLastRoom && s_Store.lastRoom?.pieces != null && s_Store.lastRoom.pieces.Count > 0;

        /// <summary>The room the user was last working on, or null if there isn't one.</summary>
        public static Lookbook.Entry LastRoom => HasLastRoom ? s_Store.lastRoom : null;

        public static void SaveLastRoom(Lookbook.Entry entry)
        {
            var store = Load();
            store.lastRoom = entry;
            store.hasLastRoom = entry != null;
            Write();
        }

        // ---- tutorial ------------------------------------------------------------------------

        /// <summary>True once the user has taken, skipped or declined the tutorial; it's offered at start-up until then.</summary>
        public static bool TutorialSeen
        {
            get => Load().tutorialSeen;
            set
            {
                if (Load().tutorialSeen == value)
                    return;
                s_Store.tutorialSeen = value;
                Write();
            }
        }
    }
}
