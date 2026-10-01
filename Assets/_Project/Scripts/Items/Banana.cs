using UnityEngine;
using VRKart.Core;

namespace VRKart.Items
{
    // 바나나: 카트 뒤 바닥에 놓이고, 밟은 카트를 SpinOut시키고 사라진다.
    // 놓은 카트는 잠시 면제(놓자마자 자기가 밟지 않게). 트리거는 Ignore Raycast 레이어 → 카트 지면 레이에 안 걸림.
    [RequireComponent(typeof(SphereCollider))]
    public sealed class Banana : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float _ownerGrace = 1f;
        [SerializeField, Min(1f)] private float _lifetime = 60f;
        [SerializeField] private LayerMask _groundMask = (1 << 8) | (1 << 9);   // Road, Grass

        private IKart _owner;
        private bool _hit;   // 카트 콜라이더가 여러 개라 같은 스텝에 트리거가 여러 번 들어온다 → 한 번만
        private float _placedAt;

        public void Place(IKart owner)
        {
            _owner = owner;
            _placedAt = Time.time;
            if (Physics.Raycast(transform.position + Vector3.up, Vector3.down, out RaycastHit hit, 5f, _groundMask, QueryTriggerInteraction.Ignore))
                transform.position = hit.point;
            Destroy(gameObject, _lifetime);
        }

        private void Awake() => GetComponent<SphereCollider>().isTrigger = true;

        private void OnTriggerEnter(Collider other)
        {
            if (_hit) return;
            var kart = other.GetComponentInParent<IKart>();
            if (kart == null) return;
            if (kart == _owner && Time.time - _placedAt < _ownerGrace) return;

            _hit = true;
            kart.SpinOut();
            Destroy(gameObject);
        }
    }
}
