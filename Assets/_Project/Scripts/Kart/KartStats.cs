using UnityEngine;

namespace VRKart.Kart
{
    // 카트 주행 튜닝 값. 플레이어/AI가 같은 에셋을 쓰거나, 필요하면 복사해서 따로 조정한다.
    [CreateAssetMenu(fileName = "KartStats", menuName = "VRKart/Kart Stats")]
    public sealed class KartStats : ScriptableObject
    {
        [Header("속도 (m/s)")]
        [SerializeField, Min(1f)] private float _maxSpeed = 20f;
        [SerializeField, Min(0f)] private float _maxReverseSpeed = 6f;
        [SerializeField, Range(0.1f, 1f)] private float _offRoadSpeedFactor = 0.5f;   // 잔디(Grass) 위 최고 속도 비율

        [Header("가감속 (m/s²)")]
        [SerializeField, Min(0f)] private float _acceleration = 10f;
        [SerializeField, Min(0f)] private float _brakeDeceleration = 20f;
        [SerializeField, Min(0f)] private float _reverseAcceleration = 6f;
        [SerializeField, Min(0f)] private float _coastDeceleration = 4f;      // 입력이 없을 때 자연 감속
        [SerializeField, Min(0f)] private float _overSpeedDeceleration = 15f; // 최고 속도를 넘었을 때(잔디 진입, 부스트 종료)

        [Header("조향 (도/초)")]
        [SerializeField, Min(0f)] private float _turnRateLowSpeed = 110f;     // 저속 회전 속도
        [SerializeField, Min(0f)] private float _turnRateHighSpeed = 60f;     // 최고 속도 회전 속도 → 빠를수록 회전 반경이 커짐
        [SerializeField, Min(0.1f)] private float _turnRampSpeed = 2f;        // 이 속도 아래에선 제자리 회전하지 않게 회전량을 줄임

        [Header("지면 / 공중")]
        [SerializeField, Min(0f)] private float _groundAlignSpeed = 8f;       // 경사면 기울기를 따라가는 속도
        [SerializeField, Min(0f)] private float _airAlignSpeed = 2f;          // 공중에서 수평으로 돌아오는 속도
        [SerializeField, Min(1f)] private float _suspensionStiffness = 200f;  // 지면 높이를 유지하는 스프링 (1/s²)
        [SerializeField, Min(0f)] private float _suspensionDamping = 28f;     // 착지 충격 흡수 (1/s), 2√강성이면 튕기지 않음
        [SerializeField, Min(1f)] private float _airGravityMultiplier = 1.5f; // 점프 후 빨리 착지하게

        [Header("피격")]
        [SerializeField, Min(0f)] private float _spinOutDuration = 1.2f;
        [SerializeField, Range(0f, 1f)] private float _spinOutSpeedFactor = 0.3f;

        [Header("부스트")]
        [SerializeField, Min(0f)] private float _boostAcceleration = 30f;

        public float MaxSpeed => _maxSpeed;
        public float MaxReverseSpeed => _maxReverseSpeed;
        public float OffRoadSpeedFactor => _offRoadSpeedFactor;
        public float Acceleration => _acceleration;
        public float BrakeDeceleration => _brakeDeceleration;
        public float ReverseAcceleration => _reverseAcceleration;
        public float CoastDeceleration => _coastDeceleration;
        public float OverSpeedDeceleration => _overSpeedDeceleration;
        public float TurnRateLowSpeed => _turnRateLowSpeed;
        public float TurnRateHighSpeed => _turnRateHighSpeed;
        public float TurnRampSpeed => _turnRampSpeed;
        public float GroundAlignSpeed => _groundAlignSpeed;
        public float AirAlignSpeed => _airAlignSpeed;
        public float SuspensionStiffness => _suspensionStiffness;
        public float SuspensionDamping => _suspensionDamping;
        public float AirGravityMultiplier => _airGravityMultiplier;
        public float SpinOutDuration => _spinOutDuration;
        public float SpinOutSpeedFactor => _spinOutSpeedFactor;
        public float BoostAcceleration => _boostAcceleration;
    }
}
