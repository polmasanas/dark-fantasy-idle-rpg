using DarkFantasy.Core.Look;
using UnityEngine;

namespace DarkFantasy.Presentation
{
    /// <summary>
    /// Two looping music layers (CD-7): the ambient layer plays all the time; the boss layer fades in
    /// when the boss camera pulls back and fades out after the fight. Volumes and the crossfade time
    /// come from look_config.json. Muting changes nothing in the game.
    /// </summary>
    internal sealed class MusicService
    {
        private readonly AudioLook _settings;
        private readonly AudioSource _ambient;
        private readonly AudioSource _boss;
        private bool _bossOn;
        private float _ambientLevel = 1f;   // 0..1 share of the configured volume
        private float _bossLevel;

        public bool Muted { get; set; }

        public MusicService(GameObject host, AudioLook settings)
        {
            _settings = settings;
            _ambient = Source(host, "Audio/ambient_loop");
            _boss = Source(host, "Audio/boss_loop");
            if (_ambient != null) _ambient.Play();
            if (_boss != null) _boss.Play(); // plays silently so the fade-in starts mid-loop, like a real track
        }

        public void SetBoss(bool on) => _bossOn = on;

        public void Tick(float unscaledDelta)
        {
            float step = _settings.CrossfadeS > 0 ? unscaledDelta / (float)_settings.CrossfadeS : 1f;
            _bossLevel = Mathf.MoveTowards(_bossLevel, _bossOn ? 1f : 0f, step);
            _ambientLevel = Mathf.MoveTowards(_ambientLevel, _bossOn ? 0.35f : 1f, step);
            float master = Muted ? 0f : 1f;
            if (_ambient != null) _ambient.volume = master * _ambientLevel * (float)_settings.AmbientVolume;
            if (_boss != null) _boss.volume = master * _bossLevel * (float)_settings.BossVolume;
        }

        private static AudioSource Source(GameObject host, string clipPath)
        {
            var clip = Resources.Load<AudioClip>(clipPath);
            if (clip == null)
            {
                Debug.LogWarning("MusicService: Resources/" + clipPath + ".wav is missing (run Tools/ArtBuild/make_audio.py).");
                return null;
            }
            var source = host.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = true;
            source.playOnAwake = false;
            source.volume = 0f;
            return source;
        }
    }
}
