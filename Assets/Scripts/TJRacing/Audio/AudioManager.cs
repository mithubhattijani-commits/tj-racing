// TJ Racing 3D — audio. No binary clips ship in v1 (see docs/ASSETS.md):
// the engine is a PROCEDURAL tone generated at runtime (pitch follows speed),
// and skid / crash / nitro / countdown slots accept Freesound clips — each clip's
// licence must be checked before it is added (CC0 / CC-BY with attribution in
// LICENCES.md). Nothing here plays a clip that isn't in the project yet.
using UnityEngine;

namespace TJRacing
{
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("Clip slots — add Freesound clips here (licence-checked, see docs/ASSETS.md)")]
        public AudioClip skidClip;
        public AudioClip crashClip;
        public AudioClip nitroClip;
        public AudioClip countdownClip;
        public AudioClip winClip;

        public bool Muted { get; private set; }

        private AudioSource _engineSource;
        private AudioSource _sfxSource;
        private CarController _playerCar;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            _engineSource = gameObject.AddComponent<AudioSource>();
            _engineSource.loop = true;
            _engineSource.clip = MakeEngineTone();
            _engineSource.volume = 0.22f;
            _sfxSource = gameObject.AddComponent<AudioSource>();
        }

        void Update()
        {
            if (_playerCar == null && PlayerDriver.Instance != null) _playerCar = PlayerDriver.Instance.Car;
            if (_playerCar == null) return;
            bool racing = RaceManager.Instance != null && RaceManager.Instance.State == RaceState.Racing;
            if (racing && !Muted)
            {
                if (!_engineSource.isPlaying) _engineSource.Play();
                // Engine note rises with speed; nitro adds a higher whine.
                float rpm = Mathf.Clamp01(_playerCar.SpeedKmh / _playerCar.topSpeedKmh);
                _engineSource.pitch = 0.75f + rpm * 1.15f + (_playerCar.Nitro != null && _playerCar.Nitro.IsActive ? 0.35f : 0f);
                _engineSource.volume = 0.16f + rpm * 0.14f;
            }
            else if (_engineSource.isPlaying) _engineSource.Pause();
        }

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

        public void SetMuted(bool muted)
        {
            Muted = muted;
            _engineSource.volume = muted ? 0f : 0.22f;
            _sfxSource.volume = muted ? 0f : 1f;
        }

        public void PlayNitro() { PlayOneShot(nitroClip); }
        public void PlaySkid() { PlayOneShot(skidClip); }
        public void PlayCrash() { PlayOneShot(crashClip); }
        public void PlayCountdown() { PlayOneShot(countdownClip); }
        public void PlayWin() { PlayOneShot(winClip); }

        private void PlayOneShot(AudioClip clip)
        {
            if (clip == null || Muted) return;
            _sfxSource.PlayOneShot(clip);
        }
    }
}
