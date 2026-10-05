using System;
using System.Collections.Generic;
using UnityEngine;

namespace VRKart.Track
{
    // 트랙 중심선. 이 오브젝트의 위치·방향에서 출발해 조각(직선/원호)을 순서대로 이어 붙인 닫힌 코스.
    // 조각마다 오르내리는 높이(Rise)를 줄 수 있고, 높이는 Height Smoothing 거리만큼 부드럽게 이어진다(#50).
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
            [Tooltip("이 조각 동안 오르내리는 높이(m). + = 오르막. 한 바퀴 합은 0이어야 함")]
            public float Rise;

            public float ArcLength => Turn == 0f ? Length : Mathf.Abs(Turn) * Mathf.Deg2Rad * Radius;
        }

        // 중심선 위 한 점. 로컬이 아니라 월드 좌표.
        public struct Sample
        {
            public Vector3 Position;
            public Vector3 Forward;
            public float Distance;      // 출발점에서 중심선을 따라 잰 거리 (m)
            public float Curvature;     // 1/반지름, + = 오른쪽 커브, 0 = 직선
            public float Slope;         // 경사 (높이 변화 / 수평 거리), + = 오르막

            public Vector3 Right => Vector3.Cross(Vector3.up, Forward);
        }

        private const float ClosingTolerance = 1f;
        private const float HeightClosingTolerance = 0.5f;

        [SerializeField] private Piece[] _pieces = Array.Empty<Piece>();
        [SerializeField, Min(0.25f)] private float _sampleSpacing = 1f;
        [Tooltip("결승선(체크포인트 0번)까지의 거리 (m)")]
        [SerializeField, Min(0f)] private float _finishDistance;
        [Tooltip("높이 변화를 이 거리(m)에 걸쳐 부드럽게 (언덕 꼭대기·골짜기가 완만해짐)")]
        [SerializeField, Min(0f)] private float _heightSmoothing = 30f;

        private List<Sample> _samples;
        private float _length;
        private float _maxGrade;
        private float _minCrestRadius;

        public event Action Changed;

        public float Length { get { EnsureBuilt(); return _length; } }
        public float MaxGrade { get { EnsureBuilt(); return _maxGrade; } }                 // 가장 가파른 경사 (0.1 = 10%)
        public float MinCrestRadius { get { EnsureBuilt(); return _minCrestRadius; } }     // 언덕 꼭대기의 가장 작은 곡률 반지름 (m), 없으면 무한
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
                Slope = Mathf.Lerp(a.Slope, b.Slope, t),
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
            float height = 0f;
            var heights = new List<float> { 0f };
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
                    height += piece.Rise / steps;
                    heights.Add(height);
                    _samples.Add(new Sample { Position = position, Forward = Direction(heading), Distance = distance, Curvature = curvature });
                }
            }

            _length = distance;
            Vector3 closingError = position;    // 시작점은 원점
            if (_pieces.Length > 0 && closingError.magnitude > ClosingTolerance)
                Debug.LogWarning($"[TrackLayout] 코스가 닫히지 않습니다: 끝점이 시작점에서 {closingError.magnitude:F1}m 떨어져 있습니다. 조각 길이/각도를 맞추세요.", this);

            if (_pieces.Length > 0 && Mathf.Abs(height) > HeightClosingTolerance)
                Debug.LogWarning($"[TrackLayout] 한 바퀴 높이 합(Rise 합)이 0이 아닙니다: {height:F1}m. 오르막과 내리막을 맞추세요.", this);
            float[] smooth = SmoothHeights(heights, height);

            _maxGrade = 0f;
            _minCrestRadius = float.PositiveInfinity;
            for (int i = 0; i < _samples.Count; i++)
            {
                Sample sample = _samples[i];
                int prev = Mathf.Max(i - 1, 0), next = Mathf.Min(i + 1, _samples.Count - 1);
                float ds = Mathf.Max(_samples[next].Distance - _samples[prev].Distance, 0.01f);
                sample.Slope = (smooth[next] - smooth[prev]) / ds;
                _maxGrade = Mathf.Max(_maxGrade, Mathf.Abs(sample.Slope));
                if (i > 0 && i < _samples.Count - 1)
                {
                    float h = ds * 0.5f;
                    float bend = (smooth[next] - 2f * smooth[i] + smooth[prev]) / (h * h);   // 음수 = 볼록(꼭대기)
                    if (bend < -1e-5f) _minCrestRadius = Mathf.Min(_minCrestRadius, -1f / bend);
                }
                sample.Position.y = smooth[i];
                Vector3 local = sample.Position - closingError * (sample.Distance / Mathf.Max(_length, 0.01f));
                sample.Position = transform.TransformPoint(local);
                sample.Forward = transform.TransformDirection(sample.Forward);
                _samples[i] = sample;
            }
        }

        // 높이 오차를 전체에 나눠 닫은 뒤, 닫힌 고리로 보고 이동 평균을 두 번(가우스에 가깝게) 적용
        private float[] SmoothHeights(List<float> raw, float closingError)
        {
            int count = raw.Count;
            int unique = Mathf.Max(count - 1, 1);   // 마지막 점 = 첫 점
            var h = new float[unique];
            for (int i = 0; i < unique; i++) h[i] = raw[i] - closingError * i / unique;
            int half = Mathf.RoundToInt(_heightSmoothing / _sampleSpacing * 0.5f);
            for (int pass = 0; pass < 2 && half > 0; pass++)
            {
                var result = new float[unique];
                float sum = 0f;
                for (int k = -half; k <= half; k++) sum += h[((k % unique) + unique) % unique];
                for (int i = 0; i < unique; i++)
                {
                    result[i] = sum / (2 * half + 1);
                    sum += h[(i + half + 1) % unique] - h[((i - half) % unique + unique) % unique];
                }
                h = result;
            }
            var output = new float[count];
            for (int i = 0; i < count; i++) output[i] = h[i % unique];
            return output;
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
