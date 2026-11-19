using System;
using System.Collections.Generic;
using UnityEngine;

namespace NaraTycoon
{
    // The authored asphalt strips meet at right angles. Split them at intersections so the guide
    // can enter or leave at any point along a road, including between intersections.
    internal sealed class RoadGuideNetwork
    {
        readonly struct Street
        {
            public readonly bool Horizontal;
            public readonly float Axis, Min, Max, HalfWidth;

            public Street(Transform surface, bool horizontal, Rect bounds)
            {
                Horizontal = horizontal;
                Vector3 size = surface.lossyScale;
                Vector3 center = surface.position;
                Axis = horizontal ? center.z : center.x;
                float along = horizontal ? center.x : center.z;
                float length = horizontal ? size.x : size.z;
                Min = Mathf.Max(along - length / 2, horizontal ? bounds.xMin : bounds.yMin);
                Max = Mathf.Min(along + length / 2, horizontal ? bounds.xMax : bounds.yMax);
                HalfWidth = (horizontal ? size.z : size.x) / 2;
            }

            public Vector3 Point(float along) => Horizontal ? new Vector3(along, 0, Axis) : new Vector3(Axis, 0, along);
        }

        readonly struct Edge
        {
            public readonly int A, B;
            public readonly float HalfWidth;

            public Edge(int a, int b, float halfWidth) => (A, B, HalfWidth) = (a, b, halfWidth);
        }

        internal readonly struct Position
        {
            public readonly int EdgeIndex;
            public readonly Vector3 Point;

            public Position(int edgeIndex, Vector3 point) => (EdgeIndex, Point) = (edgeIndex, point);
        }

        readonly List<Vector3> nodes = new();
        readonly List<Edge> edges = new();
        readonly float[,] costs;
        readonly int[,] next;
        readonly Vector3[] entries;
        readonly float[] entryDistances;
        readonly Func<Vector3, float, bool> roadAccess;

        public RoadGuideNetwork(Transform roads, Rect bounds, Func<Vector3, float, bool> roadAccess)
        {
            this.roadAccess = roadAccess;
            var streets = new List<Street>();
            if (roads != null)
                foreach (Transform surface in roads)
                {
                    Vector3 size = surface.lossyScale;
                    bool horizontal = size.x > size.z;
                    float width = horizontal ? size.z : size.x;
                    float length = horizontal ? size.x : size.z;
                    if (!surface.gameObject.activeInHierarchy || width < 5 || width > 7 || length < 20) continue;
                    var street = new Street(surface, horizontal, bounds);
                    float minAxis = horizontal ? bounds.yMin : bounds.xMin;
                    float maxAxis = horizontal ? bounds.yMax : bounds.xMax;
                    if (street.Min < street.Max && street.Axis >= minAxis && street.Axis <= maxAxis) streets.Add(street);
                }

            foreach (var street in streets)
            {
                var points = new List<float> { street.Min, street.Max };
                foreach (var cross in streets)
                    if (cross.Horizontal != street.Horizontal && street.Axis >= cross.Min && street.Axis <= cross.Max &&
                        cross.Axis > street.Min && cross.Axis < street.Max)
                        points.Add(cross.Axis);
                points.Sort();
                for (int i = 1; i < points.Count; i++)
                    if (points[i] - points[i - 1] > .001f)
                        edges.Add(new Edge(Node(street.Point(points[i - 1])), Node(street.Point(points[i])), street.HalfWidth));
            }

            entries = new Vector3[edges.Count];
            entryDistances = new float[edges.Count];
            costs = new float[nodes.Count, nodes.Count];
            next = new int[nodes.Count, nodes.Count];
            for (int a = 0; a < nodes.Count; a++)
                for (int b = 0; b < nodes.Count; b++)
                {
                    costs[a, b] = a == b ? 0 : float.PositiveInfinity;
                    next[a, b] = b;
                }
            foreach (Edge edge in edges)
                costs[edge.A, edge.B] = costs[edge.B, edge.A] = Vector3.Distance(nodes[edge.A], nodes[edge.B]);
            // Intersections are static during play. Cache their distances once for frequent replanning.
            for (int via = 0; via < nodes.Count; via++)
                for (int a = 0; a < nodes.Count; a++)
                    for (int b = 0; b < nodes.Count; b++)
                    {
                        float cost = costs[a, via] + costs[via, b];
                        if (cost >= costs[a, b]) continue;
                        costs[a, b] = cost;
                        next[a, b] = next[a, via];
                    }
        }

        int Node(Vector3 point)
        {
            int index = nodes.IndexOf(point);
            if (index >= 0) return index;
            nodes.Add(point);
            return nodes.Count - 1;
        }

        Vector3 Project(Edge edge, Vector3 position)
        {
            Vector3 from = nodes[edge.A];
            Vector3 line = nodes[edge.B] - from;
            return from + line * Mathf.Clamp01(Vector3.Dot(position - from, line) / line.sqrMagnitude);
        }

        internal Position Approach(Vector3 dock)
        {
            var result = new Position(-1, Vector3.zero);
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < edges.Count; i++)
            {
                Vector3 point = Project(edges[i], dock);
                float distance = Vector3.Distance(dock, point);
                if (distance >= nearest || !CanConnect(dock, point, distance)) continue;
                nearest = distance;
                result = new Position(i, point);
            }
            return result;
        }

        internal void FindPath(Vector3 truck, Position destination, List<Vector3> path)
        {
            path.Clear();
            if (destination.EdgeIndex < 0) return;

            truck.y = 0;
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < edges.Count; i++)
            {
                entries[i] = Project(edges[i], truck);
                float distance = Vector3.Distance(truck, entries[i]);
                entryDistances[i] = CanConnect(truck, entries[i], distance) ? distance : float.PositiveInfinity;
                nearest = Mathf.Min(nearest, entryDistances[i]);
            }
            if (float.IsPositiveInfinity(nearest)) return;

            Edge goal = edges[destination.EdgeIndex];
            float best = float.PositiveInfinity;
            int enterNode = -1, leaveNode = -1;
            Vector3 start = default;
            for (int i = 0; i < edges.Count; i++)
            {
                Edge edge = edges[i];
                float approach = entryDistances[i];
                // Try both roads at a junction. Away from the asphalt, guide from the closest reachable road.
                if (approach > Mathf.Max(edge.HalfWidth, nearest + .05f)) continue;
                if (i == destination.EdgeIndex)
                {
                    float direct = approach + Vector3.Distance(entries[i], destination.Point);
                    if (direct < best)
                    {
                        best = direct;
                        start = entries[i];
                        enterNode = leaveNode = -1;
                    }
                }
                for (int end = 0; end < 2; end++)
                {
                    int a = end == 0 ? edge.A : edge.B;
                    float entry = approach + Vector3.Distance(entries[i], nodes[a]);
                    for (int exit = 0; exit < 2; exit++)
                    {
                        int b = exit == 0 ? goal.A : goal.B;
                        float total = entry + costs[a, b] + Vector3.Distance(nodes[b], destination.Point);
                        if (total >= best) continue;
                        best = total;
                        start = entries[i];
                        enterNode = a;
                        leaveNode = b;
                    }
                }
            }
            if (float.IsPositiveInfinity(best)) return;

            path.Add(start);
            if (enterNode >= 0)
            {
                for (int node = enterNode; node != leaveNode; node = next[node, leaveNode])
                    Append(path, nodes[node]);
                Append(path, nodes[leaveNode]);
            }
            Append(path, destination.Point);
        }

        bool CanConnect(Vector3 point, Vector3 road, float distance)
        {
            int steps = Mathf.CeilToInt(distance);
            for (int i = 1; i <= steps; i++)
                if (!roadAccess(Vector3.Lerp(point, road, (float)i / steps), .94f)) return false;
            return true;
        }

        static void Append(List<Vector3> path, Vector3 point)
        {
            if ((path[^1] - point).sqrMagnitude > .0001f) path.Add(point);
        }
    }
}
