using System.IO;
using System.Xml;
using UnityEditor.Android;

namespace PaletteRoom.EditorTools
{
    /// <summary>
    /// Declares android.permission.CAMERA in the generated Android manifest. The room color scan reads
    /// the headset's RGB camera (PICO camera image API), which needs it; the app asks at runtime too.
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
            foreach (XmlNode node in manifest.SelectNodes("uses-permission"))
            {
                if (node.Attributes?["android:name"]?.Value == "android.permission.CAMERA")
                    return;
            }
            var permission = doc.CreateElement("uses-permission");
            permission.SetAttribute("name", AndroidNs, "android.permission.CAMERA");
            manifest.PrependChild(permission);
            doc.Save(path);
        }
    }
}
