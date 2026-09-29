using TMPro;
using UnityEngine;
using VRKart.Core;
using VRKart.Race;

namespace VRKart.UI
{
    // 인게임 HUD 루트. 카운트다운이 시작될 때 플레이어 눈 위치에 배치되고(PlayerSpace), 일시정지 중과 플레이어 완주 후에는 숨긴다.
    // 자식의 대시보드(눈앞 아래)와 가운데 메시지(RaceMessages)는 루트 기준 위치에 미리 놓여 있다.
    public sealed class RaceHud : MonoBehaviour
    {
        [SerializeField] private RaceManager _raceManager;
        [SerializeField] private GameObject _content;

        [Header("대시보드")]
        [SerializeField] private TMP_Text _lapText;
        [SerializeField] private TMP_Text _rankText;
        [SerializeField] private TMP_Text _speedText;
        [SerializeField] private TMP_Text _timeText;

        private RaceProgress _player;
        private IKart _playerKart;
        private int _participantCount;
        private int _shownLap = -1;
        private int _shownRank = -1;
        private int _shownSpeed = -1;
        private int _shownTenths = -1;

        public bool IsShown => _content.activeSelf;

        public void Show()
        {
            PlayerSpace.PlaceInFront(transform, 0f, 0f);
            _content.SetActive(true);
            Refresh(true);
        }

        public void Hide() => _content.SetActive(false);

        private void Awake()
        {
            if (_raceManager == null) _raceManager = FindAnyObjectByType<RaceManager>();
            _content.SetActive(false);
        }

        private void Start()
        {
            _player = _raceManager.Player;
            _playerKart = _player != null ? _player.GetComponent<IKart>() : null;
            _participantCount = _raceManager.GetResults().Count;
        }

        private void OnEnable()
        {
            if (_raceManager == null) return;
            _raceManager.StateChanged += HandleStateChanged;
            _raceManager.OnRaceFinished += Hide;
            _raceManager.PauseChanged += HandlePauseChanged;
        }

        private void OnDisable()
        {
            if (_raceManager == null) return;
            _raceManager.StateChanged -= HandleStateChanged;
            _raceManager.OnRaceFinished -= Hide;
            _raceManager.PauseChanged -= HandlePauseChanged;
        }

        private void HandleStateChanged(RaceState state)
        {
            if (state == RaceState.Countdown) Show();
        }

        // 일시정지 메뉴를 대시보드가 가리지 않도록 숨겼다가, 재개하면 그 자리에 다시 보인다
        private void HandlePauseChanged(bool paused) => _content.SetActive(!paused);

        private void Update()
        {
            if (_content.activeSelf) Refresh(false);
        }

        private void Refresh(bool force)
        {
            if (_player == null) return;

            int lap = _player.CurrentLap;
            if (force || lap != _shownLap)
            {
                _shownLap = lap;
                _lapText.text = $"{lap}<size=60%>/{_player.TotalLaps}</size>";
            }

            int rank = _player.Rank;
            if (force || rank != _shownRank)
            {
                _shownRank = rank;
                _rankText.text = $"{rank}위<size=60%> /{_participantCount}</size>";
            }

            int speed = _playerKart != null ? Mathf.RoundToInt(Mathf.Abs(_playerKart.CurrentSpeed) * 3.6f) : 0;
            if (force || speed != _shownSpeed)
            {
                _shownSpeed = speed;
                _speedText.text = speed.ToString();
            }

            float raceTime = _raceManager.RaceTime;
            int tenths = Mathf.FloorToInt(raceTime * 10f);
            if (force || tenths != _shownTenths)
            {
                _shownTenths = tenths;
                _timeText.text = TimeFormat.FormatTenths(raceTime);
            }
        }
    }
}
