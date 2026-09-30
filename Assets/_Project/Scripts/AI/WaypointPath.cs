using UnityEngine;

namespace VRKart.AI
{
    // AI가 따라가는 닫힌 경로. 자식 Transform들이 순서대로 웨이포인트이고, 마지막 점은 첫 점으로 이어진다.
    public sealed class WaypointPath : MonoBehaviour
    {
        private Vector3[] _points;

        public int Count => Points.Length;

        public Vector3 GetPoint(int index) => Points[Wrap(index)];

        public Vector3 GetDirection(int index)
        {
            Vector3 d = GetPoint(index + 1) - GetPoint(index);
            d.y = 0f;
            return d.normalized;
        }

        public int Wrap(int index) => ((index % Count) + Count) % Count;

        // 가장 가까운 구간(i → i+1)의 시작 인덱스
        public int ClosestSegment(Vector3 position)
        {
            int best = 0;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < Count; i++)
            {
                float d = DistanceToSegment(i, position, out _);
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = i;
                }
            }
            return best;
        }

        // 구간 i에 대한 수평 거리와, 구간 위 투영 비율 t(0~1 밖이면 구간 앞/뒤)
        public float DistanceToSegment(int index, Vector3 position, out float t)
        {
            Vector3 a = Flat(GetPoint(index));
            Vector3 ab = Flat(GetPoint(index + 1)) - a;
            Vector3 p = Flat(position);
            t = Vector3.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-4f);
            return (a + ab * Mathf.Clamp01(t) - p).magnitude;
        }

        // 구간 index의 비율 t 지점에서 경로를 따라 distance만큼 앞의 점
        public Vector3 PointAhead(int index, float t, float distance)
        {
            Vector3 point = Vector3.Lerp(GetPoint(index), GetPoint(index + 1), Mathf.Clamp01(t));
            for (int i = 0; i < Count && distance > 0f; i++)
            {
                Vector3 next = GetPoint(index + 1 + i);
                float length = Vector3.Distance(point, next);
                if (length >= distance) return point + (next - point) * (distance / Mathf.Max(length, 1e-4f));
                distance -= length;
                point = next;
            }
            return point;
        }

        private Vector3[] Points
        {
            get
            {
                if (_points == null || _points.Length != transform.childCount) Cache();
                return _points;
            }
        }

        private void Cache()
        {
            _points = new Vector3[transform.childCount];
            for (int i = 0; i < _points.Length; i++) _points[i] = transform.GetChild(i).position;
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            int n = transform.childCount;
            if (n < 2) return;
            Gizmos.color = new Color(1f, 0.55f, 0.1f, 0.9f);
            for (int i = 0; i < n; i++)
            {
                Vector3 a = transform.GetChild(i).position + Vector3.up * 0.3f;
                Vector3 b = transform.GetChild((i + 1) % n).position + Vector3.up * 0.3f;
                Gizmos.DrawLine(a, b);
                if (i % 5 == 0) Gizmos.DrawSphere(a, 0.3f);
            }
        }
#endif
    }
}
