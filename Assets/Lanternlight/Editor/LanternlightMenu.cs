using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Lanternlight.EditorTools
{
    /// <summary>Editor menu helpers under "Lanternlight" in the Unity menu bar.</summary>
    public static class LanternlightMenu
    {
        const string ScenePath = "Assets/Scenes/Main.unity";

        /// <summary>
        /// Creates an empty scene with just a camera and adds it to the build.
        /// The game itself is created in code when Play is pressed (see Bootstrap.cs).
        /// </summary>
        [MenuItem("Lanternlight/Create Main Scene")]
        static void CreateMainScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = cameraObject.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5.625f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.02f, 0.03f, 0.08f);
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            cameraObject.AddComponent<AudioListener>();

            System.IO.Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Debug.Log("Lanternlight: created " + ScenePath + " and added it to Build Settings. Press Play!");
        }

        /// <summary>Re-applies the pixel-art and audio import settings (if art ever looks blurry).</summary>
        [MenuItem("Lanternlight/Reimport Art and Audio")]
        static void ReimportAssets()
        {
            AssetDatabase.ImportAsset("Assets/Lanternlight/Resources",
                ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
            Debug.Log("Lanternlight: reimported art and audio.");
        }
    }
}
