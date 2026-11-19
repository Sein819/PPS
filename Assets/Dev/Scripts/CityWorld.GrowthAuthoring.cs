#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace NaraTycoon
{
    public sealed partial class CityWorld
    {
        public void AuthorGrowthMilestones()
        {
            var city = presentation.transform;
            LoadPalette();
            if (city.Find("06 솔빛 랜드마크") != null)
            {
                AuthorLakesideGarden();
                return;
            }
            var lights = new List<Renderer>();
            var garden = presentation.GrowthDistrict(0);
            var transit = presentation.GrowthDistrict(1);
            var park = presentation.GrowthDistrict(2);
            var smart = presentation.GrowthDistrict(3);
            var skyline = Node("05 도심 스카이라인", city);
            var landmark = Node("06 솔빛 랜드마크", city);
            GardenDistrict(garden, lights);
            TransitDistrict(transit, lights);
            RiverDistrict(park, lights);
            EnergyDistrict(smart, lights);
            SkylineDistrict(skyline, lights);
            FestivalDistrict(landmark, lights);

            skyline.gameObject.SetActive(false);
            landmark.gameObject.SetActive(false);
            var centers = new[]
            {
                Vector3.zero, new Vector3(-12, 2, 1), new Vector3(-63, 2, 12),
                new Vector3(9, 2, -47), new Vector3(76, 6, 9), new Vector3(15, 7, 74),
                new Vector3(86, 11, -14)
            };
            var glow = GlowGroup("Milestone lighting", "739BAA", "7CF5DF", 1.8f, lights);
            presentation.ConfigureGrowth(skyline.gameObject, landmark.gameObject, centers, glow);
            AuthorLakesideGarden();
        }

        void AuthorLakesideGarden()
        {
            var stage = presentation.GrowthDistrict(0);
            if (stage.Find("솔빛 시민정원") != null) return;
            foreach (Transform terrain in root.Find("Terrain and waterfront"))
                if (Mathf.Approximately(terrain.localScale.x, 380))
                    terrain.localScale = new Vector3(630, terrain.localScale.y, 630);

            var park = DistrictPlatform(stage, "솔빛 시민정원", 59, -47.5f, 32, 14, "82AD84");
            Box(park, 0, .11f, 2.1f, 31.8f, .2f, 2.6f, "DCCCB2");
            Box(park, 0, .10f, -.8f, 5.8f, .21f, 9.0f, "D6DCC7");
            var lights = new List<Renderer>();
            for (int side = -1; side <= 1; side += 2)
            {
                Box(park, side * 10.45f, .22f, -2.0f, 8.7f, .31f, 6.3f, "456D64");
                for (int i = 0; i < 4; i++)
                {
                    Box(park, side * 10.45f, .46f, -4.1f + i * 1.38f, 7.6f, .26f, .83f,
                        i % 2 == 0 ? "CE7E6C" : "E7B572");
                    Tree(park, side * (5.0f + i * 2.9f), 4.62f, 1.48f, i + 19);
                }
                Box(park, side * 2.37f, 1.76f, -.55f, .21f, 3.42f, .24f, "7E9681");
                Box(park, side * 2.37f, 1.76f, -4.3f, .21f, 3.42f, .24f, "7E9681");
                GlowBox(park, side * 2.34f, 3.23f, -.55f, .34f, .19f, .30f, lights);
            }
            for (int rib = -3; rib <= 3; rib++)
                Box(park, rib * .76f, 3.59f, -2.35f, .40f, .38f, 4.8f, rib % 2 == 0 ? "5B9689" : "9EC4A5");
            presentation.ConfigureGarden(new Vector3(59, 2, -47), GlowGroup("Garden pavilion lighting", "739BAA", "FFCB8D", 1.25f, lights));
        }

        void GardenDistrict(Transform stage, List<Renderer> lights)
        {
            var garden = Node("시민 정원과 녹지 회랑", stage, V(-12, 2.4f));
            for (int side = -1; side <= 1; side += 2)
            {
                Box(garden, side * 5.64f, .14f, 0, 4.8f, .23f, 9.1f, "659B76");
                for (int i = -1; i <= 1; i++)
                {
                    Tree(garden, side * 5.65f, i * 3.15f, 1.5f, i + side + 9);
                    Box(garden, side * 3.7f, .48f, i * 2.89f, .58f, .61f, 1.0f, i % 2 == 0 ? "E98272" : "E7B46C");
                }
                for (int i = -1; i <= 1; i++)
                    Box(garden, side * 2.95f, 1.43f, i * 2.66f + 1.5f, .18f, 2.64f, .20f, "607D73");
                Box(garden, side * 3.12f, 2.79f, 1.5f, 1.28f, .28f, 7.5f, "78B595");
                GlowBox(garden, side * 3.17f, 2.64f, -1.92f, .43f, .14f, .2f, lights);
            }
            var plaza = Node("광장 관목과 꽃밭", stage, V(-36, -32.1f));
            for (int i = -2; i <= 2; i++)
            {
                Box(plaza, i * 2.6f, .17f, 0, 1.9f, .30f, 2.3f, "8BA885");
                Primitive(PrimitiveType.Sphere, "Flower planting", plaza, new Vector3(i * 2.6f,.64f,0),
                    new Vector3(1.22f, .75f, 1.57f), i % 2 == 0 ? "E9A666" : "C7756A");
            }
        }

        void TransitDistrict(Transform stage, List<Renderer> lights)
        {
            var station = DistrictPlatform(stage, "솔빛 서부 환승역", -67, 11, 24, 45, "A3B9A8");
            Box(station, 0, .13f, 2, 24, .21f, 7.0f, "465E70");
            for (int x = -10; x <= 11; x += 3)
                Box(station, x, .26f, 2, 1.4f, .04f, .15f, "F2D388");
            Box(station, 0, .32f, 12, 18, .33f, 9.6f, "D5DDCD");
            for (int i = -2; i <= 2; i++)
            {
                Box(station, i * 3.4f, 2.66f, 11, .3f, 4.6f, .32f, "788F8C");
                Box(station, i * 3.4f, 5.03f, 13, 3.38f, .38f, 8.4f, i % 2 == 0 ? "4C9B9F" : "7EA9B9").localRotation = Quaternion.Euler(-8,0,0);
                GlowBox(station, i * 3.4f, 4.62f, 9.4f, .82f, .16f, .3f, lights);
            }
            for (int side = -1; side <= 1; side += 2)
                Box(station, side * 1.7f, .22f, -10.0f, .2f, .10f, 22.6f, "909A98");
            for (int i = -5; i <= 2; i++)
                Box(station, 0, .12f, i * 1.63f - 9.6f, 4.8f, .16f, .29f, "74828B");
            for (int i = 0; i < 3; i++)
            {
                float z = -14 + 5.1f * i;
                Box(station, 0, 1.19f, z, 3.82f, 1.98f, 4.81f, "E9D8B5");
                Box(station, 0, 2.06f, z, 3.87f, .29f, 4.85f, "E3A857");
                GlowBox(station, -1.95f, 1.48f, z, .065f, .73f, 3.83f, lights);
            }
        }

        void RiverDistrict(Transform stage, List<Renderer> lights)
        {
            var park = DistrictPlatform(stage, "남쪽 수변 산책로", 8, -47.5f, 65, 15, "81B58D");
            Box(park, 0, .19f, 4.4f, 65.2f, .24f, 3.3f, "DEC9AE");
            Box(park, 0, .02f, -2.1f, 32, .14f, 7.9f, "79B6C4");
            for (int side = -1; side <= 1; side += 2)
            {
                Box(park, 0, .18f, -2.1f + side * 4.05f, 34, .33f, .62f, "D7E4DC");
                for (int i = 0; i < 4; i++)
                    Tree(park, side * (19 + 3.1f * i), -4.1f + i % 2 * 2.6f, 1.3f, i + 4);
                Box(park, side * 24, .23f, -.8f, 6.5f, .32f, 6.5f, "CC9D7C");
                Box(park, side * 24, 3.1f, -.8f, 6.6f, .32f, 5.68f, "BA7E69");
                for (int i = -1; i <= 1; i++)
                {
                    Box(park, side * 24 + i * 2.53f, 1.61f, .2f, .18f, 2.81f, .18f, "708A79");
                    GlowBox(park, side * 24 + i * 2.15f, 2.89f, -3.5f, .3f, .19f, .22f, lights);
                }
            }
            for (int i = -2; i <= 2; i++)
            {
                Primitive(PrimitiveType.Cylinder, "Fountain basin", park, new Vector3(i * 5.8f,.17f,-2.1f),
                    new Vector3(2.1f,.18f,2.1f), "67A9BB");
                Primitive(PrimitiveType.Cylinder, "Water jet", park, new Vector3(i * 5.8f,.59f,-2.1f),
                    new Vector3(.24f,.64f,.24f), "C5E5E7");
            }
        }

        void EnergyDistrict(Transform stage, List<Renderer> lights)
        {
            var district = DistrictPlatform(stage, "태양광 에너지 지구", 87, 24, 21, 24, "9CB69A");
            for (int row = -1; row <= 1; row++)
                for (int col = -1; col <= 1; col++)
                {
                    float x = col * 6.1f, z = row * 5.7f;
                    Box(district, x, 1.32f, z, .21f, 2.55f, .22f, "577082");
                    var solar = Box(district, x, 2.53f, z, 5.15f, .26f, 3.43f, "4B7195");
                    solar.localRotation = Quaternion.Euler(18, 0, 0);
                    for (int line = -2; line <= 2; line++)
                        Box(district, x + line * .85f, 2.67f, z, .055f, .06f, 3.28f, "A2C4D7").localRotation = solar.localRotation;
                    GlowBox(district, x, .42f, z - 1.74f, 1.36f, .43f, .47f, lights);
                }
            Box(district, 0, .24f, 10.7f, 20.9f, .42f, 1.8f, "BED3CA");
        }

        void SkylineDistrict(Transform stage, List<Renderer> lights)
        {
            var downtown = DistrictPlatform(stage, "북쪽 업무 지구", 10, 74, 112, 22, "A8BDB5");
            Box(downtown, 0, .17f, -8.5f, 110, .23f, 3.9f, "D4DCDA");
            int[] floors = { 11, 14, 12, 17, 13 };
            for (int i = 0; i < floors.Length; i++)
            {
                float x = -45 + i * 22.5f;
                float z = i % 2 == 0 ? .4f : 1.9f;
                var highRise = Node("테라스 고층 빌딩", downtown, V(x,z));
                float width = 8.8f + i % 3 * 1.6f;
                float height = Block(highRise, width, 8.8f, floors[i], i % 2 == 0 ? "9FBFCB" : "BCCBCD", "476D91", true);
                Box(highRise, 0, height + .4f, 0, width - 2.3f, .76f, 6.6f, "79A4AE");
                Box(highRise, 0, height + 1.95f, 0, width - 3.2f, 2.48f, 5.4f, "537F9E");
                Box(highRise, 0, height + 3.24f, 0, width - 3.0f, .22f, 5.6f, "C9DFE1");
                for (int storey = 1; storey <= floors[i]; storey += 2)
                {
                    float y = .55f + (storey - 1) * 1.95f;
                    GlowBox(highRise, 0, y, -4.5f, width + .14f, .24f, .10f, lights);
                    GlowBox(highRise, -width / 2 - .04f, y, 0, .11f, .24f, 8.9f, lights);
                }
                for (int axis = -1; axis <= 1; axis += 2)
                    Tree(downtown, x + axis * 7.7f, -6.6f, 1.35f, i + 13);
            }
        }

        void FestivalDistrict(Transform stage, List<Renderer> lights)
        {
            var plaza = DistrictPlatform(stage, "솔빛 축제 광장", 87, -13, 23, 34, "D4D6BD");
            Primitive(PrimitiveType.Cylinder, "Circular observatory base", plaza, new Vector3(0,.47f,0),
                new Vector3(16.4f,.45f,16.4f), "A7BDB9");
            Primitive(PrimitiveType.Cylinder, "Reflecting pool", plaza, new Vector3(0,.98f,0),
                new Vector3(14.25f,.065f,14.25f), "8BBBC7");
            Primitive(PrimitiveType.Cylinder, "Observatory tower", plaza, new Vector3(0,14,0),
                new Vector3(3.5f,13.6f,3.5f), "B8D3D6");
            Primitive(PrimitiveType.Cylinder, "Observation deck", plaza, new Vector3(0,27.7f,0),
                new Vector3(10.7f,1.15f,10.7f), "759CB4");
            Primitive(PrimitiveType.Cylinder, "Observatory cap", plaza, new Vector3(0,29.2f,0),
                new Vector3(11.2f,.53f,11.2f), "DAE1CD");
            Box(plaza, 0, 33.2f, 0, .32f, 7.2f, .32f, "6897A9");
            GlowBox(plaza, 0, 37, 0, .8f, .74f, .8f, lights);
            for (int i = 0; i < 12; i++)
            {
                float angle = i * 2 * Mathf.PI / 12;
                float x = Mathf.Cos(angle), z = Mathf.Sin(angle);
                GlowBox(plaza, x * 5.0f, 28.35f, z * 5.0f, .84f, 1.04f, .80f, lights);
                Box(plaza, x * 8.05f, .98f, z * 8.05f, .45f, 1.77f, .42f, "B8C6BA");
                GlowBox(plaza, x * 8.05f, 1.96f, z * 8.05f, .47f, .34f, .46f, lights);
            }
            for (int side = -1; side <= 1; side += 2)
                for (int i = 0; i < 4; i++)
                {
                    Tree(plaza, side * 9.3f, i * 8.1f - 12.3f, 1.25f, i + 3);
                    Box(plaza, side * 6.7f, .38f, i * 8.1f - 12.3f, 1.18f, .4f, 1.19f, i % 2 == 0 ? "DCAC73" : "8BBBC7");
                }
        }

        Transform DistrictPlatform(Transform stage, string name, float x, float z, float width, float depth, string topColor)
        {
            var district = Node(name, stage, V(x,z));
            Box(district, 0, -1.89f, 0, width, 3.75f, depth, "78999B");
            Box(district, 0, -.08f, 0, width - .24f, .17f, depth - .24f, topColor);
            return district;
        }

        void GlowBox(Transform parent, float x, float y, float z, float width, float height, float depth, List<Renderer> lights)
        {
            lights.Add(Box(parent,x,y,z,width,height,depth,"739BAA").GetComponent<Renderer>());
        }
    }
}
#endif
