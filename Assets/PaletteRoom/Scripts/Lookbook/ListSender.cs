using System;
using System.IO;
using UnityEngine;

namespace PaletteRoom
{
    /// <summary>
    /// Sends a room list to a phone number (text message) or email address by handing it to the
    /// headset's messaging or email app, or to Android's share sheet. The list is always copied and
    /// saved to a file as well, so it's never lost if no app can take it.
    /// </summary>
    public static class ListSender
    {
        public enum Outcome
        {
            Opened,      // an app took it; the user finishes sending there
            NoApp,       // nothing on the device can send this; it's saved and copied instead
            Invalid,     // not a phone number or email address
        }

        /// <summary>Email if it has an @, phone number if it's mostly digits.</summary>
        public static bool LooksLikeEmail(string to) => to.Contains("@") && to.IndexOf('.', to.IndexOf('@')) > 0;

        public static bool LooksLikePhone(string to)
        {
            int digits = 0;
            foreach (char c in to)
            {
                if (char.IsDigit(c))
                    digits++;
                else if (" +-().".IndexOf(c) < 0)
                    return false;
            }
            return digits >= 7;
        }

        public static Outcome Send(string to, string subject, string body, out string savedPath)
        {
            savedPath = SaveCopy(subject, body);
            to = to.Trim();
            if (LooksLikeEmail(to))
                return Open($"mailto:{Uri.EscapeDataString(to)}?subject={Uri.EscapeDataString(subject)}&body={Uri.EscapeDataString(body)}", "android.intent.action.SENDTO");
            if (LooksLikePhone(to))
            {
                var digits = new System.Text.StringBuilder();
                foreach (char c in to)
                    if (char.IsDigit(c) || c == '+')
                        digits.Append(c);
                return Open($"smsto:{digits}?body={Uri.EscapeDataString(body)}", "android.intent.action.SENDTO", body);
            }
            return Outcome.Invalid;
        }

        /// <summary>Android's share sheet (messages, email, notes, whatever is installed).</summary>
        public static Outcome Share(string subject, string body, out string savedPath)
        {
            savedPath = SaveCopy(subject, body);
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var intentClass = new AndroidJavaClass("android.content.Intent");
                using var intent = new AndroidJavaObject("android.content.Intent", "android.intent.action.SEND");
                intent.Call<AndroidJavaObject>("setType", "text/plain");
                intent.Call<AndroidJavaObject>("putExtra", "android.intent.extra.SUBJECT", subject);
                intent.Call<AndroidJavaObject>("putExtra", "android.intent.extra.TEXT", body);
                using var chooser = intentClass.CallStatic<AndroidJavaObject>("createChooser", intent, "Send your room list");
                using var activity = CurrentActivity();
                if (!CanHandle(activity, intent))
                    return Outcome.NoApp;
                activity.Call("startActivity", chooser);
                return Outcome.Opened;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PaletteRoom] Share failed: {e.Message}");
                return Outcome.NoApp;
            }
#else
            return Outcome.NoApp;
#endif
        }

        static Outcome Open(string uri, string action, string smsBody = null)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var uriClass = new AndroidJavaClass("android.net.Uri");
                using var parsed = uriClass.CallStatic<AndroidJavaObject>("parse", uri);
                using var intent = new AndroidJavaObject("android.content.Intent", action, parsed);
                if (smsBody != null)
                    intent.Call<AndroidJavaObject>("putExtra", "sms_body", smsBody);
                using var activity = CurrentActivity();
                if (!CanHandle(activity, intent))
                {
                    Debug.Log($"[PaletteRoom] No app on this device can open {uri.Substring(0, uri.IndexOf(':'))}: links.");
                    return Outcome.NoApp;
                }
                activity.Call("startActivity", intent);
                return Outcome.Opened;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PaletteRoom] Couldn't open {uri.Substring(0, uri.IndexOf(':'))}: {e.Message}");
                return Outcome.NoApp;
            }
#else
            Application.OpenURL(uri); // the Editor hands it to the desktop's mail or messages app
            return Outcome.Opened;
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        static AndroidJavaObject CurrentActivity()
        {
            using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            return player.GetStatic<AndroidJavaObject>("currentActivity");
        }

        static bool CanHandle(AndroidJavaObject activity, AndroidJavaObject intent)
        {
            using var pm = activity.Call<AndroidJavaObject>("getPackageManager");
            using var component = intent.Call<AndroidJavaObject>("resolveActivity", pm);
            return component != null;
        }
#endif

        /// <summary>Copies the list to the clipboard and saves it as a text file; returns the file's path.</summary>
        static string SaveCopy(string subject, string body)
        {
            GUIUtility.systemCopyBuffer = body;
            try
            {
                var folder = Path.Combine(Application.persistentDataPath, "lookbook");
                Directory.CreateDirectory(folder);
                var safe = string.Concat(subject.Split(Path.GetInvalidFileNameChars())).Replace(' ', '_');
                var path = Path.Combine(folder, safe + ".txt");
                File.WriteAllText(path, body);
                return path;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PaletteRoom] Couldn't save the list: {e.Message}");
                return null;
            }
        }
    }
}
