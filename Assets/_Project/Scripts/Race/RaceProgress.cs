using System;
using UnityEngine;
using VRKart.Core;

namespace VRKart.Race
{
    // 카트 루트에 부착. 체크포인트/랩 진행도를 추적한다.
    public sealed class RaceProgress : MonoBehaviour, IRaceParticipant
    {
        // 물리 스텝(50Hz)과 화면 프레임(72Hz+)이 달라 프레임마다 이동량이 0인 경우가 섞이므로 구간 평균으로 판정한다.
        private const float WrongWaySampleInterval = 0.2f;

        [SerializeField] private RaceTrack _track;

        [Header("역주행 감지")]
        [SerializeField, Min(0f)] private float _wrongWayDelay = 1f;
        [SerializeField, Min(0f)] private float _wrongWayMinSpeed = 2f;

        private LapCounter _lap;
        private Pose _lastCheckpointPose;
        private int _rank = 1;
        private bool _isWrongWay;
        private float _wrongWayTimer;
        private Vector3 _sampleStart;
        private float _sampleElapsed;

        public event Action<RaceProgress, Checkpoint> CheckpointPassed;
        public event Action<RaceProgress, int> LapCompleted;
        public event Action<RaceProgress> Finished;
        public event Action<RaceProgress, bool> WrongWayChanged;

        public int CurrentLap => _lap.CurrentLap;
        public int CompletedLaps => _lap.CompletedLaps;
        public int TotalLaps => _lap.TotalLaps;
        public int Rank => _rank;
        public bool HasStarted => _lap.HasStarted;
        public bool IsFinished => _lap.IsFinished;
        public bool IsWrongWay => _isWrongWay;
        public Pose LastCheckpointPose => _lastCheckpointPose;

        // 순위 계산(#11)용: 지나온 체크포인트 수와 다음 체크포인트
        public int CheckpointsPassed => _lap.CheckpointsPassed;
        public Checkpoint NextCheckpoint => _track.GetCheckpoint(_lap.NextIndex);

        public void SetRank(int rank) => _rank = rank;

        private void Awake()
        {
            if (_track == null) _track = FindAnyObjectByType<RaceTrack>();
            if (_track == null)
            {
                Debug.LogError("[RaceProgress] 씬에 RaceTrack이 없습니다.", this);
                enabled = false;
                return;
            }

            _lap = new LapCounter(_track.CheckpointCount, _track.TotalLaps);
            _lastCheckpointPose = new Pose(transform.position, transform.rotation);
            _sampleStart = transform.position;
        }

        internal void NotifyCheckpoint(Checkpoint checkpoint)
        {
            if (!enabled) return;

            CheckpointResult result = _lap.Pass(checkpoint.Index);
            if (result == CheckpointResult.Ignored) return;

            _lastCheckpointPose = checkpoint.RespawnPose;
            CheckpointPassed?.Invoke(this, checkpoint);

            if (result == CheckpointResult.LapCompleted || result == CheckpointResult.Finished)
                LapCompleted?.Invoke(this, _lap.CompletedLaps);
            if (result == CheckpointResult.Finished)
            {
                SetWrongWay(false);
                Finished?.Invoke(this);
            }
        }

        private void Update()
        {
            if (IsFinished) return;

            _sampleElapsed += Time.deltaTime;
            if (_sampleElapsed < WrongWaySampleInterval) return;

            Vector3 position = transform.position;
            Vector3 velocity = Vector3.ProjectOnPlane(position - _sampleStart, Vector3.up) / _sampleElapsed;
            float elapsed = _sampleElapsed;
            _sampleStart = position;
            _sampleElapsed = 0f;

            float speed = velocity.magnitude;
            bool movingBackward = false;
            if (speed >= _wrongWayMinSpeed)
                movingBackward = Vector3.Dot(velocity / speed, _track.DirectionAt(position)) < -0.5f;

            _wrongWayTimer = movingBackward ? _wrongWayTimer + elapsed : 0f;
            if (!_isWrongWay && _wrongWayTimer >= _wrongWayDelay) SetWrongWay(true);
            else if (_isWrongWay && !movingBackward && speed >= _wrongWayMinSpeed) SetWrongWay(false);
        }

        private void SetWrongWay(bool value)
        {
            if (_isWrongWay == value) return;
            _isWrongWay = value;
            WrongWayChanged?.Invoke(this, value);
        }
    }
}
