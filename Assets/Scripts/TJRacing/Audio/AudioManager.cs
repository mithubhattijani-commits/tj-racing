// TJ Racing 3D — audio. ALL procedural, generated in code at runtime:
// no binary clips ship in v1 and no downloads/licences are needed.
//   (1) ENGINE / ACCELERATOR — two-detuned-sine tone; pitch follows speed,
//       and rises extra while the car is accelerating (throttle response).
//   (2) BRAKE SCREECH — looping ring-modulated noise, plays while the
//       player holds BRAKE at speed (CarController drives it).
//   (3) BACKGROUND MUSIC — dramatic loop: A-minor progression Am-F-C-G at
//       120 BPM, pulsing bass + kick + 16th-note arpeggio + swelling pad.
//       Generated as one seamless 8 s loop (every note ends inside its
//       slot; the pad swells to ~0 at each bar line, so the loop point
//       is click-free). Plays in the menu AND the race, quiet under the
//       engine. Muted by the same SOUND toggle as everything else.
//   Also: skid loop (drifting), nitro whoosh, crash thump, countdown
//   beeps + GO beep, win arpeggio, lose descend, UI click.
// Every Play* call is safe when its clip is missing or muted (no-op).
using UnityEngine;

namespace TJRacing
{
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        // Legacy clip slots (kept for compatibility — v1 ships no binary
        // clips; the procedural clips below are used instead. If a clip
        // is ever assigned in the Inspector it takes precedence for its
        // one-shot, so a future sound pack can drop in without code).
        [Header("Clip slots — optional; procedural clips are used when empty")]
        public AudioClip skidClip;
        public AudioClip crashClip;
        public AudioClip nitroClip;
        public AudioClip countdownClip;
        public AudioClip winClip;

        public bool Muted { get; private set; }

        private AudioSource _engineSource;
        private AudioSource _musicSource;
        private AudioSource _sfxSource;
        private AudioSource _brakeSource;
        private AudioSource _skidSource;
        private CarController _playerCar;
        private float _prevSpeedMs;
        private bool _brakePlaying;
        private bool _skidPlaying;

        // Procedural one-shots (built once in Awake).
        private AudioClip _nitroProc;
        private AudioClip _crashProc;
        private AudioClip _beepProc;
        private AudioClip _goProc;
        private AudioClip _winProc;
        private AudioClip _loseProc;
        private AudioClip _clickProc;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            _engineSource = gameObject.AddComponent<AudioSource>();
            _engineSource.loop = true;
            _engineSource.clip = MakeEngineTone();
            _engineSource.volume = 0.22f;
            _musicSource = gameObject.AddComponent<AudioSource>();
            _musicSource.loop = true;
            _musicSource.clip = MakeMusicLoop();
            _musicSource.volume = 0.16f;
            _sfxSource = gameObject.AddComponent<AudioSource>();
            _brakeSource = gameObject.AddComponent<AudioSource>();
            _brakeSource.loop = true;
            _brakeSource.clip = MakeLoopNoise(0.7f, 1750f, 10f, 12345);
            _brakeSource.volume = 0f;
            _skidSource = gameObject.AddComponent<AudioSource>();
            _skidSource.loop = true;
            _skidSource.clip = MakeLoopNoise(0.8f, 900f, 7.5f, 777);
            _skidSource.volume = 0f;
            _nitroProc = MakeNitroWhoosh();
            _crashProc = MakeCrashThump();
            _beepProc = MakeTone(880f, 0.12f, 0.5f);
            _goProc = MakeTone(1318.5f, 0.35f, 0.55f);
            _winProc = MakeArpeggio(new float[] { 523.25f, 659.25f, 783.99f, 1046.5f }, 0.13f, 0.5f);
            _loseProc = MakeArpeggio(new float[] { 392f, 311.13f }, 0.22f, 0.45f);
            _clickProc = MakeTone(1250f, 0.05f, 0.4f);
        }

        void Update()
        {
            if (_playerCar == null && PlayerDriver.Instance != null) _playerCar = PlayerDriver.Instance.Car;
            // Background music runs through the whole game (menu + race);
            // restart it if it ever stopped (e.g. after un-muting).
            if (!Muted && _musicSource.clip != null && !_musicSource.isPlaying) _musicSource.Play();
            if (_playerCar == null) return;
            bool racing = RaceManager.Instance != null && RaceManager.Instance.State == RaceState.Racing;
            if (racing && !Muted)
            {
                if (!_engineSource.isPlaying) _engineSource.Play();
                // Engine note rises with speed; extra rev while the car is
                // actively accelerating (throttle response) and a higher
                // whine while nitro is burning.
                float rpm = Mathf.Clamp01(_playerCar.SpeedKmh / _playerCar.topSpeedKmh);
                float accel = Mathf.Clamp01((_playerCar.SpeedMs - _prevSpeedMs) / (12f * Time.deltaTime));
                _engineSource.pitch = 0.75f + rpm * 1.15f + accel * 0.18f
                    + (_playerCar.Nitro != null && _playerCar.Nitro.IsActive ? 0.35f : 0f);
                _engineSource.volume = 0.16f + rpm * 0.14f;
            }
            else if (_engineSource.isPlaying) _engineSource.Pause();
            _prevSpeedMs = _playerCar.SpeedMs;
        }

        // ---- Music + tones (all synthesized; see header) ----------------------

        // Procedural engine tone: two detuned saw-ish waves summed into a loopable clip.
        private static AudioClip MakeEngineTone()
        {
            int rate = 22050;
            int len = rate; // 1s loop
            float[] data = new float[len];
            for (int i = 0; i < len; i++)
            {
                float t = (float)i / rate;
                float s = Mathf.Sin(2f * Mathf.PI * 82f * t) * 0.6f
                        + Mathf.Sin(2f * Mathf.PI * 123f * t) * 0.3f
                        + Mathf.Sin(2f * Mathf.PI * 164f * t) * 0.15f;
                data[i] = s * 0.5f;
            }
            var clip = AudioClip.Create("ProceduralEngine", len, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        // Dramatic background-music loop: 4 bars of Am-F-C-G at 120 BPM
        // (8 s total). Bass pulse + kick on every beat, a 16th-note
        // arpeggio over the chord tones, and a pad that swells inside
        // each bar and falls to ~0 at the bar line — so the loop point
        // is seamless (no click when the clip wraps).
        private static AudioClip MakeMusicLoop()
        {
            int rate = 22050;
            float bpm = 120f;
            float beat = 60f / bpm;          // 0.5 s
            int bars = 4;
            float totalSecs = bars * 4f * beat; // 8 s
            int len = (int)(totalSecs * rate);
            float[] data = new float[len];
            // Chord roots (Hz) + minor/major third: Am F C G.
            float[] roots = { 110.00f, 87.31f, 130.81f, 98.00f };
            int[] thirdSemis = { 3, 4, 4, 4 }; // Am minor, rest major
            for (int bar = 0; bar < bars; bar++)
            {
                float root = roots[bar];
                float third = root * Mathf.Pow(2f, thirdSemis[bar] / 12f);
                float fifth = root * Mathf.Pow(2f, 7f / 12f);
                float barStart = bar * 4f * beat;
                for (int b = 0; b < 4; b++)
                {
                    float beatT = barStart + b * beat;
                    AddKick(data, rate, beatT, 0.50f);
                    AddBassNote(data, rate, beatT, root, 0.22f, 0.42f);
                    AddBassNote(data, rate, beatT + beat * 0.5f, root * 2f, 0.15f, 0.22f);
                    // 16th-note arpeggio: root/3rd/5th/octave, one octave up.
                    float[] arp = { root * 2f, third * 2f, fifth * 2f, root * 4f };
                    for (int s = 0; s < 4; s++)
                        AddPluck(data, rate, beatT + s * beat * 0.25f, arp[(b * 4 + s) % 4], beat * 0.22f, 0.11f);
                }
                // Pad: chord tones, swelling inside the bar, ~0 at both ends.
                AddPad(data, rate, barStart, 4f * beat, new float[] { root * 2f, third * 2f, fifth * 2f }, 0.085f);
            }
            // Normalize to a safe peak so the mix never clips.
            float peak = 0.001f;
            for (int i = 0; i < len; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
            float gain = 0.55f / peak;
            for (int i = 0; i < len; i++) data[i] *= gain;
            var clip = AudioClip.Create("ProceduralMusic", len, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static void AddKick(float[] data, int rate, float startSecs, float vol)
        {
            int start = (int)(startSecs * rate);
            int n = (int)(0.14f * rate);
            for (int i = 0; i < n && start + i < data.Length; i++)
            {
                float t = (float)i / rate;
                float f = 130f + (45f - 130f) * (t / 0.14f); // 130 -> 45 Hz drop
                float env = 1f - t / 0.14f;
                data[start + i] += vol * env * env * Mathf.Sin(2f * Mathf.PI * f * t);
            }
        }

        private static void AddBassNote(float[] data, int rate, float startSecs, float freq, float dur, float vol)
        {
            int start = (int)(startSecs * rate);
            int n = (int)(dur * rate);
            for (int i = 0; i < n && start + i < data.Length; i++)
            {
                float t = (float)i / rate;
                float env = Mathf.Min(1f, t / 0.01f) * (1f - t / dur);
                data[start + i] += vol * env * (Mathf.Sin(2f * Mathf.PI * freq * t) + 0.35f * Mathf.Sin(2f * Mathf.PI * freq * 2f * t));
            }
        }

        private static void AddPluck(float[] data, int rate, float startSecs, float freq, float dur, float vol)
        {
            int start = (int)(startSecs * rate);
            int n = (int)(dur * rate);
            for (int i = 0; i < n && start + i < data.Length; i++)
            {
                float t = (float)i / rate;
                float env = Mathf.Min(1f, t / 0.008f) * (1f - t / dur);
                data[start + i] += vol * env * (Mathf.Sin(2f * Mathf.PI * freq * t) + 0.3f * Mathf.Sin(2f * Mathf.PI * freq * 3f * t));
            }
        }

        private static void AddPad(float[] data, int rate, float startSecs, float durSecs, float[] freqs, float vol)
        {
            int start = (int)(startSecs * rate);
            int n = (int)(durSecs * rate);
            for (int i = 0; i < n && start + i < data.Length; i++)
            {
                float t = (float)i / rate;
                // Swell: 0 at both bar edges, full in the middle.
                float edge = Mathf.Min(t / 0.25f, (durSecs - t) / 0.25f);
                float env = Mathf.Clamp01(edge);
                float s = 0f;
                foreach (float f in freqs) s += Mathf.Sin(2f * Mathf.PI * f * t);
                data[start + i] += vol * env * s / freqs.Length;
            }
        }

        // Seamless looping noise: a sum of sines at whole-cycle
        // frequencies with random phases (periodic over the clip, so
        // the loop wraps without a click), ring-modulated by a tone
        // for a screechy tyre character. Used for brake + skid loops.
        private static AudioClip MakeLoopNoise(float durSecs, float ringFreq, float tremoloHz, int seed)
        {
            int rate = 22050;
            int len = (int)(durSecs * rate);
            float[] data = new float[len];
            var rnd = new System.Random(seed);
            int partials = 48;
            float[] freqs = new float[partials];
            float[] phases = new float[partials];
            for (int k = 0; k < partials; k++)
            {
                freqs[k] = (float)((int)(rnd.Next(120, 4200) / (1f / durSecs)) * (1f / durSecs));
                phases[k] = (float)(rnd.NextDouble() * 2.0 * Mathf.PI);
            }
            for (int i = 0; i < len; i++)
            {
                float t = (float)i / rate;
                float s = 0f;
                for (int k = 0; k < partials; k++) s += Mathf.Sin(2f * Mathf.PI * freqs[k] * t + phases[k]);
                s /= partials;
                float ring = 0.55f + 0.45f * Mathf.Sin(2f * Mathf.PI * ringFreq * t);
                float trem = 0.75f + 0.25f * Mathf.Sin(2f * Mathf.PI * tremoloHz * t);
                data[i] = s * ring * trem * 0.9f;
            }
            var clip = AudioClip.Create("ProceduralLoopNoise", len, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip MakeNitroWhoosh()
        {
            int rate = 22050;
            float dur = 0.6f;
            int len = (int)(dur * rate);
            float[] data = new float[len];
            var rnd = new System.Random(4242);
            for (int i = 0; i < len; i++)
            {
                float t = (float)i / rate;
                float env = Mathf.Sin(Mathf.PI * t / dur); // rise then fall
                float sweep = 300f + 600f * (t / dur);
                data[i] = env * 0.5f * ((float)rnd.NextDouble() * 2f - 1f) * 0.6f
                        + env * 0.3f * Mathf.Sin(2f * Mathf.PI * sweep * t);
            }
            var clip = AudioClip.Create("ProceduralNitro", len, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip MakeCrashThump()
        {
            int rate = 22050;
            float dur = 0.3f;
            int len = (int)(dur * rate);
            float[] data = new float[len];
            var rnd = new System.Random(99);
            for (int i = 0; i < len; i++)
            {
                float t = (float)i / rate;
                float env = 1f - t / dur;
                float f = 100f + (40f - 100f) * (t / dur);
                data[i] = env * env * (0.7f * Mathf.Sin(2f * Mathf.PI * f * t)
                        + 0.5f * ((float)rnd.NextDouble() * 2f - 1f));
            }
            var clip = AudioClip.Create("ProceduralCrash", len, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip MakeTone(float freq, float dur, float vol)
        {
            int rate = 22050;
            int len = (int)(dur * rate);
            float[] data = new float[len];
            for (int i = 0; i < len; i++)
            {
                float t = (float)i / rate;
                float env = Mathf.Min(1f, t / 0.008f) * (1f - t / dur);
                data[i] = vol * env * (Mathf.Sin(2f * Mathf.PI * freq * t) + 0.25f * Mathf.Sin(2f * Mathf.PI * freq * 2f * t));
            }
            var clip = AudioClip.Create("ProceduralTone", len, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip MakeArpeggio(float[] freqs, float noteDur, float vol)
        {
            int rate = 22050;
            int len = (int)(noteDur * freqs.Length * rate);
            float[] data = new float[len];
            for (int n = 0; n < freqs.Length; n++)
            {
                int start = (int)(n * noteDur * rate);
                int m = (int)(noteDur * rate);
                for (int i = 0; i < m && start + i < len; i++)
                {
                    float t = (float)i / rate;
                    float env = Mathf.Min(1f, t / 0.01f) * (1f - t / noteDur);
                    data[start + i] = vol * env * (Mathf.Sin(2f * Mathf.PI * freqs[n] * t)
                        + 0.3f * Mathf.Sin(2f * Mathf.PI * freqs[n] * 2f * t));
                }
            }
            var clip = AudioClip.Create("ProceduralArpeggio", len, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        public void SetMuted(bool muted)
        {
            Muted = muted;
            if (muted)
            {
                _engineSource.Pause();
                _musicSource.Pause();
                StopBrake();
                StopSkid();
                _sfxSource.volume = 0f;
            }
            else
            {
                _sfxSource.volume = 1f;
                // Engine + music restart themselves in Update() when due.
            }
        }

        // ---- One-shots ---------------------------------------------------------
        public void PlayNitro() { PlayOneShot(nitroClip ?? _nitroProc); }
        public void PlayCrash() { PlayOneShot(crashClip ?? _crashProc); }
        public void PlayCountdown() { PlayOneShot(countdownClip ?? _beepProc); }
        public void PlayGo() { PlayOneShot(_goProc); }
        public void PlayWin() { PlayOneShot(winClip ?? _winProc); }
        public void PlayLose() { PlayOneShot(_loseProc); }
        public void PlayClick() { PlayOneShot(_clickProc); }

        private void PlayOneShot(AudioClip clip)
        {
            if (clip == null || Muted) return;
            _sfxSource.PlayOneShot(clip);
        }

        // ---- Loops (brake screech + skid) --------------------------------------
        // Called every physics step by CarController for the PLAYER car
        // only; the sources stay silent unless their loop is playing.
        public void StartBrake()
        {
            if (Muted || _brakePlaying) return;
            _brakePlaying = true;
            _brakeSource.volume = 0.28f;
            if (!_brakeSource.isPlaying) _brakeSource.Play();
        }

        public void StopBrake()
        {
            if (!_brakePlaying) return;
            _brakePlaying = false;
            _brakeSource.volume = 0f;
            _brakeSource.Stop();
        }

        public void StartSkid()
        {
            if (Muted || _skidPlaying) return;
            _skidPlaying = true;
            _skidSource.volume = 0.22f;
            if (!_skidSource.isPlaying) _skidSource.Play();
        }

        public void StopSkid()
        {
            if (!_skidPlaying) return;
            _skidPlaying = false;
            _skidSource.volume = 0f;
            _skidSource.Stop();
        }
    }
}
