using UnityEngine;
using VRKart.Core;

namespace VRKart.Kart
{
    // 차 모델 표시 전용: 속도만큼 바퀴를 굴리고, 앞바퀴를 조향만큼 꺾고, 브레이크를 밟으면 브레이크등을 켠다.
    // 물리·입력에는 영향이 없다. 카트 루트(부모)의 IKart, IKartInput을 읽는다. (디자인 규칙: 이슈 #40)
    public sealed class CarVisual : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [Tooltip("바퀴 회전축 피벗. 피벗 중심 = 바퀴 중심, 로컬 X = 차축")]
        [SerializeField] private Transform[] _frontWheels;
        [SerializeField] private Transform[] _rearWheels;
        [SerializeField, Min(0.05f)] private float _wheelRadius = 0.35f;
        [SerializeField, Range(0f, 45f)] private float _maxSteerAngle = 25f;
        [SerializeField, Min(0f)] private float _steerSmoothing = 10f;

        [Header("브레이크등 (Unlit 머티리얼의 색을 바꿈)")]
        [SerializeField] private Renderer[] _brakeLights;
        [SerializeField] private Color _brakeOff = new Color(0.45f, 0.04f, 0.04f);
        [SerializeField] private Color _brakeOn = new Color(1f, 0.12f, 0.08f);

        private IKart _kart;
        private IKartInput _input;
        private MaterialPropertyBlock _block;
        private float _spin;
        private float _steer;
        private bool _braking;

        private void Awake()
        {
            _kart = GetComponentInParent<IKart>();
            _input = GetComponentInParent<IKartInput>();
            _block = new MaterialPropertyBlock();
            SetBrakeLights(false);
        }

        private void Update()
        {
            float speed = _kart != null ? _kart.CurrentSpeed : 0f;
            float dt = Time.deltaTime;
            _spin = Mathf.Repeat(_spin + speed / _wheelRadius * Mathf.Rad2Deg * dt, 360f);
            float targetSteer = _input != null ? Mathf.Clamp(_input.Steer, -1f, 1f) * _maxSteerAngle : 0f;
            _steer = Mathf.Lerp(_steer, targetSteer, 1f - Mathf.Exp(-_steerSmoothing * dt));

            Quaternion spin = Quaternion.Euler(_spin, 0f, 0f);
            foreach (Transform wheel in _frontWheels)
                if (wheel != null) wheel.localRotation = Quaternion.Euler(0f, _steer, 0f) * spin;
            foreach (Transform wheel in _rearWheels)
                if (wheel != null) wheel.localRotation = spin;

            bool braking = _input != null && _input.Brake > 0.1f;
            if (braking != _braking) SetBrakeLights(braking);
        }

        private void SetBrakeLights(bool on)
        {
            _braking = on;
            if (_brakeLights == null) return;
            foreach (Renderer light in _brakeLights)
            {
                if (light == null) continue;
                light.GetPropertyBlock(_block);
                _block.SetColor(BaseColorId, on ? _brakeOn : _brakeOff);
                light.SetPropertyBlock(_block);
            }
        }
    }
}
