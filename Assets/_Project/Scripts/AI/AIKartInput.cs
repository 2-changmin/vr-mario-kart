using System.Collections.Generic;
using UnityEngine;
using VRKart.Core;
using VRKart.Items;
using VRKart.Race;

namespace VRKart.AI
{
    // AI 카트 입력. 경로를 따라 앞쪽 지점을 보고 조향하고, 앞의 커브가 급하면 감속한다.
    // KartController가 같은 게임오브젝트에서 IKartInput으로 읽는다. RaceProgress도 같은 오브젝트에 있어야 한다.
    public sealed class AIKartInput : MonoBehaviour, IKartInput
    {
        [SerializeField] private WaypointPath _path;

        [Header("실력")]
        [SerializeField, Range(0.5f, 1f)] private float _speedFactor = 0.95f;   // 최고 속도 배율
        [SerializeField] private float _lineOffset;                               // 경로 중심에서 오른쪽(+)/왼쪽(-)으로 달리는 거리 (m)
        [SerializeField, Min(0f)] private float _lineWobble = 0.6f;              // 라인 오차: 천천히 흔들리는 좌우 편차 (m)

        [Header("조향")]
        [SerializeField, Min(1f)] private float _lookAheadBase = 5f;             // 앞을 보는 거리 (m) = 기본 + 속도 × 계수
        [SerializeField, Min(0f)] private float _lookAheadPerSpeed = 0.35f;
        [SerializeField, Range(5f, 90f)] private float _fullSteerAngle = 30f;    // 목표가 이 각도 이상 옆이면 핸들을 끝까지

        [Header("코너 감속")]
        [SerializeField, Min(1f)] private float _cornerLookAhead = 25f;          // 이 거리 안의 커브를 보고 미리 감속 (m)
        [SerializeField, Range(0.2f, 1f)] private float _minCornerSpeedFactor = 0.55f;
        [SerializeField, Range(10f, 180f)] private float _sharpCornerAngle = 90f; // 이 각도 이상 꺾이면 최저 속도

        [Header("끼임")]
        [SerializeField, Min(0.1f)] private float _stuckSpeed = 1f;
        [SerializeField, Min(0.1f)] private float _stuckTime = 1.5f;
        [SerializeField, Min(0.1f)] private float _reverseTime = 1.2f;
        [SerializeField, Min(0)] private int _reverseTriesBeforeRespawn = 2;

        [Header("아이템 (#5)")]
        [Tooltip("아이템을 받은 뒤 이 시간(초) 사이 무작위로 기다렸다가 쓸 기회를 본다")]
        [SerializeField] private Vector2 _itemThinkTime = new Vector2(1f, 3f);
        [SerializeField, Min(1f)] private float _itemGiveUpTime = 8f;      // 기회가 안 와도 이 시간이 지나면 그냥 쓴다
        [SerializeField, Min(5f)] private float _shellRange = 40f;         // 앞 카트가 이 거리·각도 안이면 쉘
        [SerializeField, Range(1f, 30f)] private float _shellAngle = 8f;
        [SerializeField, Min(2f)] private float _bananaRange = 20f;        // 뒤 카트가 이 거리 안이면 바나나
        [SerializeField, Range(5f, 90f)] private float _boosterMaxTurn = 15f; // 앞 커브가 이 각도 이하(직선)면 부스터

        private IKart _kart;
        private RaceProgress _progress;
        private RaceManager _raceManager;
        private int _segment;
        private float _wobbleSeed;
        private float _stuckTimer;
        private float _reverseTimer;
        private float _reverseSteer;
        private int _reverseTries;
        private ItemHolder _items;
        private readonly List<Transform> _others = new List<Transform>();
        private float _itemHeldTime;
        private float _itemThinkDelay;
        private int _useItemFrame = -10;

        public float Throttle { get; private set; }
        public float Brake { get; private set; }
        public float Steer { get; private set; }
        public bool Drift => false;
        // 한 번 누른 것처럼 두 프레임 동안 true (ItemHolder와 Update 순서가 정해져 있지 않아서). 쓰면 아이템이 None이 되므로 두 번 쓰이지 않음
        public bool UseItem => Time.frameCount - _useItemFrame <= 1;

        private void Awake()
        {
            _kart = GetComponent<IKart>();
            _progress = GetComponent<RaceProgress>();
            _raceManager = FindAnyObjectByType<RaceManager>();
            if (_path == null) _path = FindAnyObjectByType<WaypointPath>();
            _wobbleSeed = Random.value * 100f;
            _items = GetComponent<ItemHolder>();
            if (_items != null) _items.ItemChanged += HandleItemChanged;
        }

        private void OnDestroy()
        {
            if (_items != null) _items.ItemChanged -= HandleItemChanged;
        }

        private void Start()
        {
            if (_path != null) _segment = _path.ClosestSegment(transform.position);
            foreach (var other in FindObjectsByType<RaceProgress>(FindObjectsSortMode.None))
                if (other.gameObject != gameObject) _others.Add(other.transform);
        }

        private void Update()
        {
            if (_path == null || _kart == null) return;
            float dt = Time.deltaTime;
            Vector3 position = transform.position;
            float t = TrackSegment(position);
            float speed = Mathf.Abs(_kart.CurrentSpeed);

            if (_reverseTimer > 0f)
            {
                _reverseTimer -= dt;
                Throttle = 0f;
                Brake = 1f;
                Steer = _reverseSteer;
                return;
            }

            // 조향: 경로를 따라 앞쪽 지점 + 좌우 라인
            float lookAhead = _lookAheadBase + speed * _lookAheadPerSpeed;
            Vector3 target = _path.PointAhead(_segment, t, lookAhead);
            Vector3 right = Vector3.Cross(Vector3.up, _path.GetDirection(_segment));
            float wobble = (Mathf.PerlinNoise(Time.time * 0.15f, _wobbleSeed) * 2f - 1f) * _lineWobble;
            target += right * (_lineOffset + wobble);
            float desiredSteer = SteerTowards(target);
            Steer = desiredSteer;

            // 속도: 앞의 커브가 급할수록 목표 속도를 낮춤
            float corner = UpcomingTurnAngle(t);
            float targetSpeed = _kart.MaxSpeed * _speedFactor * Mathf.Lerp(1f, _minCornerSpeedFactor, Mathf.Clamp01(corner / _sharpCornerAngle));
            if (_progress != null && _progress.IsFinished) targetSpeed *= 0.6f;

            if (speed < targetSpeed - 0.5f)
            {
                Throttle = 1f;
                Brake = 0f;
            }
            else if (speed > targetSpeed + 1.5f)
            {
                Throttle = 0f;
                Brake = Mathf.Clamp01((speed - targetSpeed) / 5f);
            }
            else
            {
                Throttle = 0f;
                Brake = 0f;
            }

            UpdateStuck(speed, desiredSteer, dt);
            UpdateItem(corner, dt);
        }

        private void HandleItemChanged(ItemType item)
        {
            _itemHeldTime = 0f;
            _itemThinkDelay = Random.Range(_itemThinkTime.x, _itemThinkTime.y);
        }

        // 들고 있는 아이템을 쓸 기회인지 판단: 부스터 = 직선, 쉘 = 바로 앞에 카트, 바나나 = 바로 뒤에 카트. 오래 들고 있으면 그냥 쓴다.
        private void UpdateItem(float upcomingTurn, float dt)
        {
            if (_items == null || _items.CurrentItem == ItemType.None) return;
            bool racing = _raceManager != null && _raceManager.State == RaceState.Racing && !_raceManager.IsPaused;
            if (!racing) return;
            _itemHeldTime += dt;
            if (_itemHeldTime < _itemThinkDelay) return;

            bool use = _itemHeldTime >= _itemGiveUpTime || _items.CurrentItem switch
            {
                ItemType.Booster => upcomingTurn <= _boosterMaxTurn,
                ItemType.Shell => KartInCone(transform.forward, _shellRange, _shellAngle),
                ItemType.Banana => KartInCone(-transform.forward, _bananaRange, 35f),
                _ => false,
            };
            if (use) _useItemFrame = Time.frameCount;
        }

        private bool KartInCone(Vector3 direction, float range, float angle)
        {
            foreach (Transform other in _others)
            {
                if (other == null) continue;
                Vector3 to = other.position - transform.position;
                to.y = 0f;
                if (to.sqrMagnitude > range * range || to.sqrMagnitude < 1f) continue;
                if (Vector3.Angle(direction, to) <= angle) return true;
            }
            return false;
        }

        // 현재 구간을 앞으로 갱신하고 구간 위 비율 t를 돌려준다. 경로에서 많이 벗어났으면(리스폰 등) 가장 가까운 구간을 다시 찾는다.
        private float TrackSegment(Vector3 position)
        {
            float distance = _path.DistanceToSegment(_segment, position, out float t);
            if (distance > 15f)
            {
                _segment = _path.ClosestSegment(position);
                _path.DistanceToSegment(_segment, position, out t);
                return t;
            }
            for (int i = 0; i < 10 && t > 1f; i++)
            {
                _segment = _path.Wrap(_segment + 1);
                _path.DistanceToSegment(_segment, position, out t);
            }
            return t;
        }

        private float SteerTowards(Vector3 target)
        {
            Vector3 local = transform.InverseTransformPoint(target);
            float angle = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            return Mathf.Clamp(angle / _fullSteerAngle, -1f, 1f);
        }

        // 지금 진행 방향과, 앞 _cornerLookAhead 안의 경로 방향 사이 가장 큰 각도
        private float UpcomingTurnAngle(float t)
        {
            Vector3 here = _path.GetDirection(_segment);
            float max = 0f;
            for (float d = 5f; d <= _cornerLookAhead; d += 5f)
            {
                Vector3 a = _path.PointAhead(_segment, t, d);
                Vector3 b = _path.PointAhead(_segment, t, d + 2f);
                Vector3 dir = b - a;
                dir.y = 0f;
                if (dir.sqrMagnitude < 1e-4f) continue;
                max = Mathf.Max(max, Vector3.Angle(here, dir));
            }
            return max;
        }

        // 거의 못 움직이면 후진하며 방향을 틀고, 그래도 안 되면 마지막 체크포인트로 리스폰
        private void UpdateStuck(float speed, float desiredSteer, float dt)
        {
            bool racing = _raceManager != null && _raceManager.State == RaceState.Racing && !_raceManager.IsPaused;
            if (!racing || speed >= _stuckSpeed)
            {
                _stuckTimer = 0f;
                if (speed > 5f) _reverseTries = 0;
                return;
            }

            _stuckTimer += dt;
            if (_stuckTimer < _stuckTime) return;
            _stuckTimer = 0f;

            if (_reverseTries < _reverseTriesBeforeRespawn)
            {
                _reverseTries++;
                _reverseTimer = _reverseTime;
                _reverseSteer = -Mathf.Sign(desiredSteer == 0f ? 1f : desiredSteer);   // 후진할 땐 반대로 꺾어야 앞머리가 목표 쪽으로 돈다
            }
            else if (_progress != null)
            {
                _reverseTries = 0;
                _kart.Respawn(_progress.LastCheckpointPose);
                _segment = _path.ClosestSegment(_progress.LastCheckpointPose.position);
            }
        }
    }
}
