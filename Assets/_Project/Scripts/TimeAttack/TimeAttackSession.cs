using System.Collections.Generic;
using UnityEngine;
using VRKart.Core;
using VRKart.Items;
using VRKart.Race;

namespace VRKart.TimeAttack
{
    // 타임어택 한 판 (#47). Kart_Player 루트에 붙어 있고, 메뉴에서 타임어택을 골랐을 때만 동작한다.
    // 1) RaceManager.Awake가 참가자를 모으기 전에 AI 카트와 아이템 박스를 끈다 → 혼자 달리는 레이스가 된다 (그래서 실행 순서를 가장 앞으로).
    // 2) 이전 최고 기록을 불러와 고스트(GhostCar)를 띄우고, HUD·랩 메시지·결과 화면이 Current로 기록을 읽는다.
    // 3) 이번 주행을 20Hz로 기록하고, 완주했을 때 최고 기록이면 저장한다.
    // RaceProgress는 트랙 씬에서 Kart_Player에 붙이므로, 없으면(샌드박스 씬 등) 아무것도 하지 않는다.
    [DefaultExecutionOrder(-1000)]
    public sealed class TimeAttackSession : MonoBehaviour
    {
        [SerializeField] private GhostCar _ghostPrefab;
        [SerializeField, Min(0.01f)] private float _sampleInterval = 0.05f;

        private readonly List<GhostSample> _samples = new List<GhostSample>();
        private RaceManager _raceManager;
        private RaceProgress _player;
        private string _trackScene;
        private TimeAttackRecord _record;   // 이번 주행 전까지의 기록 (없으면 null)
        private bool _recording;
        private float _nextSampleTime;

        // 타임어택 중이면 이번 판, 아니면 null
        public static TimeAttackSession Current { get; private set; }

        public bool HasRecord => _record != null;
        public float BestTotal => _record != null ? _record.BestTotal : 0f;   // 이번 주행 전 최고 전체 기록
        public float BestLap => _record != null ? _record.BestLap : 0f;
        public bool IsNewRecord { get; private set; }    // 완주 후에 의미 있음
        public bool IsNewBestLap { get; private set; }

        // 이번 주행에서 lap랩을 끝낸 레이스 시간과 최고 기록 주행(고스트)의 같은 지점 차이. 음수면 고스트보다 빠름
        public bool TryGetSplitDelta(int lap, float raceTime, out float delta)
        {
            delta = 0f;
            if (_record == null || lap < 1 || lap > _record.Splits.Count) return false;
            delta = raceTime - _record.Splits[lap - 1];
            return true;
        }

        private void Awake()
        {
            _raceManager = FindAnyObjectByType<RaceManager>();
            _player = GetComponent<RaceProgress>();
            if (!TimeAttackSettings.IsTimeAttack || _raceManager == null || _player == null)
            {
                enabled = false;
                return;
            }

            RemoveRivals();
            _trackScene = gameObject.scene.name;
            _record = TimeAttackRecord.Load(_trackScene);
            Current = this;
        }

        private void Start()
        {
            // 랩 수가 바뀐 트랙이면 이전 기록과 비교하지 않는다 (RaceProgress.Awake 뒤라 여기서 확인)
            if (_record != null && _record.LapCount != _player.TotalLaps) _record = null;

            _raceManager.OnRaceStarted += HandleRaceStarted;
            _raceManager.OnParticipantFinished += HandleParticipantFinished;

            if (TimeAttackSettings.ShowGhost && _record != null && _record.Samples.Count >= 2 && _ghostPrefab != null)
                Instantiate(_ghostPrefab).Play(_record.Samples, _raceManager, transform);
        }

        private void OnDestroy()
        {
            if (Current == this) Current = null;
            if (_raceManager == null) return;
            _raceManager.OnRaceStarted -= HandleRaceStarted;
            _raceManager.OnParticipantFinished -= HandleParticipantFinished;
        }

        private void LateUpdate()
        {
            if (!_recording) return;
            float time = _raceManager.RaceTime;
            if (time < _nextSampleTime) return;
            AddSample(time);
            _nextSampleTime = time + _sampleInterval;
        }

        // 플레이어가 아닌 참가자(AI 카트)와 아이템 박스. 대시 패드는 그대로 둔다
        private void RemoveRivals()
        {
            foreach (RaceProgress participant in FindObjectsByType<RaceProgress>(FindObjectsSortMode.None))
                if (participant != _player) participant.gameObject.SetActive(false);
            foreach (ItemBox box in FindObjectsByType<ItemBox>(FindObjectsSortMode.None))
                box.gameObject.SetActive(false);
        }

        private void HandleRaceStarted()
        {
            _samples.Clear();
            AddSample(0f);
            _nextSampleTime = _sampleInterval;
            _recording = true;
        }

        private void HandleParticipantFinished(IRaceParticipant participant, float totalTime)
        {
            if (!ReferenceEquals(participant, _player)) return;
            AddSample(totalTime);
            _recording = false;
            SaveIfBest(totalTime);
        }

        private void AddSample(float time) => _samples.Add(new GhostSample(time, transform.position, transform.rotation));

        // 전체 기록이 빠르면 고스트·구간 기록까지 교체, 랩 기록만 빠르면 최고 랩만 교체
        private void SaveIfBest(float totalTime)
        {
            IReadOnlyList<float> laps = _raceManager.GetResult(_player).LapTimes;
            float bestLap = float.MaxValue;
            foreach (float lap in laps) bestLap = Mathf.Min(bestLap, lap);

            IsNewRecord = _record == null || totalTime < _record.BestTotal;
            IsNewBestLap = _record == null || bestLap < _record.BestLap;
            if (!IsNewRecord && !IsNewBestLap) return;

            var saved = new TimeAttackRecord { LapCount = _player.TotalLaps };
            if (IsNewRecord)
            {
                saved.BestTotal = totalTime;
                float elapsed = 0f;
                foreach (float lap in laps) saved.Splits.Add(elapsed += lap);
                saved.Samples.AddRange(_samples);
            }
            else
            {
                saved.BestTotal = _record.BestTotal;
                saved.Splits.AddRange(_record.Splits);
                saved.Samples.AddRange(_record.Samples);
            }
            saved.BestLap = IsNewBestLap ? bestLap : _record.BestLap;
            saved.Save(_trackScene);
        }
    }
}
