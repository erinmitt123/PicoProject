using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace PaletteRoom
{
    /// <summary>
    /// The user's lookbook: every revealed room, kept on the headset (JSON plus a snapshot PNG in the
    /// app's persistent data folder). Pieces are stored relative to the room's centre, so a design can
    /// be laid out again in front of the user in any room.
    /// </summary>
    public static class Lookbook
    {
        [Serializable]
        public class ZonePick
        {
            public string zone;
            public int slot; // PaletteSlot
        }

        [Serializable]
        public class Piece
        {
            public string kind;
            public string variant;
            public Vector3 offset;   // from the design's centre, in its frame (+Z = toward the viewer)
            public float yaw;        // relative to the design's frame
            public Vector3 euler;    // full local rotation, for tilted pieces such as pillows
            public List<ZonePick> picks = new List<ZonePick>();
            public List<PartColor> colors = new List<PartColor>(); // what each part looked like when saved
        }

        [Serializable]
        public class PartColor
        {
            public string part;
            public string hex;
            public bool natural; // a fixed finish (oak, brass…) rather than a palette color
        }

        [Serializable]
        public class Entry
        {
            public string id;
            public string title;
            public string briefId;
            public int stars;
            public string date;
            public string paletteName;
            public string[] paletteHex;
            public string snapshotFile;
            public List<Piece> pieces = new List<Piece>();
        }

        [Serializable]
        class Store
        {
            public List<Entry> entries = new List<Entry>();
        }

        static Store s_Store;

        static string Folder => Path.Combine(Application.persistentDataPath, "lookbook");
        static string IndexPath => Path.Combine(Folder, "entries.json");

        public static IReadOnlyList<Entry> Entries => Load().entries;

        public static event Action Changed;

        static Store Load()
        {
            if (s_Store != null)
                return s_Store;
            s_Store = new Store();
            try
            {
                if (File.Exists(IndexPath))
                    s_Store = JsonUtility.FromJson<Store>(File.ReadAllText(IndexPath)) ?? new Store();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PaletteRoom] Couldn't read the lookbook: {e.Message}");
            }
            return s_Store;
        }

        /// <summary>Best stars earned so far on a brief (0 if never revealed).</summary>
        public static int BestStars(string briefId)
        {
            int best = 0;
            foreach (var e in Load().entries)
                if (e.briefId == briefId)
                    best = Mathf.Max(best, e.stars);
            return best;
        }

        /// <summary>Saves a revealed room, newest first, with its snapshot.</summary>
        public static Entry Save(Entry entry, Texture2D snapshot)
        {
            Directory.CreateDirectory(Folder);
            entry.id = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            entry.date = DateTime.Now.ToString("MMM d, h:mm tt");
            if (snapshot != null)
            {
                entry.snapshotFile = entry.id + ".png";
                File.WriteAllBytes(Path.Combine(Folder, entry.snapshotFile), snapshot.EncodeToPNG());
            }
            Load().entries.Insert(0, entry);
            File.WriteAllText(IndexPath, JsonUtility.ToJson(s_Store, true));
            Debug.Log($"[PaletteRoom] Saved \"{entry.title}\" to the lookbook ({entry.pieces.Count} pieces, {entry.stars}★).");
            Changed?.Invoke();
            return entry;
        }

        public static Texture2D LoadSnapshot(Entry entry)
        {
            if (string.IsNullOrEmpty(entry.snapshotFile))
                return null;
            var path = Path.Combine(Folder, entry.snapshotFile);
            if (!File.Exists(path))
                return null;
            var tex = new Texture2D(2, 2);
            tex.LoadImage(File.ReadAllBytes(path));
            return tex;
        }

        /// <summary>Captures the room's pieces relative to their centre, facing the viewer.</summary>
        public static List<Piece> Capture(IReadOnlyList<FurnitureItem> items, Vector3 viewer, out Vector3 centre, out Quaternion frame)
        {
            centre = Vector3.zero;
            int n = 0;
            foreach (var item in items)
            {
                if (item == null)
                    continue;
                centre += item.transform.position;
                n++;
            }
            centre = n > 0 ? centre / n : viewer;
            var toViewer = Vector3.ProjectOnPlane(viewer - centre, Vector3.up);
            frame = Quaternion.LookRotation(toViewer.sqrMagnitude > 0.01f ? toViewer.normalized : Vector3.forward, Vector3.up);
            var inverse = Quaternion.Inverse(frame);

            var pieces = new List<Piece>();
            foreach (var item in items)
            {
                if (item == null)
                    continue;
                var local = inverse * item.transform.rotation;
                var piece = new Piece
                {
                    kind = item.Kind.ToString(),
                    variant = item.VariantId,
                    offset = inverse * (item.transform.position - centre),
                    yaw = local.eulerAngles.y,
                    euler = local.eulerAngles,
                };
                foreach (var zone in item.Zones)
                {
                    if (zone.Override.HasValue)
                        piece.picks.Add(new ZonePick { zone = zone.ZoneName, slot = (int)zone.Override.Value });
                    if (!zone.AllowOverride)
                        continue; // bulbs, soil, leaves: not something you'd shop for by color
                    piece.colors.Add(new PartColor
                    {
                        part = zone.ZoneName,
                        hex = "#" + ColorUtility.ToHtmlStringRGB(zone.CurrentColor),
                        natural = !zone.FollowsPalette,
                    });
                }
                pieces.Add(piece);
            }
            return pieces;
        }

        public static ColorPalette PaletteOf(Entry entry)
        {
            foreach (var p in PalettePresets.All)
                if (p.name == entry.paletteName)
                    return p;
            if (entry.paletteHex == null || entry.paletteHex.Length < 5)
                return null;
            return new ColorPalette(entry.paletteName, HarmonyRule.Analogous,
                entry.paletteHex[0], entry.paletteHex[1], entry.paletteHex[2], entry.paletteHex[3], entry.paletteHex[4]);
        }
    }
}
