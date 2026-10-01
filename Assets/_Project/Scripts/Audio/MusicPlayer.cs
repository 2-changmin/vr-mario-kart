using UnityEngine;
using VRKart.Race;

namespace VRKart.Audio
{
    // 배경음악 루프(2D). 씬에 1개. 레이스 씬에서는 일시정지 중 작게, 플레이어 완주 후 조금 작게 튼다.
    [RequireComponent(typeof(AudioSource))]
    public sealed class MusicPlayer : MonoBehaviour
    {
        [SerializeField] private AudioClip _clip;
        [SerializeField, Range(0f, 1f)] private float _baseVolume = 0.35f;
        [SerializeField, Range(0f, 1f)] private float _pausedFactor = 0.4f;
        [SerializeField, Range(0f, 1f)] private float _finishedFactor = 0.7f;

        private AudioSource _source;
        private RaceManager _raceManager;
        private float _factor = 1f;

        private void Awake()
        {
            _source = GetComponent<AudioSource>();
            _source.clip = _clip;
            _source.loop = true;
            _source.spatialBlend = 0f;
            _source.playOnAwake = false;
            _source.ignoreListenerPause = true;   // 일시정지(AudioListener.pause) 중에도 작게 계속
            _raceManager = FindAnyObjectByType<RaceManager>();
        }

        private void OnEnable()
        {
            AudioVolumes.Changed += Apply;
            if (_raceManager == null) return;
            _raceManager.PauseChanged += HandlePause;
            _raceManager.OnRaceFinished += HandleFinished;
        }

        private void OnDisable()
        {
            AudioVolumes.Changed -= Apply;
            if (_raceManager == null) return;
            _raceManager.PauseChanged -= HandlePause;
            _raceManager.OnRaceFinished -= HandleFinished;
        }

        private void Start()
        {
            Apply();
            if (_clip != null) _source.Play();
        }

        private void HandlePause(bool paused)
        {
            _factor = paused ? _pausedFactor : (_raceManager.State == RaceState.Finished ? _finishedFactor : 1f);
            Apply();
        }

        private void HandleFinished()
        {
            _factor = _finishedFactor;
            Apply();
        }

        private void Apply() => _source.volume = _baseVolume * _factor * AudioVolumes.Music;
    }
}
