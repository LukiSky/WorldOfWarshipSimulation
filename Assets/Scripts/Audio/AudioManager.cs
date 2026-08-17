using System.Collections.Generic;
using UnityEngine;

namespace Naval
{
    public enum SoundId
    {
        MainGun, MainGunHeavy, SecondaryGun, TorpedoLaunch, Explosion, Impact, Splash,
        Alarm, Sonar, Sinking, Fire, Repair, Dive, DepthCharge, Detected, Smoke,
        OrderConfirm, Victory, Defeat, Engine
    }

    /// <summary>
    /// All audio is synthesised at load - no sample assets. Sources are pooled and mixed by distance
    /// from the tactical camera, so a battleship salvo across the map is a distant rumble.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager I { get; private set; }

        const int SampleRate = 44100;
        const int VoiceCount = 24;

        readonly Dictionary<SoundId, AudioClip> _clips = new Dictionary<SoundId, AudioClip>();
        AudioSource[] _voices;
        int _voiceCursor;
        readonly Dictionary<SoundId, float> _lastPlayed = new Dictionary<SoundId, float>();

        public float MasterVolume = 0.8f;
        public bool Muted;

        public static AudioManager Create(Transform parent)
        {
            var go = new GameObject("AudioManager");
            go.transform.SetParent(parent, false);
            var a = go.AddComponent<AudioManager>();
            a.Init();
            I = a;
            return a;
        }

        void Init()
        {
            BuildClips();
            _voices = new AudioSource[VoiceCount];
            for (int i = 0; i < VoiceCount; i++)
            {
                var go = new GameObject("Voice" + i);
                go.transform.SetParent(transform, false);
                var src = go.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 0f;      // we do our own distance mixing
                src.rolloffMode = AudioRolloffMode.Linear;
                _voices[i] = src;
            }
        }

        // ------------------------------------------------------------------ playback

        public static void PlayAt(SoundId id, Vector2 worldPos, float volume = 1f)
        {
            if (I == null || I.Muted) return;
            I.PlayInternal(id, worldPos, volume, false);
        }

        public static void PlayUI(SoundId id, float volume = 1f)
        {
            if (I == null || I.Muted) return;
            I.PlayInternal(id, Vector2.zero, volume, true);
        }

        void PlayInternal(SoundId id, Vector2 pos, float volume, bool ui)
        {
            if (!_clips.TryGetValue(id, out var clip) || clip == null) return;

            // throttle: never stack more than a few of the same sound per instant
            if (_lastPlayed.TryGetValue(id, out float last) && Time.unscaledTime - last < 0.045f) return;
            _lastPlayed[id] = Time.unscaledTime;

            float vol = volume * MasterVolume;
            float pan = 0f;

            if (!ui && RTSCamera.I != null && RTSCamera.I.Cam != null)
            {
                Vector2 camPos = RTSCamera.I.Cam.transform.position;
                float audible = Mathf.Max(300f, RTSCamera.I.Zoom * 3.2f);
                float d = Vector2.Distance(camPos, pos);
                if (d > audible) return;
                vol *= Mathf.Clamp01(1f - d / audible);
                vol *= Mathf.Lerp(1f, 0.55f, Mathf.InverseLerp(150f, 800f, RTSCamera.I.Zoom));
                pan = Mathf.Clamp((pos.x - camPos.x) / Mathf.Max(1f, RTSCamera.I.Zoom * 1.4f), -1f, 1f);
                if (vol < 0.02f) return;
            }

            var src = _voices[_voiceCursor];
            _voiceCursor = (_voiceCursor + 1) % VoiceCount;
            src.clip = clip;
            src.volume = Mathf.Clamp01(vol);
            src.panStereo = pan;
            src.pitch = Random.Range(0.93f, 1.07f);
            src.Play();
        }

        // ------------------------------------------------------------------ synthesis

        void BuildClips()
        {
            _clips[SoundId.MainGun] = Gun(0.55f, 150f, 0.75f);
            _clips[SoundId.MainGunHeavy] = Gun(1.1f, 70f, 1f);
            _clips[SoundId.SecondaryGun] = Gun(0.22f, 320f, 0.5f);
            _clips[SoundId.Explosion] = Explosion(1.4f);
            _clips[SoundId.DepthCharge] = Explosion(1.0f, true);
            _clips[SoundId.Impact] = Gun(0.3f, 200f, 0.6f);
            _clips[SoundId.Splash] = Splash(0.6f);
            _clips[SoundId.TorpedoLaunch] = Hiss(0.7f, 900f, 220f);
            _clips[SoundId.Smoke] = Hiss(1.2f, 600f, 300f);
            _clips[SoundId.Dive] = Hiss(1.4f, 400f, 120f);
            _clips[SoundId.Sonar] = Ping(0.9f, 880f);
            _clips[SoundId.Detected] = Blip(0.22f, 1200f, 1600f);
            _clips[SoundId.OrderConfirm] = Blip(0.10f, 720f, 900f);
            _clips[SoundId.Alarm] = Alarm(1.1f);
            _clips[SoundId.Sinking] = Rumble(2.4f, 62f);
            _clips[SoundId.Fire] = Hiss(0.8f, 1500f, 700f);
            _clips[SoundId.Repair] = Clank(0.5f);
            _clips[SoundId.Victory] = Fanfare(1.4f, true);
            _clips[SoundId.Defeat] = Fanfare(1.6f, false);
            _clips[SoundId.Engine] = Rumble(1.5f, 45f);
        }

        static AudioClip Make(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static float[] Buffer(float seconds) => new float[Mathf.Max(16, Mathf.RoundToInt(SampleRate * seconds))];

        static AudioClip Gun(float len, float thumpHz, float power)
        {
            var d = Buffer(len);
            float lp = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SampleRate;
                float env = Mathf.Exp(-t * (7f / len));
                float noise = Random.Range(-1f, 1f);
                lp += (noise - lp) * 0.28f;                       // low passed crack
                float thump = Mathf.Sin(2f * Mathf.PI * thumpHz * Mathf.Pow(t, 0.85f)) * Mathf.Exp(-t * 9f);
                d[i] = Mathf.Clamp((lp * 0.75f + thump * 0.8f) * env * power, -1f, 1f);
            }
            return Make("gun", d);
        }

        static AudioClip Explosion(float len, bool underwater = false)
        {
            var d = Buffer(len);
            float lp = 0f, lp2 = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SampleRate;
                float env = Mathf.Exp(-t * (4.2f / len));
                float noise = Random.Range(-1f, 1f);
                lp += (noise - lp) * (underwater ? 0.06f : 0.16f);
                lp2 += (lp - lp2) * 0.5f;
                float boom = Mathf.Sin(2f * Mathf.PI * (underwater ? 40f : 60f) * Mathf.Pow(t + 0.001f, 0.7f)) * Mathf.Exp(-t * 5f);
                d[i] = Mathf.Clamp((lp2 * 1.6f + boom) * env, -1f, 1f);
            }
            return Make("explosion", d);
        }

        static AudioClip Splash(float len)
        {
            var d = Buffer(len);
            float hp = 0f, prev = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SampleRate;
                float env = Mathf.Exp(-t * (6f / len)) * Mathf.Min(1f, t * 60f);
                float noise = Random.Range(-1f, 1f);
                hp = 0.85f * (hp + noise - prev);                 // high passed: watery hiss
                prev = noise;
                d[i] = Mathf.Clamp(hp * env * 0.7f, -1f, 1f);
            }
            return Make("splash", d);
        }

        static AudioClip Hiss(float len, float startHz, float endHz)
        {
            var d = Buffer(len);
            float lp = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SampleRate;
                float k = t / len;
                float env = Mathf.Sin(Mathf.Clamp01(k) * Mathf.PI) ;
                float cutoff = Mathf.Lerp(startHz, endHz, k) / 8000f;
                lp += (Random.Range(-1f, 1f) - lp) * Mathf.Clamp01(cutoff);
                d[i] = Mathf.Clamp(lp * env * 0.75f, -1f, 1f);
            }
            return Make("hiss", d);
        }

        static AudioClip Ping(float len, float hz)
        {
            var d = Buffer(len);
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SampleRate;
                float env = Mathf.Exp(-t * 4.5f) * Mathf.Min(1f, t * 200f);
                float f = hz * (1f + Mathf.Sin(t * 12f) * 0.01f);
                d[i] = Mathf.Sin(2f * Mathf.PI * f * t) * env * 0.6f;
            }
            return Make("ping", d);
        }

        static AudioClip Blip(float len, float hz0, float hz1)
        {
            var d = Buffer(len);
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SampleRate;
                float k = t / len;
                float env = Mathf.Sin(Mathf.Clamp01(k) * Mathf.PI);
                float f = Mathf.Lerp(hz0, hz1, k);
                d[i] = Mathf.Sin(2f * Mathf.PI * f * t) * env * 0.45f;
            }
            return Make("blip", d);
        }

        static AudioClip Alarm(float len)
        {
            var d = Buffer(len);
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SampleRate;
                float env = Mathf.Exp(-t * 1.2f);
                float f = (Mathf.FloorToInt(t * 6f) % 2 == 0) ? 620f : 880f;
                float sq = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * f * t));
                d[i] = sq * env * 0.28f;
            }
            return Make("alarm", d);
        }

        static AudioClip Rumble(float len, float hz)
        {
            var d = Buffer(len);
            float lp = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SampleRate;
                float env = Mathf.Sin(Mathf.Clamp01(t / len) * Mathf.PI);
                lp += (Random.Range(-1f, 1f) - lp) * 0.04f;
                float tone = Mathf.Sin(2f * Mathf.PI * hz * t + Mathf.Sin(t * 3f) * 2f);
                d[i] = Mathf.Clamp((tone * 0.6f + lp * 1.2f) * env * 0.7f, -1f, 1f);
            }
            return Make("rumble", d);
        }

        static AudioClip Clank(float len)
        {
            var d = Buffer(len);
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SampleRate;
                float env = Mathf.Exp(-Mathf.Repeat(t, 0.16f) * 40f);
                float tone = Mathf.Sin(2f * Mathf.PI * 430f * t) + Mathf.Sin(2f * Mathf.PI * 1170f * t) * 0.5f;
                d[i] = Mathf.Clamp(tone * env * 0.3f, -1f, 1f);
            }
            return Make("clank", d);
        }

        static AudioClip Fanfare(float len, bool up)
        {
            var d = Buffer(len);
            float[] notes = up ? new[] { 392f, 523f, 659f, 784f } : new[] { 392f, 349f, 294f, 233f };
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SampleRate;
                int n = Mathf.Clamp(Mathf.FloorToInt(t / (len / notes.Length)), 0, notes.Length - 1);
                float local = t - n * (len / notes.Length);
                float env = Mathf.Exp(-local * 3.2f);
                d[i] = (Mathf.Sin(2f * Mathf.PI * notes[n] * t) + Mathf.Sin(2f * Mathf.PI * notes[n] * 2f * t) * 0.3f) * env * 0.28f;
            }
            return Make("fanfare", d);
        }
    }
}
