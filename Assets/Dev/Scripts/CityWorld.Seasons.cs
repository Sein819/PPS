using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace NaraTycoon
{
    public sealed partial class CityWorld
    {
        enum Season { Spring, Summer, Autumn, Winter }

        sealed class SeasonalSurface
        {
            internal MeshRenderer Renderer;
            internal MaterialPropertyBlock Original;
            internal Color Color;
            internal GameObject Snow;
        }

        const float SeasonSeconds = CityPresentation.DaySeconds * 2;
        readonly List<SeasonalSurface> seasonTrees = new();
        readonly List<SeasonalSurface> seasonGround = new();
        readonly Material[] weatherMaterials = new Material[4];
        readonly Texture2D[] weatherSprites = new Texture2D[4];
        ParticleSystem weather;
        Material snowMaterial;
        Season? currentSeason;
        float weatherBudget;

        static Season SeasonAt(float clock) => (Season)(Mathf.FloorToInt(Mathf.Max(0, clock) / SeasonSeconds) % 4);

        internal static string SeasonName(float clock) => SeasonAt(clock) switch
        {
            Season.Spring => "봄",
            Season.Summer => "여름",
            Season.Autumn => "가을",
            _ => "겨울"
        };

        internal void UpdateSeason(float clock, float dt)
        {
            Season season = SeasonAt(clock);
            if (season != currentSeason)
            {
                if (currentSeason == null) FindSeasonalSurfaces();
                PaintSeason(season);
                currentSeason = season;
                weatherBudget = 0;
            }
            if (season == Season.Summer || dt <= 0) return;

            float size = cityCamera.Lens.OrthographicSize;
            bool wide = size > 40;
            float rate = season == Season.Winter ? 27 : 17;
            weatherBudget += Mathf.Min(dt, .1f) * rate * (wide ? 1.45f : 1);
            float radius = wide ? size * 1.22f : size * 1.05f + 6;
            while (weatherBudget >= 1)
            {
                weatherBudget--;
                var spread = Random.insideUnitCircle * radius;
                float y = Random.Range(12f, wide ? 34 : 24);
                bool snow = season == Season.Winter;
                float flakeSize = snow ? Random.Range(.18f, .38f) : Random.Range(.26f, .55f);
                if (wide) flakeSize *= 1.32f;
                weather.Emit(new ParticleSystem.EmitParams
                {
                    position = V(viewTarget.x + spread.x, viewTarget.z + spread.y) + Vector3.up * y,
                    velocity = new Vector3(Random.Range(.22f, 1.2f), Random.Range(-2.05f, -.85f), Random.Range(-.42f, .42f)),
                    startSize = flakeSize,
                    startLifetime = Random.Range(4.2f, 7.6f),
                    rotation = Random.Range(0, 360f),
                    angularVelocity = snow ? Random.Range(-24, 24) : Random.Range(-87, 87),
                    startColor = season switch
                    {
                        Season.Spring => ColorOf(Random.value < .42f ? "F29ABC" : "FFDCE8"),
                        Season.Autumn => ColorOf(Random.value < .48f ? "DE8842" : "EFBA64"),
                        _ => ColorOf("F5FAFF")
                    }
                }, 1);
            }
        }

        void FindSeasonalSurfaces()
        {
            var terrain = root.Find("Terrain and waterfront");
            var roads = root.Find("Roads and crosswalks");
            var roadDetails = root.Find("도시 외관과 성장 시설/도로 표시와 보도");
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                var material = renderer.sharedMaterial;
                if (material == null || !material.HasProperty("_BaseColor")) continue;
                bool tree = renderer.transform.parent?.name == "Street tree" &&
                    renderer.name is ("Canopy" or "Canopy tip");
                var color = material.color;
                var bounds = renderer.bounds;
                bool road = (roads != null && renderer.transform.IsChildOf(roads)) ||
                    (roadDetails != null && renderer.transform.IsChildOf(roadDetails));
                bool districtSurface = bounds.size.x >= 8 && bounds.size.z >= 5 && color.b <= color.g * 1.08f;
                bool grass = !tree && bounds.max.y is > -.16f and < .52f &&
                    bounds.size.x > 3 && bounds.size.z > 2 && bounds.size.y < 1 && !road &&
                    (renderer.transform.parent == terrain || districtSurface || (color.g > color.r * .92f && color.g > color.b * 1.12f));
                if (!tree && !grass) continue;
                var original = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(original);
                if (original.HasColor("_BaseColor")) color = original.GetColor("_BaseColor");
                (tree ? seasonTrees : seasonGround).Add(new SeasonalSurface { Renderer = renderer, Color = color, Original = original });
            }
        }

        void PaintSeason(Season season)
        {
            var paint = new MaterialPropertyBlock();
            for (int i = 0; i < seasonTrees.Count; i++)
            {
                var tree = seasonTrees[i];
                Color tint = season switch
                {
                    Season.Spring => ColorOf(i % 3 == 0 ? "EF9FBB" : i % 3 == 1 ? "F9C3D4" : "FFE0E7"),
                    Season.Autumn => ColorOf(i % 3 == 0 ? "CB7040" : i % 3 == 1 ? "DF8B45" : "EBAC59"),
                    Season.Winter => Color.Lerp(tree.Color, ColorOf("919A83"), .58f),
                    _ => tree.Color
                };
                PaintSurface(tree, paint, tint, season != Season.Summer);
                if (season == Season.Winter && tree.Snow == null) tree.Snow = SnowCap(tree.Renderer);
                if (tree.Snow != null) tree.Snow.SetActive(season == Season.Winter);
            }
            foreach (var ground in seasonGround)
                PaintSurface(ground, paint, Color.Lerp(ground.Color, ColorOf("E6EEF0"), .80f), season == Season.Winter);

            if (weather == null && season != Season.Summer) BuildWeather();
            if (weather == null) return;
            weather.Clear();
            if (season != Season.Summer)
                weather.GetComponent<ParticleSystemRenderer>().sharedMaterial = WeatherMaterial(season);
        }

        static void PaintSurface(SeasonalSurface surface, MaterialPropertyBlock paint, Color color, bool colored)
        {
            surface.Renderer.SetPropertyBlock(surface.Original);
            if (!colored) return;
            surface.Renderer.GetPropertyBlock(paint);
            paint.SetColor("_BaseColor", color);
            surface.Renderer.SetPropertyBlock(paint);
        }

        GameObject SnowCap(MeshRenderer foliage)
        {
            var mesh = foliage.GetComponent<MeshFilter>().sharedMesh;
            var cap = new GameObject("Snow on canopy", typeof(MeshFilter), typeof(MeshRenderer));
            cap.transform.SetParent(foliage.transform, false);
            cap.transform.localPosition = Vector3.up * .29f;
            cap.transform.localScale = new Vector3(.85f, .51f, .85f);
            cap.GetComponent<MeshFilter>().sharedMesh = mesh;
            if (snowMaterial == null)
                snowMaterial = new Material(foliage.sharedMaterial) { name = "Canopy snow", color = ColorOf("F2F6F9") };
            var renderer = cap.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = snowMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return cap;
        }

        void BuildWeather()
        {
            weather = CreateVisualParticles("Seasonal weather", 430, .024f);
            var main = weather.main;
            main.useUnscaledTime = true;
            var noise = weather.noise;
            noise.enabled = true;
            noise.strength = .37f;
            noise.frequency = .28f;
            noise.scrollSpeed = .42f;
            var alpha = weather.colorOverLifetime;
            alpha.enabled = true;
            var tint = new Gradient();
            tint.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(.86f, .15f), new GradientAlphaKey(.75f, .8f), new GradientAlphaKey(0, 1) });
            alpha.color = tint;
        }

        Material WeatherMaterial(Season season)
        {
            int key = (int)season;
            if (weatherMaterials[key] != null) return weatherMaterials[key];
            const int side = 32;
            var pixels = new Color32[side * side];
            for (int py = 0; py < side; py++)
                for (int px = 0; px < side; px++)
                {
                    float x = (px + .5f) / side * 2 - 1, y = (py + .5f) / side * 2 - 1;
                    float edge = season switch
                    {
                        Season.Spring => 1 - Mathf.Sqrt(x * x * 2.15f + y * y * 1.25f) - .14f * Mathf.Max(0, y - 5 * Mathf.Abs(x)),
                        Season.Autumn => 1 - Mathf.Abs(y) - 1.55f * Mathf.Abs(x - y * .21f),
                        _ => 1 - Mathf.Sqrt(x * x + y * y)
                    };
                    float detail = season == Season.Autumn && Mathf.Abs(x - y * .21f) < .065f ? .80f : 1;
                    pixels[py * side + px] = new Color(detail, detail, detail, Mathf.Clamp01(edge * (season == Season.Winter ? 2.1f : 8)));
                }
            var sprite = new Texture2D(side, side, TextureFormat.RGBA32, false) { name = season + " particle", wrapMode = TextureWrapMode.Clamp };
            sprite.SetPixels32(pixels);
            sprite.Apply(false, true);
            var material = AtmosphereMaterial(season + " weather");
            material.SetFloat("_UseVertexColor", 1);
            material.SetTexture("_BaseMap", sprite);
            weatherSprites[key] = sprite;
            return weatherMaterials[key] = material;
        }

        ParticleSystem CreateVisualParticles(string name, int capacity, float gravity)
        {
            var system = EventNode(root, name, Vector3.zero).gameObject.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = capacity;
            main.gravityModifier = gravity;
            var emission = system.emission;
            emission.enabled = false;
            var shape = system.shape;
            shape.enabled = false;
            var renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = particles.GetComponent<ParticleSystemRenderer>().sharedMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            system.Play();
            return system;
        }

        static Material AtmosphereMaterial(string name) =>
            new Material(Resources.Load<Shader>("SeasonAndFestival")) { name = name };

        void DisposeSeasons()
        {
            foreach (var material in weatherMaterials) if (material != null) Destroy(material);
            foreach (var sprite in weatherSprites) if (sprite != null) Destroy(sprite);
            if (snowMaterial != null) Destroy(snowMaterial);
        }
    }
}
