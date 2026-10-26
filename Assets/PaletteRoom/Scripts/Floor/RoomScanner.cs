using System;
using UnityEngine;
#if ENABLE_PICO_XR_SDK
using ByteDance.PICO.XR;
#endif

namespace PaletteRoom
{
    /// <summary>
    /// Sends the user to the headset's own room set-up (PICO Room Capture) to scan their walls, floor and
    /// furniture. The app pauses while it runs; when the user comes back, plane detection and the spatial
    /// mesh deliver the new scan and <see cref="SceneObstacles"/> picks it up as usual.
    /// Needs Scene Capture enabled in the PICO project settings.
    /// </summary>
    public class RoomScanner : MonoBehaviour
    {
        /// <summary>True while Room Capture is being started or is running.</summary>
        public bool Busy { get; private set; }

        /// <summary>Room Capture exists only on the headset.</summary>
        public static bool Available =>
#if ENABLE_PICO_XR_SDK && !UNITY_EDITOR
            true;
#else
            false;
#endif

        /// <summary>Raised when Room Capture has finished (true) or couldn't be started (false).</summary>
        public event Action<bool> Finished;

        public async void Launch()
        {
            if (Busy || !Available)
                return;
            Busy = true;
            bool ok = false;
#if ENABLE_PICO_XR_SDK && !UNITY_EDITOR
            try
            {
                // Asks for the spatial-data permission the first time.
                var provider = await PXR_MixedReality.StartSenseDataProvider(PxrSenseDataProviderType.SceneCapture);
                if (provider != PxrResult.SUCCESS)
                    Debug.LogWarning($"[PaletteRoom] Scene capture provider not started ({provider}); trying Room Capture anyway.");
                var result = await PXR_MixedReality.StartSceneCaptureAsync();
                ok = result == PxrResult.SUCCESS;
                Debug.Log($"[PaletteRoom] Room Capture finished: {result}.");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PaletteRoom] Room Capture couldn't be started: {e.Message}");
            }
#else
            await System.Threading.Tasks.Task.CompletedTask;
#endif
            Busy = false;
            Finished?.Invoke(ok);
        }
    }
}
