using System;

namespace VRKart.Race
{
    public enum CheckpointResult
    {
        Ignored,
        Passed,
        RaceStarted,
        LapCompleted,
        Finished,
    }

    // 체크포인트 순서와 랩 수만 판정한다. 0번 = 결승선.
    // 카트는 결승선 뒤에서 출발하므로 결승선을 처음 넘을 때는 랩으로 세지 않는다.
    public sealed class LapCounter
    {
        private readonly int _checkpointCount;
        private readonly int _totalLaps;

        public LapCounter(int checkpointCount, int totalLaps)
        {
            if (checkpointCount < 2) throw new ArgumentOutOfRangeException(nameof(checkpointCount), "체크포인트는 결승선 포함 2개 이상 필요");
            if (totalLaps < 1) throw new ArgumentOutOfRangeException(nameof(totalLaps));
            _checkpointCount = checkpointCount;
            _totalLaps = totalLaps;
        }

        public int NextIndex { get; private set; }
        public int CompletedLaps { get; private set; }
        public int CheckpointsPassed { get; private set; }
        public bool HasStarted { get; private set; }
        public bool IsFinished { get; private set; }
        public int TotalLaps => _totalLaps;
        public int CurrentLap => Math.Min(CompletedLaps + 1, _totalLaps);

        public CheckpointResult Pass(int index)
        {
            if (IsFinished || index != NextIndex) return CheckpointResult.Ignored;

            NextIndex = (index + 1) % _checkpointCount;

            if (index != 0)
            {
                CheckpointsPassed++;
                return CheckpointResult.Passed;
            }

            if (!HasStarted)
            {
                HasStarted = true;
                return CheckpointResult.RaceStarted;
            }

            CheckpointsPassed++;
            CompletedLaps++;
            if (CompletedLaps >= _totalLaps)
            {
                IsFinished = true;
                return CheckpointResult.Finished;
            }
            return CheckpointResult.LapCompleted;
        }
    }
}
