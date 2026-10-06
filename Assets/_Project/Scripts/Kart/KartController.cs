using System;
using UnityEngine;
using VRKart.Core;

namespace VRKart.Kart
{
    // Rigidbody 기반 아케이드 카트 주행 (IKart). 같은 게임오브젝트의 IKartInput(플레이어/AI)에서 입력을 받는다.
    // 회전은 Rigidbody에 맡기지 않고 직접 정한다(요 = 조향, 기울기 = 지면 법선) → 뒤집히지 않는다.
    // 지면 위에서는 진행 방향 속도(_speed)로 속도를 덮어써서 옆 미끄러짐·경사 미끄러짐이 없고,
    // 높이는 앞/뒤 레이 서스펜션이 유지한다(콜라이더는 지면에서 떠 있음).
    [RequireComponent(typeof(Rigidbody))]
    public sealed class KartController : MonoBehaviour, IKart
    {
        [SerializeField] private KartStats _stats;
        [SerializeField] private LayerMask _groundMask = (1 << 8) | (1 << 9);   // Road, Grass
        [SerializeField] private LayerMask _offRoadMask = 1 << 9;               // Grass
        [SerializeField, Min(0.1f)] private float _groundProbeHeight = 0.5f;   // 피벗 위에서 레이 시작
        [SerializeField, Min(0f)] private float _groundProbeDepth = 0.3f;      // 피벗 아래로 이만큼까지 지면으로 인정
        [SerializeField, Min(0f)] private float _groundProbeSpacing = 0.9f;    // 앞/뒤 레이 위치 (피벗에서 앞뒤로)

        private Rigidbody _rigidbody;
        private IKartInput _input;
        private float _speed;          // 진행 방향 속도 (m/s), 후진이면 음수
        private float _yaw;
        private Vector3 _up = Vector3.up;
        private bool _controlEnabled = true;
        private bool _isGrounded;
        private bool _isOffRoad;
        private Vector3 _groundNormal = Vector3.up;
        private Vector3 _moveDirection = Vector3.forward;   // 지난 스텝에 속도를 준 방향 (막힘 판정용)
        private float _groundHeight;   // 피벗이 지면보다 얼마나 위에 있는지 (서스펜션 목표 = 0)
        private float _boostPower;
        private float _boostTimer;
        private float _spinOutTimer;
        private int _driftDirection;   // 0 = 드리프트 아님, 1 = 오른쪽, -1 = 왼쪽
        private float _slipAngle;      // 차 앞 방향 대비 진행 방향 각도 (드리프트 미끄러짐)

        // 부스트가 걸릴 때마다 (미니 터보, 대시 패드, 아이템 공통). power, duration — 이펙트·사운드용
        public event Action<float, float> BoostStarted;

        // 바나나·쉘에 맞았을 때 (피격 소리·진동용)
        public event Action SpunOut;

        public float CurrentSpeed => _speed;
        public float MaxSpeed => _stats.MaxSpeed;
        public bool IsGrounded => _isGrounded;
        public bool IsOffRoad => _isOffRoad;
        public bool IsBoosting => _boostTimer > 0f;
        public bool IsSpinningOut => _spinOutTimer > 0f;
        public bool IsControlEnabled => _controlEnabled;
        public bool IsDrifting => _driftDirection != 0;
        public int DriftDirection => _driftDirection;
        public KartStats Stats => _stats;

        public void SetControlEnabled(bool enabled)
        {
            _controlEnabled = enabled;
            if (!enabled)
            {
                _speed = 0f;
                StopDrift();
            }
        }

        // 드리프트 상태는 DriftBoost가 정한다. 드리프트 중에는 direction 쪽으로만 돌고 진행 방향이 바깥으로 미끄러진다.
        public void StartDrift(int direction) => _driftDirection = direction > 0 ? 1 : -1;
        public void StopDrift() => _driftDirection = 0;

        // power = 최고 속도에 더하는 비율 (0.3 → +30%), duration = 초. 겹치면 큰 값을 쓴다.
        public void ApplyBoost(float power, float duration)
        {
            _boostPower = IsBoosting ? Mathf.Max(_boostPower, power) : power;
            _boostTimer = Mathf.Max(_boostTimer, duration);
            BoostStarted?.Invoke(power, duration);
        }

        // 피격: 크게 감속하고 잠시 조작 불가. 멀미 때문에 카트(=시점)를 회전시키지 않는다 (NFR-03).
        public void SpinOut()
        {
            _spinOutTimer = _stats.SpinOutDuration;
            _speed *= _stats.SpinOutSpeedFactor;
            _boostTimer = 0f;
            StopDrift();
            SpunOut?.Invoke();
        }

        public void Respawn(Pose pose)
        {
            _rigidbody.position = pose.position;
            _rigidbody.rotation = pose.rotation;
            transform.SetPositionAndRotation(pose.position, pose.rotation);
            _rigidbody.linearVelocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;
            _speed = 0f;
            _yaw = pose.rotation.eulerAngles.y;
            _up = pose.rotation * Vector3.up;
            _moveDirection = pose.rotation * Vector3.forward;
            _boostTimer = 0f;
            _spinOutTimer = 0f;
            _driftDirection = 0;
            _slipAngle = 0f;
        }

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _rigidbody.constraints = RigidbodyConstraints.FreezeRotation;
            _rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            _input = GetComponent<IKartInput>();
            _yaw = transform.eulerAngles.y;
            _up = transform.up;
            _moveDirection = transform.forward;
        }

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            _boostTimer = Mathf.Max(0f, _boostTimer - dt);
            _spinOutTimer = Mathf.Max(0f, _spinOutTimer - dt);

            ProbeGround();
            ApplyBlockedSpeed();

            float throttle = 0f, brake = 0f, steer = 0f;
            if (_controlEnabled && !IsSpinningOut && _input != null)
            {
                throttle = Mathf.Clamp01(_input.Throttle);
                brake = Mathf.Clamp01(_input.Brake);
                steer = Mathf.Clamp(_input.Steer, -1f, 1f);
            }

            if (!_controlEnabled) _speed = Mathf.MoveTowards(_speed, 0f, _stats.BrakeDeceleration * dt);
            else if (_isGrounded) UpdateSpeed(throttle, brake, dt);

            UpdateYaw(steer, dt);
            UpdateRotation(dt);
            UpdateVelocity(dt);
        }

        // 앞/뒤 두 점에서 지면을 읽는다. 앞 레이가 경사(점프대)에 먼저 닿으므로 몸체가 닿기 전에 기울기를 따라간다.
        private void ProbeGround()
        {
            var origin = _rigidbody.position + _up * _groundProbeHeight;
            var along = ForwardOnPlane(_up) * _groundProbeSpacing;
            bool front = ProbeAt(origin + along, out RaycastHit frontHit);
            bool rear = ProbeAt(origin - along, out RaycastHit rearHit);

            _isGrounded = front || rear;
            if (front && rear)
            {
                // 앞뒤 높이 차로 앞뒤 기울기, 두 법선 평균으로 좌우 기울기
                var slope = frontHit.point - rearHit.point;
                _groundNormal = Vector3.ProjectOnPlane(frontHit.normal + rearHit.normal, slope).normalized;
                _isOffRoad = IsOffRoadHit(frontHit) && IsOffRoadHit(rearHit);
                _groundHeight = (frontHit.distance + rearHit.distance) * 0.5f - _groundProbeHeight;
            }
            else if (front || rear)
            {
                var hit = front ? frontHit : rearHit;
                _groundNormal = hit.normal;
                _isOffRoad = IsOffRoadHit(hit);
                _groundHeight = hit.distance - _groundProbeHeight;
            }
            else
            {
                _groundNormal = Vector3.up;
                _isOffRoad = false;
            }
        }

        private bool ProbeAt(Vector3 origin, out RaycastHit hit) =>
            Physics.Raycast(origin, -_up, out hit, _groundProbeHeight + _groundProbeDepth, _groundMask, QueryTriggerInteraction.Ignore);

        private bool IsOffRoadHit(RaycastHit hit) => (_offRoadMask.value & (1 << hit.collider.gameObject.layer)) != 0;

        // 벽·다른 카트에 막혀 실제로 덜 움직였으면 그만큼 속도를 잃는다 (정면 충돌이면 거의 0)
        private void ApplyBlockedSpeed()
        {
            if (!_isGrounded) return;
            float actual = Vector3.Dot(_rigidbody.linearVelocity, _moveDirection);
            if (Mathf.Abs(actual) < Mathf.Abs(_speed) - 0.5f) _speed = actual;
        }

        private float CurrentMaxSpeed()
        {
            float max = _stats.MaxSpeed * (_isOffRoad ? _stats.OffRoadSpeedFactor : 1f);
            return IsBoosting ? max * (1f + _boostPower) : max;
        }

        private void UpdateSpeed(float throttle, float brake, float dt)
        {
            float max = CurrentMaxSpeed();

            if (brake > 0.01f && _speed > 0.1f)
                _speed = Mathf.MoveTowards(_speed, 0f, _stats.BrakeDeceleration * brake * dt);
            else if (brake > 0.01f && throttle <= 0.01f)
                _speed = Mathf.MoveTowards(_speed, -_stats.MaxReverseSpeed, _stats.ReverseAcceleration * brake * dt);
            else if (throttle > 0.01f && _speed < 0f)
                _speed = Mathf.MoveTowards(_speed, 0f, _stats.BrakeDeceleration * throttle * dt);
            else if (throttle > 0.01f && _speed < max)
                _speed = Mathf.MoveTowards(_speed, max, _stats.Acceleration * throttle * dt);
            else if (throttle <= 0.01f)
                _speed = Mathf.MoveTowards(_speed, 0f, _stats.CoastDeceleration * dt);

            if (IsBoosting && _speed < max) _speed = Mathf.MoveTowards(_speed, max, _stats.BoostAcceleration * dt);
            if (_speed > max) _speed = Mathf.MoveTowards(_speed, max, _stats.OverSpeedDeceleration * dt);
        }

        // 빠를수록 초당 회전각이 줄어든다 → 회전 반경이 커진다. 후진이면 반대로 돈다.
        private void UpdateYaw(float steer, float dt)
        {
            float speedRatio = Mathf.Clamp01(Mathf.Abs(_speed) / _stats.MaxSpeed);
            float turnRate = Mathf.Lerp(_stats.TurnRateLowSpeed, _stats.TurnRateHighSpeed, speedRatio);
            float ramp = Mathf.Clamp01(Mathf.Abs(_speed) / _stats.TurnRampSpeed);

            if (IsDrifting)
            {
                // 드리프트 방향으로만 돈다. 안쪽으로 꺾으면 더 날카롭게, 바깥쪽으로 꺾으면 완만하게.
                float inward = Mathf.InverseLerp(-1f, 1f, steer * _driftDirection);
                steer = _driftDirection * Mathf.Lerp(_stats.DriftTurnMin, _stats.DriftTurnMax, inward);
            }
            _yaw += steer * turnRate * ramp * Mathf.Sign(_speed) * dt;

            float targetSlip = _driftDirection * _stats.DriftSlipAngle;
            _slipAngle = Mathf.Lerp(_slipAngle, targetSlip, 1f - Mathf.Exp(-_stats.DriftSlipSpeed * dt));
        }

        private void UpdateRotation(float dt)
        {
            float align = _isGrounded ? _stats.GroundAlignSpeed : _stats.AirAlignSpeed;
            _up = Vector3.Slerp(_up, _groundNormal, 1f - Mathf.Exp(-align * dt)).normalized;
            _rigidbody.MoveRotation(Quaternion.LookRotation(ForwardOnPlane(_up), _up));
        }

        // 이동 방향은 지면에 바로 맞추고, 몸체(=시점) 회전만 UpdateRotation에서 부드럽게 따라간다.
        // 콜라이더는 지면에서 떠 있고 높이는 레이 서스펜션(스프링-댐퍼)이 유지한다 → 경사 입구·작은 턱에 걸리지 않는다.
        private void UpdateVelocity(float dt)
        {
            var velocity = _rigidbody.linearVelocity;
            if (_isGrounded)
            {
                float normalSpeed = Vector3.Dot(velocity, _groundNormal);
                float spring = -_stats.SuspensionStiffness * _groundHeight - _stats.SuspensionDamping * normalSpeed;
                float gravityCancel = -Vector3.Dot(Physics.gravity, _groundNormal);   // 이번 스텝에 더해질 중력을 상쇄
                normalSpeed += (spring + gravityCancel) * dt;

                _moveDirection = Quaternion.AngleAxis(-_slipAngle, _groundNormal) * ForwardOnPlane(_groundNormal);
                velocity = _moveDirection * _speed + _groundNormal * normalSpeed;
                // 이번 스텝에 더해질 중력의 경사 방향 성분도 상쇄 → 경사에서 멈춰 있을 때(카운트다운 등) 미끄러지지 않음 (#50)
                // 경사에서의 속도는 가속·브레이크 입력으로만 정해진다(아케이드)
                _rigidbody.AddForce(-Vector3.ProjectOnPlane(Physics.gravity, _groundNormal), ForceMode.Acceleration);
            }
            else
            {
                _moveDirection = ForwardOnPlane(Vector3.up);
                velocity = _moveDirection * _speed + Vector3.up * velocity.y;
                _rigidbody.AddForce(Physics.gravity * (_stats.AirGravityMultiplier - 1f), ForceMode.Acceleration);
            }
            _rigidbody.linearVelocity = velocity;
        }

        // 조향 방향(요)을 주어진 면(법선) 위로 눕힌 방향
        private Vector3 ForwardOnPlane(Vector3 normal)
        {
            var yawForward = Quaternion.Euler(0f, _yaw, 0f) * Vector3.forward;
            return Vector3.ProjectOnPlane(yawForward, normal).normalized;
        }
    }
}
