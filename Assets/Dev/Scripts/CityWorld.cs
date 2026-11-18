using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace NaraTycoon
{
    public sealed class CityWorld : MonoBehaviour
    {
        [System.Serializable]
        struct Citizen
        {
            public Transform Root, Left, Right;
            public Vector3 Start, End;
            public Site Site;
            public float Phase;
        }

        internal static Color ColorOf(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out Color color);
            return color;
        }

        static readonly string[] SiteColors =
        {
            "298581", "DD9060", "527F9A", "EBAB5F", "E5B94B", "D4777A",
            "79A95F", "3B9C9D", "827CB6", "CF7953", "498CA7", "6B9F70", "477E9B", "B77B56"
        };
        internal static readonly string[] Names =
        {
            "조달청", "비축기지", "한결산업", "초등학교", "가로등 거리", "시립병원", "푸른숲공원",
            "교통센터", "도서관", "전통시장", "안전센터", "시민광장", "솔빛테크", "바로상사"
        };

        readonly Vector3[] centers =
        {
            V(-12,-27), V(12,-27), V(36,-27), V(-36,0), V(-12,0), V(12,0),
            V(36,0), V(-36,27), V(-12,27), V(12,27), V(36,27), V(-36,-27), V(60,-27), V(60,27)
        };
        [SerializeField] GameObject[] unlocked = new GameObject[Names.Length];
        [SerializeField] Transform[] lots = new Transform[Names.Length], docks = new Transform[Names.Length], labels = new Transform[Names.Length];
        [SerializeField] List<Citizen> citizens = new();
        [SerializeField] List<Rect> buildings = new();
        [SerializeField] List<Light> streetLights = new();
        [SerializeField] List<GameObject> routeDots = new();
        [SerializeField] List<GameObject> stockCrates = new();
        [SerializeField] Transform root, cameraRig, travelRig, heroArt, hero;
        [SerializeField] Light sun, fill;
        [SerializeField] Camera camera;
        [SerializeField] CinemachineCamera cityCamera, travelCamera;
        [SerializeField] ParticleSystem particles;
        [SerializeField] Material lampGlow;
        [SerializeField] Shader lit;
        [SerializeField] Animator heroAnimator;
        [SerializeField] GameObject reserveLevel, fleetLevel;
        [SerializeField] Transform[] officeStaff = new Transform[2];
        [SerializeField] Transform manualTruck, supplyTruck, deliveryTruck;
        readonly Vector3 overview = new(-9, 37, -40);
        static readonly Vector3 FollowOffset = new(0, 14, -12.5f);
        static readonly Vector3 FollowDirection = Vector3.forward * 1.3f - FollowOffset;
        Material runtimeGlow;
        Site? focus;

        internal Transform Hero => hero;
        internal Transform ManualTruck => manualTruck;
        internal Transform SupplyTruck => supplyTruck;
        internal Transform DeliveryTruck => deliveryTruck;
        internal Camera Camera => camera;
        internal Vector3 Center(Site site) => (int)site < lots.Length && lots[(int)site] != null ? lots[(int)site].position : centers[(int)site];
        internal Vector3 Label(Site site) => (int)site < labels.Length && labels[(int)site] != null ? labels[(int)site].position : Center(site) + Vector3.up * 7;
        internal Vector3 Dock(Site site) => (int)site < docks.Length && docks[(int)site] != null ? docks[(int)site].position : Center(site) + V(0, Center(site).z < 0 ? 9.4f : -8.8f);
        internal Vector3 Road(Site site) => new(Center(site).x, 0, Center(site).z >= 20 ? 13 : -13);
        internal static Vector3 V(float x, float z) => new(x, 0, z);

#if UNITY_EDITOR
        readonly Dictionary<string, Material> materials = new();

        public void Author(GameObject narmi, RuntimeAnimatorController animation, Shader cityShader, Shader particleShader)
        {
            root = Node("솔빛시", transform);
            lit = cityShader;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 116;
            RenderSettings.fogEndDistance = 235;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = ColorOf("C3E2F1");
            RenderSettings.ambientEquatorColor = ColorOf("95ACAC");
            RenderSettings.ambientGroundColor = ColorOf("677F7E");

            sun = LightAt("Warm sun", LightType.Directional, new Vector3(42, -38, 0), ColorOf("FFE4BE"), 1.55f);
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = .67f;
            fill = LightAt("Cool sky fill", LightType.Directional, new Vector3(31, 140, 0), ColorOf("A4D8F5"), .55f);
            var cameraObject = Node("Main Camera", root);
            cameraObject.gameObject.tag = "MainCamera";
            camera = cameraObject.gameObject.AddComponent<Camera>();
            camera.allowHDR = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.farClipPlane = 280;
            cameraObject.gameObject.AddComponent<AudioListener>();
            var cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = true;
            cameraData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            var brain = cameraObject.gameObject.AddComponent<CinemachineBrain>();
            brain.LensModeOverride = new CinemachineBrain.LensModeOverrideSettings
            {
                Enabled = true,
                DefaultMode = LensSettings.OverrideModes.Orthographic
            };
            camera.orthographic = true;
            camera.orthographicSize = 38;
            brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, .65f);
            brain.IgnoreTimeScale = true;
            cameraRig = Node("Cinemachine · City", root);
            travelRig = Node("Cinemachine · Follow", root);
            cityCamera = ConfigureCamera(cameraRig, 38, 36, 10);
            travelCamera = ConfigureCamera(travelRig, 11.5f, 36, 0);
            cameraRig.SetPositionAndRotation(overview, Quaternion.LookRotation(new Vector3(-1, 0, 0) - overview));
            camera.transform.SetPositionAndRotation(cameraRig.position, cameraRig.rotation);

            var volume = Node("URP · City Grade", root).gameObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            volume.sharedProfile = profile;
            profile.Add<Tonemapping>().mode.Override(TonemappingMode.Neutral);
            var bloom = profile.Add<Bloom>();
            bloom.threshold.Override(1.04f);
            bloom.intensity.Override(.34f);
            bloom.scatter.Override(.66f);
            profile.Add<ColorAdjustments>().saturation.Override(6);
            var vignette = profile.Add<Vignette>();
            vignette.intensity.Override(.13f);
            vignette.smoothness.Override(.49f);
            vignette.color.Override(ColorOf("243B5A"));

            lampGlow = new Material(Material("FFD59A")) { name = "Street lamp · Emissive" };
            lampGlow.EnableKeyword("_EMISSION");
            MakeTerrain();
            for (int i = 0; i < centers.Length; i++) MakeLot((Site)i);
            for (int x = -44; x <= 44; x += 8)
            {
                var lamp = Lamp(root, V(x,-8.8f), x >= -22 && x <= -2);
                if (x < -22 || x > -2) lamp.SetActive(true);
            }

            reserveLevel = Node("Additional warehouse racks", root, Dock(Site.Depot) + V(5, -3)).gameObject;
            fleetLevel = Node("Logistics annex", root, Center(Site.Depot) + V(-6, 5)).gameObject;
            Box(fleetLevel.transform, 0, 1, 0, 3, 2, 3, "4F939A");
            Box(fleetLevel.transform, 0, 2.12f, 0, 3.4f, .25f, 3.4f, "8CC8BB");
            for (int i = 0; i < 14; i++)
            {
                var rack = Node("Stock pallet " + i, i < 5 ? root : reserveLevel.transform);
                rack.position = Dock(Site.Depot) + V(4 + (i % 5) * .84f, -2f - (i / 5) * 1.05f);
                Box(rack, 0, .38f, 0, .74f, .72f, .74f, i % 2 == 0 ? "ADC3C5" : "D4946C");
                Box(rack, 0, .76f, 0, .77f, .08f, .77f, "EEE1BF");
                rack.gameObject.SetActive(false);
                stockCrates.Add(rack.gameObject);
            }
            officeStaff[0] = Person(root, Dock(Site.Office), "D79C62", "465C73", out _, out _);
            officeStaff[1] = Person(root, Dock(Site.Factory), "489994", "445D79", out _, out _);

            hero = Node("나르미", root, Dock(Site.Office) + V(-2, .9f));
            heroArt = Node("Character motion", Hero);
            if (narmi != null)
            {
                GameObject model = Object.Instantiate(narmi, heroArt);
                model.transform.localRotation = Quaternion.Euler(-90, 0, 0);
                heroAnimator = model.GetComponent<Animator>();
                if (heroAnimator == null) heroAnimator = model.AddComponent<Animator>();
                heroAnimator.runtimeAnimatorController = animation;
                heroAnimator.applyRootMotion = false;
                heroAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }
            else MakeMascot(heroArt);
            Hero.rotation = Quaternion.Euler(0, 195, 0);

            particles = Node("Shared celebration particles", root).gameObject.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.playOnAwake = false;
            main.loop = false;
            main.startColor = ColorOf("FFD886");
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 230;
            main.gravityModifier = .45f;
            var emission = particles.emission;
            emission.enabled = false;
            var shape = particles.shape;
            shape.enabled = false;
            var shrink = particles.sizeOverLifetime;
            shrink.enabled = true;
            shrink.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.EaseInOut(0, 1, 1, 0));
            particles.GetComponent<ParticleSystemRenderer>().sharedMaterial = new Material(particleShader) { name = "Municipal confetti" };
            for (int i = 0; i < 65; i++)
            {
                var dot = Primitive(PrimitiveType.Cube, "Route dash", root, Vector3.zero, new Vector3(.25f,.045f,1.1f), "F1C559");
                dot.gameObject.SetActive(false);
                routeDots.Add(dot.gameObject);
            }
            manualTruck = Truck("Manual truck", "E3A349", false);
            supplyTruck = Truck("Reserve truck", "6F9CB9", true);
            deliveryTruck = Truck("Delivery truck", "66AB96", false);
            UpdateActivity(new Procurement(), 0);
            ApplyDay(0);
        }

        static CinemachineCamera ConfigureCamera(Transform rig, float size, float fov, int priority)
        {
            var active = rig.gameObject.AddComponent<CinemachineCamera>();
            var lens = LensSettings.Default;
            lens.ModeOverride = LensSettings.OverrideModes.Orthographic;
            lens.OrthographicSize = size;
            lens.FieldOfView = fov;
            lens.NearClipPlane = .1f;
            lens.FarClipPlane = 280;
            active.Lens = lens;
            active.Priority = priority;
            return active;
        }

        Light LightAt(string name, LightType type, Vector3 angles, Color color, float intensity)
        {
            var light = Node(name, root).gameObject.AddComponent<Light>();
            light.type = type;
            light.transform.rotation = Quaternion.Euler(angles);
            light.color = color;
            light.intensity = intensity;
            return light;
        }
#endif

        internal void Focus(Site? site) => focus = site;

        internal void UpdateCamera(Transform follow, bool title, float dt)
        {
            Vector3 target = title ? V(0,0) : focus.HasValue ? Center(focus.Value) + V(6,0) : V(26,0);
            Vector3 lookFrom = focus.HasValue && !title ? target - FollowDirection * 2.2f : overview + target;
            cameraRig.position = Vector3.Lerp(cameraRig.position, lookFrom, 1 - Mathf.Exp(-4 * dt));
            cameraRig.rotation = Quaternion.Slerp(cameraRig.rotation, Quaternion.LookRotation(target - lookFrom), 1 - Mathf.Exp(-4 * dt));
            var lens = cityCamera.Lens;
            float size = title ? 43 : focus.HasValue ? 16 : 52;
            lens.OrthographicSize = Mathf.Lerp(lens.OrthographicSize, size, 1 - Mathf.Exp(-4 * dt));
            cityCamera.Lens = lens;
            if (follow == null) return;
            Vector3 flat = new Vector3(follow.position.x, 0, follow.position.z);
            travelRig.SetPositionAndRotation(flat + FollowOffset, Quaternion.LookRotation(FollowDirection));
        }

        internal void TrackPlayer(bool track) => travelCamera.Priority = track ? 20 : 0;

        internal void AnimateHero(bool moving)
        {
            if (heroAnimator != null && heroAnimator.runtimeAnimatorController != null)
                heroAnimator.SetBool("Moving", moving);
            else
                heroArt.localPosition = Vector3.up * (moving ? .14f * Mathf.Abs(Mathf.Sin(Time.time * 16)) : .035f * Mathf.Sin(Time.time * 3));
        }

        internal void ApplyDay(float elapsed)
        {
            float phase = elapsed % 174f / 174f;
            float night = Mathf.SmoothStep(0, 1, Mathf.Clamp01((phase - .49f) / .14f)) *
                (1 - Mathf.SmoothStep(0, 1, Mathf.Clamp01((phase - .86f) / .14f)));
            camera.backgroundColor = Color.Lerp(ColorOf("B8E4EA"), ColorOf("345173"), night);
            RenderSettings.fogColor = camera.backgroundColor;
            sun.color = Color.Lerp(ColorOf("FFE4BE"), ColorOf("92BAF5"), night);
            sun.intensity = Mathf.Lerp(1.55f, .42f, night);
            fill.intensity = Mathf.Lerp(.55f, .34f, night);
            if (Application.isPlaying && runtimeGlow == null)
            {
                runtimeGlow = new Material(lampGlow);
                foreach (Light light in streetLights) light.GetComponent<Renderer>().sharedMaterial = runtimeGlow;
            }
            (runtimeGlow != null ? runtimeGlow : lampGlow).SetColor("_EmissionColor", ColorOf("FFD18A") * Mathf.Lerp(.14f, 3.5f, night));
            foreach (var light in streetLights) light.intensity = night * 5.5f;
        }

        internal static bool Night(float elapsed) => elapsed % 174f > 100 && elapsed % 174f < 163;

        internal void UpdateActivity(Procurement model, float t)
        {
            for (int i = 0; i < Procurement.Catalog.Length; i++)
                unlocked[(int)Procurement.Catalog[i].Site].SetActive(model.IsActive(i));
            reserveLevel.SetActive(model.Data.WarehouseLevel >= 1);
            fleetLevel.SetActive(model.Data.WarehouseLevel >= 2);
            int unitsPerCrate = model.Capacity > 28 ? 3 : 2;
            int visibleCrates = (model.Stored + unitsPerCrate - 1) / unitsPerCrate;
            for (int i = 0; i < stockCrates.Count; i++) stockCrates[i].SetActive(i < visibleCrates);
            MoveWorker(officeStaff[0], Dock(Site.Office), model.Has(Upgrade.Clerk), t);
            Site labSite = Site.Factory;
            for (int i = 0; i < Procurement.Catalog.Length; i++)
                if (model.Data.Orders[i].Step == Step.Inspect) { labSite = model.Supplier(i); break; }
            MoveWorker(officeStaff[1], Dock(labSite), model.Has(Upgrade.Lab), t + 4);
            foreach (var citizen in citizens)
            {
                bool visible = unlocked[(int)citizen.Site].activeSelf && (citizen.Site != Site.Boulevard || Night(t));
                citizen.Root.gameObject.SetActive(visible);
                if (!visible) continue;
                float walk = (t * .13f + citizen.Phase) % 2;
                citizen.Root.position = Vector3.Lerp(citizen.Start, citizen.End, Mathf.PingPong(walk, 1));
                citizen.Root.rotation = Quaternion.LookRotation((citizen.End - citizen.Start) * (walk < 1 ? 1 : -1));
                float stride = Mathf.Sin((t + citizen.Phase) * 11) * 24;
                citizen.Left.localRotation = Quaternion.Euler(stride, 0, 0);
                citizen.Right.localRotation = Quaternion.Euler(-stride, 0, 0);
            }
        }

        static void MoveWorker(Transform worker, Vector3 dock, bool active, float t)
        {
            worker.gameObject.SetActive(active);
            float direction = Mathf.Cos(t * .65f);
            worker.position = dock + new Vector3(Mathf.Sin(t * .65f) * 3.4f, .06f + Mathf.Abs(Mathf.Sin(t * 9)) * .10f, 1.7f);
            worker.rotation = Quaternion.Euler(0, direction > 0 ? 90 : -90, 0);
        }

        internal bool Free(Vector3 p, float margin = .25f)
        {
            if (p.x < -53 || p.x > 77 || Mathf.Abs(p.z) > 39) return false;
            foreach (Rect building in buildings)
                if (p.x > building.xMin - margin && p.x < building.xMax + margin &&
                    p.z > building.yMin - margin && p.z < building.yMax + margin)
                    return false;
            return true;
        }

        internal bool IsRoad(Vector3 p)
        {
            float nearestStreet = Mathf.Abs(p.x - Mathf.Round(p.x / 24) * 24);
            return nearestStreet < 3.3f || Mathf.Abs(Mathf.Abs(p.z) - 13) < 3.4f;
        }

        internal Vector3[] Route(Site from, Site to)
        {
            Vector3 a = Road(from), b = Road(to);
            if (Mathf.Abs(a.z - b.z) < .1f) return new[] { Dock(from), a, b, Dock(to) };
            float cross = Mathf.Round((a.x + b.x) / 48) * 24;
            return new[] { Dock(from), a, V(cross, a.z), V(cross,b.z), b, Dock(to) };
        }

        internal void ShowRoute(Vector3[] path)
        {
            int used = 0;
            if (path != null)
                for (int i = 1; i < path.Length; i++)
                {
                    Vector3 dir = (path[i] - path[i - 1]).normalized;
                    float length = Vector3.Distance(path[i],path[i - 1]);
                    for (float j = 1; j < length && used < routeDots.Count; j += 2.7f)
                    {
                        GameObject dot = routeDots[used++];
                        dot.transform.SetPositionAndRotation(path[i - 1] + dir * j + Vector3.up * .18f, Quaternion.LookRotation(dir));
                        dot.SetActive(true);
                    }
                }
            for (int i = used; i < routeDots.Count; i++) routeDots[i].SetActive(false);
        }

#if UNITY_EDITOR
        Transform Truck(string name, string paint, bool supply)
        {
            Transform truck = Node(name, root);
            Box(truck, 0, .45f, 0, 1.50f, .30f, 3.15f, "263C4C");
            Box(truck, 0, .95f, .90f, 1.5f, .95f, 1.23f, paint);
            Box(truck, 0, 1.52f, .77f, 1.46f, .33f, 1.02f, "E9F5ED");
            Box(truck, 0, 1.33f, 1.39f, 1.22f, .37f, .06f, "4E7188");
            Box(truck, 0, 1.02f, -.75f, 1.46f, supply ? .64f : 1.15f, 1.92f, supply ? "E1A361" : "F6E8C7");
            Box(truck, 0, .84f, -1.73f, 1.35f, .32f, .06f, paint);
            for (int side = -1; side <= 1; side += 2)
            {
                Box(truck, side * .51f, .85f, 1.54f, .26f, .19f, .07f, "FFF0AD");
                foreach (float z in new[] {-1.06f,.95f})
                {
                    var tire = Primitive(PrimitiveType.Cylinder, "Tire", truck, new Vector3(side * .78f,.37f,z), new Vector3(.65f,.17f,.65f), "273544");
                    tire.localRotation = Quaternion.Euler(0,0,90);
                    var hub = Primitive(PrimitiveType.Cylinder, "Hub", truck, new Vector3(side * .96f,.37f,z), new Vector3(.33f,.03f,.33f), "95ACA9");
                    hub.localRotation = tire.localRotation;
                }
            }
            truck.gameObject.SetActive(false);
            return truck;
        }
#endif

        internal void Burst(Vector3 position, int count, bool dust = false)
        {
            for (int i = 0; i < count; i++)
            {
                Color tint = dust ? ColorOf("CFBA96") : ColorOf(i % 3 == 0 ? "FF8C8E" : i % 3 == 1 ? "FDD778" : "76DBC3");
                var random = Random.insideUnitCircle * (dust ? 1 : 3.2f);
                particles.Emit(new ParticleSystem.EmitParams
                {
                    position = position,
                    velocity = new Vector3(random.x, dust ? 1 : Random.Range(3, 7), random.y),
                    startColor = tint,
                    startSize = dust ? .32f : Random.Range(.28f, .53f),
                    startLifetime = dust ? .38f : Random.Range(.66f, 1.36f),
                    rotation = Random.Range(0f, 180f)
                }, 1);
            }
        }

#if UNITY_EDITOR
        public void UpgradeMap()
        {
            if (lots.Length >= Names.Length && lots[(int)Site.FactoryExpress] != null) return;
            const string palettePrefix = "Solbit · ";
            foreach (var asset in UnityEditor.AssetDatabase.LoadAllAssetsAtPath("Assets/Dev/Materials/Solbit Palette.mat"))
                if (asset is Material material && material.name.StartsWith(palettePrefix, System.StringComparison.Ordinal))
                    materials[material.name.Substring(palettePrefix.Length)] = material;
            System.Array.Resize(ref lots, Names.Length);
            System.Array.Resize(ref docks, Names.Length);
            System.Array.Resize(ref labels, Names.Length);
            System.Array.Resize(ref unlocked, Names.Length);
            lots[(int)Site.Factory].name = Names[(int)Site.Factory];
            if (heroAnimator != null) heroAnimator.transform.localRotation = Quaternion.Euler(-90, 0, 0);

            var terrain = root.Find("Terrain and waterfront");
            foreach (Transform layer in terrain)
            {
                var size = layer.localScale;
                if (!Mathf.Approximately(size.x, 110) && !Mathf.Approximately(size.x, 111)) continue;
                layer.localScale = new Vector3(size.x + 24, size.y, size.z);
                layer.localPosition += V(12, 0);
            }
            var roads = root.Find("Roads and crosswalks");
            foreach (Transform segment in roads)
            {
                var size = segment.localScale;
                if (Mathf.Approximately(size.x, 105))
                {
                    segment.localScale = new Vector3(129, size.y, size.z);
                    segment.localPosition += V(12, 0);
                }
                else if (Mathf.Approximately(size.x, 6.3f) && Mathf.Approximately(size.z, 79))
                    segment.localPosition += Vector3.down * .007f;
            }
            AddVerticalStreet(roads, 72);
            foreach (int z in new[] { -13, 13 })
            {
                for (float x = 55; x <= 76; x += 4f)
                    if (Mathf.Abs(x - Mathf.Round(x / 24) * 24) > 5)
                        Box(roads,x,.096f,z,1.56f,.012f,.12f,"F3DBA2");
                for (int i = -2; i <= 2; i++)
                    Box(roads,72+i*.72f,.10f,z-4.05f,.38f,.016f,1.05f,"E5ECDC");
            }

            for (int i = 0; i <= (int)Site.Plaza; i++)
                foreach (Transform part in lots[i])
                {
                    var size = part.localScale;
                    if (Mathf.Approximately(size.x, 18.6f) && Mathf.Approximately(size.z, 15.8f))
                        part.localScale = new Vector3(17.4f, size.y, size.z);
                    if (Mathf.Approximately(size.x, 17.5f) && Mathf.Approximately(size.z, 3.6f))
                        part.localPosition += Vector3.down * .013f;
                }

            MakeLot(Site.FactoryTech);
            MakeLot(Site.FactoryExpress);
            var landscape = root.Find("Trees and foothills");
            for (int i = 0; i < 6; i++)
                Tree(landscape,56+i*3.55f,-38+(i%2)*.75f,.88f+(i%3)*.13f,i+6);
        }

        void MakeTerrain()
        {
            var terrain = Node("Terrain and waterfront", root);
            var roads = Node("Roads and crosswalks", root);
            var landscape = Node("Trees and foothills", root);
            Box(terrain, 12, -2.1f, 0, 135, 3.5f, 83, "7FA894");
            Box(terrain, 12, -.4f, 0, 134, .73f, 82, "A8C98E");
            Box(terrain, 0, -4.0f, 0, 380, .4f, 380, "9DD4D5");
            for (int x = -48; x <= 72; x += 24) AddVerticalStreet(roads, x);
            for (int sign = -1; sign <= 1; sign += 2)
            {
                int z = sign * 13;
                Box(roads, 12, .029f,z,129,.09f,8.35f,"D1DDC4");
                Box(roads,12,.071f,z,129,.04f,6.35f,"657C85");
                for (float x = -49; x < 76; x += 4f)
                    if (Mathf.Abs(x - Mathf.Round(x / 24) * 24) > 5)
                        Box(roads,x,.096f,z,1.56f,.012f,.12f,"F3DBA2");
                for (int x = -48; x <= 72; x += 24)
                    for (int i = -2; i <= 2; i++)
                        Box(roads, x + i * .72f,.10f,z - 4.05f,.38f,.016f,1.05f,"E5ECDC");
            }
            var random = new System.Random(8276);
            for (int i = 0; i < 22; i++)
            {
                float x = -110 + i * 10.45f;
                float z = 54f + random.Next(0, 50);
                float height = random.Next(7, 20);
                Primitive(PrimitiveType.Sphere, "Soft foothill", landscape, new Vector3(x,-4,z), new Vector3(27,height,23), i % 3 == 0 ? "9CD2BB" : "84BFAE");
            }
            for (int i = 0; i < 37; i++)
            {
                float x = -53 + i * 3.5f;
                Tree(landscape, x, -38 + (i % 2) * .75f, .82f + (i % 5) * .13f, i);
            }
            for (int i = 0; i < 28; i++)
            {
                float x = -51 + i * 3.9f;
                Box(terrain, x,-3.75f, (i % 2 == 0 ? -1 : 1) * (44 + i % 7), 1.8f + i % 4,.025f,.13f,"B7E4DF");
            }
        }

        void AddVerticalStreet(Transform roads, int x)
        {
            Box(roads, x, .01f, 0, 8.2f, .10f, 79, "CAD7BE");
            Box(roads, x, .057f, 0, 6.3f, .042f, 79, "657C85");
            for (float z = -37; z < 38; z += 4)
                if (Mathf.Abs(Mathf.Abs(z) - 13) > 6)
                    Box(roads,x,.092f,z,.11f,.01f,1.55f,"F3DBA2");
        }

        void MakeLot(Site site)
        {
            int id = (int)site;
            var lot = Node(Names[id], root, Center(site));
            lots[id] = lot;
            docks[id] = Node("Interaction and loading point", lot, V(0, lot.position.z < 0 ? 9.4f : -8.8f));
            labels[id] = Node("Map label", lot, Vector3.up * (site <= Site.Factory ? 7.1f : 6.8f));
            string paint = SiteColors[id];
            bool outdoor = site == Site.Park || site == Site.Boulevard || site == Site.Plaza;
            Box(lot,0,.02f,0,17.4f,.13f,15.8f,outdoor ? "B9D299" : "E2D7BA");
            float entry = Center(site).z < 0 ? 7 : -7;
            Box(lot,0,.07f,entry,4.2f,.1f,6.2f,"EDE3CB");
            Box(lot,0,.11f,entry + (entry > 0 ? 2.6f : -1.9f),3.1f,.045f,1.75f,"83ABA1");
            if (!outdoor)
            {
                float height = site == Site.Hospital ? 5.9f : site == Site.Office || site == Site.Center ? 5.2f : 3.7f;
                float width = site == Site.School ? 12.5f : site == Site.Factory || site == Site.Market ? 12.1f : 10.7f;
                Box(lot,0,height *.5f,0,width,height,6.6f,"F4E8D0");
                Box(lot,0,.35f,0,width+.18f,.65f,6.78f,paint);
                Box(lot,0,height-.53f,0,width+.20f,.66f,6.8f,paint);
                if (site == Site.School || site == Site.Market)
                {
                    var left = Box(lot,0,height+.55f,-1.8f,width+.8f,.26f,4.1f,paint);
                    var right = Box(lot,0,height+.55f,1.8f,width+.8f,.26f,4.1f,paint);
                    left.localRotation = Quaternion.Euler(-16,0,0);
                    right.localRotation = Quaternion.Euler(16,0,0);
                }
                else
                {
                    Box(lot,0,height+.07f,0,width+.56f,.32f,7.08f,paint);
                    Box(lot,-1,height+.30f,.4f,width*.64f,.16f,4.1f,"E9DEC6");
                }
                int rows = height > 4.5f ? 2 : 1;
                for (int row = 0; row < rows; row++)
                    for (int col = -2; col <= 2; col++)
                    {
                        if (row == 0 && col == 0) continue;
                        for (int side = -1; side <= 1; side += 2)
                        {
                            float px = col * width / 5.55f, py = 1.53f + row * 1.84f;
                            Box(lot,px,py,side*3.33f,1.43f,1.33f,.13f,"C3DAD0");
                            Box(lot,px,py,side*3.43f,1.17f,1.07f,.08f,"598696");
                            Box(lot,px+.32f,py+.12f,side*3.48f,.12f,.83f,.035f,"ADD1CE");
                        }
                    }
                for (int side = -1; side <= 1; side += 2)
                {
                    for (int w = -1; w <= 1; w++)
                        Box(lot,side*(width*.5f+.04f),2.03f,w*1.6f,.09f,1.18f,1.15f,"72A0A7");
                    Box(lot,0,1.13f,side*3.37f,1.9f,2.27f,.19f,"365B6C");
                    Box(lot,0,2.53f,side*3.62f,2.82f,.19f,1.0f,paint);
                }
                buildings.Add(new Rect(Center(site).x-width*.5f, Center(site).z-3.3f,width,6.6f));
                DecorateBuilding(lot, site, height, paint);
            }
            else
            {
                Box(lot,0,.12f,0,4.2f,.10f,14.8f,"EDE2C5");
                Box(lot,0,.107f,0,17.5f,.10f,3.6f,"EDE2C5");
                if (site == Site.Park)
                {
                    Primitive(PrimitiveType.Cylinder,"Fountain",lot,new Vector3(3.8f,.28f,1.6f),new Vector3(3.6f,.23f,3.6f),"A3B5AE");
                    Primitive(PrimitiveType.Cylinder,"Water",lot,new Vector3(3.8f,.52f,1.6f),new Vector3(3.2f,.04f,3.2f),"80BECB");
                }
                if (site == Site.Plaza)
                {
                    Primitive(PrimitiveType.Cylinder,"Plaza mosaic",lot,new Vector3(0,.26f,0),new Vector3(7.8f,.16f,7.8f),"EAC783");
                    Primitive(PrimitiveType.Cylinder,"Plaza inlay",lot,new Vector3(0,.44f,0),new Vector3(6.7f,.035f,6.7f),"89B5A8");
                }
            }
            Tree(lot,-7.2f,-4.7f,.9f,id);
            Tree(lot,7.1f,4.8f,1.05f,id+3);
            Tree(lot,-7.3f,4.8f,.85f,id+4);
            for (int i = -1; i <= 1; i++)
            {
                Box(lot, i * 1.3f, .24f, -5.4f, 1.08f, .27f, .68f, "A9B797");
                Primitive(PrimitiveType.Sphere, "Flower box", lot,new Vector3(i*1.3f,.48f,-5.4f),new Vector3(.79f,.44f,.5f),i % 2 == 0 ? "E2A17A" : "E6BC68");
            }
            var activity = Node("Facility activation", lot);
            unlocked[id] = activity.gameObject;
            if (site == Site.Boulevard)
            {
                for (int i = -2; i <= 2; i++) Lamp(activity, V(i * 3.55f, -2.35f), true);
            }
            else if (site == Site.Park || site == Site.Plaza)
            {
                for (int i = -1; i <= 1; i += 2)
                {
                    var bench = Node("Smart furniture",activity,V(i*4.3f,-2.35f));
                    Box(bench,0,.78f,0,2.6f,.19f,.68f,"CA925B");
                    Box(bench,0,1.16f,.31f,2.6f,.43f,.16f,paint);
                    Box(bench,-.87f,.33f,0,.22f,.64f,.58f,"4C696D");
                    Box(bench,.87f,.33f,0,.22f,.64f,.58f,"4C696D");
                    if (site == Site.Plaza)
                    {
                        Box(bench,0,2.71f,.14f,3.35f,.22f,1.9f,"557B9A").localRotation = Quaternion.Euler(-14,0,0);
                        Box(bench,0,1.37f,.66f,.26f,2.72f,.27f,"668A87");
                    }
                }
            }
            else
            {
                // Equipment is visible on the forecourt from the management camera.
                for (int i = -1; i <= 1; i++)
                {
                    var item = Node("Installed equipment", activity, V(2.6f + i * 1.5f,-5.55f));
                    Box(item,0,.32f,0,1.15f,.56f,.8f,paint);
                    Box(item,0,.69f,0,1.25f,.17f,.9f,"F6D88F");
                    if (site == Site.School)
                    {
                        Box(item,0,.27f,-.69f,.58f,.11f,.44f,"CF9F76");
                        Box(item,0,.52f,-.88f,.58f,.34f,.09f,"CF9F76");
                    }
                    else
                        Box(item,0,.60f,-.43f,.48f,.20f,.055f,"4C7082");
                }
            }
            if (id >= 3 && !Procurement.IsSupplier(site))
                for (int i = 0; i < (site == Site.School ? 6 : 4); i++)
                {
                    Vector3 start = Center(site) + V(-7.1f + i * .65f,-6.25f + (i % 2) * .7f);
                    Vector3 end = Center(site) + V(7.4f - i * .48f,-6.25f + (i % 2) * .7f);
                    var person = Person(root,start,i % 3 == 0 ? paint : i % 3 == 1 ? "D6879A" : "69989F","465E73",out var left,out var right);
                    if (site == Site.School) person.localScale = Vector3.one * .77f;
                    citizens.Add(new Citizen { Root = person, Left = left, Right = right, Start = start, End = end, Site = site, Phase = i*.34f });
                }
            activity.gameObject.SetActive(id < 3);
        }

        void DecorateBuilding(Transform lot, Site site, float height, string paint)
        {
            if (site == Site.Hospital)
            {
                Box(lot,0,height-.9f,-3.5f,.35f,1.44f,.12f,"DC777A");
                Box(lot,0,height-.9f,-3.54f,1.38f,.35f,.12f,"DC777A");
            }
            if (site == Site.Office || site == Site.School)
            {
                Box(lot,-2.6f,height+.85f,.8f,.12f,1.88f,.12f,"678486");
                Box(lot,-1.97f,height+1.38f,.8f,1.2f,.58f,.12f,paint);
            }
            if (Procurement.IsSupplier(site) || site == Site.Depot)
            {
                Primitive(PrimitiveType.Cylinder,"Vent",lot,new Vector3(3,height+.68f,1.2f),new Vector3(.8f,.7f,.8f),"64808B");
                for (int i = 0; i < 3; i++) Box(lot,-3.2f+i*1.28f,.53f,-4.8f,1.05f,1.02f,.95f,"D5A66E");
            }
            if (site == Site.Center || site == Site.Transit)
            {
                Box(lot,2.8f,height+.85f,.88f,.16f,1.7f,.16f,"557B88");
                Primitive(PrimitiveType.Sphere,"Antenna cap",lot,new Vector3(2.8f,height+1.72f,.88f),Vector3.one*.47f,"E8BA68");
            }
            if (site == Site.Market || site == Site.Library)
                for (int i = 0; i < 8; i++) Box(lot,-2.9f+i*.83f,2.52f,-4f,.8f,.19f,1.58f,i % 2 == 0 ? paint : "F8E7C4");
        }

        GameObject Lamp(Transform parent, Vector3 pos, bool powered)
        {
            var pole = Node("Streetlight", parent, pos);
            Box(pole,0,.12f,0,.49f,.23f,.49f,"9CAFA5");
            Box(pole,0,1.65f,0,.14f,3.1f,.14f,"4B6C79");
            Box(pole,0,3.16f,.28f,.18f,.16f,.65f,"4B6C79");
            var head = Box(pole,0,3.09f,.55f,.51f,.16f,.34f,powered ? "FFD59A" : "9CABA7");
            if (powered)
            {
                head.GetComponent<Renderer>().sharedMaterial = lampGlow;
                var light = head.gameObject.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = ColorOf("FFD198");
                light.range = 6.1f;
                streetLights.Add(light);
            }
            return pole.gameObject;
        }

        Transform Person(Transform parent, Vector3 pos, string top, string bottom, out Transform left, out Transform right)
        {
            var person = Node("Citizen", parent, pos);
            left = Node("Left foot", person, new Vector3(-.15f,.56f,0));
            right = Node("Right foot", person, new Vector3(.15f,.56f,0));
            foreach (var leg in new[] {left,right})
            {
                Box(leg,0,-.24f,0,.22f,.46f,.24f,bottom);
                Box(leg,0,-.47f,.07f,.23f,.15f,.35f,"314554");
            }
            Box(person,0,.84f,0,.55f,.64f,.43f,top);
            Box(person,-.36f,.81f,0,.18f,.43f,.23f,top);
            Box(person,.36f,.81f,0,.18f,.43f,.23f,top);
            Primitive(PrimitiveType.Sphere,"Head",person,new Vector3(0,1.43f,0),new Vector3(.58f,.59f,.56f),"EEC19E");
            Primitive(PrimitiveType.Sphere,"Hair",person,new Vector3(0,1.61f,-.045f),new Vector3(.61f,.34f,.58f),bottom);
            return person;
        }

        void MakeMascot(Transform figure)
        {
            Primitive(PrimitiveType.Sphere,"Body",figure,new Vector3(0,.65f,0),new Vector3(1.16f,1.14f,.91f),"FFF2D7");
            Primitive(PrimitiveType.Sphere,"Head",figure,new Vector3(0,1.49f,0),new Vector3(1.46f,1.28f,1.13f),"FFF2D7");
            Primitive(PrimitiveType.Sphere,"Beak",figure,new Vector3(0,1.32f,.58f),new Vector3(.40f,.21f,.36f),"F3B95F");
            Primitive(PrimitiveType.Cylinder,"Scarf",figure,new Vector3(0,1.12f,0),new Vector3(.91f,.16f,.84f),"388982");
            Box(figure,.48f,.83f,-.12f,.25f,.57f,.17f,"388982").localRotation = Quaternion.Euler(0,0,35);
            for (int s = -1; s <= 1; s+=2)
            {
                Primitive(PrimitiveType.Sphere,"Eye",figure,new Vector3(s*.27f,1.63f,.48f),new Vector3(.155f,.205f,.13f),"293D4C");
                Primitive(PrimitiveType.Sphere,"Wing",figure,new Vector3(s*.51f,.78f,0),new Vector3(.37f,.6f,.58f),"DADAB8");
                Primitive(PrimitiveType.Sphere,"Foot",figure,new Vector3(s*.30f,.16f,.20f),new Vector3(.51f,.24f,.56f),"F1B65B");
            }
        }

        void Tree(Transform parent, float x, float z, float size, int theme)
        {
            string leaf = theme % 5 == 0 ? "DBAA79" : theme % 3 == 0 ? "82B580" : "75AA87";
            var tree = Node("Street tree",parent,V(x,z));
            tree.localScale = Vector3.one * size;
            Primitive(PrimitiveType.Cylinder,"Trunk",tree,new Vector3(0,.95f,0),new Vector3(.30f,.95f,.30f),"9A805F");
            Primitive(PrimitiveType.Sphere,"Canopy",tree,new Vector3(0,2.42f,0),new Vector3(2.28f,2.55f,2.09f),leaf);
            Primitive(PrimitiveType.Sphere,"Canopy tip",tree,new Vector3(.43f,3.12f,.09f),new Vector3(1.45f,1.84f,1.53f),theme%5 == 0 ? "E7BC8E" : "8DBA86");
        }

        Material Material(string hex)
        {
            if (!materials.TryGetValue(hex, out var m))
            {
                m = new Material(lit) { name = "Solbit · " + hex, color = ColorOf(hex), enableInstancing = true };
                if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", .12f);
                materials.Add(hex,m);
            }
            return m;
        }

        Transform Box(Transform parent, float x, float y, float z, float sx, float sy, float sz, string color) =>
            Primitive(PrimitiveType.Cube,"Trim",parent,new Vector3(x,y,z),new Vector3(sx,sy,sz),color);

        Transform Primitive(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, string color)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent,false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            if (Application.isPlaying) Object.Destroy(go.GetComponent<Collider>());
            else Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().sharedMaterial = Material(color);
            return go.transform;
        }

        static Transform Node(string name, Transform parent, Vector3 local = default)
        {
            var node = new GameObject(name).transform;
            node.SetParent(parent, false);
            node.localPosition = local;
            return node;
        }
#endif
    }
}
