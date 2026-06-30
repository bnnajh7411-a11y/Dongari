using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

public static class IntroCutsceneSceneGenerator
{
    private const string ScenePath = "Assets/Scenes/IntroCutscene.unity";
    private const string ControllerObjectName = "Intro Cutscene Controller";
    private const string SessionStateKey = "IntroCutsceneSceneGenerator.GeneratedThisSession";

    private static readonly string[] PreferredCutsceneVideoPaths =
    {
        "Assets/Videos/start.mp4",
        "Assets/Videos/Start.mp4"
    };

    private static readonly string[] CutsceneVideoSearchFilters =
    {
        "IntroCutscene t:VideoClip",
        "Intro t:VideoClip",
        "start t:VideoClip"
    };

    private static readonly string[] CutscenePagePaths =
    {
        "Assets/Sprites/CutScenes/1.png",
        "Assets/Sprites/CutScenes/2.png",
        "Assets/Sprites/CutScenes/3.png",
        "Assets/Sprites/CutScenes/4.png",
        "Assets/Sprites/CutScenes/5.png"
    };

    [InitializeOnLoadMethod]
    private static void GenerateIntroCutsceneSceneOnLoad()
    {
        if (SessionState.GetBool(SessionStateKey, false))
        {
            return;
        }

        EditorApplication.delayCall += () =>
        {
            if (SessionState.GetBool(SessionStateKey, false))
            {
                return;
            }

            if (!File.Exists(ScenePath))
            {
                GenerateIntroCutsceneScene();
            }

            SessionState.SetBool(SessionStateKey, true);
        };
    }

    [MenuItem("Tools/Generate Intro Cutscene Scene")]
    public static void GenerateIntroCutsceneScene()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);

        GameObject controllerObject = new GameObject(ControllerObjectName);
        IntroCutsceneController controller = controllerObject.AddComponent<IntroCutsceneController>();
        SceneManager.MoveGameObjectToScene(controllerObject, scene);

        AssignCutsceneMedia(controller);

        Directory.CreateDirectory(Path.GetDirectoryName(ScenePath) ?? "Assets/Scenes");
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorSceneManager.CloseScene(scene, true);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Generated intro cutscene scene at '{ScenePath}'.");
    }

    private static void AssignCutsceneMedia(IntroCutsceneController controller)
    {
        SerializedObject serializedObject = new SerializedObject(controller);
        SerializedProperty introVideoClipProperty = serializedObject.FindProperty("introVideoClip");
        SerializedProperty pagesProperty = serializedObject.FindProperty("pages");

        VideoClip introVideoClip = FindCutsceneVideoClip();
        introVideoClipProperty.objectReferenceValue = introVideoClip;

        if (introVideoClip != null)
        {
            pagesProperty.arraySize = 0;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            return;
        }

        pagesProperty.arraySize = CutscenePagePaths.Length;

        for (int i = 0; i < CutscenePagePaths.Length; i++)
        {
            Sprite pageSprite = AssetDatabase.LoadAssetAtPath<Sprite>(CutscenePagePaths[i]);
            if (pageSprite == null)
            {
                Debug.LogWarning($"Could not load cutscene page sprite at '{CutscenePagePaths[i]}'.");
            }

            pagesProperty.GetArrayElementAtIndex(i).objectReferenceValue = pageSprite;
        }

        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static VideoClip FindCutsceneVideoClip()
    {
        for (int pathIndex = 0; pathIndex < PreferredCutsceneVideoPaths.Length; pathIndex++)
        {
            VideoClip preferredVideoClip = AssetDatabase.LoadAssetAtPath<VideoClip>(PreferredCutsceneVideoPaths[pathIndex]);
            if (preferredVideoClip != null)
            {
                return preferredVideoClip;
            }
        }

        for (int searchFilterIndex = 0; searchFilterIndex < CutsceneVideoSearchFilters.Length; searchFilterIndex++)
        {
            string[] assetGuids = AssetDatabase.FindAssets(CutsceneVideoSearchFilters[searchFilterIndex]);
            for (int guidIndex = 0; guidIndex < assetGuids.Length; guidIndex++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(assetGuids[guidIndex]);
                VideoClip videoClip = AssetDatabase.LoadAssetAtPath<VideoClip>(assetPath);
                if (videoClip != null)
                {
                    return videoClip;
                }
            }
        }

        return null;
    }
}
