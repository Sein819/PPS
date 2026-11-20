using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace NaraTycoon.Editor
{
    public static class NaraCityAuthoring
    {
        public const string GameScene = "Assets/Dev/Scenes/NaraTycoon.unity";
        const string Dev = "Assets/Dev";
        const string Model = Dev + "/Models/Narmi.fbx";
        const string DariModel = Dev + "/Models/Dari.fbx";
        const string Soundtrack = Dev + "/Audios/audiomass-output (1).mp3";
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

        public static string UpgradeExistingScene() => EditScene(city =>
        {
            city.UpgradeMap();
            ConfigureDari(city);
        });

        public static string InstallUrbanScene() => EditScene(ConfigureUrbanDistricts);

        public static string InstallGrowthScene() => EditScene(city =>
        {
            city.AuthorGrowthMilestones();
            city.AuthorFacadeGrowth();
        });

        static string EditScene(Action<CityWorld> configure)
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
                configure(city);
                city.AlignRoadsideProps();
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
            var animator = PrepareCharacter("Narmi", 72, 93);
            var cityShader = Shader.Find("Universal Render Pipeline/Lit");
            var particleShader = Shader.Find("Sprites/Default");
            var regular = AssetDatabase.LoadAssetAtPath<Font>(Dev + "/Fonts/SolbitSans-Regular.ttf");
            var bold = AssetDatabase.LoadAssetAtPath<Font>(Dev + "/Fonts/SolbitSans-Bold.ttf");
            var soundtrack = AssetDatabase.LoadAssetAtPath<AudioClip>(Soundtrack);
            if (regular == null || bold == null || narmi == null || cityShader == null || particleShader == null)
                throw new InvalidOperationException("City authoring requires Narmi, the Korean fonts and the URP shaders.");
            if (soundtrack == null) throw new InvalidOperationException($"Missing soundtrack at {Soundtrack}.");
            city.Author(narmi, animator, cityShader, particleShader);
            ConfigureDari(city);
            gameObject.AddComponent<NaraGame>().Configure(city, regular, bold, soundtrack);
            PersistAssets(city);
            ConfigureUrbanDistricts(city);
            city.AlignRoadsideProps();
            PersistMaterials(city);
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

        static void ConfigureDari(CityWorld city)
        {
            RuntimeAnimatorController clips = PrepareCharacter("Dari", 48, 68);
            city.ConfigureWorkforce(AssetDatabase.LoadAssetAtPath<GameObject>(DariModel), clips);
        }

        static void ConfigureUrbanDistricts(CityWorld city)
        {
            var dawn = UrbanProfile("Solbit Daylight", "FFF6E8", 0, 5, 6, .24f, 1.13f, 4, true);
            var dusk = UrbanProfile("Solbit Sunset", "FFE7CE", -.08f, 8, 6, .38f, 1.02f, 17, false);
            var night = UrbanProfile("Solbit Night", "D8E3FF", -.25f, 12, 4, .63f, .88f, -12, false);
            city.AuthorUrbanDistricts(dawn, dusk, night);
            city.AuthorGrowthMilestones();
            city.AuthorFacadeGrowth();

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Dev + "/Materials/Dari Face.png");
            const string faceFile = "/Materials/Dari Dari · orange, white muzzle and forehead stripes.002.mat";
            var face = AssetDatabase.LoadAssetAtPath<Material>(Dev + faceFile);
            if (texture == null || face == null) throw new InvalidOperationException("The baked Dari face material is missing.");
            face.SetTexture("_BaseMap", texture);
            face.SetColor("_BaseColor", Color.white);
            face.SetFloat("_Smoothness", .26f);
            EditorUtility.SetDirty(face);
        }

        static VolumeProfile UrbanProfile(string title, string tint, float exposure, float contrast, float saturation,
            float bloomStrength, float threshold, float temperature, bool baseProfile)
        {
            string path = Dev + "/Materials/" + title + ".asset";
            var saved = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (saved != null) return saved;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = title;
            if (baseProfile) profile.Add<Tonemapping>().mode.Override(TonemappingMode.Neutral);
            var adjust = profile.Add<ColorAdjustments>();
            adjust.postExposure.Override(exposure);
            adjust.contrast.Override(contrast);
            adjust.saturation.Override(saturation);
            ColorUtility.TryParseHtmlString("#" + tint, out Color filter);
            adjust.colorFilter.Override(filter);

            var bloom = profile.Add<Bloom>();
            bloom.threshold.Override(threshold);
            bloom.intensity.Override(bloomStrength);
            bloom.scatter.Override(.60f);
            profile.Add<WhiteBalance>().temperature.Override(temperature);
            var vignette = profile.Add<Vignette>();
            vignette.intensity.Override(baseProfile ? .17f : .21f);
            vignette.smoothness.Override(.51f);
            ColorUtility.TryParseHtmlString("#273B5B", out Color vignetteColor);
            vignette.color.Override(vignetteColor);

            AssetDatabase.CreateAsset(profile, path);
            foreach (VolumeComponent component in profile.components) AssetDatabase.AddObjectToAsset(component, profile);
            return profile;
        }

        static RuntimeAnimatorController PrepareCharacter(string character, int idleEnd, int runEnd)
        {
            string modelPath = Dev + "/Models/" + character + ".fbx";
            string controllerPath = Dev + "/Models/" + character + ".controller";
            var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (existing != null) return existing;
            var importer = (ModelImporter)AssetImporter.GetAtPath(modelPath);
            if (importer == null) throw new FileNotFoundException("Character asset is missing.", modelPath);
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
                new ModelImporterClipAnimation { name = "Idle", takeName = original.takeName, firstFrame = frame, lastFrame = frame + idleEnd, loopTime = true },
                new ModelImporterClipAnimation { name = "Run", takeName = original.takeName, firstFrame = frame + idleEnd + 1, lastFrame = frame + runEnd, loopTime = true }
            };
            importer.SaveAndReimport();
            // Assign URP materials while retaining the original paint colors from the Blender asset.
            var modelMaterials = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Material>().ToArray();
            var modelShader = Shader.Find("Universal Render Pipeline/Lit");
            foreach (Material source in modelMaterials)
            {
                string safeName = string.Concat(source.name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
                string path = Dev + "/Materials/" + character + " " + safeName + ".mat";
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
            var clips = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<AnimationClip>().ToArray();
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
