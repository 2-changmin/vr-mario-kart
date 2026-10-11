using UnityEngine;
using VRKart.Core;
using VRKart.XR;

namespace VRKart.Kart
{
    // 핸들 모양만 돌린다(표시 전용, 디자인 규칙 #40-4·8). 손으로 잡고 있을 때는 SteeringWheel이 돌리고,
    // 놓았을 때(스틱·PC 키보드로 조향)는 같은 카트의 IKartInput.Steer × _maxAngle만큼 부드럽게 돌린다.
    // SteeringWheel의 잡기·회전 로직은 건드리지 않고, 그 뒤(LateUpdate)에 Visual 회전만 덮어쓴다.
    // 다시 잡으면 SteeringWheel이 0°부터 손을 따라 돈다.
    public sealed class SteeringWheelVisual : MonoBehaviour
    {
        [SerializeField] private SteeringWheel _wheel;
        [SerializeField] private Transform _visual;
        [SerializeField, Range(10f, 180f)] private float _maxAngle = 90f;   // 조향 1 = 핸들 90°
        [SerializeField, Min(0f)] private float _smoothing = 10f;

        private IKartInput _input;
        private float _angle;

        private void Awake()
        {
            if (_wheel == null) _wheel = GetComponent<SteeringWheel>();
            if (_visual == null) _visual = transform.Find("Visual");
            _input = GetComponentInParent<IKartInput>();
        }

        private void LateUpdate()
        {
            if (_visual == null) return;
            if (_wheel != null && _wheel.IsHeld)
            {
                _angle = _wheel.Angle;   // 놓았을 때 이어서 돌도록 기억만
                return;
            }
            float target = _input != null ? Mathf.Clamp(_input.Steer, -1f, 1f) * _maxAngle : 0f;
            _angle = Mathf.Lerp(_angle, target, 1f - Mathf.Exp(-_smoothing * Time.deltaTime));
            _visual.localRotation = Quaternion.Euler(0f, 0f, -_angle);
        }
    }
}
