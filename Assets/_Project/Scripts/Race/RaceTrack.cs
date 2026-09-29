using System;
using UnityEngine;

namespace VRKart.Race
{
    // 씬에 1개. 자식의 Checkpoint들을 순번대로 모아 둔다.
    public sealed class RaceTrack : MonoBehaviour
    {
        [SerializeField, Min(1)] private int _totalLaps = 3;

        private Checkpoint[] _checkpoints;

        public int TotalLaps => _totalLaps;
        public int CheckpointCount => Checkpoints.Length;

        public Checkpoint GetCheckpoint(int index) => Checkpoints[index];

        // 위치에서 가장 가까운 체크포인트 구간(i → i+1)의 수평 진행 방향
        public Vector3 DirectionAt(Vector3 position)
        {
            Checkpoint[] checkpoints = Checkpoints;
            Vector3 p = Flat(position);
            Vector3 best = Vector3.forward;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < checkpoints.Length; i++)
            {
                Vector3 a = Flat(checkpoints[i].transform.position);
                Vector3 ab = Flat(checkpoints[(i + 1) % checkpoints.Length].transform.position) - a;
                float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-4f));
                float distance = (a + ab * t - p).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = ab;
                }
            }
            return best.normalized;
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        private Checkpoint[] Checkpoints
        {
            get
            {
                if (_checkpoints == null) Collect();
                return _checkpoints;
            }
        }

        private void Collect()
        {
            _checkpoints = GetComponentsInChildren<Checkpoint>();
            Array.Sort(_checkpoints, (a, b) => a.Index.CompareTo(b.Index));

            for (int i = 0; i < _checkpoints.Length; i++)
            {
                if (_checkpoints[i].Index != i)
                {
                    Debug.LogError($"[RaceTrack] 체크포인트 순번이 0부터 연속이어야 합니다. {i}번 자리에 {_checkpoints[i].Index}번({_checkpoints[i].name})이 있습니다.", this);
                    break;
                }
            }
        }
    }
}
