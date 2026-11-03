using UnityEngine;
using UnityEngine.XR;

namespace PaletteRoom
{
    /// <summary>
    /// Tells a real headset apart from an emulator: the Editor with no headset streaming (Play mode with
    /// the mouse or the XR Device Simulator), or an Android emulator. Steps that need the real world, such
    /// as tapping the floor, are skipped there.
    /// </summary>
    public static class Emulator
    {
        static bool? s_AndroidEmulator;

        public static bool IsActive
        {
            get
            {
                if (Application.isEditor)
                    return !XRSettings.isDeviceActive;
                return IsAndroidEmulator || !XRSettings.isDeviceActive;
            }
        }

        static bool IsAndroidEmulator
        {
            get
            {
                if (s_AndroidEmulator.HasValue)
                    return s_AndroidEmulator.Value;
                string model = (SystemInfo.deviceModel + " " + SystemInfo.deviceName).ToLowerInvariant();
                s_AndroidEmulator = model.Contains("emulator") || model.Contains("sdk_gphone") || model.Contains("android sdk built for") ||
                                    model.Contains("generic_x86") || model.Contains("generic x86") || model.Contains("goldfish") ||
                                    model.Contains("ranchu");
                return s_AndroidEmulator.Value;
            }
        }
    }
}
