using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VRKart.Core;
using VRKart.Race;

namespace VRKart.UI
{
    // 플레이어가 완주하면 플레이어 앞(기본 1.5m)에 결과를 띄운다.
    // 머리가 아니라 플레이어 루트(XR Origin, 나중엔 카트)의 자식으로 붙여서 카트와 함께 움직이고, 고개를 돌려도 따라오지 않는다.
    public sealed class ResultScreen : MonoBehaviour
    {
        [SerializeField] private RaceManager _raceManager;
        [SerializeField] private GameObject _panel;
        [SerializeField] private RectTransform _rowContainer;
        [SerializeField] private ResultRow _rowPrefab;
        [SerializeField] private TMP_Text _totalTimeText;
        [SerializeField] private TMP_Text _lapTimesText;
        [SerializeField] private Button _restartButton;
        [SerializeField] private Button _menuButton;

        [Header("배치")]
        [SerializeField, Min(0.5f)] private float _distance = 1.5f;
        [SerializeField] private float _heightOffset = -0.15f;

        private readonly List<ResultRow> _rows = new List<ResultRow>();

        public bool IsShown => _panel.activeSelf;

        public void Show()
        {
            PlaceInFrontOfPlayer();
            _panel.SetActive(true);
            Refresh();
        }

        public void Hide() => _panel.SetActive(false);

        public void Refresh()
        {
            IReadOnlyList<RaceResult> results = _raceManager.GetResults();
            while (_rows.Count < results.Count) _rows.Add(Instantiate(_rowPrefab, _rowContainer));

            for (int i = 0; i < _rows.Count; i++)
            {
                bool used = i < results.Count;
                _rows[i].gameObject.SetActive(used);
                if (used) _rows[i].Set(results[i], DisplayName(results[i]));
            }

            RaceResult player = _raceManager.Player != null ? _raceManager.GetResult(_raceManager.Player) : null;
            _totalTimeText.text = player != null && player.IsFinished ? TimeFormat.Format(player.TotalTime) : "--:--.---";
            _lapTimesText.text = player != null ? BuildLapTimes(player.LapTimes) : string.Empty;
        }

        private void Awake()
        {
            if (_raceManager == null) _raceManager = FindAnyObjectByType<RaceManager>();
            _panel.SetActive(false);
        }

        private void OnEnable()
        {
            if (_raceManager != null)
            {
                _raceManager.OnRaceFinished += Show;
                _raceManager.OnParticipantFinished += HandleParticipantFinished;
            }
            _restartButton.onClick.AddListener(HandleRestart);
            _menuButton.onClick.AddListener(HandleMenu);
        }

        private void OnDisable()
        {
            if (_raceManager != null)
            {
                _raceManager.OnRaceFinished -= Show;
                _raceManager.OnParticipantFinished -= HandleParticipantFinished;
            }
            _restartButton.onClick.RemoveListener(HandleRestart);
            _menuButton.onClick.RemoveListener(HandleMenu);
        }

        private void HandleParticipantFinished(IRaceParticipant participant, float totalTime)
        {
            if (IsShown) Refresh();
        }

        private void HandleRestart() => _raceManager.Restart();

        private void HandleMenu() => _raceManager.ExitToMenu();

        private void PlaceInFrontOfPlayer()
        {
            Camera head = Camera.main;
            if (head == null) return;

            Vector3 forward = Vector3.ProjectOnPlane(head.transform.forward, Vector3.up);
            if (forward.sqrMagnitude < 1e-4f) forward = Vector3.ProjectOnPlane(head.transform.up, Vector3.up);
            forward.Normalize();

            transform.SetParent(head.transform.root, true);
            transform.SetPositionAndRotation(
                head.transform.position + forward * _distance + Vector3.up * _heightOffset,
                Quaternion.LookRotation(forward, Vector3.up));
        }

        private static string DisplayName(RaceResult result) => result.IsPlayer ? "YOU" : result.Participant.name;

        private static string BuildLapTimes(IReadOnlyList<float> lapTimes)
        {
            int best = -1;
            for (int i = 0; i < lapTimes.Count; i++)
                if (best < 0 || lapTimes[i] < lapTimes[best]) best = i;

            var text = new StringBuilder();
            for (int i = 0; i < lapTimes.Count; i++)
            {
                if (i > 0) text.Append('\n');
                text.Append($"LAP {i + 1}   {TimeFormat.Format(lapTimes[i])}");
                if (i == best && lapTimes.Count > 1) text.Append("  <color=#FFC933>BEST</color>");
            }
            return text.ToString();
        }
    }
}
