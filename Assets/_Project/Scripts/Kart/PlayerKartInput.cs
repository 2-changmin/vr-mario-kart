using UnityEngine;
using UnityEngine.InputSystem;
using VRKart.Core;
using VRKart.XR;

namespace VRKart.Kart
{
    // 플레이어 카트 입력 (PRD 조작표). 핸들을 잡고 있으면 핸들 각도, 아니면 왼손 스틱으로 조향한다.
    // 오른손 Trigger = 가속, 왼손 Trigger = 브레이크/후진, A = 드리프트(누르는 동안), B = 아이템.
    // 왼손 메뉴(≡)는 일시정지(PauseMenu), 왼손 Y는 리센터(ViewRecenter)가 쓴다.
    public sealed class PlayerKartInput : MonoBehaviour, IKartInput
    {
        [SerializeField] private SteeringWheel _wheel;
        [SerializeField, Range(0f, 0.5f)] private float _stickDeadzone = 0.15f;

        private InputAction _throttleAction;
        private InputAction _brakeAction;
        private InputAction _steerAction;
        private InputAction _driftAction;
        private InputAction _useItemAction;

        public float Throttle => _throttleAction.ReadValue<float>();
        public float Brake => _brakeAction.ReadValue<float>();
        public float Steer => _wheel != null && _wheel.IsHeld ? _wheel.Normalized : StickSteer();
        public bool Drift => _driftAction.IsPressed();
        public bool UseItem => _useItemAction.WasPressedThisFrame();

        private void Awake()
        {
            if (_wheel == null) _wheel = GetComponentInChildren<SteeringWheel>();

            _throttleAction = CreateAction("Throttle", InputActionType.Value, "<XRController>{RightHand}/{Trigger}");
            _brakeAction = CreateAction("Brake", InputActionType.Value, "<XRController>{LeftHand}/{Trigger}");
            _steerAction = CreateAction("Steer", InputActionType.Value, "<XRController>{LeftHand}/{Primary2DAxis}");
            _driftAction = CreateAction("Drift", InputActionType.Button, "<XRController>{RightHand}/{PrimaryButton}");
            _useItemAction = CreateAction("UseItem", InputActionType.Button, "<XRController>{RightHand}/{SecondaryButton}");
        }

        private void OnEnable()
        {
            _throttleAction.Enable();
            _brakeAction.Enable();
            _steerAction.Enable();
            _driftAction.Enable();
            _useItemAction.Enable();
        }

        private void OnDisable()
        {
            _throttleAction.Disable();
            _brakeAction.Disable();
            _steerAction.Disable();
            _driftAction.Disable();
            _useItemAction.Disable();
        }

        private void OnDestroy()
        {
            _throttleAction.Dispose();
            _brakeAction.Dispose();
            _steerAction.Dispose();
            _driftAction.Dispose();
            _useItemAction.Dispose();
        }

        private float StickSteer()
        {
            float x = _steerAction.ReadValue<Vector2>().x;
            if (Mathf.Abs(x) < _stickDeadzone) return 0f;
            // 데드존 밖을 다시 0~1로 펼친다
            return Mathf.Sign(x) * Mathf.InverseLerp(_stickDeadzone, 1f, Mathf.Abs(x));
        }

        private static InputAction CreateAction(string name, InputActionType type, string binding)
        {
            var action = new InputAction(name, type);
            action.AddBinding(binding);
            return action;
        }
    }
}
