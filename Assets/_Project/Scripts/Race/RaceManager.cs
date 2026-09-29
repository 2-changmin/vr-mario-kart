using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VRKart.Core;

namespace VRKart.Race
{
    public enum RaceState
    {
        Waiting,
        Countdown,
        Racing,
        Finished,
    }

    // 씬에 1개. 레이스 한 판의 흐름(대기 → 카운트다운 → 진행 → 종료)과 기록을 관리한다.
    // 시간은 Time.timeAsDouble(게임 시간) 기준이라 일시정지(timeScale 0) 동안은 기록에 포함되지 않는다.
    public sealed class RaceManager : MonoBehaviour
    {
        [SerializeField] private RaceProgress _player;
        [SerializeField] private bool _autoStart = true;
        [SerializeField, Min(0f)] private float _startDelay = 1f;
        [SerializeField, Min(1)] private int _countdownFrom = 3;
        [SerializeField, Min(0.1f)] private float _countdownInterval = 1f;

        private readonly List<RaceResult> _results = new List<RaceResult>();
        private readonly Dictionary<RaceProgress, RaceResult> _resultByParticipant = new Dictionary<RaceProgress, RaceResult>();
        private readonly List<IKart> _karts = new List<IKart>();
        private double _raceStartTime;
        private int _finishedCount;
        private float _timeScaleBeforePause = 1f;

        public event Action<RaceState> StateChanged;
        public event Action<int> OnCountdownTick;                            // 3, 2, 1, 0(GO)
        public event Action OnRaceStarted;                                   // GO
        public event Action<IRaceParticipant, int, float> OnLapCompleted;    // 참가자, 완료한 랩(1부터), 랩 타임(초)
        public event Action<IRaceParticipant, float> OnParticipantFinished;  // 참가자, 전체 기록(초)
        public event Action OnRaceFinished;                                  // 플레이어 완주 → 결과 화면
        public event Action<bool> PauseChanged;

        public RaceState State { get; private set; } = RaceState.Waiting;
        public bool IsPaused { get; private set; }
        public RaceProgress Player => _player;

        public float RaceTime => State == RaceState.Racing || State == RaceState.Finished
            ? (float)(Time.timeAsDouble - _raceStartTime)
            : 0f;

        public RaceResult GetResult(RaceProgress participant) =>
            _resultByParticipant.TryGetValue(participant, out RaceResult result) ? result : null;

        public float GetCurrentLapTime(RaceProgress participant)
        {
            RaceResult result = GetResult(participant);
            if (result == null || State != RaceState.Racing && State != RaceState.Finished || result.IsFinished) return 0f;
            return (float)(Time.timeAsDouble - result.LapStartTime);
        }

        // 완주자는 완주 순서, 미완주자는 지나온 체크포인트 수 → 다음 체크포인트까지 거리 순
        public IReadOnlyList<RaceResult> GetResults()
        {
            var sorted = new List<RaceResult>(_results);
            sorted.Sort(CompareResults);
            for (int i = 0; i < sorted.Count; i++) sorted[i].Position = i + 1;
            return sorted;
        }

        public void StartRace()
        {
            if (State != RaceState.Waiting) return;
            StartCoroutine(RunCountdown());
        }

        public void Pause()
        {
            if (IsPaused || State != RaceState.Countdown && State != RaceState.Racing) return;
            _timeScaleBeforePause = Time.timeScale;
            Time.timeScale = 0f;
            IsPaused = true;
            PauseChanged?.Invoke(true);
        }

        public void Resume()
        {
            if (!IsPaused) return;
            Time.timeScale = _timeScaleBeforePause;
            IsPaused = false;
            PauseChanged?.Invoke(false);
        }

        public void TogglePause()
        {
            if (IsPaused) Resume();
            else Pause();
        }

        public void Restart() => SceneLoader.ReloadCurrent();

        public void ExitToMenu() => SceneLoader.LoadMainMenu();

        private void Awake()
        {
            foreach (RaceProgress participant in FindObjectsByType<RaceProgress>(FindObjectsSortMode.InstanceID))
            {
                var result = new RaceResult(participant, participant == _player);
                _results.Add(result);
                _resultByParticipant.Add(participant, result);
                participant.LapCompleted += HandleLapCompleted;
                participant.Finished += HandleFinished;

                IKart kart = participant.GetComponent<IKart>();
                if (kart != null) _karts.Add(kart);
            }

            if (_player == null && _results.Count == 1)
            {
                _player = _results[0].Participant;
                _results[0] = new RaceResult(_player, true);
                _resultByParticipant[_player] = _results[0];
            }
            if (_player == null) Debug.LogWarning("[RaceManager] 플레이어 RaceProgress가 지정되지 않아 OnRaceFinished가 발생하지 않습니다.", this);
        }

        private void Start()
        {
            SetKartsEnabled(false);
            if (_autoStart) StartRace();
        }

        private void OnDestroy()
        {
            if (IsPaused) Time.timeScale = 1f;
            foreach (RaceResult result in _results)
            {
                if (result.Participant == null) continue;
                result.Participant.LapCompleted -= HandleLapCompleted;
                result.Participant.Finished -= HandleFinished;
            }
        }

        private IEnumerator RunCountdown()
        {
            if (_startDelay > 0f) yield return new WaitForSeconds(_startDelay);

            SetState(RaceState.Countdown);
            for (int n = _countdownFrom; n > 0; n--)
            {
                OnCountdownTick?.Invoke(n);
                yield return new WaitForSeconds(_countdownInterval);
            }

            _raceStartTime = Time.timeAsDouble;
            foreach (RaceResult result in _results) result.LapStartTime = _raceStartTime;

            SetState(RaceState.Racing);
            SetKartsEnabled(true);
            OnCountdownTick?.Invoke(0);
            OnRaceStarted?.Invoke();
        }

        private void HandleLapCompleted(RaceProgress participant, int lap)
        {
            if (State != RaceState.Racing && State != RaceState.Finished) return;

            RaceResult result = _resultByParticipant[participant];
            double now = Time.timeAsDouble;
            float lapTime = (float)(now - result.LapStartTime);
            result.LapStartTime = now;
            result.AddLap(lapTime);
            OnLapCompleted?.Invoke(participant, lap, lapTime);
        }

        private void HandleFinished(RaceProgress participant)
        {
            if (State != RaceState.Racing && State != RaceState.Finished) return;

            RaceResult result = _resultByParticipant[participant];
            result.IsFinished = true;
            result.TotalTime = (float)(Time.timeAsDouble - _raceStartTime);
            result.FinishOrder = ++_finishedCount;
            participant.SetRank(result.FinishOrder);
            OnParticipantFinished?.Invoke(participant, result.TotalTime);

            if (participant == _player)
            {
                SetState(RaceState.Finished);
                OnRaceFinished?.Invoke();
            }
        }

        private void SetKartsEnabled(bool enabled)
        {
            foreach (IKart kart in _karts) kart.SetControlEnabled(enabled);
        }

        private void SetState(RaceState state)
        {
            if (State == state) return;
            State = state;
            StateChanged?.Invoke(state);
        }

        private static int CompareResults(RaceResult a, RaceResult b)
        {
            if (a.IsFinished != b.IsFinished) return a.IsFinished ? -1 : 1;
            if (a.IsFinished) return a.FinishOrder.CompareTo(b.FinishOrder);

            int passed = b.Participant.CheckpointsPassed.CompareTo(a.Participant.CheckpointsPassed);
            if (passed != 0) return passed;
            return DistanceToNext(a).CompareTo(DistanceToNext(b));
        }

        private static float DistanceToNext(RaceResult result) =>
            Vector3.Distance(result.Participant.transform.position, result.Participant.NextCheckpoint.transform.position);
    }
}
