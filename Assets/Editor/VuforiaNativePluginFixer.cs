using UnityEditor;
using UnityEngine;
using System.IO;
using System.Linq;

[InitializeOnLoad]
public class VuforiaNativePluginFixer
{
    private const string PluginGuid = "f735d317ca9afb6468c5c5ca06d9e39a";
    private const string TargetFolder = "Assets/Plugins/x86_64";

    static VuforiaNativePluginFixer()
    {
        EnsureNativePluginsCopied();
    }

    [MenuItem("CardWars/Fix Vuforia Native Plugins")]
    public static void EnsureNativePluginsCopied()
    {
        var packagePath = Path.Combine(
            Application.dataPath, "..", "Library", "PackageCache"
        );

        if (!Directory.Exists(packagePath))
        {
            Debug.LogWarning("No Library/PackageCache found; skipping Vuforia plugin fix.");
            return;
        }

        var vuforiaDirs = Directory.GetDirectories(packagePath, "com.ptc.vuforia.engine*");
        if (vuforiaDirs.Length == 0)
        {
            Debug.LogWarning("Vuforia Engine package not found in Library/PackageCache.");
            return;
        }

        var win64Dir = Path.Combine(vuforiaDirs[0], "Vuforia", "Plugins", "Windows", "x64");
        if (!Directory.Exists(win64Dir))
        {
            Debug.LogWarning($"Windows x64 plugin folder not found: {win64Dir}");
            return;
        }

        if (!Directory.Exists(TargetFolder))
            Directory.CreateDirectory(TargetFolder);

        var dlls = new[] { "VuforiaEngine.dll", "UnityDriver.dll", "FileDriver.dll" };
        bool copiedAny = false;

        foreach (var dll in dlls)
        {
            var src = Path.Combine(win64Dir, dll);
            var dst = Path.Combine(TargetFolder, dll);
            if (File.Exists(src))
            {
                if (!File.Exists(dst) || File.GetLastWriteTime(src) != File.GetLastWriteTime(dst))
                {
                    File.Copy(src, dst, true);
                    copiedAny = true;
                    Debug.Log($"Copied {dll} to {TargetFolder}");
                }
            }
        }

        if (copiedAny)
            AssetDatabase.Refresh();
    }
}
