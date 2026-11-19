#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace NaraTycoon
{
    public sealed partial class CityWorld
    {
        const string Asphalt = "435568";
        const string Pavement = "D8D8CC";
        const string Concrete = "EBE6D8";
        const string BlueGlass = "5884A0";
        const string TrimSteel = "A8C2CD";
        const string Garden = "88AC8A";
        const string River = "66A1B4";
        const string Amber = "ECAF68";
        readonly List<Renderer> warmWindows = new();
        readonly List<Renderer> coolWindows = new();
        readonly List<Renderer> electricLights = new();
        readonly List<Light> avenueLights = new();
        readonly List<CityPresentation.Traffic> vehicles = new();

        public int AlignRoadsideProps()
        {
            var lanes = new List<Bounds>();
            foreach (var owner in new[] { root.Find("Roads and crosswalks"), presentation?.transform.Find("도로 표시와 보도") })
            {
                if (owner == null) continue;
                foreach (Transform part in owner)
                    if (Mathf.Approximately(part.localScale.x, 6.3f) || Mathf.Approximately(part.localScale.z, 6.35f) ||
                        Mathf.Approximately(part.localScale.z, 6.3f))
                        if (part.TryGetComponent<Renderer>(out var surface)) lanes.Add(surface.bounds);
            }

            int moved = 0;
            foreach (var prop in root.GetComponentsInChildren<Transform>(true))
            {
                if (prop.name is not ("Street tree" or "가로등" or "중앙 버스 쉼터")) continue;
                var parts = prop.GetComponentsInChildren<MeshRenderer>(true);
                if (parts.Length == 0) continue;
                var bounds = parts[0].bounds;
                foreach (var part in parts) bounds.Encapsulate(part.bounds);
                if (!lanes.Exists(lane => OverlapsRoad(bounds, lane))) continue;
                var best = Vector3.zero;
                float distance = float.PositiveInfinity;
                foreach (var lane in lanes)
                {
                    if (!OverlapsRoad(bounds, lane)) continue;
                    foreach (var offset in new[] {
                        V(lane.min.x - bounds.max.x - .12f, 0), V(lane.max.x - bounds.min.x + .12f, 0),
                        V(0, lane.min.z - bounds.max.z - .12f), V(0, lane.max.z - bounds.min.z + .12f) })
                    {
                        if (offset.sqrMagnitude >= distance) continue;
                        var shifted = bounds;
                        shifted.center += offset;
                        if (lanes.Exists(other => OverlapsRoad(shifted, other))) continue;
                        best = offset;
                        distance = offset.sqrMagnitude;
                    }
                }
                if (best == Vector3.zero) continue;
                prop.position += best;
                moved++;
            }
            return moved;
        }

        static bool OverlapsRoad(Bounds prop, Bounds lane) => prop.min.x < lane.max.x && prop.max.x > lane.min.x &&
            prop.min.z < lane.max.z && prop.max.z > lane.min.z;

        public void AuthorUrbanDistricts(VolumeProfile day, VolumeProfile dusk, VolumeProfile night)
        {
            LoadPalette();
            if (presentation != null)
            {
                AddCityLandmarks(presentation.transform);
                return;
            }

            var city = Node("도시 외관과 성장 시설", root);
            var baseGrade = root.Find("URP · City Grade").GetComponent<Volume>();
            baseGrade.sharedProfile = day;
            var duskVolume = UrbanVolume(city, "URP · Shifting sunset", dusk, 11);
            var nightVolume = UrbanVolume(city, "URP · City at night", night, 12);
            var districtStages = new GameObject[4];
            var landmarks = new Vector3[5];
            RefinishRoads(city);
            for (int i = 0; i < lots.Length; i++) DevelopFacade(lots[i], (Site)i);
            BuildSkyline(city);
            Rect tower = BuildDevelopment(city, districtStages, landmarks);

            var glow = new[]
            {
                GlowGroup("Urban warm windows", "69859A", "FFA24A", 1.85f, warmWindows),
                GlowGroup("Urban cool windows", "436B87", "5C9EE8", .42f, coolWindows),
                GlowGroup("Urban street signs", "9DBEC3", "A7EAFF", 2.45f, electricLights)
            };
            presentation = city.gameObject.AddComponent<CityPresentation>();
            presentation.Configure(duskVolume, nightVolume, districtStages, landmarks, avenueLights.ToArray(), glow, vehicles.ToArray(), tower);
            AddCityLandmarks(city);
            UpdateCamera(Hero, true, 1);
            camera.transform.SetPositionAndRotation(cameraRig.position, cameraRig.rotation);
            camera.orthographicSize = cityCamera.Lens.OrthographicSize;
        }

        static Volume UrbanVolume(Transform owner, string name, VolumeProfile profile, float priority)
        {
            var volume = Node(name, owner).gameObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = priority;
            volume.sharedProfile = profile;
            volume.weight = 0;
            return volume;
        }

        void AddCityLandmarks(Transform city)
        {
            var skyline = city.Find("북쪽 상업 지구");
            var tallest = skyline.Find("상업 블록 2/업무용 빌딩");
            if (tallest.Find("계단형 전망층") != null) return;

            foreach (Transform hill in root.Find("Trees and foothills"))
                if (hill.name == "Soft foothill") hill.gameObject.SetActive(false);

            var crown = Node("계단형 전망층", tallest, new Vector3(.6f,18.1f,.20f));
            Box(crown,0,.34f,0,6.55f,.65f,5.35f,"5E869A");
            Box(crown,0,1.47f,0,5.86f,1.65f,4.74f,"507D95");
            Box(crown,0,2.39f,0,6.1f,.22f,4.97f,"B8CBCD");
            Box(crown,.15f,3.68f,.29f,.17f,2.5f,.17f,"537688");
            Box(crown,.15f,4.96f,.29f,.3f,.29f,.3f,Amber);
            for (int i = -2; i <= 2; i++)
            {
                Box(crown,i*.98f,1.47f,-2.39f,.055f,1.49f,.06f,TrimSteel);
                Box(crown,-2.95f,1.47f,i*.79f,.06f,1.49f,.055f,TrimSteel);
            }

            var sixth = skyline.Find("상업 블록 4");
            var annex = Node("세로 루버 별관", sixth, V(-5.35f,4.55f));
            Box(annex,0,4.75f,0,4.1f,9.5f,3.9f,"9EB8C1");
            Box(annex,0,9.6f,0,4.33f,.28f,4.12f,Concrete);
            for (int i = -2; i <= 2; i++)
            {
                Box(annex,i*.71f,4.9f,-1.975f,.16f,8.9f,.125f,TrimSteel);
                Box(annex,-2.08f,4.9f,i*.68f,.13f,8.9f,.14f,TrimSteel);
            }

            var park = city.Find("03 수변 공원");
            var pier = Node("강변 전망 데크", park, V(35,63.75f));
            Box(pier,0,.02f,1.1f,13.2f,.21f,4.8f,"C5A48A");
            Box(pier,0,.86f,3.45f,13.18f,.12f,.16f,TrimSteel);
            for (int i = -3; i <= 3; i++)
                Box(pier,i*2.09f,.39f,3.45f,.13f,.85f,.13f,"5D8590");
            for (int sign = -1; sign <= 1; sign += 2)
            {
                Box(pier,sign*5.08f,1.48f,.65f,.20f,2.82f,.20f,"705B50");
                Box(pier,sign*2.85f,.39f,2.1f,2.16f,.38f,.58f,"9D775E");
            }
            for (int i = -4; i <= 4; i++)
                Box(pier,i*1.29f,2.89f,.64f,.22f,.18f,3.3f,"A77F62");
            Box(pier,0,2.83f,.30f,10.93f,.29f,.19f,"856451");

            var smartTower = city.Find("04 스마트시티/디지털 도시 센터/스마트 타워");
            Box(smartTower,0,18.58f,0,5.22f,.42f,4.07f,"618D9F");
            Box(smartTower,.38f,19.58f,.25f,4.13f,1.88f,3.21f,"78A8B5");
            Box(smartTower,.38f,20.61f,.25f,4.44f,.19f,3.55f,TrimSteel);
        }

        CityPresentation.Glow GlowGroup(string name, string dayColor, string nightColor, float intensity, List<Renderer> surfaces)
        {
            var mat = new Material(Material(dayColor)) { name = name };
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            mat.EnableKeyword("_EMISSION");
            foreach (var surface in surfaces) surface.sharedMaterial = mat;
            return new CityPresentation.Glow { Material = mat, Surfaces = surfaces.ToArray(), NightColor = ColorOf(nightColor), Brightness = intensity };
        }

        void RefinishRoads(Transform city)
        {
            var land = root.Find("Terrain and waterfront");
            foreach (Transform part in land)
            {
                var size = part.localScale;
                var renderer = part.GetComponent<MeshRenderer>();
                if (size.x == 134 || size.x == 135)
                {
                    part.localScale += Vector3.forward * 25;
                    part.localPosition += Vector3.forward * 12.5f;
                    renderer.sharedMaterial = Material(size.x == 134 ? "A2B999" : "718F94");
                }
                else if (size.x == 380) renderer.sharedMaterial = Material(River);
            }
            foreach (Transform hill in root.Find("Trees and foothills"))
                if (hill.name == "Soft foothill") hill.localPosition += Vector3.forward * 39;

            var roads = root.Find("Roads and crosswalks");
            foreach (Transform part in roads)
            {
                var size = part.localScale;
                var renderer = part.GetComponent<MeshRenderer>();
                if (size.x == 6.3f || size.z == 6.35f) renderer.sharedMaterial = Material(Asphalt);
                else if (size.x == 8.2f || size.z == 8.35f) renderer.sharedMaterial = Material(Pavement);
                else if (size.x < .4f && size.z > 1 && size.z < 1.1f) part.gameObject.SetActive(false);
            }

            var details = Node("도로 표시와 보도", city);
            foreach (int junctionZ in new[] { -13, 13, 40 })
            {
                for (int cross = -48; cross <= 72; cross += 24)
                {
                    if (junctionZ == 40)
                    {
                        Box(details, cross, .042f, 40.25f, 8.2f, .105f, 10, Pavement);
                        Box(details, cross, .089f, 40.25f, 6.3f, .04f, 10, Asphalt);
                    }
                    for (int side = -1; side <= 1; side += 2)
                        for (int stripe = -4; stripe <= 4; stripe++)
                            Box(details, cross + stripe * .59f, .130f, junctionZ + side * 3.98f, .30f, .027f, .96f, "F4F3E8");
                }
                for (int lane = -1; lane <= 1; lane += 2)
                    Box(details, 12, junctionZ == 40 ? .139f : .115f, junctionZ + lane * 2.88f, 128, .012f, .075f, "C5CBD0");
            }
            Box(details, 12, .058f, 40, 129, .095f, 8.45f, Pavement);
            Box(details, 12, .103f, 40, 129, .04f, 6.3f, Asphalt);
            for (float dash = -49; dash < 76; dash += 4.5f)
                if (Mathf.Abs(dash - Mathf.Round(dash / 24) * 24) > 5)
                    Box(details, dash, .135f, 40, 1.7f, .02f, .13f, "DDD39C");

            for (int x = -48; x <= 72; x += 24)
                for (int side = -1; side <= 1; side += 2)
                    for (int row = -1; row <= 1; row += 2)
                        Box(details, x + side * 2.9f, .104f, row * 26.8f, .07f, .012f, 18.8f, "C5CBD0");

            for (int i = 0; i < 17; i++)
            {
                float x = -49 + i * 7.65f;
                Box(details, x, .014f, 59.8f, 7.5f, .095f, 4, "CDCBC1");
                Box(details, x, .33f, 61.9f, 7.51f, .72f, .32f, "587F8B");
                Box(details, x, .76f, 61.9f, 7.52f, .15f, .53f, TrimSteel);
            }
            AddTraffic(details, false, "E6A078", 0, 6.6f, 9, 1);
            AddTraffic(details, false, "77A9BA", 0, 7.7f, 94, -1);
        }

        void DevelopFacade(Transform lot, Site site)
        {
            bool openSpace = site is Site.Boulevard or Site.Park or Site.Plaza;
            if (openSpace)
            {
                var courtyard = Node("광장과 보행로", lot);
                for (int side = -1; side <= 1; side += 2)
                {
                    Box(courtyard, side * 6.55f, .19f, 2.3f, 1.6f, .24f, 3.34f, "C6CAB6");
                    for (int i = 0; i < 3; i++)
                        Primitive(PrimitiveType.Sphere, "Box hedge", courtyard, new Vector3(side * 6.55f, .56f, 1.23f + i * .98f), new Vector3(1.21f, .57f, 1.12f), Garden);
                }
                return;
            }

            float width = site == Site.School ? 12.5f : site is Site.Factory or Site.Market ? 12.1f : 10.7f;
            float facade = lot.position.z < 0 ? 1 : -1;
            foreach (Transform old in lot)
                if (old.name == "Trim" && old.localPosition.y >= .3f && Mathf.Abs(old.localPosition.z) <= 4.25f)
                    old.gameObject.SetActive(false);

            Transform building = Node("건축 외장", lot);
            string accent = SiteColors[(int)site];
            bool service = Procurement.IsSupplier(site) || site == Site.Depot;
            int floors = site is Site.Office or Site.Hospital or Site.Center ? 3 : 2;
            if (service)
                IndustrialHall(building, width, accent, facade);
            else
            {
                float roof = Block(building, width, 6.6f, floors, Concrete, accent, site == Site.Library);
                MakeEntrance(building, width, facade, accent);
                if (site == Site.Center)
                {
                    roof += Block(Node("관제 센터 타워", building, new Vector3(.85f, roof, .2f)), 7.2f, 5.45f, 6, "B9D0D7", "497E97", true);
                    Box(building,1.55f,roof+1.3f,.8f,.17f,2.6f,.17f, "567886");
                    Electric(Box(building,1.55f,roof+2.58f,.8f,.35f,.24f,.34f, "CBE4EA"));
                }
                else if (site == Site.Office || site == Site.Hospital)
                {
                    bool hospital = site == Site.Hospital;
                    roof += Block(Node("상층부", building, new Vector3(hospital ? 1.1f : -1.25f, roof, .12f)), hospital ? 8.1f : 7.5f, 5.65f,
                        hospital ? 4 : 2, "DDE4E0", accent, !hospital);
                    if (hospital)
                    {
                        Box(building,0,5.66f,-3.49f,.42f,1.37f,.17f,"CE696A");
                        Box(building,0,5.66f,-3.6f,1.38f,.42f,.12f,"CE696A");
                        Box(building,1.12f,roof+.11f,.17f,3.75f,.18f,3.5f,"526B79");
                        for (int side = -1; side <= 1; side += 2)
                            Box(building,1.12f+side*.49f,roof+.205f,.17f,.19f,.023f,1.35f,"EDE7C8");
                        Box(building,1.12f,roof+.205f,.17f,.98f,.023f,.18f,"EDE7C8");
                    }
                }
                else if (site == Site.Library)
                    roof += Block(Node("독서실", building, new Vector3(-1.8f, roof, .45f)), 6.1f, 4.9f, 3, "DAD4DC", accent, true);
                else if (site == Site.School)
                {
                    Box(building,0,roof+.32f,-1.69f,width+.22f,.24f,3.6f,"C78964").localRotation = Quaternion.Euler(-12,0,0);
                    Box(building,0,roof+.32f,1.69f,width+.22f,.24f,3.6f,"C78964").localRotation = Quaternion.Euler(12,0,0);
                    roof += .9f;
                }
                else if (site == Site.Market)
                    for (int i = 0; i < 9; i++)
                        Box(building, (i-4)*1.02f, 2.78f, facade*4.15f, 1.02f,.19f,1.72f,i%2==0 ? accent : "F0DCAB").localRotation = Quaternion.Euler(facade*8,0,0);
                else
                    Box(building, 0, roof+.13f, facade*.4f,width+.38f,.24f,8.2f,"94BEC6");

                if (site != Site.School)
                {
                    Box(building,-2.2f,roof+.40f,-.45f,1.45f,.62f,.92f,TrimSteel);
                    Box(building,-2.2f,roof+.73f,-.45f,1.56f,.09f,.97f,"5F7889");
                }
                labels[(int)site].localPosition = new Vector3(0, roof+1.65f, 0);
            }

            for (int i = -1; i <= 1; i += 2)
            {
                Box(lot,i*3.4f,.23f,facade*6.62f,1.64f,.26f,.74f,"BCC8B6");
                Primitive(PrimitiveType.Sphere,"Facade planter",lot,new Vector3(i*3.4f,.51f,facade*6.62f),new Vector3(1.46f,.57f,.64f),Garden);
            }
        }

        float Block(Transform building, float width, float depth, int floors, string wall, string accent, bool glazed)
        {
            const float storey = 1.95f;
            float top = .38f + floors * storey;
            Box(building,0,top*.5f,0,width,top,depth,wall);
            Box(building,0,.22f,0,width+.12f,.36f,depth+.11f,"8EA5A9");
            Box(building,0,top+.06f,0,width+.35f,.22f,depth+.32f,accent);
            Box(building,0,top+.18f,0,width-.32f,.10f,depth-.31f,"B9C6C2");
            int columns = Mathf.RoundToInt(width / 1.7f);
            for (int floor = 0; floor < floors; floor++)
            {
                float y = 1.32f + floor*storey;
                if (glazed)
                    Box(building,0,y-.72f,0,width+.17f,.16f,depth+.16f,TrimSteel);
                else if (floor > 0)
                    Box(building,0,y-.65f,0,width+.07f,.11f,depth+.07f,"C6D1CA");
                for (int face = -1; face <= 1; face += 2)
                {
                    for (int column = 0; column < columns; column++)
                    {
                        float x = (column - (columns-1)*.5f) * width / (columns+.7f);
                        float pane = width/(columns+1) * (glazed ? .88f : .68f);
                        Box(building,x,y,face*(depth*.5f+.045f),pane+.17f,1.21f,.12f,TrimSteel);
                        Window(Box(building,x,y,face*(depth*.5f+.12f),pane,1.04f,.07f,BlueGlass), (column+floor+face)%4!=0);
                    }
                    for (int sideWindow = -1; sideWindow <= 1; sideWindow++)
                    {
                        float z = sideWindow * depth * .25f;
                        Window(Box(building,face*(width*.5f+.069f),y,z,.14f,1.08f,depth*.164f,BlueGlass), (sideWindow+floor)%3!=0);
                    }
                }
            }
            return top + .23f;
        }

        void MakeEntrance(Transform parent, float width, float side, string accent)
        {
            Box(parent,0,1.21f,side*3.47f,3.15f,2.36f,.22f,Asphalt);
            Window(Box(parent,0,1.21f,side*3.61f,2.62f,2.13f,.10f,BlueGlass), true);
            Box(parent,0,1.23f,side*3.67f,.085f,2.15f,.075f,Concrete);
            Box(parent,0,2.57f,side*3.99f,Mathf.Min(width*.55f,4.5f),.27f,1.62f,accent);
        }

        void IndustrialHall(Transform parent, float width, string accent, float face)
        {
            Box(parent,0,2.30f,0,width,4.6f,6.6f,"CFD9D8");
            for (int z = -1; z <= 1; z += 2)
            {
                var roof = Box(parent,0,4.86f,z*1.71f,width+.46f,.21f,3.8f,accent);
                roof.localRotation = Quaternion.Euler(z*9,0,0);
            }
            Box(parent,0,5.19f,.51f,width*.57f,.65f,1.15f,"7D9AA7");
            for (int col = -1; col <= 1; col++)
            {
                Box(parent,col*2.85f,1.54f,face*3.38f,1.96f,2.76f,.21f,Asphalt);
                for (int row = 0; row < 6; row++)
                    Box(parent,col*2.85f,.68f+row*.35f,face*3.5f,1.76f,.11f,.035f,"627F8D");
                Window(Box(parent,col*2.85f,3.72f,-face*3.39f,1.75f,.64f,.15f,BlueGlass),false);
            }
            Box(parent,0,3.12f,face*3.96f,width+.14f,.16f,1.28f,accent);
        }

        void Window(Transform glass, bool warm) => (warm ? warmWindows : coolWindows).Add(glass.GetComponent<Renderer>());
        void Electric(Transform lamp) => electricLights.Add(lamp.GetComponent<Renderer>());

        void BuildSkyline(Transform city)
        {
            var skyline = Node("북쪽 상업 지구", city);
            int[] floors = { 6, 9, 5, 8, 7 };
            for (int block = 0; block < floors.Length; block++)
            {
                float x = -39 + block*24f;
                Transform parcel = Node("상업 블록 " + (block+1), skyline, V(x,50));
                Box(parcel,0,.025f,0,20,.12f,15.5f,Pavement);
                float offset = block%2==0 ? -2.9f : 2.7f;
                var tower = Node("업무용 빌딩", parcel, new Vector3(offset, .1f, 1.2f));
                float height = Block(tower, 8.3f, 6.8f, floors[block], block%2==0 ? "C9DCE1" : "D8DFD6",
                    block%2==0 ? "5F8BA4" : "759EAB", block!=2);
                Box(tower,1.8f,height+.45f,-1.22f,2.7f,.88f,2.28f,"7D99A8");
                Box(tower,1.8f,height+.92f,-1.22f,2.95f,.12f,2.43f,"BBCACB");
                if (block%2 == 1)
                {
                    Box(tower,-1.9f,height+.88f,.8f,.17f,1.8f,.17f,"507A8D");
                    Electric(Box(tower,-1.9f,height+1.77f,.8f,.30f,.25f,.3f,"A6D9DF"));
                }
                var shop = Node("거리 상가", parcel, V(-offset*1.96f,-1.5f));
                float shopHeight = Block(shop, 5.6f, 5.5f, 2 + block%2, "DFCDBB", "B17B63", false);
                Box(shop,0,shopHeight+.08f,0,3,.17f,2.5f,"7F9E8D");
                Tree(parcel,-7.7f,-5.6f,1.2f,block+3);
                Tree(parcel,7.8f,4.5f,1.06f,block+5);
            }
        }

        Rect BuildDevelopment(Transform city, GameObject[] stages, Vector3[] landmarks)
        {
            var streets = Node("01 가로 정비", city);
            stages[0] = streets.gameObject;
            landmarks[1] = V(-12,-9);
            for (int i = 0; i < 12; i++)
                UrbanLamp(streets,V(-46+i*10.35f,-8.75f),i%2==0);
            for (int f = -1; f <= 1; f += 2)
                for (int x = -20; x <= 63; x += 13)
                    Box(streets, x,.13f,f*16.7f,6.8f,.14f,.67f,Garden);

            var transit = Node("02 대중교통", city);
            stages[1] = transit.gameObject;
            landmarks[2] = V(-41,35);
            var stop = Node("중앙 버스 쉼터", transit, V(-41,35));
            Box(stop,0,.11f,0,9.35f,.18f,2.42f,"D8DDCB");
            Box(stop,0,2.40f,0,9.28f,.28f,2.2f,"528C96");
            Box(stop,0,2.57f,0,8.85f,.10f,1.94f,"B9DDD9");
            for (int post = -1; post <= 1; post++)
            {
                Box(stop,post*3.85f,1.20f,.78f,.15f,2.41f,.14f,"517983");
                Window(Box(stop,post*2.57f,1.3f,1.02f,2.43f,1.7f,.095f,BlueGlass),false);
            }
            Electric(Box(stop,-4.2f,1.9f,-1.22f,.48f,.50f,.22f,"A5D9DC"));
            AddTraffic(transit,true,"43A19D",2,4.9f,51,-1);
            AddTraffic(transit,true,"DAA565",2,5.5f,155,1);

            var waterfront = Node("03 수변 공원", city);
            stages[2] = waterfront.gameObject;
            landmarks[3] = V(35,57);
            for (int i = 0; i < 13; i++)
            {
                float x = -43 + i*9.12f;
                Box(waterfront,x,.10f,56.55f,8.7f,.24f,2.1f,"A7BE96");
                Tree(waterfront,x,56.4f,1.16f+(i%3)*.14f,i+5);
                if (i%3 == 0)
                    Box(waterfront,x-2.6f,.63f,58.8f,2.84f,.24f,.66f,"B18D66");
            }
            var fountain = Node("정원 분수", waterfront, V(36,-3.7f));
            Primitive(PrimitiveType.Cylinder,"Stone fountain",fountain,new Vector3(0,.39f,0),new Vector3(3.7f,.25f,3.7f),"BBC9C2");
            Primitive(PrimitiveType.Cylinder,"Fountain basin",fountain,new Vector3(0,.65f,0),new Vector3(3.27f,.05f,3.27f),"5B9CB4");
            Primitive(PrimitiveType.Cylinder,"Central jet",fountain,new Vector3(0,1.04f,0),new Vector3(.13f,.33f,.13f),"8BCAD1");
            for (int i = 0; i < 5; i++) UrbanLamp(waterfront,V(-37 + i*24,60.2f),i%2==0);

            var smart = Node("04 스마트시티", city);
            stages[3] = smart.gameObject;
            landmarks[4] = V(61, 1);
            var plaza = Node("디지털 도시 센터", smart, V(60,1));
            Box(plaza,0,.08f,0,16.7f,.18f,14,"DDDCD0");
            var raised = Node("스마트 타워", plaza, V(3.5f,1.9f));
            float height = Block(raised,6.9f,5.21f,9,"C5D9DE","477D9E",true);
            Box(raised,-1.88f,height+1.05f,0,.18f,2.1f,.18f,"527689");
            Electric(Box(raised,-1.88f,height+2.09f,0,.28f,.26f,.28f,"AADDDF"));
            Box(plaza,-3.5f,1.53f,0,.21f,2.84f,.21f,"576E81");
            Box(plaza,-3.5f,3.05f,0,5.52f,.31f,2.1f,"4D728D").localRotation = Quaternion.Euler(-11,0,0);
            for (int i = 0; i < 3; i++)
            {
                Electric(Box(plaza,-5.5f+i*1.72f,.48f,-4.62f,.4f,.83f,.38f,"A2D2CF"));
                Box(plaza,-5.5f+i*1.72f,.13f,-4.62f,1.16f,.18f,1.16f,TrimSteel);
            }
            for (int i = 0; i < stages.Length; i++) stages[i].SetActive(false);
            return new Rect(raised.position.x - 3.45f, raised.position.z - 2.605f, 6.9f, 5.21f);
        }

        void UrbanLamp(Transform owner, Vector3 position, bool illuminate)
        {
            Transform pole = Node("가로등", owner, position);
            Box(pole,0,.18f,0,.45f,.36f,.45f,"718992");
            Box(pole,0,1.78f,0,.12f,3.31f,.12f,"405B70");
            Box(pole,0,3.44f,.35f,.15f,.14f,.69f,"405B70");
            Electric(Box(pole,0,3.4f,.68f,.45f,.115f,.29f,Amber));
            if (!illuminate) return;
            var pool = Node("보도 조명", pole, new Vector3(0,3.19f,.71f)).gameObject.AddComponent<Light>();
            pool.type = LightType.Point;
            pool.color = ColorOf("FFD69C");
            pool.range = 6.7f;
            pool.intensity = 0;
            avenueLights.Add(pool);
        }

        void AddTraffic(Transform parent, bool bus, string paint, int level, float speed, float along, int direction)
        {
            var car = Node(bus ? "시내 버스" : "승용차", parent);
            float length = bus ? 4.75f : 2.68f;
            Box(car,0,.73f,0,1.50f,bus ? .96f : .58f,length,paint);
            Box(car,0,bus ? 1.42f : 1.15f,bus ? 0 : -.03f,1.32f,.5f,bus ? 4.57f : 1.43f,"E3E7DB");
            float glassLength = bus ? 4.06f : 1.21f;
            float glassHeight = bus ? 1.38f : 1.16f;
            foreach (int side in new[] { -1, 1 })
            {
                Box(car,side*.675f,glassHeight,0,.041f,.32f,glassLength,"476984");
                for (int tire = -1; tire <= 1; tire += 2)
                {
                    var wheel = Primitive(PrimitiveType.Cylinder,"Wheel",car,new Vector3(side*.74f,.34f,tire*length*.32f),new Vector3(.54f,.12f,.54f),"324457");
                    wheel.localRotation = Quaternion.Euler(0,0,90);
                    Electric(Box(car,side*.48f,.72f,tire*length*.501f,.24f,.14f,.03f,tire>0?"F9E1AC":"D78D76"));
                }
            }
            Box(car,0,glassHeight,bus ? 2.30f : .73f,1.11f,.35f,.04f,"476984");
            float lane = 1.62f * direction;
            Vector3[] path = { V(-48+lane,13-lane), V(72+lane,13-lane), V(72+lane,40+lane), V(-48+lane,40+lane) };
            if (direction < 0) System.Array.Reverse(path);
            car.position = path[0];
            car.gameObject.SetActive(level == 0);
            vehicles.Add(new CityPresentation.Traffic { Vehicle=car, Route=path, Speed=speed, Offset=along, Level=level });
        }
    }
}
#endif
