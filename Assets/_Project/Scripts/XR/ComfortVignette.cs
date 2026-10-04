using UnityEngine;
using VRKart.Core;

namespace VRKart.XR
{
    // 회전·가감속 비례 시야 가림 (FR-XR-06). 카트 루트(IKart와 같은 오브젝트)에 붙인다.
    // 멀미는 몸(전정기관)이 느끼지 못하는 "속도 변화·회전"에서 크게 오므로, 일정한 속도로 직진할 때는 가리지 않는다.
    // 가리는 방식은 ComfortSettings.VignetteStyle:
    //   WindowTint = 차 옆유리(_windows)가 짙어짐 / Soft·Black = 메인 카메라 자식 반구(_vignette)로 화면 가장자리를 어둡게
    public sealed class ComfortVignette : MonoBehaviour
    {
        private static readonly int ApertureSizeId = Shader.PropertyToID("_ApertureSize");
        private static readonly int FeatheringEffectId = Shader.PropertyToID("_FeatheringEffect");
        private static readonly int VignetteColorId = Shader.PropertyToID("_VignetteColor");
        private static readonly int VignetteColorBlendId = Shader.PropertyToID("_VignetteColorBlend");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [Header("가리는 대상")]
        [SerializeField] private Renderer _vignette;     // 화면 비네팅 반구 (XRI 샘플 TunnelingVignette 셰이더)
        [SerializeField] private Renderer[] _windows;    // 옆유리 (URP Unlit 반투명)

        [Header("화면 비네팅 (Soft · Black)")]
        [SerializeField, Range(0f, 1f)] private float _maxClosing = 0.45f;   // 강도 1, 최대 움직임에서 닫히는 양
        [SerializeField, Range(0f, 1f)] private float _feathering = 0.2f;
        [SerializeField, Range(0f, 1f)] private float _softAlpha = 0.55f;    // Soft의 최대 불투명도

        [Header("옆유리 (WindowTint)")]
        [SerializeField] private Color _tintColor = new Color(0.04f, 0.05f, 0.07f);
        [SerializeField, Range(0f, 1f)] private float _maxTint = 0.9f;       // 강도 1, 최대 움직임에서 유리 불투명도

        [Header("움직임 → 가림")]
        [SerializeField, Min(0f)] private float _turnRateStart = 10f;        // 도/초 (실측: 키보드 코너 약 40°/s)
        [SerializeField, Min(1f)] private float _turnRateFull = 60f;
        [SerializeField, Min(0f)] private float _accelStart = 1.5f;          // m/s² (실측: 출발 가속 약 4~5, 브레이크·부스트는 더 큼)
        [SerializeField, Min(0.1f)] private float _accelFull = 6f;
        [SerializeField, Min(0.01f)] private float _accelSmoothing = 0.15f;  // 가감속 값 평활 시간 (초)
        [SerializeField, Min(0.01f)] private float _easeIn = 0.3f;           // 가려지는 시간 (초)
        [SerializeField, Min(0.01f)] private float _easeOut = 0.6f;          // 걷히는 시간 (초)

        private IKart _kart;
        private MaterialPropertyBlock _block;
        private float _amount;          // 0 ~ 1, 지금 가리는 정도 (강도 반영 전)
        private float _lastYaw;
        private float _lastSpeed;
        private float _accel;           // 평활한 |가감속| (m/s²)

        private void Awake()
        {
            _kart = GetComponent<IKart>();
            _block = new MaterialPropertyBlock();
            _lastYaw = transform.eulerAngles.y;
            _lastSpeed = _kart != null ? _kart.CurrentSpeed : 0f;
            Apply(0f);
        }

        // 카트 속도는 물리 스텝마다 바뀌므로 가감속도 물리 스텝에서 잰다
        private void FixedUpdate()
        {
            if (_kart == null) return;
            float dt = Time.fixedDeltaTime;
            float speed = _kart.CurrentSpeed;
            float raw = Mathf.Abs(speed - _lastSpeed) / dt;
            _lastSpeed = speed;
            _accel = Mathf.Lerp(_accel, raw, 1f - Mathf.Exp(-dt / _accelSmoothing));
        }

        private void LateUpdate()
        {
            float yaw = transform.eulerAngles.y;
            float target = 0f;

            // 일시정지(timeScale 0)·메뉴 중에는 걷어 낸다
            if (ComfortSettings.VignetteEnabled && _kart != null && Time.timeScale > 0f && Time.deltaTime > 0f)
            {
                float turnRate = Mathf.Abs(Mathf.DeltaAngle(_lastYaw, yaw)) / Time.deltaTime;
                float turn = Mathf.InverseLerp(_turnRateStart, _turnRateFull, turnRate);
                float accel = Mathf.InverseLerp(_accelStart, _accelFull, _accel);
                target = Mathf.Max(turn, accel);
            }
            _lastYaw = yaw;

            float time = target > _amount ? _easeIn : _easeOut;
            _amount = Mathf.MoveTowards(_amount, target, Time.unscaledDeltaTime / time);
            Apply(_amount * ComfortSettings.VignetteIntensity);
        }

        private void Apply(float amount)
        {
            var style = ComfortSettings.VignetteStyle;
            ApplyScreen(style == ComfortVignetteStyle.WindowTint ? 0f : amount, style == ComfortVignetteStyle.Black ? 1f : _softAlpha);
            ApplyWindows(style == ComfortVignetteStyle.WindowTint ? amount : 0f);
        }

        private void ApplyScreen(float amount, float alpha)
        {
            if (_vignette == null) return;
            float closing = amount * _maxClosing;
            bool visible = closing > 0.001f;
            if (_vignette.enabled != visible) _vignette.enabled = visible;
            if (!visible) return;

            var color = new Color(0f, 0f, 0f, alpha);
            _vignette.GetPropertyBlock(_block);
            _block.SetFloat(ApertureSizeId, 1f - closing);
            _block.SetFloat(FeatheringEffectId, _feathering);
            _block.SetColor(VignetteColorId, color);
            _block.SetColor(VignetteColorBlendId, color);
            _vignette.SetPropertyBlock(_block);
        }

        private void ApplyWindows(float amount)
        {
            if (_windows == null) return;
            float alpha = amount * _maxTint;
            bool visible = alpha > 0.001f;
            var color = new Color(_tintColor.r, _tintColor.g, _tintColor.b, alpha);
            foreach (var window in _windows)
            {
                if (window == null) continue;
                if (window.enabled != visible) window.enabled = visible;
                if (!visible) continue;
                window.GetPropertyBlock(_block);
                _block.SetColor(BaseColorId, color);
                window.SetPropertyBlock(_block);
            }
        }
    }
}
