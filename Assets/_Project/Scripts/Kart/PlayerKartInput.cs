using UnityEngine;
using UnityEngine.InputSystem;
using VRKart.Core;
using VRKart.XR;

namespace VRKart.Kart
{
    // 플레이어 카트 입력 (PRD 조작표). 핸들을 잡고 있으면 핸들 각도, 아니면 왼손 스틱으로 조향한다.
    // 오른손 Trigger = 가속, 왼손 Trigger = 브레이크/후진, A = 드리프트(누르는 동안), B = 아이템.
    // 왼손 메뉴(≡)는 일시정지(PauseMenu), 왼손 Y는 리센터(ViewRecenter)가 쓴다.
    // 에디터·PC에서 DesktopDriveMode가 켜져 있으면 키보드도 읽는다(T 가속, Shift 브레이크, R 후진, J/L 조향, Space 드리프트, E 아이템).
    public sealed class PlayerKartInput : MonoBehaviour, IKartInput
    {
        [SerializeField] private SteeringWheel _wheel;
        [SerializeField, Range(0f, 0.5f)] private float _stickDeadzone = 0.15f;

        private InputAction _throttleAction;
        private InputAction _brakeAction;
        private InputAction _steerAction;
        private InputAction _driftAction;
        private InputAction _useItemAction;
#if UNITY_EDITOR || UNITY_STANDALONE
        private IKart _kart;
#endif

        public float Throttle => Mathf.Max(_throttleAction.ReadValue<float>(), KeyThrottle());
        public float Brake => Mathf.Max(_brakeAction.ReadValue<float>(), KeyBrake());
        public float Steer => _wheel != null && _wheel.IsHeld ? _wheel.Normalized : KeySteer(out float key) ? key : StickSteer();
        public bool Drift => _driftAction.IsPressed() || KeyDrift();
        public bool UseItem => _useItemAction.WasPressedThisFrame() || KeyUseItem();

        private void Awake()
        {
            if (_wheel == null) _wheel = GetComponentInChildren<SteeringWheel>();
#if UNITY_EDITOR || UNITY_STANDALONE
            _kart = GetComponent<IKart>();
#endif

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

#if UNITY_EDITOR || UNITY_STANDALONE
        // ---- PC 테스트용 키보드 (Quest 빌드에는 들어가지 않음) ----
        private static Keyboard Keys => DesktopDriveMode.Active ? Keyboard.current : null;

        private static float KeyThrottle() => Keys != null && Keys.tKey.isPressed ? 1f : 0f;

        // Shift = 브레이크만(멈추면 놓음), R = 브레이크 → 멈추면 후진 (KartController는 Brake를 계속 누르면 후진한다)
        private float KeyBrake()
        {
            Keyboard keys = Keys;
            if (keys == null) return 0f;
            if (keys.rKey.isPressed) return 1f;
            bool movingForward = _kart == null || _kart.CurrentSpeed > 0.3f;
            return keys.shiftKey.isPressed && movingForward ? 1f : 0f;
        }

        private static bool KeySteer(out float steer)
        {
            Keyboard keys = Keys;
            steer = keys == null ? 0f : (keys.lKey.isPressed ? 1f : 0f) - (keys.jKey.isPressed ? 1f : 0f);
            return keys != null && (keys.jKey.isPressed || keys.lKey.isPressed);
        }

        private static bool KeyDrift() => Keys != null && Keys.spaceKey.isPressed;
        private static bool KeyUseItem() => Keys != null && Keys.eKey.wasPressedThisFrame;
#else
        private static float KeyThrottle() => 0f;
        private float KeyBrake() => 0f;
        private static bool KeySteer(out float steer) { steer = 0f; return false; }
        private static bool KeyDrift() => false;
        private static bool KeyUseItem() => false;
#endif

        private static InputAction CreateAction(string name, InputActionType type, string binding)
        {
            var action = new InputAction(name, type);
            action.AddBinding(binding);
            return action;
        }
    }
}
