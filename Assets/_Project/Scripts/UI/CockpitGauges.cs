using TMPro;
using UnityEngine;
using VRKart.Core;

namespace VRKart.UI
{
    // 조종석 계기판: 속도계·회전계 바늘, 디지털 속도, 기어 단수. 카트 루트(부모)의 IKart 속도만 읽는 표시 전용.
    // 회전수와 단수는 실제 변속기가 없어서 속도 구간으로 흉내 낸다. 바늘은 로컬 Z축으로 돈다(0 = 눈금 시작).
    public sealed class CockpitGauges : MonoBehaviour
    {
        [Header("속도계")]
        [SerializeField] private Transform _speedNeedle;
        [SerializeField, Min(10f)] private float _speedDialMaxKmh = 120f;
        [SerializeField] private TMP_Text _speedText;

        [Header("회전계")]
        [SerializeField] private Transform _rpmNeedle;
        [SerializeField, Min(1000f)] private float _rpmDialMax = 8000f;
        [SerializeField] private float _idleRpm = 900f;
        [SerializeField] private float _shiftRpm = 7200f;
        [SerializeField] private TMP_Text _gearText;
        [Tooltip("최고 속도 대비 각 단의 상한 비율 (마지막 = 1)")]
        [SerializeField] private float[] _gearTopRatios = { 0.18f, 0.34f, 0.52f, 0.70f, 0.86f, 1f };

        [Header("바늘")]
        [Tooltip("눈금 시작(0)에서 끝까지 바늘이 도는 각도. 시계 방향")]
        [SerializeField, Range(90f, 300f)] private float _sweepAngle = 260f;
        [SerializeField, Min(0f)] private float _needleSmoothing = 12f;

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

            float inGear = 0f;
            int gear = speed < -0.3f ? -1 : GearFor(Mathf.Abs(speed) / maxSpeed, out inGear);
            float rpm = gear == -1 ? Mathf.Lerp(_idleRpm, 3500f, Mathf.Abs(speed) / 6f) : Mathf.Lerp(_idleRpm, _shiftRpm, inGear);
            if (gear == 1 && Mathf.Abs(speed) < 0.3f) gear = 0;

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

        // 속도 비율 → 단수(1부터), 그 단 안에서의 진행도(0~1)
        private int GearFor(float speedRatio, out float inGear)
        {
            float low = 0f;
            for (int i = 0; i < _gearTopRatios.Length; i++)
            {
                float high = _gearTopRatios[i];
                if (speedRatio <= high || i == _gearTopRatios.Length - 1)
                {
                    inGear = Mathf.Clamp01(Mathf.InverseLerp(i == 0 ? 0f : low * 0.75f, high, speedRatio));
                    return i + 1;
                }
                low = high;
            }
            inGear = 0f;
            return 1;
        }
    }
}
