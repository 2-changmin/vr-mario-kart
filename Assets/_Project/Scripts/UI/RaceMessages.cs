using TMPro;
using UnityEngine;
using VRKart.Core;
using VRKart.Race;

namespace VRKart.UI
{
    // 시야 가운데(±15°)에 잠깐 띄우는 메시지: 카운트다운, 랩 완료·마지막 랩, 역주행 경고.
    // 게임 시간(Time.deltaTime) 기준이라 일시정지하면 메시지도 멈춘다.
    public sealed class RaceMessages : MonoBehaviour
    {
        private const float PopScale = 1.4f;
        private const float PopDuration = 0.25f;
        private const float FadeDuration = 0.3f;

        [SerializeField] private RaceManager _raceManager;
        [SerializeField] private TMP_Text _countdownText;
        [SerializeField] private TMP_Text _popupText;
        [SerializeField] private TMP_Text _wrongWayText;
        [SerializeField] private Color _countColor = Color.white;
        [SerializeField] private Color _goColor = new Color(0.35f, 1f, 0.5f);
        [SerializeField, Min(0.5f)] private float _popupDuration = 2.5f;

        private RaceProgress _player;
        private float _countdownTime = -1f;
        private float _countdownLife;
        private float _popupTime = -1f;

        private void Awake()
        {
            if (_raceManager == null) _raceManager = FindAnyObjectByType<RaceManager>();
            HideAll();
        }

        private void Start()
        {
            _player = _raceManager.Player;
            if (_player != null) _player.WrongWayChanged += HandleWrongWay;
        }

        private void OnDestroy()
        {
            if (_player != null) _player.WrongWayChanged -= HandleWrongWay;
        }

        private void OnEnable()
        {
            if (_raceManager == null) return;
            _raceManager.OnCountdownTick += HandleCountdownTick;
            _raceManager.OnLapCompleted += HandleLapCompleted;
            _raceManager.OnRaceFinished += HideAll;
        }

        private void OnDisable()
        {
            if (_raceManager == null) return;
            _raceManager.OnCountdownTick -= HandleCountdownTick;
            _raceManager.OnLapCompleted -= HandleLapCompleted;
            _raceManager.OnRaceFinished -= HideAll;
        }

        private void HandleCountdownTick(int n)
        {
            _countdownText.text = n > 0 ? n.ToString() : "출발!";
            _countdownText.color = n > 0 ? _countColor : _goColor;
            _countdownText.enabled = true;
            _countdownLife = n > 0 ? 1f : 1.2f;
            _countdownTime = 0f;
        }

        private void HandleLapCompleted(IRaceParticipant participant, int lap, float lapTime)
        {
            if (!ReferenceEquals(participant, _player) || lap >= _player.TotalLaps) return;

            string text = $"{lap}랩  {TimeFormat.Format(lapTime)}";
            if (lap == _player.TotalLaps - 1) text += "\n<color=#FFC933>마지막 랩!</color>";
            _popupText.text = text;
            _popupText.enabled = true;
            _popupTime = 0f;
        }

        private void HandleWrongWay(RaceProgress participant, bool wrongWay) => _wrongWayText.enabled = wrongWay;

        private void HideAll()
        {
            _countdownText.enabled = false;
            _popupText.enabled = false;
            _wrongWayText.enabled = false;
            _countdownTime = -1f;
            _popupTime = -1f;
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            if (_countdownTime >= 0f)
            {
                _countdownTime += dt;
                float pop = Mathf.Clamp01(_countdownTime / PopDuration);
                _countdownText.rectTransform.localScale = Vector3.one * Mathf.Lerp(PopScale, 1f, pop);
                _countdownText.alpha = FadeOut(_countdownTime, _countdownLife);
                if (_countdownTime >= _countdownLife)
                {
                    _countdownText.enabled = false;
                    _countdownTime = -1f;
                }
            }

            if (_popupTime >= 0f)
            {
                _popupTime += dt;
                _popupText.alpha = FadeOut(_popupTime, _popupDuration);
                if (_popupTime >= _popupDuration)
                {
                    _popupText.enabled = false;
                    _popupTime = -1f;
                }
            }

            if (_wrongWayText.enabled) _wrongWayText.alpha = 0.9f + 0.1f * Mathf.Sin(Time.time * 8f);
        }

        private static float FadeOut(float time, float life) => Mathf.Clamp01((life - time) / FadeDuration);
    }
}
