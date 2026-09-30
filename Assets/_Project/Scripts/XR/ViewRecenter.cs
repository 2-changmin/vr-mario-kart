using System.Collections;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;

namespace VRKart.XR
{
    // 시점 리센터 (FR-XR-05). 머리(카메라)를 좌석 눈 위치(_seatEye)로 옮기고, 정면을 카트 정면에 맞춘다.
    // 시작 직후 한 번, 그리고 왼손 Y 버튼을 누를 때마다 실행한다. XR Origin은 카트의 자식이어야 한다.
    public sealed class ViewRecenter : MonoBehaviour
    {
        [SerializeField] private XROrigin _origin;
        [SerializeField] private Transform _seatEye;   // 좌석에 앉았을 때 눈 위치. forward = 카트 정면
        [SerializeField, Min(0)] private int _startDelayFrames = 3;   // 트래킹이 잡힐 때까지 기다리는 프레임 수

        private InputAction _recenterAction;

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
            Recenter();
        }

        public void Recenter()
        {
            _origin.MatchOriginUpCameraForward(_seatEye.up, _seatEye.forward);
            _origin.MoveCameraToWorldLocation(_seatEye.position);
        }

        private void HandleRecenterPressed(InputAction.CallbackContext context) => Recenter();
    }
}
