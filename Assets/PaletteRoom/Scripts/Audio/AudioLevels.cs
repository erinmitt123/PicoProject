using System;
using UnityEngine;

namespace PaletteRoom
{
    /// <summary>
    /// The user's sound levels (0–1 as shown on the sliders): master, music, effects and the tutorial
    /// voice. Master drives the whole app through <see cref="AudioListener.volume"/>; the others are read by
    /// <see cref="AmbientMusic"/>, <see cref="Sfx"/> and the tutorial. Kept between sessions in
    /// <see cref="SaveData"/>.
    /// </summary>
    public static class AudioLevels
    {
        public const float DefaultMaster = 1f, DefaultMusic = 0.5f, DefaultEffects = 0.8f, DefaultVoice = 0.9f;

        static bool s_Loaded, s_Dirty;
        static float s_Master, s_Music, s_Effects, s_Voice;

        /// <summary>Raised when any level changes.</summary>
        public static event Action Changed;

        public static float Master { get { Ensure(); return s_Master; } set => Set(ref s_Master, value); }
        public static float Music { get { Ensure(); return s_Music; } set => Set(ref s_Music, value); }
        public static float Effects { get { Ensure(); return s_Effects; } set => Set(ref s_Effects, value); }
        public static float Voice { get { Ensure(); return s_Voice; } set => Set(ref s_Voice, value); }

        /// <summary>Volume to play music at: the level on a curve that sounds even across the slider.</summary>
        public static float MusicGain => Gain(Music);
        public static float EffectsGain => Gain(Effects);
        public static float VoiceGain => Gain(Voice);

        // Loudness is heard roughly logarithmically, so a squared slider feels evenly spaced.
        static float Gain(float level) => level * level;

        /// <summary>Loads the saved levels and applies the master volume. Safe to call more than once.</summary>
        public static void Ensure()
        {
            if (s_Loaded)
                return;
            s_Loaded = true;
            if (!SaveData.GetAudioLevels(out s_Master, out s_Music, out s_Effects))
            {
                s_Master = DefaultMaster;
                s_Music = DefaultMusic;
                s_Effects = DefaultEffects;
            }
            if (!SaveData.GetVoiceLevel(out s_Voice))
                s_Voice = DefaultVoice;
            AudioListener.volume = Gain(s_Master);
        }

        public static void ResetToDefaults()
        {
            Ensure();
            s_Master = DefaultMaster;
            s_Music = DefaultMusic;
            s_Effects = DefaultEffects;
            s_Voice = DefaultVoice;
            Apply();
        }

        /// <summary>Writes the levels to the headset if they changed. Called when the settings close and on pause.</summary>
        public static void Save()
        {
            if (!s_Dirty)
                return;
            s_Dirty = false;
            SaveData.SaveAudioLevels(s_Master, s_Music, s_Effects);
            SaveData.SaveVoiceLevel(s_Voice);
        }

        static void Set(ref float field, float value)
        {
            Ensure();
            value = Mathf.Clamp01(value);
            if (Mathf.Approximately(field, value))
                return;
            field = value;
            Apply();
        }

        static void Apply()
        {
            s_Dirty = true;
            AudioListener.volume = Gain(s_Master);
            Changed?.Invoke();
        }
    }
}
