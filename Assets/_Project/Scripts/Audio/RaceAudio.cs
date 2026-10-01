using UnityEngine;
using VRKart.Core;
using VRKart.Race;

namespace VRKart.Audio
{
    // 레이스 씬 효과음(2D): 카운트다운 삐-삐-삐-빵, 플레이어 랩 완료·마지막 랩·완주.
    // 일시정지하면 월드 소리(엔진·충돌)를 멈춘다(AudioListener.pause). UI·음악 소리는 ignoreListenerPause라 계속 난다.
    [RequireComponent(typeof(AudioSource))]
    public sealed class RaceAudio : MonoBehaviour
    {
        [SerializeField] private RaceManager _raceManager;

        [Header("카운트다운")]
        [SerializeField] private AudioClip _countdownBeep;
        [SerializeField, Range(0.5f, 2f)] private float _countPitch = 1f;
        [SerializeField, Range(0.5f, 3f)] private float _goPitch = 1.5f;

        [Header("랩 / 완주 (플레이어만)")]
        [SerializeField] private AudioClip _lapComplete;
        [SerializeField] private AudioClip _finalLap;
        [SerializeField] private AudioClip _finish;
        [SerializeField, Range(0f, 1f)] private float _volume = 0.8f;

        private AudioSource _source;

        private void Awake()
        {
            if (_raceManager == null) _raceManager = FindAnyObjectByType<RaceManager>();
            _source = GetComponent<AudioSource>();
            _source.spatialBlend = 0f;
            _source.playOnAwake = false;
            _source.ignoreListenerPause = true;
        }

        private void OnEnable()
        {
            if (_raceManager == null) return;
            _raceManager.OnCountdownTick += HandleCountdown;
            _raceManager.OnLapCompleted += HandleLap;
            _raceManager.OnRaceFinished += HandleFinished;
            _raceManager.PauseChanged += HandlePause;
        }

        private void OnDisable()
        {
            AudioListener.pause = false;
            if (_raceManager == null) return;
            _raceManager.OnCountdownTick -= HandleCountdown;
            _raceManager.OnLapCompleted -= HandleLap;
            _raceManager.OnRaceFinished -= HandleFinished;
            _raceManager.PauseChanged -= HandlePause;
        }

        // 3, 2, 1 = 낮은 삐, 0(GO) = 높은 삐
        private void HandleCountdown(int count) => Play(_countdownBeep, count == 0 ? _goPitch : _countPitch, count == 0 ? 1f : 0.8f);

        private void HandleLap(IRaceParticipant participant, int lap, float lapTime)
        {
            if (!IsPlayer(participant)) return;
            // 방금 끝낸 랩 다음이 마지막 랩이면 마지막 랩 알림, 마지막 랩 완료(= 완주)는 HandleFinished가 처리
            if (lap == _raceManager.Player.TotalLaps - 1) Play(_finalLap, 1f, 1f);
            else if (lap < _raceManager.Player.TotalLaps) Play(_lapComplete, 1f, 1f);
        }

        private void HandleFinished() => Play(_finish, 1f, 1f);

        private void HandlePause(bool paused) => AudioListener.pause = paused;

        private bool IsPlayer(IRaceParticipant participant) =>
            _raceManager.Player != null && ReferenceEquals(participant, _raceManager.Player);

        private void Play(AudioClip clip, float pitch, float volume)
        {
            if (clip == null) return;
            _source.pitch = pitch;
            _source.PlayOneShot(clip, volume * _volume * AudioVolumes.Sfx);
        }
    }
}
