using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
#if ENABLE_PICO_XR_SDK && !UNITY_EDITOR
using System.Runtime.InteropServices;
using ByteDance.PICO.XR;
using UnityEngine.Android;
#endif

namespace PaletteRoom
{
    /// <summary>
    /// Looks at the room through the headset's RGB camera (PICO camera image API), finds its main
    /// colors, and picks the palette that sits closest to them, so the starting palette suits the room.
    /// Colors are compared in CIELAB, weighted by how much of the view each room color covers.
    /// </summary>
    public class RoomColorScanner : MonoBehaviour
    {
        /// <summary>One of the room's main colors and the share of the view it covers (0–1).</summary>
        public struct RoomColor
        {
            public Color color;
            public float share;
        }

        public struct Match
        {
            public ColorPalette palette;
            public RoomColor[] roomColors;
        }

        const int Clusters = 6;
        const int FramesToSample = 3;
        const int SamplesPerSide = 48;

        /// <summary>Raised once the room has been read and a palette chosen.</summary>
        public event Action<Match> Matched;

        /// <summary>Raised when the camera can't be read (no permission, unsupported device, Editor).</summary>
        public event Action<string> Failed;

        public async void Scan()
        {
            try
            {
                var samples = await CaptureSamples();
                if (samples == null || samples.Count < 50)
                {
                    Failed?.Invoke("Couldn't read the camera");
                    return;
                }
                Matched?.Invoke(MatchColors(samples));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PaletteRoom] Room color scan failed: {e.Message}");
                Failed?.Invoke(e.Message);
            }
        }

        /// <summary>Finds the room's main colors in <paramref name="samples"/> and the closest palette to them.</summary>
        public static Match MatchColors(IReadOnlyList<Color> samples)
        {
            var roomColors = MainColors(samples);
            ColorPalette best = null;
            float bestScore = float.MaxValue;
            foreach (var palette in PalettePresets.All)
            {
                float score = Score(palette, roomColors);
                if (score < bestScore)
                {
                    bestScore = score;
                    best = palette;
                }
            }
            var summary = string.Join(", ", Array.ConvertAll(roomColors, c => $"#{ColorUtility.ToHtmlStringRGB(c.color)} {c.share:P0}"));
            Debug.Log($"[PaletteRoom] Room colors: {summary} → palette \"{best.name}\" (score {bestScore:F1}).");
            return new Match { palette = best, roomColors = roomColors };
        }

        /// <summary>
        /// How far the palette is from the room: each room color is matched to the palette's nearest
        /// color, weighted by how much of the room it covers. The room's big areas (walls, floor) should
        /// find themselves in the palette's dominant, neutral and secondary colors; accents count less.
        /// </summary>
        static float Score(ColorPalette palette, RoomColor[] room)
        {
            var slots = new[] { (palette.dominant, 1f), (palette.neutral, 1f), (palette.secondary, 1.1f), (palette.accent, 1.35f), (palette.highlight, 1.35f) };
            float total = 0f;
            foreach (var rc in room)
            {
                var lab = ToLab(rc.color);
                float nearest = float.MaxValue;
                foreach (var (color, penalty) in slots)
                    nearest = Mathf.Min(nearest, Vector3.Distance(lab, ToLab(color)) * penalty);
                total += nearest * rc.share;
            }
            return total;
        }

        /// <summary>k-means in CIELAB; clusters covering under 4% of the view are dropped.</summary>
        static RoomColor[] MainColors(IReadOnlyList<Color> samples)
        {
            var labs = new Vector3[samples.Count];
            for (int i = 0; i < labs.Length; i++)
                labs[i] = ToLab(samples[i]);

            var centres = new Vector3[Clusters];
            for (int k = 0; k < Clusters; k++)
                centres[k] = labs[k * labs.Length / Clusters];
            var assign = new int[labs.Length];
            for (int iteration = 0; iteration < 12; iteration++)
            {
                var sums = new Vector3[Clusters];
                var counts = new int[Clusters];
                for (int i = 0; i < labs.Length; i++)
                {
                    int bestK = 0;
                    float bestD = float.MaxValue;
                    for (int k = 0; k < Clusters; k++)
                    {
                        float d = (labs[i] - centres[k]).sqrMagnitude;
                        if (d < bestD) { bestD = d; bestK = k; }
                    }
                    assign[i] = bestK;
                    sums[bestK] += labs[i];
                    counts[bestK]++;
                }
                for (int k = 0; k < Clusters; k++)
                    if (counts[k] > 0)
                        centres[k] = sums[k] / counts[k];
            }

            var shares = new float[Clusters];
            var rgbSums = new Color[Clusters];
            for (int i = 0; i < labs.Length; i++)
            {
                shares[assign[i]] += 1f / labs.Length;
                rgbSums[assign[i]] += samples[i];
            }
            var result = new List<RoomColor>();
            for (int k = 0; k < Clusters; k++)
            {
                if (shares[k] < 0.04f)
                    continue;
                var mean = rgbSums[k] / (shares[k] * labs.Length);
                mean.a = 1f;
                result.Add(new RoomColor { color = mean, share = shares[k] });
            }
            result.Sort((a, b) => b.share.CompareTo(a.share));
            float total = 0f;
            foreach (var r in result) total += r.share;
            for (int i = 0; i < result.Count; i++)
            {
                var r = result[i];
                r.share /= total;
                result[i] = r;
            }
            return result.ToArray();
        }

        /// <summary>sRGB → CIELAB (D65).</summary>
        static Vector3 ToLab(Color c)
        {
            float R = Linear(c.r), G = Linear(c.g), B = Linear(c.b);
            float x = (R * 0.4124f + G * 0.3576f + B * 0.1805f) / 0.95047f;
            float y = R * 0.2126f + G * 0.7152f + B * 0.0722f;
            float z = (R * 0.0193f + G * 0.1192f + B * 0.9505f) / 1.08883f;
            x = F(x); y = F(y); z = F(z);
            return new Vector3(116f * y - 16f, 500f * (x - y), 200f * (y - z));

            static float Linear(float v) => v <= 0.04045f ? v / 12.92f : Mathf.Pow((v + 0.055f) / 1.055f, 2.4f);
            static float F(float t) => t > 0.008856f ? Mathf.Pow(t, 1f / 3f) : 7.787f * t + 16f / 116f;
        }

#if ENABLE_PICO_XR_SDK && !UNITY_EDITOR
        async Task<List<Color>> CaptureSamples()
        {
            if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
            {
                Permission.RequestUserPermission(Permission.Camera);
                float until = Time.realtimeSinceStartup + 15f;
                while (!Permission.HasUserAuthorizedPermission(Permission.Camera) && Time.realtimeSinceStartup < until)
                    await Task.Delay(250);
                if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
                {
                    Debug.LogWarning("[PaletteRoom] Camera permission not granted; keeping the default palette.");
                    return null;
                }
            }

            if (PXR_CameraImage.GetAvailableCameras(out var ids) != PxrResult.SUCCESS || ids == null || ids.Length == 0)
            {
                Debug.LogWarning("[PaletteRoom] No camera available for room colors.");
                return null;
            }
            var camera = Array.IndexOf(ids, XrCameraIdPICO.XR_CAMERA_ID_RGB_LEFT_PICO) >= 0 ? XrCameraIdPICO.XR_CAMERA_ID_RGB_LEFT_PICO : ids[0];

            int width = 640, height = 480;
            if (PXR_CameraImage.GetCameraImageResolutionCapability(camera, out var sizes) == PxrResult.SUCCESS && sizes != null && sizes.Length > 0)
            {
                // The smallest resolution is plenty for averaging colors.
                var smallest = sizes[0];
                foreach (var s in sizes)
                    if (s.width * s.height < smallest.width * smallest.height)
                        smallest = s;
                width = smallest.width;
                height = smallest.height;
            }

            var created = await PXR_CameraImage.CreateCameraDeviceAsync(camera);
            if (created != PxrResult.SUCCESS)
            {
                Debug.LogWarning($"[PaletteRoom] Camera device: {created}");
                return null;
            }
            var samples = new List<Color>();
            try
            {
                var session = await PXR_CameraImage.CreateCameraCaptureSessionAsync(camera, width, height,
                    XrCameraImageFpsPICO.XR_CAMERA_IMAGE_FPS_30_PICO, XrCameraImageFormatPICO.XR_CAMERA_IMAGE_FORMAT_RGBA_8888_PICO,
                    XrCameraDataTransferTypePICO.XR_CAMERA_DATA_TRANSFER_TYPE_RAW_BUFFER_PICO, XrCameraModelPICO.XR_CAMERA_MODEL_PINHOLE_PICO);
                if (session != PxrResult.SUCCESS)
                {
                    Debug.LogWarning($"[PaletteRoom] Camera session: {session}");
                    return null;
                }
                PXR_CameraImage.BeginCameraCapture(camera);
                long last = 0;
                int frames = 0;
                float deadline = Time.realtimeSinceStartup + 5f;
                while (frames < FramesToSample && Time.realtimeSinceStartup < deadline)
                {
                    if (PXR_CameraImage.AcquireCameraImage(camera, last, out ulong imageId, out long captured) == PxrResult.SUCCESS && imageId > 0)
                    {
                        if (PXR_CameraImage.GetCameraImageData(camera, imageId, out var data) == PxrResult.SUCCESS)
                        {
                            Sample(data, samples);
                            frames++;
                        }
                        PXR_CameraImage.ReleaseCameraImage(camera, imageId);
                        last = captured;
                    }
                    await Task.Delay(150);
                }
                PXR_CameraImage.EndCameraCapture(camera);
                PXR_CameraImage.DestroyCameraCaptureSession(camera);
                Debug.Log($"[PaletteRoom] Read {frames} camera frames ({width}×{height}) for room colors.");
            }
            finally
            {
                PXR_CameraImage.DestroyCameraDevice(camera);
            }
            return samples;
        }

        /// <summary>Reads an even grid of pixels from an RGBA8888 frame.</summary>
        static void Sample(XrCameraImageDataRawBuffer data, List<Color> into)
        {
            if (data.buffer == IntPtr.Zero || data.width == 0 || data.height == 0)
                return;
            int pixelStride = data.pixelStride > 0 ? (int)data.pixelStride : 4;
            for (int sy = 0; sy < SamplesPerSide; sy++)
            {
                int y = (int)((sy + 0.5f) / SamplesPerSide * data.height);
                for (int sx = 0; sx < SamplesPerSide; sx++)
                {
                    int x = (int)((sx + 0.5f) / SamplesPerSide * data.width);
                    long offset = (long)y * data.stride + (long)x * pixelStride;
                    if (offset + 3 >= data.bufferSize)
                        continue;
                    var p = data.buffer + (int)offset;
                    into.Add(new Color32(Marshal.ReadByte(p, 0), Marshal.ReadByte(p, 1), Marshal.ReadByte(p, 2), 255));
                }
            }
        }
#else
        Task<List<Color>> CaptureSamples()
        {
            Debug.Log("[PaletteRoom] Room color scan needs the headset camera; skipped in the Editor.");
            return Task.FromResult<List<Color>>(null);
        }
#endif
    }
}
