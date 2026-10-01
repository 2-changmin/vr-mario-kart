using TMPro;
using UnityEngine;
using VRKart.Core;

namespace VRKart.UI
{
    // 조종석 계기판: 속도계·회전계 바늘, 디지털 속도, 기어 단수. 카트 루트(부모)의 IKart 속도만 읽는 표시 전용.
    // 회전수와 단수는 SimulatedGearbox(엔진음과 공용)가 속도 구간으로 흉내 낸다. 바늘은 로컬 Z축으로 돈다(0 = 눈금 시작).
    public sealed class CockpitGauges : MonoBehaviour
    {
        [Header("속도계")]
        [SerializeField] private Transform _speedNeedle;
        [SerializeField, Min(10f)] private float _speedDialMaxKmh = 120f;
        [SerializeField] private TMP_Text _speedText;

        [Header("회전계")]
        [SerializeField] private Transform _rpmNeedle;
        [SerializeField, Min(1000f)] private float _rpmDialMax = 8000f;
        [SerializeField] private TMP_Text _gearText;

        [Header("바늘")]
        [Tooltip("눈금 시작(0)에서 끝까지 바늘이 도는 각도. 시계 방향")]
        [SerializeField, Range(90f, 300f)] private float _sweepAngle = 260f;
        [SerializeField, Min(0f)] private float _needleSmoothing = 12f;

        private readonly SimulatedGearbox _gearbox = new SimulatedGearbox();
        private IKart _kart;
        private float _speedFraction;
        private float _rpmFraction;
        private int _shownSpeed = -1;
        private int _shownGear = int.MinValue;

        private void Awake() => _kart = GetComponentInParent<IKart>();

        private void Update()
        {
            float speed = _kart != null ? _kart.CurrentSpeed : 0f;
            float maxSpeed = _kart != null ? Mathf.Max(_kart.MaxSpeed, 0.1f) : 20f;
            float kmh = Mathf.Abs(speed) * 3.6f;

            _gearbox.Update(speed, maxSpeed);
            int gear = _gearbox.Gear;
            float rpm = _gearbox.Rpm;

            float k = 1f - Mathf.Exp(-_needleSmoothing * Time.deltaTime);
            _speedFraction = Mathf.Lerp(_speedFraction, Mathf.Clamp01(kmh / _speedDialMaxKmh), k);
            _rpmFraction = Mathf.Lerp(_rpmFraction, Mathf.Clamp01(rpm / _rpmDialMax), k);
            if (_speedNeedle != null) _speedNeedle.localRotation = Quaternion.Euler(0f, 0f, -_sweepAngle * _speedFraction);
            if (_rpmNeedle != null) _rpmNeedle.localRotation = Quaternion.Euler(0f, 0f, -_sweepAngle * _rpmFraction);

            int shownSpeed = Mathf.RoundToInt(kmh);
            if (_speedText != null && shownSpeed != _shownSpeed)
            {
                _shownSpeed = shownSpeed;
                _speedText.text = shownSpeed.ToString();
            }
            if (_gearText != null && gear != _shownGear)
            {
                _shownGear = gear;
                _gearText.text = gear switch { -1 => "R", 0 => "N", _ => gear.ToString() };
            }
        }
    }
}
