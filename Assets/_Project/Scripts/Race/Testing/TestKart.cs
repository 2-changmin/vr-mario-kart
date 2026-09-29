using UnityEngine;
using UnityEngine.InputSystem;
using VRKart.Core;

namespace VRKart.Race.Testing
{
    // 임시 카트: #3 KartController가 나오기 전까지 레이스 로직 테스트용. Kart.prefab이 들어오면 삭제한다.
    // 방향키로 주행하거나, Autopilot을 켜면 _waypointRoot의 자식을 순서대로 따라 달린다.
    [RequireComponent(typeof(Rigidbody))]
    public sealed class TestKart : MonoBehaviour, IKart
    {
        [SerializeField] private float _maxSpeed = 20f;
        [SerializeField] private float _acceleration = 15f;
        [SerializeField] private float _turnSpeed = 120f;
        [SerializeField] private float _rideHeight = 0.3f;
        [SerializeField] private LayerMask _groundMask = (1 << 8) | (1 << 9); // Road, Grass

        [Header("자동 주행")]
        [SerializeField] private bool _autopilot = true;
        [SerializeField] private Transform _waypointRoot;
        [SerializeField] private float _waypointRadius = 6f;

        [Header("디버그")]
        [SerializeField] private bool _logRaceEvents = true;

        private Rigidbody _rigidbody;
        private RaceProgress _progress;
        private float _speed;
        private float _yaw;
        private float _fallSpeed;
        private bool _controlEnabled = true;
        private int _waypoint = -1;

        public float CurrentSpeed => _speed;
        public float MaxSpeed => _maxSpeed;

        public void SetControlEnabled(bool enabled)
        {
            _controlEnabled = enabled;
            if (!enabled) _speed = 0f;
        }

        public void ApplyBoost(float power, float duration) { }

        public void SpinOut() => _speed = 0f;

        public void Respawn(Pose pose)
        {
            _rigidbody.position = pose.position;
            _rigidbody.rotation = pose.rotation;
            transform.SetPositionAndRotation(pose.position, pose.rotation);
            _yaw = pose.rotation.eulerAngles.y;
            _speed = 0f;
            _fallSpeed = 0f;
            _waypoint = -1;
            if (_logRaceEvents) Debug.Log($"[TestKart] 리스폰 → {pose.position}");
        }

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _rigidbody.isKinematic = true;
            _rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            _progress = GetComponent<RaceProgress>();
            _yaw = transform.eulerAngles.y;
        }

        private void OnEnable()
        {
            if (_progress == null || !_logRaceEvents) return;
            _progress.LapCompleted += LogLap;
            _progress.Finished += LogFinished;
            _progress.WrongWayChanged += LogWrongWay;
        }

        private void OnDisable()
        {
            if (_progress == null) return;
            _progress.LapCompleted -= LogLap;
            _progress.Finished -= LogFinished;
            _progress.WrongWayChanged -= LogWrongWay;
        }

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            float throttle = 0f;
            float steer = 0f;

            if (_controlEnabled)
            {
                if (_autopilot && _waypointRoot != null && _waypointRoot.childCount > 0) FollowWaypoints(dt, out throttle);
                else ReadKeyboard(out throttle, out steer);
            }

            _speed = Mathf.MoveTowards(_speed, throttle * _maxSpeed, _acceleration * dt);
            _yaw += steer * _turnSpeed * dt * Mathf.Clamp01(Mathf.Abs(_speed) / 3f) * Mathf.Sign(_speed);

            Quaternion rotation = Quaternion.Euler(0f, _yaw, 0f);
            Vector3 position = _rigidbody.position + rotation * Vector3.forward * (_speed * dt);

            if (Physics.Raycast(position + Vector3.up * 3f, Vector3.down, out RaycastHit hit, 6f, _groundMask, QueryTriggerInteraction.Ignore))
            {
                position.y = hit.point.y + _rideHeight;
                _fallSpeed = 0f;
            }
            else
            {
                _fallSpeed += Physics.gravity.magnitude * dt;
                position.y -= _fallSpeed * dt;
            }

            _rigidbody.MovePosition(position);
            _rigidbody.MoveRotation(rotation);
        }

        private void ReadKeyboard(out float throttle, out float steer)
        {
            throttle = 0f;
            steer = 0f;
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.upArrowKey.isPressed) throttle += 1f;
            if (keyboard.downArrowKey.isPressed) throttle -= 1f;
            if (keyboard.rightArrowKey.isPressed) steer += 1f;
            if (keyboard.leftArrowKey.isPressed) steer -= 1f;
        }

        private void FollowWaypoints(float dt, out float throttle)
        {
            Vector3 position = _rigidbody.position;
            if (_waypoint < 0) _waypoint = FindWaypointAhead(position);

            Vector3 toTarget = Vector3.ProjectOnPlane(_waypointRoot.GetChild(_waypoint).position - position, Vector3.up);
            if (toTarget.magnitude < _waypointRadius)
            {
                _waypoint = (_waypoint + 1) % _waypointRoot.childCount;
                toTarget = Vector3.ProjectOnPlane(_waypointRoot.GetChild(_waypoint).position - position, Vector3.up);
            }

            float targetYaw = Mathf.Atan2(toTarget.x, toTarget.z) * Mathf.Rad2Deg;
            float turnRate = Mathf.Max(_turnSpeed, _speed / 20f * Mathf.Rad2Deg * 1.5f);
            _yaw = Mathf.MoveTowardsAngle(_yaw, targetYaw, turnRate * dt);
            throttle = 1f;
        }

        private int FindWaypointAhead(Vector3 position)
        {
            int best = 0;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < _waypointRoot.childCount; i++)
            {
                float distance = (_waypointRoot.GetChild(i).position - position).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = i;
                }
            }

            Vector3 forward = Quaternion.Euler(0f, _yaw, 0f) * Vector3.forward;
            if (Vector3.Dot(forward, _waypointRoot.GetChild(best).position - position) < 0f) best = (best + 1) % _waypointRoot.childCount;
            return best;
        }

        private void LogLap(RaceProgress progress, int lap) => Debug.Log($"[TestKart] {lap}/{progress.TotalLaps} 랩 완료");
        private void LogFinished(RaceProgress progress) => Debug.Log("[TestKart] 완주!");
        private void LogWrongWay(RaceProgress progress, bool wrongWay) => Debug.Log(wrongWay ? "[TestKart] 역주행 중" : "[TestKart] 정주행 복귀");
    }
}
