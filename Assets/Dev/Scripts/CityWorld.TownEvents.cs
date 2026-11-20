using System.Collections.Generic;
using UnityEngine;

namespace NaraTycoon
{
    public sealed partial class CityWorld
    {
        sealed class EventCitizen
        {
            internal Transform Root, LeftArm, RightArm;
            internal Quaternion LeftRest, RightRest;
            internal GameObject Food;
        }

        readonly TownEventEffects[] eventScenes = new TownEventEffects[4];
        EventCitizen[] partyGuests, marketGuests;
        TownEvent shownTownEvent;

        internal void UpdateTownEventVisuals(TownEvent townEvent, float clock)
        {
            if (townEvent != shownTownEvent)
            {
                var previous = eventScenes[(int)shownTownEvent];
                if (previous != null) previous.gameObject.SetActive(false);
                shownTownEvent = townEvent;
                if (townEvent != TownEvent.None)
                {
                    var scene = eventScenes[(int)townEvent];
                    if (scene == null) scene = LoadTownEvent(townEvent);
                    if (scene != null) scene.gameObject.SetActive(true);
                }
            }

            var effects = eventScenes[(int)townEvent];
            if (effects == null) return;
            effects.Present(clock);
            if (townEvent == TownEvent.Fireworks) AnimateParty(clock, effects.PartyPositions);
            else if (townEvent == TownEvent.NightMarket) AnimateMarket(clock, effects.MarketStalls);
        }

        TownEventEffects LoadTownEvent(TownEvent townEvent)
        {
            string path = townEvent switch
            {
                TownEvent.Fireworks => "TownEvents/SolbitFireworks",
                TownEvent.NightMarket => "TownEvents/SolbitNightMarket",
                _ => "TownEvents/GoldenGearFestival"
            };
            var prefab = Resources.Load<GameObject>(path);
            if (prefab == null || !prefab.TryGetComponent<TownEventEffects>(out _))
            {
                Debug.LogError($"Town event prefab missing its effects: {path}", this);
                return null;
            }
            var instance = Instantiate(prefab, root, false);
            instance.name = Procurement.EventName(townEvent);
            instance.transform.position += townEvent switch
            {
                TownEvent.Fireworks => Center(Site.Plaza),
                TownEvent.NightMarket => Center(Site.Market),
                _ => Vector3.zero
            };
            var effects = instance.GetComponent<TownEventEffects>();
            eventScenes[(int)townEvent] = effects;
            if (townEvent == TownEvent.Fireworks && effects.PartyPositions != null)
                partyGuests = MakeEventCitizens(instance.transform, effects.PartyPositions.childCount, null, 0);
            else if (townEvent == TownEvent.NightMarket && effects.MarketStalls.Length > 0)
                marketGuests = MakeEventCitizens(instance.transform, effects.MarketStalls.Length * 3,
                    effects.Takeout, effects.MarketStalls.Length);
            return effects;
        }

        EventCitizen[] MakeEventCitizens(Transform scene, int count, GameObject food, int vendors)
        {
            var models = new List<Animation>();
            foreach (var citizen in citizens)
                if (citizen.Run != null) models.Add(citizen.Run);
            if (models.Count == 0) return System.Array.Empty<EventCitizen>();
            var crowd = new EventCitizen[count];
            var tint = new MaterialPropertyBlock();
            for (int i = 0; i < count; i++)
            {
                var source = models[(i * 5) % models.Count];
                var guest = new EventCitizen { Root = EventNode(scene, "Event citizen " + (i + 1), Vector3.zero) };
                var clone = Instantiate(source.gameObject, guest.Root, false);
                clone.GetComponent<Animation>().enabled = false;
                var original = source.GetComponentsInChildren<Renderer>(true);
                var copies = clone.GetComponentsInChildren<Renderer>(true);
                for (int renderer = 0; renderer < copies.Length; renderer++)
                    for (int slot = 0; slot < copies[renderer].sharedMaterials.Length; slot++)
                    {
                        original[renderer].GetPropertyBlock(tint, slot);
                        copies[renderer].SetPropertyBlock(tint, slot);
                    }
                foreach (var joint in clone.GetComponentsInChildren<Transform>(true))
                {
                    if (joint.name == "UpperArm.L") { guest.LeftArm = joint; guest.LeftRest = joint.localRotation; }
                    if (joint.name == "UpperArm.R") { guest.RightArm = joint; guest.RightRest = joint.localRotation; }
                }
                if (food != null && i >= vendors) guest.Food = Instantiate(food, guest.Root, false);
                crowd[i] = guest;
            }
            return crowd;
        }

        void AnimateParty(float clock, Transform positions)
        {
            if (partyGuests == null || positions == null) return;
            for (int i = 0; i < partyGuests.Length; i++)
            {
                var guest = partyGuests[i];
                var mark = positions.GetChild(i);
                float beat = clock * 5.2f + i * .64f;
                float hop = Mathf.Pow(Mathf.Max(0, Mathf.Sin(beat)), 2) * .39f;
                guest.Root.SetPositionAndRotation(mark.position + Vector3.up * hop,
                    mark.rotation * Quaternion.Euler(0, 11 * Mathf.Sin(beat), 12 * Mathf.Cos(beat)));
                PoseArms(guest, 83 + 26 * Mathf.Sin(beat), 83 - 26 * Mathf.Sin(beat), -12);
            }
        }

        void AnimateMarket(float clock, Transform[] stalls)
        {
            if (marketGuests == null || stalls.Length == 0) return;
            for (int i = 0; i < marketGuests.Length; i++)
            {
                var guest = marketGuests[i];
                var stall = stalls[i % stalls.Length];
                if (stall == null)
                {
                    guest.Root.gameObject.SetActive(false);
                    continue;
                }
                if (i < stalls.Length)
                {
                    guest.Root.SetPositionAndRotation(stall.TransformPoint(new Vector3(0, .18f, -.61f)), stall.rotation);
                    PoseArms(guest, 8, 12, -20 - 34 * Mathf.Max(0, Mathf.Sin(clock * 3 + i)));
                    continue;
                }
                float phase = Mathf.Repeat(clock * .19f + i * .31f, 1);
                float walk = Mathf.SmoothStep(0, 1, Mathf.Clamp01(phase * 4 - Mathf.Max(0, phase - .7f) * 12));
                float reach = Mathf.Max(0, 1 - Mathf.Abs(phase - .52f) * 8);
                float lane = i < stalls.Length * 2 ? -.47f : .58f;
                guest.Root.position = stall.TransformPoint(new Vector3(lane, .16f + .055f * Mathf.Abs(Mathf.Sin(clock * 12 + i)),
                    Mathf.Lerp(3.25f, .98f, walk)));
                guest.Root.rotation = stall.rotation * Quaternion.Euler(0, phase < .72f ? 180 : 12, 0);
                if (guest.Food != null) guest.Food.SetActive(phase >= .55f);
                PoseArms(guest, 5, 8, -50 * reach - (guest.Food != null && guest.Food.activeSelf ? 23 : 0));
            }
        }

        static void PoseArms(EventCitizen guest, float left, float right, float reach)
        {
            if (guest.LeftArm != null)
                guest.LeftArm.localRotation = Quaternion.AngleAxis(reach, guest.LeftArm.parent.InverseTransformDirection(guest.Root.right)) *
                    Quaternion.AngleAxis(left, guest.LeftArm.parent.InverseTransformDirection(guest.Root.forward)) * guest.LeftRest;
            if (guest.RightArm != null)
                guest.RightArm.localRotation = Quaternion.AngleAxis(reach, guest.RightArm.parent.InverseTransformDirection(guest.Root.right)) *
                    Quaternion.AngleAxis(-right, guest.RightArm.parent.InverseTransformDirection(guest.Root.forward)) * guest.RightRest;
        }

        static Transform EventNode(Transform parent, string name, Vector3 localPosition)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            child.localPosition = localPosition;
            return child;
        }

    }
}
