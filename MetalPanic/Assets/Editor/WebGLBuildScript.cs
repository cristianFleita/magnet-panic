using UnityEditor;
using UnityEngine;

public class WebGLBuildScript
{
    [MenuItem("Magnet Panic/Build WebGL")]
    public static void BuildWebGL()
    {
        string outputPath = "../react-app/public/unity-build";
        
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
        PlayerSettings.WebGL.decompressionFallback = true;
        
        BuildPlayerOptions opts = new BuildPlayerOptions
        {
            scenes = new[]
            {
                "Assets/Scenes/MainMenu.unity",
                "Assets/Scenes/GameScene.unity"
            },
            locationPathName = outputPath,
            target = BuildTarget.WebGL,
            options = BuildOptions.None
        };
        
        BuildPipeline.BuildPlayer(opts);
        Debug.Log($"WebGL build complete → {outputPath}");
    }
}
