using System.Collections.Generic;
using UnityEngine;

namespace NaraTycoon
{
    internal sealed class TransportGuide
    {
        const float ArrowSpacing = 3.2f;
        const int RoofSparks = 14;
        const int EntranceSparks = 8;
        readonly RoadGuideNetwork network;
        readonly ParticleSystem arrows, beacon;
        readonly Mesh chevron, diamond;
        readonly ParticleSystem.Particle[] arrowParticles = new ParticleSystem.Particle[88];
        readonly ParticleSystem.Particle[] beaconParticles = new ParticleSystem.Particle[RoofSparks + EntranceSparks + 2];
        readonly List<Vector3> path = new(24);
        RoadGuideNetwork.Position approach;
        Vector3 previousPosition, landmark, entrance;
        float elapsed;
        bool needsPath;

        internal TransportGuide(RoadGuideNetwork network, Transform owner, Material material)
        {
            this.network = network;
            chevron = MakeMesh("Road chevron", new[]
            {
                new Vector3(-.67f, 0, -.49f), new Vector3(0, 0, -.23f), new Vector3(.67f, 0, -.49f),
                new Vector3(-.67f, 0, -.12f), new Vector3(0, 0, .47f), new Vector3(.67f, 0, -.12f)
            }, new[] { 0, 3, 4, 0, 4, 1, 1, 4, 5, 1, 5, 2 });
            diamond = MakeMesh("Destination diamond", new[]
            {
                new Vector3(0, -.55f, 0), new Vector3(-.42f, .04f, 0),
                new Vector3(0, .61f, 0), new Vector3(.42f, .04f, 0)
            }, new[] { 0, 1, 2, 0, 2, 3 });
            arrows = MakeSystem("Road guide particles", owner, material, chevron, arrowParticles.Length);
            beacon = MakeSystem("Destination beacon particles", owner, material, diamond, beaconParticles.Length);
        }

        internal void Begin(Vector3 dock, Vector3 label)
        {
            approach = network.Approach(dock);
            landmark = label;
            entrance = dock + Vector3.up * 2.25f;
            needsPath = true;
            elapsed = 0;
            arrows.Play(false);
            beacon.Play(false);
        }

        internal void Update(Vector3 position, bool atDestination, float deltaTime)
        {
            elapsed += deltaTime;
            if (atDestination)
            {
                needsPath = true;
                path.Clear();
            }
            else if (needsPath || (position - previousPosition).sqrMagnitude >= .35f * .35f)
            {
                network.FindPath(position, approach, path);
                previousPosition = position;
                needsPath = false;
            }

            DrawArrows();
            DrawBeacon();
        }

        internal void Hide()
        {
            path.Clear();
            if (arrows != null) arrows.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (beacon != null) beacon.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        internal void Dispose()
        {
            if (arrows != null) Object.Destroy(arrows.gameObject);
            if (beacon != null) Object.Destroy(beacon.gameObject);
            Object.Destroy(chevron);
            Object.Destroy(diamond);
        }

        void DrawArrows()
        {
            int count = 0;
            float offset = Mathf.Repeat(elapsed * 5.2f, ArrowSpacing);
            for (int i = 1; i < path.Count && count < arrowParticles.Length; i++)
            {
                Vector3 leg = path[i] - path[i - 1];
                float length = leg.magnitude;
                Vector3 direction = leg / length;
                Vector3 rotation = Quaternion.LookRotation(direction).eulerAngles;
                for (; offset < length && count < arrowParticles.Length; offset += ArrowSpacing)
                {
                    Vector3 point = path[i - 1] + direction * offset + Vector3.up * .23f;
                    float shimmer = .5f + .5f * Mathf.Sin(point.x * .24f + point.z * .27f - elapsed * 4);
                    arrowParticles[count++] = Particle(point, 1.36f + shimmer * .17f,
                        new Color32(76, 255, 223, (byte)(205 + shimmer * 50)), rotation);
                }
                offset -= length;
            }
            arrows.SetParticles(arrowParticles, count);
        }

        void DrawBeacon()
        {
            DrawMarker(0, landmark, 2.5f, RoofSparks);
            DrawMarker(RoofSparks + 1, entrance, 1.65f, EntranceSparks);
            beacon.SetParticles(beaconParticles, beaconParticles.Length);
        }

        void DrawMarker(int first, Vector3 anchor, float diameter, int sparkCount)
        {
            float scale = diameter / 2.5f;
            Vector3 center = anchor + Vector3.up * (1.02f + .42f * Mathf.Sin(elapsed * 3.1f)) * scale;
            beaconParticles[first] = Particle(center, diameter, new Color32(255, 206, 100, 255), Vector3.zero);
            for (int i = 0; i < sparkCount; i++)
            {
                float cycle = Mathf.Repeat(elapsed * .55f + i / (float)sparkCount, 1);
                float angle = i * 2.4f + elapsed * 2;
                float spread = 1.2f + cycle;
                Vector3 point = anchor + new Vector3(Mathf.Cos(angle) * spread, cycle * 2.5f, Mathf.Sin(angle) * spread) * scale;
                float pulse = Mathf.Sin(Mathf.PI * cycle);
                beaconParticles[first + i + 1] = Particle(point, (.3f + pulse * .36f) * scale,
                    new Color32(119, 255, 220, (byte)(210 * pulse)), Vector3.zero);
            }
        }

        static ParticleSystem.Particle Particle(Vector3 point, float size, Color32 color, Vector3 rotation) =>
            new()
            {
                position = point,
                startSize = size,
                startColor = color,
                rotation3D = rotation,
                startLifetime = 1,
                remainingLifetime = 1
            };

        static Mesh MakeMesh(string name, Vector3[] vertices, int[] indices)
        {
            var mesh = new Mesh { name = name, vertices = vertices, triangles = indices };
            mesh.RecalculateBounds();
            return mesh;
        }

        static ParticleSystem MakeSystem(string name, Transform owner, Material material, Mesh mesh, int capacity)
        {
            var objectWithParticles = new GameObject(name);
            objectWithParticles.transform.SetParent(owner, false);
            var system = objectWithParticles.AddComponent<ParticleSystem>();
            system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.playOnAwake = false;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = capacity;
            main.startSpeed = 0;
            main.gravityModifier = 0;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            var emission = system.emission;
            emission.enabled = false;
            var shape = system.shape;
            shape.enabled = false;
            var renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = mesh;
            renderer.alignment = ParticleSystemRenderSpace.World;
            renderer.enableGPUInstancing = false;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return system;
        }
    }
}
