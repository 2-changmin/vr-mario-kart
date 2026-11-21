using System.Collections;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;

namespace VRKart.XR
{
    // 시점 리센터 (FR-XR-05). 머리(카메라)를 좌석 눈 위치(_seatEye)로 옮기고, 정면을 카트 정면에 맞춘다.
    // 시작 직후 한 번, 그리고 왼손 Y 버튼을 누를 때마다 실행한다. XR Origin은 카트 루트 아래 ViewPivot(HorizonLock)의 자식이다.
    // 3인칭(#64)에서는 ViewModeSwitcher가 기준을 차 뒤·위의 ChaseEye로 바꾼다(SetEye).
    public sealed class ViewRecenter : MonoBehaviour
    {
        [SerializeField] private XROrigin _origin;
        [SerializeField] private Transform _seatEye;   // 머리를 맞출 눈 위치(기본: 좌석). forward = 카트 정면
        [SerializeField, Min(0)] private int _startDelayFrames = 3;   // 트래킹이 잡힐 때까지 기다리는 프레임 수

        private InputAction _recenterAction;
        private bool _started;

        private void Awake()
        {
            if (_origin == null) _origin = GetComponentInChildren<XROrigin>();

            // 왼손 Y 버튼 (PRD 조작표에서 비어 있는 버튼)
            _recenterAction = new InputAction("Recenter", InputActionType.Button);
            _recenterAction.AddBinding("<XRController>{LeftHand}/{SecondaryButton}");
        }

        private void OnEnable()
        {
            _recenterAction.performed += HandleRecenterPressed;
            _recenterAction.Enable();
        }

        private void OnDisable()
        {
            _recenterAction.performed -= HandleRecenterPressed;
            _recenterAction.Disable();
        }

        private void OnDestroy() => _recenterAction.Dispose();

        private IEnumerator Start()
        {
            for (int i = 0; i < _startDelayFrames; i++) yield return null;
            _started = true;
            Recenter();
        }

        public void Recenter()
        {
            // 위쪽 기준 = XR Origin의 부모(ViewPivot). 수평 유지(HorizonLock)가 켜져 있으면 월드 위쪽, 꺼져 있으면 카트 위쪽
            var parent = _origin.transform.parent;
            var up = parent != null ? parent.up : _seatEye.up;
            _origin.MatchOriginUpCameraForward(up, Vector3.ProjectOnPlane(_seatEye.forward, up).normalized);
            _origin.MoveCameraToWorldLocation(_seatEye.position);
        }

        // 머리를 맞출 눈 위치를 바꾼다 (1인칭 SeatEye ↔ 3인칭 ChaseEye). 시작 전이면 Start에서, 아니면 바로 다시 맞춘다
        public void SetEye(Transform eye)
        {
            if (eye == null) return;
            _seatEye = eye;
            if (_started) Recenter();
        }

        private void HandleRecenterPressed(InputAction.CallbackContext context) => Recenter();
    }
}
