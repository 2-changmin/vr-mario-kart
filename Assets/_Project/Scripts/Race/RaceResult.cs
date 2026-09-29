using System.Collections.Generic;

namespace VRKart.Race
{
    // 참가자 한 명의 기록. 결과 화면(#12)과 HUD(#13)에서 읽는다.
    public sealed class RaceResult
    {
        private readonly List<float> _lapTimes = new List<float>();

        internal RaceResult(RaceProgress participant, bool isPlayer)
        {
            Participant = participant;
            IsPlayer = isPlayer;
        }

        public RaceProgress Participant { get; }
        public bool IsPlayer { get; }
        public IReadOnlyList<float> LapTimes => _lapTimes;
        public bool IsFinished { get; internal set; }
        public float TotalTime { get; internal set; }       // 완주한 경우만 의미 있음 (초)
        public int FinishOrder { get; internal set; }       // 완주 순서, 미완주는 0
        public int Position { get; internal set; }          // GetResults() 시점의 순위 (1부터)

        internal double LapStartTime { get; set; }

        internal void AddLap(float lapTime) => _lapTimes.Add(lapTime);
    }
}
