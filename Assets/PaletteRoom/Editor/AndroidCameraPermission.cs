using System.IO;
using System.Xml;
using UnityEditor.Android;

namespace PaletteRoom.EditorTools
{
    /// <summary>
    /// Adds what the app needs to the generated Android manifest: android.permission.CAMERA for the room
    /// color scan (PICO camera image API; the app asks at runtime too), and package-visibility queries so
    /// a room list can be handed to an email or messaging app.
    /// </summary>
    class AndroidCameraPermission : IPostGenerateGradleAndroidProject
    {
        const string AndroidNs = "http://schemas.android.com/apk/res/android";

        public int callbackOrder => 100;

        public void OnPostGenerateGradleAndroidProject(string unityLibraryPath)
        {
            var path = Path.Combine(unityLibraryPath, "src", "main", "AndroidManifest.xml");
            if (!File.Exists(path))
                return;
            var doc = new XmlDocument();
            doc.Load(path);
            var manifest = doc.DocumentElement;
            bool hasCamera = false;
            foreach (XmlNode node in manifest.SelectNodes("uses-permission"))
                if (node.Attributes?["android:name"]?.Value == "android.permission.CAMERA")
                    hasCamera = true;
            if (!hasCamera)
            {
                var permission = doc.CreateElement("uses-permission");
                permission.SetAttribute("name", AndroidNs, "android.permission.CAMERA");
                manifest.PrependChild(permission);
            }
            AddSendQueries(doc, manifest);
            doc.Save(path);
        }

        /// <summary>
        /// Android 11+ hides other apps unless the manifest says which intents it will hand off. The room
        /// list goes to email (mailto:), messages (smsto:) or the share sheet (SEND text/plain).
        /// </summary>
        static void AddSendQueries(XmlDocument doc, XmlElement manifest)
        {
            foreach (XmlNode data in manifest.SelectNodes("queries/intent/data"))
                if (data.Attributes?["android:scheme"]?.Value == "smsto")
                    return; // already added
            var queries = doc.CreateElement("queries");
            void Intent(string action, string scheme, string mime)
            {
                var intent = doc.CreateElement("intent");
                var a = doc.CreateElement("action");
                a.SetAttribute("name", AndroidNs, action);
                intent.AppendChild(a);
                var data = doc.CreateElement("data");
                if (scheme != null)
                    data.SetAttribute("scheme", AndroidNs, scheme);
                if (mime != null)
                    data.SetAttribute("mimeType", AndroidNs, mime);
                intent.AppendChild(data);
                queries.AppendChild(intent);
            }
            Intent("android.intent.action.SENDTO", "mailto", null);
            Intent("android.intent.action.SENDTO", "smsto", null);
            Intent("android.intent.action.SEND", null, "text/plain");
            manifest.AppendChild(queries);
        }
    }
}
