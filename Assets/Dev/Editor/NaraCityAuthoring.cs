using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace NaraTycoon.Editor
{
    public static class NaraCityAuthoring
    {
        public const string GameScene = "Assets/Dev/Scenes/NaraTycoon.unity";
        const string Dev = "Assets/Dev";
        const string Model = Dev + "/Models/Narmi.fbx";
        const string BuildPath = "Builds/NaraTycoon";

        [MenuItem("나라장터 타이쿤/게임 씬 열기")]
        public static void OpenGameScene()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(GameScene);
        }

        [MenuItem("나라장터 타이쿤/초기 도시 씬 생성")]
        static void CreateCityFromMenu()
        {
            Debug.Log("NARATYCOON_SCENE_OK " + CreateCityScene());
            OpenGameScene();
        }

        [MenuItem("나라장터 타이쿤/초기 도시 씬 생성", true)]
        static bool CanCreateCity() => AssetDatabase.LoadAssetAtPath<SceneAsset>(GameScene) == null;

        public static string UpgradeExistingScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play mode before editing the city scene.");
            var previous = SceneManager.GetActiveScene();
            var scene = SceneManager.GetSceneByPath(GameScene);
            bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(GameScene, OpenSceneMode.Additive);
            if (scene.isDirty) throw new InvalidOperationException("The city scene has unsaved changes.");
            try
            {
                SceneManager.SetActiveScene(scene);
                var city = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CityWorld>()).Single();
                city.UpgradeMap();
                PersistMaterials(city);
                if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save the revised city.");
                AssetDatabase.SaveAssets();
                return GameScene;
            }
            finally
            {
                if (opened)
                {
                    SceneManager.SetActiveScene(previous);
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        [MenuItem("나라장터 타이쿤/Windows 빌드")]
        public static void BuildWindows()
        {
            Directory.CreateDirectory(BuildPath);
            string path = BuildPath + "/NaraTycoon.exe";
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { GameScene },
                locationPathName = path,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException($"NaraTycoon build {report.summary.result}: {report.summary.totalErrors} errors.");
            File.Copy(Dev + "/Fonts/OFL.txt", BuildPath + "/OFL.txt", true);
            Debug.Log($"NARATYCOON_BUILD_OK {report.summary.totalSize} bytes · {path}");
        }

        public static string CreateCityScene()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(GameScene) != null)
                throw new InvalidOperationException("The editable NaraTycoon scene already exists.");
            EnsureFolder("Scenes");
            var oldScene = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            var gameObject = new GameObject("나라장터 타이쿤");
            var city = new GameObject("City Map").AddComponent<CityWorld>();
            var narmi = AssetDatabase.LoadAssetAtPath<GameObject>(Model);
            var animator = PrepareNarmi();
            var cityShader = Shader.Find("Universal Render Pipeline/Lit");
            var particleShader = Shader.Find("Sprites/Default");
            var regular = AssetDatabase.LoadAssetAtPath<Font>(Dev + "/Fonts/SolbitSans-Regular.ttf");
            var bold = AssetDatabase.LoadAssetAtPath<Font>(Dev + "/Fonts/SolbitSans-Bold.ttf");
            if (regular == null || bold == null || narmi == null || cityShader == null || particleShader == null)
                throw new InvalidOperationException("City authoring requires Narmi, the Korean fonts and the URP shaders.");
            city.Author(narmi, animator, cityShader, particleShader);
            gameObject.AddComponent<NaraGame>().Configure(city, regular, bold);
            PersistAssets(city);
            if (!EditorSceneManager.SaveScene(scene, GameScene)) throw new IOException("Could not save the NaraTycoon scene.");
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(GameScene, true) }
                .Concat(EditorBuildSettings.scenes.Where(entry => entry.path != GameScene).Select(entry => new EditorBuildSettingsScene(entry.path, false)))
                .ToArray();
            AssetDatabase.SaveAssets();
            if (oldScene.IsValid()) SceneManager.SetActiveScene(oldScene);
            EditorSceneManager.CloseScene(scene, true);
            return GameScene;
        }

        static void PersistAssets(CityWorld city)
        {
            PersistMaterials(city);
            var profile = city.GetComponentInChildren<Volume>(true).sharedProfile;
            profile.name = "Solbit Post Processing";
            AssetDatabase.CreateAsset(profile, Dev + "/Materials/Solbit Post Processing.asset");
            foreach (VolumeComponent effect in profile.components) AssetDatabase.AddObjectToAsset(effect, profile);
        }

        static void PersistMaterials(CityWorld city)
        {
            const string materialPack = Dev + "/Materials/Solbit Palette.mat";
            var unique = city.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials)
                .Where(m => m != null && !AssetDatabase.Contains(m)).Distinct().OrderBy(m => m.name).ToList();
            if (unique.Count > 0)
            {
                var existing = AssetDatabase.LoadAssetAtPath<Material>(materialPack);
                if (existing == null)
                {
                    AssetDatabase.CreateAsset(unique[0], materialPack);
                    unique.RemoveAt(0);
                }
                foreach (Material material in unique) AssetDatabase.AddObjectToAsset(material, materialPack);
            }
        }

        static RuntimeAnimatorController PrepareNarmi()
        {
            const string controllerPath = Dev + "/Models/Narmi.controller";
            var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (existing != null) return existing;
            var importer = (ModelImporter)AssetImporter.GetAtPath(Model);
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.importAnimation = true;
            importer.optimizeGameObjects = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
            importer.animationCompression = ModelImporterAnimationCompression.Optimal;
            var original = importer.defaultClipAnimations.Single();
            float frame = original.firstFrame;
            importer.clipAnimations = new[]
            {
                new ModelImporterClipAnimation { name = "Idle", takeName = original.takeName, firstFrame = frame, lastFrame = frame+72, loopTime = true },
                new ModelImporterClipAnimation { name = "Run", takeName = original.takeName, firstFrame = frame+73, lastFrame = frame+93, loopTime = true }
            };
            importer.SaveAndReimport();
            // Assign URP materials while retaining the original paint colors from the Blender asset.
            var modelMaterials = AssetDatabase.LoadAllAssetsAtPath(Model).OfType<Material>().ToArray();
            var modelShader = Shader.Find("Universal Render Pipeline/Lit");
            foreach (Material source in modelMaterials)
            {
                string safeName = string.Concat(source.name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
                string path = Dev + "/Materials/Narmi " + safeName + ".mat";
                var replacement = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (replacement == null)
                {
                    replacement = new Material(modelShader) { name = source.name, color = source.color };
                    replacement.SetFloat("_Smoothness", .22f);
                    AssetDatabase.CreateAsset(replacement, path);
                }
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(source), replacement);
            }
            importer.SaveAndReimport();
            var clips = AssetDatabase.LoadAllAssetsAtPath(Model).OfType<AnimationClip>().ToArray();
            var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            controller.AddParameter("Moving", AnimatorControllerParameterType.Bool);
            var stateMachine = controller.layers[0].stateMachine;
            var idle = stateMachine.AddState("Idle", new Vector3(250,70,0));
            var run = stateMachine.AddState("Run", new Vector3(250,170,0));
            idle.motion = clips.Single(clip => clip.name == "Idle");
            run.motion = clips.Single(clip => clip.name == "Run");
            stateMachine.defaultState = idle;
            BindState(idle, run, AnimatorConditionMode.If);
            BindState(run, idle, AnimatorConditionMode.IfNot);
            EditorUtility.SetDirty(controller);
            return controller;
        }

        static void BindState(AnimatorState source, AnimatorState target, AnimatorConditionMode condition)
        {
            var transition = source.AddTransition(target);
            transition.hasExitTime = false;
            transition.hasFixedDuration = true;
            transition.duration = .12f;
            transition.AddCondition(condition, 0, "Moving");
        }

        static void EnsureFolder(string name)
        {
            if (!AssetDatabase.IsValidFolder(Dev + "/" + name)) AssetDatabase.CreateFolder(Dev, name);
        }
    }
}
