using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace VRKart.XR
{
    // 멀미 평가(#46)용 편의 옵션 프리셋 전환. 헤드셋을 벗기지 않고 바꿀 수 있게
    // 왼손 X를 1.5초 누르면 켬 ↔ 끔 (PC 운전 모드에서는 F2를 1.5초). 전환 직후 몇 초 동안 시야에 현재 프리셋을 표시한다.
    // X는 조작표에서 비어 있는 버튼이고, 길게 눌러야 해서 주행 중 실수로 바뀌지 않는다.
    public sealed class ComfortPresetToggle : MonoBehaviour
    {
        [SerializeField] private TMP_Text _label;                 // 메인 카메라 자식의 작은 글자
        [SerializeField, Min(0f)] private float _labelSeconds = 3f;

        private InputAction _toggleAction;
        private float _labelUntil;

        private void Awake()
        {
            _toggleAction = new InputAction("ComfortPreset", InputActionType.Button, interactions: "hold(duration=1.5)");
            _toggleAction.AddBinding("<XRController>{LeftHand}/{PrimaryButton}");
#if UNITY_EDITOR || UNITY_STANDALONE
            _toggleAction.AddBinding("<Keyboard>/f2");
#endif
        }

        private void OnEnable()
        {
            _toggleAction.performed += HandleToggle;
            _toggleAction.Enable();
            ComfortSettings.Changed += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            _toggleAction.performed -= HandleToggle;
            _toggleAction.Disable();
            ComfortSettings.Changed -= Refresh;
        }

        private void OnDestroy() => _toggleAction.Dispose();

        private void Update()
        {
            if (_label == null) return;
            bool visible = ComfortSettings.ShowPresetLabel || Time.unscaledTime < _labelUntil;
            if (_label.gameObject.activeSelf != visible) _label.gameObject.SetActive(visible);
        }

        private void HandleToggle(InputAction.CallbackContext context)
        {
            var next = ComfortSettings.CurrentPreset == ComfortPreset.On ? ComfortPreset.Off : ComfortPreset.On;
            ComfortSettings.ApplyPreset(next);
            ComfortSettings.Save();
            _labelUntil = Time.unscaledTime + _labelSeconds;
        }

        private void Refresh()
        {
            if (_label != null) _label.text = ComfortSettings.CurrentPreset == ComfortPreset.On ? "COMFORT ON" : "COMFORT OFF";
        }
    }
}
