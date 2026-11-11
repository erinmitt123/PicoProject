using System;
using Unity.XR.CoreUtils;
using UnityEngine;
#if ENABLE_PICO_XR_SDK
using ByteDance.PICO.XR;
#endif

namespace PaletteRoom
{
    /// <summary>
    /// Palette Room is a walk-around experience: the user restyles their whole room, so it runs room-scale
    /// rather than standing-only. Tracking is floor-level with PICO Stage Mode (Project Settings → PICO →
    /// Stage Mode), so the Home button can't recenter the room out from under the furniture. At start-up the
    /// headset's boundary is checked: a stationary boundary only allows standing in place, so the user is
    /// asked to set up a room-scale (custom) boundary instead.
    /// </summary>
    public static class RoomScale
    {
        public enum Boundary
        {
            /// <summary>Not known: the Editor, or the boundary couldn't be read.</summary>
            Unknown,
            /// <summary>A custom boundary with a play area to walk around in.</summary>
            RoomScale,
            /// <summary>A stationary boundary: standing in place only.</summary>
            Stationary,
            /// <summary>The boundary is off or not set up (passthrough MR); the user sees their room and can walk it.</summary>
            Off,
        }

        /// <summary>A play area narrower than this (either way) doesn't count as room-scale.</summary>
        const float MinPlayAreaSide = 0.5f;

        /// <summary>Asks for floor-level tracking and logs whether Stage Mode is in effect.</summary>
        public static void UseRoomScaleTracking(XROrigin origin)
        {
            if (origin != null && origin.RequestedTrackingOriginMode != XROrigin.TrackingOriginMode.Floor)
            {
                origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;
                Debug.Log("[PaletteRoom] Tracking origin set to Floor for room-scale.");
            }
#if ENABLE_PICO_XR_SDK && !UNITY_EDITOR
            try
            {
                PXR_System.GetTrackingOrigin(out var mode);
                if (mode == PxrTrackingOrigin.Stage)
                    Debug.Log("[PaletteRoom] Tracking origin: Stage (room-scale, no recentering).");
                else
                    Debug.LogWarning($"[PaletteRoom] Tracking origin is {mode}, not Stage. Turn on Stage Mode in the PICO project settings.");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PaletteRoom] Couldn't read the tracking origin: {e.Message}");
            }
#endif
        }

        /// <summary>What kind of boundary the user has set up, and the size of its play area in metres.</summary>
        public static Boundary Check(out Vector2 playArea)
        {
            playArea = Vector2.zero;
#if ENABLE_PICO_XR_SDK && !UNITY_EDITOR
            try
            {
                if (!PXR_Boundary.GetEnabled() || !PXR_Boundary.GetConfigured())
                {
                    Debug.Log("[PaletteRoom] Boundary is off or not set up.");
                    return Boundary.Off;
                }
                // A stationary boundary has no play-area rectangle and reports (0, 1, 0).
                var size = PXR_Boundary.GetDimensions(BoundaryType.PlayArea);
                playArea = new Vector2(size.x, size.z);
                var result = size.x >= MinPlayAreaSide && size.z >= MinPlayAreaSide ? Boundary.RoomScale : Boundary.Stationary;
                Debug.Log($"[PaletteRoom] Boundary: {result}, play area {size.x:F2} × {size.z:F2} m.");
                return result;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PaletteRoom] Couldn't read the boundary: {e.Message}");
                return Boundary.Unknown;
            }
#else
            return Boundary.Unknown;
#endif
        }
    }
}
