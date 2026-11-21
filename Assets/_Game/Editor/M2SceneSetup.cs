using DarkFantasy.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkFantasy.EditorTools
{
    /// <summary>
    /// One-click setup for the M2 playable scene (replaces the manual steps in Docs/M2_SCENE_SETUP.md).
    /// Safe to run again: it reuses existing assets and components.
    /// </summary>
    public static class M2SceneSetup
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string ThemePath = "Assets/_Game/UI/GameRuntimeTheme.tss";
        private const string PanelSettingsPath = "Assets/_Game/UI/GamePanelSettings.asset";
        private const string ConfigPath = "Assets/_Game/Data/balance_config.json";
        private const string LookConfigPath = "Assets/_Game/Data/look_config.json";
        private const string SkillsConfigPath = "Assets/_Game/Data/skills_config.json";

        [MenuItem("DFIRPG/Set Up M2 Scene")]
        public static void SetUp()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
            if (theme == null)
            {
                Debug.LogError("M2 setup: theme not found at " + ThemePath);
                return;
            }

            var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (panelSettings == null)
            {
                panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(panelSettings, PanelSettingsPath);
            }
            panelSettings.themeStyleSheet = theme;
            panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panelSettings.referenceResolution = new Vector2Int(1080, 1920);
            panelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panelSettings.match = 0.5f;
            EditorUtility.SetDirty(panelSettings);

            var config = AssetDatabase.LoadAssetAtPath<TextAsset>(ConfigPath);
            if (config == null)
            {
                Debug.LogError("M2 setup: balance config not found at " + ConfigPath);
                return;
            }

            var game = GameObject.Find("Game");
            if (game == null)
            {
                game = new GameObject("Game");
                Undo.RegisterCreatedObjectUndo(game, "Create Game object");
            }

            var document = game.GetComponent<UIDocument>();
            if (document == null) document = Undo.AddComponent<UIDocument>(game);
            document.panelSettings = panelSettings;
            EditorUtility.SetDirty(document);

            var bootstrap = game.GetComponent<GameBootstrap>();
            if (bootstrap == null) bootstrap = Undo.AddComponent<GameBootstrap>(game);
            var serialized = new SerializedObject(bootstrap);
            serialized.FindProperty("balanceConfig").objectReferenceValue = config;
            serialized.FindProperty("lookConfig").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TextAsset>(LookConfigPath);
            serialized.FindProperty("skillsConfig").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TextAsset>(SkillsConfigPath);
            serialized.ApplyModifiedProperties();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = game;
            Debug.Log("M2 setup done: SampleScene has a 'Game' object with UI Document and Game Bootstrap. Press Play.");
        }
    }
}
