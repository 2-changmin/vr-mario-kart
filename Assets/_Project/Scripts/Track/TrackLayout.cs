using System;
using System.Collections.Generic;
using UnityEngine;

namespace VRKart.Track
{
    // 트랙 중심선. 이 오브젝트의 위치·방향에서 출발해 조각(직선/원호)을 순서대로 이어 붙인 닫힌 코스.
    // 도로 메시(TrackMeshBuilder), 체크포인트·AI 경로(에디터 버튼)가 모두 이 중심선에서 만들어진다.
    public sealed class TrackLayout : MonoBehaviour
    {
        [Serializable]
        public struct Piece
        {
            [Tooltip("꺾는 각도(°). 0 = 직선, + = 오른쪽, - = 왼쪽")]
            public float Turn;
            [Tooltip("직선 길이(m). 직선일 때만 사용")]
            public float Length;
            [Tooltip("원호 반지름(m, 도로 중심 기준). 커브일 때만 사용")]
            public float Radius;

            public float ArcLength => Turn == 0f ? Length : Mathf.Abs(Turn) * Mathf.Deg2Rad * Radius;
        }

        // 중심선 위 한 점. 로컬이 아니라 월드 좌표.
        public struct Sample
        {
            public Vector3 Position;
            public Vector3 Forward;
            public float Distance;      // 출발점에서 중심선을 따라 잰 거리 (m)
            public float Curvature;     // 1/반지름, + = 오른쪽 커브, 0 = 직선

            public Vector3 Right => Vector3.Cross(Vector3.up, Forward);
        }

        private const float ClosingTolerance = 1f;

        [SerializeField] private Piece[] _pieces = Array.Empty<Piece>();
        [SerializeField, Min(0.25f)] private float _sampleSpacing = 1f;
        [Tooltip("결승선(체크포인트 0번)까지의 거리 (m)")]
        [SerializeField, Min(0f)] private float _finishDistance;

        private List<Sample> _samples;
        private float _length;

        public event Action Changed;

        public float Length { get { EnsureBuilt(); return _length; } }
        public float FinishDistance => _finishDistance;
        public IReadOnlyList<Sample> Samples { get { EnsureBuilt(); return _samples; } }

        // 중심선을 따라 distance 지점 (한 바퀴를 넘으면 감싼다)
        public Sample Evaluate(float distance)
        {
            EnsureBuilt();
            if (_samples.Count < 2) return new Sample { Position = transform.position, Forward = transform.forward };
            distance = Mathf.Repeat(distance, _length);
            int i = Mathf.Clamp(Mathf.FloorToInt(distance / _sampleSpacing), 0, _samples.Count - 2);
            while (i < _samples.Count - 2 && _samples[i + 1].Distance < distance) i++;
            Sample a = _samples[i];
            Sample b = _samples[i + 1];
            float t = Mathf.InverseLerp(a.Distance, b.Distance, distance);
            return new Sample
            {
                Position = Vector3.Lerp(a.Position, b.Position, t),
                Forward = Vector3.Slerp(a.Forward, b.Forward, t).normalized,
                Distance = distance,
                Curvature = Mathf.Lerp(a.Curvature, b.Curvature, t),
            };
        }

        private void OnValidate()
        {
            _samples = null;
            Changed?.Invoke();
        }

        private void EnsureBuilt()
        {
            if (_samples != null && !transform.hasChanged) return;
            transform.hasChanged = false;
            Build();
        }

        // 거북이 그래픽처럼 조각을 따라 걸으며 점을 찍는다. 끝점이 시작점과 조금 어긋나면 그 오차를 전체에 나눠 닫는다.
        private void Build()
        {
            _samples = new List<Sample>();
            Vector3 position = Vector3.zero;
            float heading = 0f;     // +Z에서 시계 방향 (rad)
            float distance = 0f;
            _samples.Add(new Sample { Position = position, Forward = Direction(heading), Distance = 0f });

            foreach (Piece piece in _pieces)
            {
                float arc = piece.ArcLength;
                if (arc <= 0f) continue;
                int steps = Mathf.Max(1, Mathf.RoundToInt(arc / _sampleSpacing));
                float curvature = piece.Turn == 0f ? 0f : Mathf.Sign(piece.Turn) / Mathf.Max(piece.Radius, 0.01f);
                float turnPerStep = piece.Turn * Mathf.Deg2Rad / steps;

                for (int s = 0; s < steps; s++)
                {
                    if (piece.Turn == 0f)
                    {
                        position += Direction(heading) * (arc / steps);
                    }
                    else
                    {
                        // 원의 중심을 기준으로 정확히 돌려서 오차가 쌓이지 않게
                        float side = Mathf.Sign(piece.Turn);
                        Vector3 center = position + Right(heading) * (side * piece.Radius);
                        heading += turnPerStep;
                        position = center - Right(heading) * (side * piece.Radius);
                    }
                    distance += arc / steps;
                    _samples.Add(new Sample { Position = position, Forward = Direction(heading), Distance = distance, Curvature = curvature });
                }
            }

            _length = distance;
            Vector3 closingError = position;    // 시작점은 원점
            if (_pieces.Length > 0 && closingError.magnitude > ClosingTolerance)
                Debug.LogWarning($"[TrackLayout] 코스가 닫히지 않습니다: 끝점이 시작점에서 {closingError.magnitude:F1}m 떨어져 있습니다. 조각 길이/각도를 맞추세요.", this);

            for (int i = 0; i < _samples.Count; i++)
            {
                Sample sample = _samples[i];
                Vector3 local = sample.Position - closingError * (sample.Distance / Mathf.Max(_length, 0.01f));
                sample.Position = transform.TransformPoint(local);
                sample.Forward = transform.TransformDirection(sample.Forward);
                _samples[i] = sample;
            }
        }

        private static Vector3 Direction(float heading) => new Vector3(Mathf.Sin(heading), 0f, Mathf.Cos(heading));
        private static Vector3 Right(float heading) => new Vector3(Mathf.Cos(heading), 0f, -Mathf.Sin(heading));

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            IReadOnlyList<Sample> samples = Samples;
            Gizmos.color = Color.cyan;
            for (int i = 1; i < samples.Count; i++)
                Gizmos.DrawLine(samples[i - 1].Position + Vector3.up * 0.2f, samples[i].Position + Vector3.up * 0.2f);
            Gizmos.color = Color.white;
            Sample finish = Evaluate(_finishDistance);
            Gizmos.DrawLine(finish.Position - finish.Right * 6f + Vector3.up * 0.2f, finish.Position + finish.Right * 6f + Vector3.up * 0.2f);
        }
#endif
    }
}
