using UnityEngine;
using VRKart.Core;

namespace VRKart.XR
{
    // 속도·회전 비례 터널 비네팅 (FR-XR-06). 카트 루트(IKart와 같은 오브젝트)에 붙이고,
    // _vignette = 메인 카메라 자식의 반구 메시(XRI 샘플의 TunnelingVignette 셰이더)를 연결한다.
    // 멀미는 회전에서 가장 크게 오므로 직진 속도와 좌우 회전 속도 중 큰 쪽으로 시야를 좁힌다.
    public sealed class ComfortVignette : MonoBehaviour
    {
        private static readonly int ApertureSizeId = Shader.PropertyToID("_ApertureSize");
        private static readonly int FeatheringEffectId = Shader.PropertyToID("_FeatheringEffect");

        [SerializeField] private Renderer _vignette;
        [SerializeField, Range(0f, 1f)] private float _maxClosing = 0.45f;   // 강도 1, 최대 움직임에서 닫히는 양 (조리개 1 → 0.55)
        [SerializeField, Range(0f, 1f)] private float _feathering = 0.2f;    // 가장자리 번짐

        [Header("움직임 → 비네팅")]
        [SerializeField, Range(0f, 1f)] private float _speedStart = 0.15f;   // 최고 속도 대비 이 비율부터 좁아지기 시작
        [SerializeField, Min(0f)] private float _turnRateStart = 20f;        // 도/초
        [SerializeField, Min(1f)] private float _turnRateFull = 90f;         // 도/초
        [SerializeField, Min(0.01f)] private float _easeIn = 0.3f;           // 좁아지는 시간 (초)
        [SerializeField, Min(0.01f)] private float _easeOut = 0.6f;          // 걷히는 시간 (초)

        private IKart _kart;
        private MaterialPropertyBlock _block;
        private float _closing;
        private float _lastYaw;

        private void Awake()
        {
            _kart = GetComponent<IKart>();
            _block = new MaterialPropertyBlock();
            _lastYaw = transform.eulerAngles.y;
            Apply(0f);
        }

        private void LateUpdate()
        {
            float yaw = transform.eulerAngles.y;
            float target = 0f;

            // 일시정지(timeScale 0)·메뉴 중에는 걷어 낸다
            if (ComfortSettings.VignetteEnabled && _kart != null && Time.timeScale > 0f && Time.deltaTime > 0f)
            {
                float speedRatio = Mathf.Abs(_kart.CurrentSpeed) / Mathf.Max(_kart.MaxSpeed, 0.1f);
                float speedFactor = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(_speedStart, 1f, speedRatio));
                float turnRate = Mathf.Abs(Mathf.DeltaAngle(_lastYaw, yaw)) / Time.deltaTime;
                float turnFactor = Mathf.InverseLerp(_turnRateStart, _turnRateFull, turnRate);
                target = Mathf.Max(speedFactor, turnFactor) * ComfortSettings.VignetteIntensity * _maxClosing;
            }
            _lastYaw = yaw;

            float time = target > _closing ? _easeIn : _easeOut;
            _closing = Mathf.MoveTowards(_closing, target, _maxClosing / time * Time.unscaledDeltaTime);
            Apply(_closing);
        }

        private void Apply(float closing)
        {
            if (_vignette == null) return;
            bool visible = closing > 0.001f;
            if (_vignette.enabled != visible) _vignette.enabled = visible;
            if (!visible) return;

            _vignette.GetPropertyBlock(_block);
            _block.SetFloat(ApertureSizeId, 1f - closing);
            _block.SetFloat(FeatheringEffectId, _feathering);
            _vignette.SetPropertyBlock(_block);
        }
    }
}
