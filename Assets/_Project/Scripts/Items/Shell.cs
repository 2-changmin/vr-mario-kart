using UnityEngine;
using VRKart.Core;

namespace VRKart.Items
{
    // 쉘: 앞으로 직진, 지면 높이를 따라가고, 벽(Wall 레이어)에서 1회 반사, 두 번째 벽이면 사라진다.
    // 맞은 카트는 SpinOut. 쏜 카트는 잠시 면제. 물리 대신 직접 움직이는 키네마틱 트리거.
    [RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
    public sealed class Shell : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float _speed = 35f;        // m/s (부스트 중인 카트보다 빠르게)
        [SerializeField, Min(0.05f)] private float _radius = 0.3f;   // 벽 감지 반지름
        [SerializeField, Min(0f)] private float _hoverHeight = 0.3f; // 지면 위 중심 높이
        [SerializeField, Min(0)] private int _maxBounces = 1;
        [SerializeField, Min(0.5f)] private float _lifetime = 6f;
        [SerializeField, Min(0f)] private float _ownerGrace = 0.4f;
        [SerializeField] private LayerMask _groundMask = (1 << 8) | (1 << 9);   // Road, Grass
        [SerializeField] private LayerMask _wallMask = 1 << 10;                 // Wall

        private Rigidbody _rigidbody;
        private Vector3 _direction;
        private IKart _owner;
        private bool _hit;   // 카트 콜라이더가 여러 개라 같은 스텝에 트리거가 여러 번 들어온다 → 한 번만
        private float _launchedAt;
        private int _bounces;
        private float _fallSpeed;

        public void Launch(Vector3 direction, IKart owner)
        {
            _direction = Vector3.ProjectOnPlane(direction, Vector3.up).normalized;
            _owner = owner;
            _launchedAt = Time.time;
            Destroy(gameObject, _lifetime);
        }

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _rigidbody.isKinematic = true;
            _rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            GetComponent<SphereCollider>().isTrigger = true;
        }

        private void FixedUpdate()
        {
            if (_direction == Vector3.zero) return;
            float dt = Time.fixedDeltaTime;
            float step = _speed * dt;
            var position = _rigidbody.position;

            if (Physics.SphereCast(position, _radius, _direction, out RaycastHit wall, step, _wallMask, QueryTriggerInteraction.Ignore))
            {
                if (_bounces >= _maxBounces)
                {
                    Destroy(gameObject);
                    return;
                }
                _bounces++;
                _direction = Vector3.ProjectOnPlane(Vector3.Reflect(_direction, wall.normal), Vector3.up).normalized;
            }

            var next = position + _direction * step;
            if (Physics.Raycast(next + Vector3.up * 2f, Vector3.down, out RaycastHit ground, 4f, _groundMask, QueryTriggerInteraction.Ignore))
            {
                next.y = ground.point.y + _hoverHeight;
                _fallSpeed = 0f;
            }
            else
            {
                _fallSpeed -= Physics.gravity.y * dt;   // 트랙 밖으로 나가면 떨어진다
                next.y -= _fallSpeed * dt;
            }

            _rigidbody.MovePosition(next);
            transform.rotation = Quaternion.LookRotation(_direction);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_hit) return;
            var kart = other.GetComponentInParent<IKart>();
            if (kart == null) return;
            if (kart == _owner && Time.time - _launchedAt < _ownerGrace) return;

            _hit = true;
            kart.SpinOut();
            Destroy(gameObject);
        }
    }
}
