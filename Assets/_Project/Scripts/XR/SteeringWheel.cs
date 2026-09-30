using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace VRKart.XR
{
    // 카트 핸들. Grip으로 한 손/두 손 잡기, 잡은 손이 핸들 축을 중심으로 돈 만큼 핸들이 돈다(±_maxAngle).
    // 놓으면 중앙으로 돌아온다. transform.forward = 핸들 축(운전자가 바라보는 방향), 콜라이더는 이 오브젝트나 자식에 둔다.
    public sealed class SteeringWheel : XRBaseInteractable
    {
        [Header("핸들")]
        [SerializeField, Range(10f, 180f)] private float _maxAngle = 90f;
        [SerializeField, Min(0f)] private float _returnSpeed = 360f;   // 놓았을 때 중앙 복귀 속도 (도/초)
        [SerializeField] private Transform _visual;                    // 실제로 돌아가는 모델 (이 오브젝트의 자식)

        // 손마다 지난 프레임의 방향(핸들 평면 위, 월드 좌표)
        private readonly Dictionary<IXRSelectInteractor, Vector3> _handDirections = new();
        private float _angle;   // 오른쪽 회전이 +

        public float Angle => _angle;
        public float Normalized => _angle / _maxAngle;   // -1(좌) ~ 1(우)
        public bool IsHeld => isSelected;

        protected override void Awake()
        {
            base.Awake();
            selectMode = InteractableSelectMode.Multiple;
        }

        protected override void OnSelectEntered(SelectEnterEventArgs args)
        {
            base.OnSelectEntered(args);
            _handDirections[args.interactorObject] = HandDirection(args.interactorObject);
        }

        protected override void OnSelectExited(SelectExitEventArgs args)
        {
            base.OnSelectExited(args);
            _handDirections.Remove(args.interactorObject);
        }

        public override void ProcessInteractable(XRInteractionUpdateOrder.UpdatePhase updatePhase)
        {
            base.ProcessInteractable(updatePhase);
            if (updatePhase != XRInteractionUpdateOrder.UpdatePhase.Dynamic) return;

            if (isSelected) UpdateHeld();
            else _angle = Mathf.MoveTowards(_angle, 0f, _returnSpeed * Time.deltaTime);

            if (_visual != null) _visual.localRotation = Quaternion.Euler(0f, 0f, -_angle);
        }

        private void UpdateHeld()
        {
            // 두 손이면 두 손이 돈 각도의 평균만큼 돌린다
            float delta = 0f;
            int count = 0;
            foreach (var interactor in interactorsSelecting)
            {
                var current = HandDirection(interactor);
                if (_handDirections.TryGetValue(interactor, out var previous)
                    && previous != Vector3.zero && current != Vector3.zero)
                {
                    // SignedAngle은 축 방향으로 볼 때 반시계가 + → 오른쪽(시계) 회전을 +로 뒤집는다
                    delta -= Vector3.SignedAngle(previous, current, transform.forward);
                    count++;
                }
                _handDirections[interactor] = current;
            }

            if (count > 0) _angle = Mathf.Clamp(_angle + delta / count, -_maxAngle, _maxAngle);
        }

        // 핸들 중심에서 손까지의 방향을 핸들 평면에 투영. 손이 축 위에 있으면 zero.
        private Vector3 HandDirection(IXRSelectInteractor interactor)
        {
            var offset = interactor.transform.position - transform.position;
            var onPlane = Vector3.ProjectOnPlane(offset, transform.forward);
            return onPlane.sqrMagnitude < 0.0001f ? Vector3.zero : onPlane.normalized;
        }
    }
}
