using System;
using UnityEngine;

namespace NaraTycoon
{
    internal sealed class GameAudio
    {
        const int Rate = 24000;
        readonly AudioSource bed, effects;
        readonly AudioClip click, confirm, error, reward;
        internal float Music { get; private set; }
        internal float Sfx { get; private set; }
        bool quiet;

        internal GameAudio(Transform parent, AudioClip soundtrack)
        {
            Music = PlayerPrefs.GetFloat("Nara.Music", .55f);
            Sfx = PlayerPrefs.GetFloat("Nara.Sfx", .75f);
            var node = new GameObject("Solbit soundtrack");
            node.transform.SetParent(parent, false);
            bed = node.AddComponent<AudioSource>();
            effects = node.AddComponent<AudioSource>();
            bed.playOnAwake = effects.playOnAwake = false;
            click = Make("Paper tap", .058f, (t, _) => Tone(507 - t * 1700, t) * Mathf.Exp(-t * 57));
            confirm = Make("Office bell", .24f, (t, _) => Tone(t < .09f ? 587 : 784, t) * Mathf.Exp(-t * 10));
            error = Make("Review request", .18f, (t, _) => Tone(410 - t * 690, t) * Mathf.Exp(-t * 12));
            reward = Make("Solbit fanfare", .85f, (t, _) =>
                Note(t, .00f, 523) + Note(t, .11f, 659) + Note(t, .20f, 784) + Note(t, .35f, 1047));
            bed.clip = soundtrack;
            bed.loop = true;
            UpdateVolume();
            if (soundtrack != null) bed.Play();
        }

        internal void Click() => effects.PlayOneShot(click, .18f);
        internal void Confirm() => effects.PlayOneShot(confirm, .20f);
        internal void Error() => effects.PlayOneShot(error, .17f);
        internal void Reward() => effects.PlayOneShot(reward, .27f);
        internal void Duck(bool duck) { quiet = duck; UpdateVolume(); }
        internal void SetMusic(float value) { Music = value; PlayerPrefs.SetFloat("Nara.Music", value); UpdateVolume(); }
        internal void SetEffects(float value) { Sfx = value; PlayerPrefs.SetFloat("Nara.Sfx", value); UpdateVolume(); }

        void UpdateVolume()
        {
            bed.volume = Music * (quiet ? .40f : .81f);
            effects.volume = Sfx;
        }

        static float Tone(float hz, float time) => Mathf.Sin(hz * time * Mathf.PI * 2);
        static float Note(float time, float start, float hz)
        {
            float local = time - start;
            if (local < 0) return 0;
            return (Tone(hz, local) + Tone(hz * 2, local) * .18f) * Mathf.Exp(-local * 8) * Mathf.Clamp01(local * 160) * .56f;
        }

        static AudioClip Make(string name, float seconds, Func<float, int, float> render)
        {
            var values = new float[Mathf.RoundToInt(Rate * seconds)];
            for (int i = 0; i < values.Length; i++)
            {
                float time = (float)i / Rate;
                float fade = Mathf.Clamp01(Mathf.Min(time, seconds - time) * 200);
                values[i] = Mathf.Clamp(render(time, i) * fade, -1, 1);
            }
            var audio = AudioClip.Create(name, values.Length, 1, Rate, false);
            audio.SetData(values, 0);
            return audio;
        }
    }
}
