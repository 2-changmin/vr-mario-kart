using UnityEngine;
using VRKart.Core;

namespace VRKart.Audio
{
    // 벽·다른 카트와 부딪힐 때 충돌음(세기 = 상대 속도). 플레이어 카트는 컨트롤러 진동도.
    // OnCollisionEnter를 받으려고 Rigidbody가 있는 카트 루트에 붙인다. 바닥(Road/Grass)과의 접촉은 무시.
    [RequireComponent(typeof(AudioSource))]
    public sealed class CollisionAudio : MonoBehaviour
    {
        private const int WallLayer = 10;

        [SerializeField] private AudioClip[] _wallHits;
        [SerializeField] private AudioClip[] _kartHits;
        [SerializeField, Min(0.1f)] private float _minImpactSpeed = 1.5f;
        [SerializeField, Min(0.1f)] private float _fullImpactSpeed = 12f;
        [SerializeField, Min(0f)] private float _cooldown = 0.15f;
        [SerializeField, Range(0f, 1f)] private float _volume = 0.9f;
        [SerializeField] private bool _haptics;

        private AudioSource _source;
        private float _lastHitTime = -1f;

        private void Awake()
        {
            _source = GetComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.spatialBlend = 1f;
            _source.minDistance = 3f;
            _source.maxDistance = 50f;
        }

        private void OnCollisionEnter(Collision collision)
        {
            bool wall = collision.collider.gameObject.layer == WallLayer;
            bool kart = !wall && collision.collider.GetComponentInParent<IKart>() != null;
            if (!wall && !kart) return;

            float impact = collision.relativeVelocity.magnitude;
            if (impact < _minImpactSpeed || Time.time - _lastHitTime < _cooldown) return;
            _lastHitTime = Time.time;

            float strength = Mathf.InverseLerp(_minImpactSpeed, _fullImpactSpeed, impact);
            AudioClip[] clips = wall ? _wallHits : _kartHits;
            if (clips != null && clips.Length > 0)
            {
                _source.pitch = Random.Range(0.9f, 1.1f);
                _source.PlayOneShot(clips[Random.Range(0, clips.Length)], Mathf.Lerp(0.3f, 1f, strength) * _volume * AudioVolumes.Sfx);
            }
            if (_haptics) ControllerHaptics.Pulse(Mathf.Lerp(0.25f, 1f, strength), Mathf.Lerp(0.06f, 0.2f, strength));
        }
    }
}
