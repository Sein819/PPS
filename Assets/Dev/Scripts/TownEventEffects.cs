using System.Collections.Generic;
using UnityEngine;

namespace NaraTycoon
{
    [DisallowMultipleComponent]
    public sealed class TownEventEffects : MonoBehaviour
    {
        [Header("Fireworks")]
        [SerializeField] ParticleSystem fireworks;
        [SerializeField] Transform launchPoints;
        [SerializeField, Min(.08f)] float burstInterval = .58f;

        [Header("Night market")]
        [SerializeField] Transform lanterns;
        [SerializeField, Min(0)] float lanternDrift = 1;
        [SerializeField] float lanternTurnSpeed = 11;
        [SerializeField] Transform[] marketStalls;
        [SerializeField] GameObject takeout;

        [Header("Fireworks crowd")]
        [SerializeField] Transform partyPositions;

        [Header("Golden gear")]
        [SerializeField] Transform rotor;
        [SerializeField] float rotationSpeed = 19;
        [SerializeField, Min(0)] float brightness = 1.36f;
        [SerializeField, Min(0)] float brightnessPulse = .19f;

        Vector3[] lanternRest;
        Quaternion[] lanternRotations;
        Quaternion rotorRest;
        Renderer rotorRenderer;
        MaterialPropertyBlock rotorPaint;
        float nextBurst;
        int burstIndex;
        readonly List<ParticleSystem> bursts = new();

        internal Transform PartyPositions => partyPositions;
        internal Transform[] MarketStalls => marketStalls ?? System.Array.Empty<Transform>();
        internal GameObject Takeout => takeout;

        void Awake()
        {
            if (lanterns != null)
            {
                lanternRest = new Vector3[lanterns.childCount];
                lanternRotations = new Quaternion[lanternRest.Length];
                for (int i = 0; i < lanternRest.Length; i++)
                {
                    lanternRest[i] = lanterns.GetChild(i).localPosition;
                    lanternRotations[i] = lanterns.GetChild(i).localRotation;
                }
            }
            if (rotor == null) return;
            rotorRest = rotor.localRotation;
            rotorRenderer = rotor.GetComponent<Renderer>();
            rotorPaint = new MaterialPropertyBlock();
        }

        void OnEnable()
        {
            nextBurst = float.NegativeInfinity;
            burstIndex = 0;
        }

        void OnDisable()
        {
            foreach (var burst in bursts)
                if (burst != null) burst.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        internal void Present(float clock)
        {
            if (fireworks != null && launchPoints != null && launchPoints.childCount > 0 && clock >= nextBurst)
            {
                nextBurst = clock + Mathf.Max(.08f, burstInterval);
                PlayBurst(burstIndex);
                if (launchPoints.childCount > 1) PlayBurst(burstIndex + launchPoints.childCount / 2);
                burstIndex++;
            }
            if (lanternRest != null)
                for (int i = 0; i < lanternRest.Length; i++)
                {
                    float phase = clock * .62f + i * .87f;
                    var lantern = lanterns.GetChild(i);
                    lantern.localPosition = lanternRest[i] + lanternDrift *
                        new Vector3(1.1f * Mathf.Sin(phase), 1.55f * Mathf.Sin(phase * .58f), .9f * Mathf.Cos(phase));
                    lantern.localRotation = lanternRotations[i] * Quaternion.Euler(7 * Mathf.Cos(phase), clock * lanternTurnSpeed + i * 23, 8 * Mathf.Sin(phase));
                }
            if (rotor == null) return;
            rotor.localRotation = rotorRest * Quaternion.AngleAxis(clock * rotationSpeed, Vector3.up);
            if (rotorRenderer == null) return;
            rotorRenderer.GetPropertyBlock(rotorPaint);
            rotorPaint.SetFloat("_Glow", Mathf.Max(0, brightness + brightnessPulse * Mathf.Sin(clock * 3.4f)));
            rotorRenderer.SetPropertyBlock(rotorPaint);
        }

        void PlayBurst(int index)
        {
            var anchor = launchPoints.GetChild(index % launchPoints.childCount);
            ParticleSystem burst = null;
            for (int i = bursts.Count - 1; i >= 0; i--)
            {
                var candidate = bursts[i];
                if (candidate == null) bursts.RemoveAt(i);
                else if (!candidate.IsAlive(true))
                {
                    burst = candidate;
                    break;
                }
            }
            if (burst == null)
            {
                burst = Instantiate(fireworks, transform);
                bursts.Add(burst);
            }
            burst.transform.SetPositionAndRotation(anchor.position, anchor.rotation);
            burst.gameObject.SetActive(true);
            burst.Play(true);
        }
    }
}
