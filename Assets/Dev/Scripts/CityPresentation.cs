using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace NaraTycoon
{
    public sealed class CityPresentation : MonoBehaviour
    {
        static readonly string[] Names = { "기초 도시", "시민 정원", "교통 지구", "수변 공원", "스마트 지구", "도심 스카이라인", "솔빛 랜드마크" };
        static readonly int Emission = Shader.PropertyToID("_EmissionColor");
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly string[] WallColors = { "", "E4DEC9", "D0DEE2", "D5E5D9", "BDD7DE", "DAE4E8", "FAE8CF" };
        static readonly string[] RoofColors = { "", "779B84", "718BA7", "5C9E8D", "3F8F9F", "547992", "DAAB6B" };
        static readonly string[] GlassColors = { "", "86A8A0", "7F9DBA", "73B6A8", "67C4C8", "88B2D1", "9DBCD2" };

        [Serializable]
        public sealed class Glow
        {
            public Material Material;
            public Renderer[] Surfaces;
            public Color NightColor;
            public float Brightness;
        }

        [Serializable]
        public sealed class Traffic
        {
            public Transform Vehicle;
            public Vector3[] Route;
            public float Speed, Offset;
            public int Level;
            [NonSerialized] public float Length;
        }

        [SerializeField] Volume duskGrade, nightGrade;
        [SerializeField] GameObject[] development;
        [SerializeField] Vector3[] developmentCenters;
        [SerializeField] Light[] roadLights;
        [SerializeField] Glow[] windows;
        [SerializeField] Traffic[] traffic;
        [SerializeField] Rect towerFootprint;
        [SerializeField] Renderer[] buildingWalls = Array.Empty<Renderer>();
        [SerializeField] Renderer[] buildingRoofs = Array.Empty<Renderer>();
        MaterialPropertyBlock facadePaint;
        Material[] activeGlow;
        UniversalRenderPipelineAsset pipeline;
        float originalShadowDistance;
        int currentLevel = -1;

        internal static int LevelFor(int happiness) => Procurement.GrowthLevel(happiness);

        internal static string LevelName(int level) => Names[level];
        internal static int MaxLevel => Procurement.GrowthMilestones;
        internal int CurrentLevel => Mathf.Max(0, currentLevel);
        internal static int Threshold(int level) => Procurement.GrowthThreshold(level);
        internal Vector3 Landmark(int level) => developmentCenters[Mathf.Min(level, developmentCenters.Length - 1)];
        internal bool IsBlocked(Vector3 position, float margin) => currentLevel >= 4 &&
            position.x > towerFootprint.xMin - margin && position.x < towerFootprint.xMax + margin &&
            position.z > towerFootprint.yMin - margin && position.z < towerFootprint.yMax + margin;

        internal Vector3 KeepClear(Vector3 position, float margin)
        {
            if (!IsBlocked(position, margin)) return position;

            float left = position.x - (towerFootprint.xMin - margin);
            float right = towerFootprint.xMax + margin - position.x;
            float near = position.z - (towerFootprint.yMin - margin);
            float far = towerFootprint.yMax + margin - position.z;
            if (Mathf.Min(left, right) <= Mathf.Min(near, far))
                position.x += left <= right ? -left - .05f : right + .05f;
            else
                position.z += near <= far ? -near - .05f : far + .05f;
            return position;
        }

        internal static float NightWeight(float elapsed)
        {
            float phase = elapsed % 174f / 174f;
            return Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.49f, .64f, phase)) *
                (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.86f, 1, phase)));
        }

        internal static float TwilightWeight(float time) =>
            Mathf.Max(0, 1 - Mathf.Abs(time % 174f / 174f - .52f) / .11f);

        void Awake()
        {
            pipeline = UniversalRenderPipeline.asset;
            if (pipeline != null)
            {
                originalShadowDistance = pipeline.shadowDistance;
                pipeline.shadowDistance = Mathf.Max(originalShadowDistance, 148);
            }

            facadePaint = new MaterialPropertyBlock();
            activeGlow = new Material[windows.Length];
            for (int i = 0; i < windows.Length; i++)
            {
                activeGlow[i] = new Material(windows[i].Material);
                activeGlow[i].EnableKeyword("_EMISSION");
                foreach (Renderer surface in windows[i].Surfaces) surface.sharedMaterial = activeGlow[i];
            }

            foreach (var car in traffic)
                for (int i = 0; i < car.Route.Length; i++)
                    car.Length += Vector3.Distance(car.Route[i], car.Route[(i + 1) % car.Route.Length]);
        }

        void OnDestroy()
        {
            if (pipeline != null) pipeline.shadowDistance = originalShadowDistance;
            if (activeGlow == null) return;
            foreach (var material in activeGlow) Destroy(material);
        }

        internal void ApplyAtmosphere(float time, float night, Camera camera, Light sun, Light fill)
        {
            float phase = time % 174f / 174f;
            float twilight = TwilightWeight(time);
            duskGrade.weight = Mathf.SmoothStep(0, 1, twilight) * (1 - night);
            nightGrade.weight = night;
            var sky = CityWorld.ColorOf("B9DCEB");
            var dusk = CityWorld.ColorOf("E3B3B0");
            var darkSky = CityWorld.ColorOf("17243E");
            camera.backgroundColor = Color.Lerp(Color.Lerp(sky, dusk, twilight * .72f), darkSky, night);
            RenderSettings.fogColor = camera.backgroundColor;
            RenderSettings.fogStartDistance = 132;
            RenderSettings.fogEndDistance = 270;
            RenderSettings.ambientSkyColor = Color.Lerp(CityWorld.ColorOf("BCD7EB"), CityWorld.ColorOf("586C9D"), night);
            RenderSettings.ambientEquatorColor = Color.Lerp(CityWorld.ColorOf("829CA6"), CityWorld.ColorOf("3B4E76"), night);
            RenderSettings.ambientGroundColor = Color.Lerp(CityWorld.ColorOf("626F78"), CityWorld.ColorOf("232E4B"), night);

            float dayElevation = Mathf.Lerp(34, 49, Mathf.Sin(Mathf.Clamp01(phase / .57f) * Mathf.PI));
            sun.transform.rotation = Quaternion.Euler(Mathf.Lerp(dayElevation, 38, night), Mathf.Lerp(-47 + phase * 53, 135, night), 0);
            sun.color = Color.Lerp(Color.Lerp(CityWorld.ColorOf("FFF0D6"), CityWorld.ColorOf("FFC68F"), twilight * .86f), CityWorld.ColorOf("A9C5FF"), night);
            sun.intensity = Mathf.Lerp(1.56f - twilight * .33f, .23f, night);
            sun.shadowStrength = Mathf.Lerp(.81f, .35f, night);
            fill.color = Color.Lerp(CityWorld.ColorOf("C9E4FF"), CityWorld.ColorOf("88ACFA"), night);
            fill.intensity = Mathf.Lerp(.48f, .36f, night);

            if (activeGlow != null)
                for (int i = 0; i < windows.Length; i++)
                    activeGlow[i].SetColor(Emission, windows[i].NightColor * (windows[i].Brightness * night));
            foreach (Light light in roadLights) light.intensity = night * 5.4f;
        }

        internal void UpdateTown(int happiness, float clock)
        {
            int level = LevelFor(happiness);
            if (level != currentLevel)
            {
                for (int i = 0; i < development.Length; i++) development[i].SetActive(i < level);
                foreach (var car in traffic) car.Vehicle.gameObject.SetActive(level >= car.Level);
                UpdateFacadeMaterials(level);
                currentLevel = level;
            }
            foreach (var car in traffic)
            {
                if (level < car.Level || car.Length <= 0) continue;
                float distance = Mathf.Repeat(clock * car.Speed + car.Offset, car.Length);
                for (int i = 0; i < car.Route.Length; i++)
                {
                    Vector3 from = car.Route[i], to = car.Route[(i + 1) % car.Route.Length];
                    float length = Vector3.Distance(from, to);
                    if (length <= .001f) continue;
                    if (distance > length)
                    {
                        distance -= length;
                        continue;
                    }
                    car.Vehicle.SetPositionAndRotation(Vector3.Lerp(from, to, distance / length), Quaternion.LookRotation(to - from));
                    break;
                }
            }
        }

        void UpdateFacadeMaterials(int level)
        {
            PaintBuildings(buildingWalls, level == 0 ? Color.white : CityWorld.ColorOf(WallColors[level]), level == 0 ? 0 : .48f);
            PaintBuildings(buildingRoofs, level == 0 ? Color.white : CityWorld.ColorOf(RoofColors[level]), level == 0 ? 0 : .46f);
            for (int i = 0; i < windows.Length; i++)
            {
                Material source = windows[i].Material;
                if (!source.name.EndsWith("windows", StringComparison.Ordinal)) continue;
                Color tint = level == 0 ? source.color : Color.Lerp(source.color, CityWorld.ColorOf(GlassColors[level]), .48f);
                activeGlow[i].SetColor(BaseColor, tint);
            }
        }

        void PaintBuildings(Renderer[] surfaces, Color accent, float blend)
        {
            foreach (var surface in surfaces)
            {
                surface.GetPropertyBlock(facadePaint);
                facadePaint.SetColor(BaseColor, Color.Lerp(surface.sharedMaterial.color, accent, blend));
                surface.SetPropertyBlock(facadePaint);
            }
        }

#if UNITY_EDITOR
        internal void ConfigureFacades(Renderer[] walls, Renderer[] roofs)
        {
            buildingWalls = walls;
            buildingRoofs = roofs;
        }

        internal Transform GrowthDistrict(int index) => development[index].transform;

        internal void ConfigureGrowth(GameObject district, GameObject landmark, Vector3[] centers, Glow lights)
        {
            Array.Resize(ref development, MaxLevel);
            development[4] = district;
            development[5] = landmark;
            developmentCenters = centers;
            AddGlow(lights);
        }

        internal void ConfigureGarden(Vector3 center, Glow lights)
        {
            developmentCenters[1] = center;
            AddGlow(lights);
        }

        void AddGlow(Glow lights)
        {
            Array.Resize(ref windows, windows.Length + 1);
            windows[^1] = lights;
        }

        public void Configure(Volume dusk, Volume night, GameObject[] stages, Vector3[] spots, Light[] lights,
            Glow[] glow, Traffic[] vehicles, Rect tower)
        {
            duskGrade = dusk;
            nightGrade = night;
            development = stages;
            developmentCenters = spots;
            roadLights = lights;
            windows = glow;
            traffic = vehicles;
            towerFootprint = tower;
        }
#endif
    }
}
