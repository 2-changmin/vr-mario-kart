using System.Collections.Generic;
using UnityEngine;
using VRKart.Race;

namespace VRKart.TimeAttack
{
    // 고스트 카 (#47). 최고 기록 주행의 샘플을 레이스 시간에 맞춰 보간 재생한다. 콜라이더·물리가 없어 부딪히지 않는다.
    // 레이스 시간(RaceManager.RaceTime)을 따르므로 카운트다운 동안은 출발 자리에 서 있고, 일시정지하면 같이 멈춘다.
    // 플레이어 카트와 겹칠 만큼 가까워지면 흐려진다 (반투명 차가 조종석을 통과하면 VR에서 거슬리므로). 완주 지점에 도착하면 사라진다.
    public sealed class GhostCar : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private Material _ghostMaterial;
        [SerializeField, Range(0f, 1f)] private float _alpha = 0.45f;
        [Tooltip("플레이어와 이 거리(m) 안이면 완전히 사라짐")]
        [SerializeField, Min(0f)] private float _hideDistance = 2.5f;
        [Tooltip("플레이어와 이 거리(m)부터 흐려지기 시작")]
        [SerializeField, Min(0f)] private float _fadeDistance = 6f;

        private readonly List<Renderer> _renderers = new List<Renderer>();
        private IReadOnlyList<GhostSample> _samples;
        private RaceManager _raceManager;
        private Transform _player;
        private Material _material;   // 이 고스트 전용 사본 (알파를 바꾼다)
        private Color _baseColor;
        private int _index;
        private bool _visible = true;

        public void Play(IReadOnlyList<GhostSample> samples, RaceManager raceManager, Transform player)
        {
            _samples = samples;
            _raceManager = raceManager;
            _player = player;
            _index = 0;
            ApplyPose(0f);
        }

        private void Awake()
        {
            GetComponentsInChildren(true, _renderers);
            if (_ghostMaterial == null) return;
            _material = new Material(_ghostMaterial);
            _baseColor = _material.GetColor(BaseColorId);
            foreach (Renderer target in _renderers)
            {
                var materials = new Material[target.sharedMaterials.Length];
                for (int i = 0; i < materials.Length; i++) materials[i] = _material;
                target.sharedMaterials = materials;
            }
        }

        private void OnDestroy()
        {
            if (_material != null) Destroy(_material);
        }

        private void LateUpdate()
        {
            if (_samples == null || _samples.Count == 0) return;

            RaceState state = _raceManager.State;
            float time = state == RaceState.Racing || state == RaceState.Finished ? _raceManager.RaceTime : 0f;
            bool ended = time > _samples[_samples.Count - 1].Time;
            if (!ended) ApplyPose(time);

            float alpha = ended ? 0f : _alpha;
            if (_player != null)
            {
                float distance = Vector3.Distance(transform.position, _player.position);
                alpha *= Mathf.InverseLerp(_hideDistance, _fadeDistance, distance);
            }
            SetAlpha(alpha);
        }

        // time을 사이에 둔 두 샘플을 보간. 시간은 앞으로만 흐르므로 지난번 위치부터 찾는다
        private void ApplyPose(float time)
        {
            if (_index >= _samples.Count || _samples[_index].Time > time) _index = 0;
            while (_index < _samples.Count - 2 && _samples[_index + 1].Time <= time) _index++;

            GhostSample a = _samples[_index];
            GhostSample b = _samples[Mathf.Min(_index + 1, _samples.Count - 1)];
            float t = b.Time > a.Time ? Mathf.Clamp01((time - a.Time) / (b.Time - a.Time)) : 0f;
            transform.SetPositionAndRotation(Vector3.Lerp(a.Position, b.Position, t), Quaternion.Slerp(a.Rotation, b.Rotation, t));
        }

        private void SetAlpha(float alpha)
        {
            bool visible = alpha > 0.01f;
            if (visible != _visible)
            {
                _visible = visible;
                foreach (Renderer target in _renderers) target.enabled = visible;
            }
            if (!visible || _material == null) return;

            Color color = _baseColor;
            color.a = alpha;
            _material.SetColor(BaseColorId, color);
        }
    }
}
