using UnityEngine;
using VRKart.Core;

namespace VRKart.Audio
{
    // 엔진음: 공회전/중간/고회전 루프 3개를 회전수(SimulatedGearbox)에 따라 섞고 음높이를 올린다.
    // 가속 페달을 밟을수록 크게. 카트 루트(부모)의 IKart, IKartInput을 읽기만 한다(표시·소리 전용).
    // 카트의 자식 오브젝트(예: "Audio")에 붙이고, 루프 개수만큼 AudioSource를 실행 중에 만든다.
    public sealed class EngineAudio : MonoBehaviour
    {
        [Tooltip("낮은 회전수 → 높은 회전수 순서")]
        [SerializeField] private AudioClip[] _layers;
        [SerializeField, Range(0f, 1f)] private float _volume = 0.6f;
        [SerializeField, Range(0f, 1f)] private float _offThrottleVolume = 0.65f;
        [SerializeField, Range(0f, 1f)] private float _pitchRange = 0.35f;   // 한 층 안에서 음높이 변화 폭

        [Header("3D")]
        [SerializeField, Range(0f, 1f)] private float _spatialBlend = 1f;
        [SerializeField, Min(0.1f)] private float _minDistance = 3f;
        [SerializeField, Min(1f)] private float _maxDistance = 60f;

        private readonly SimulatedGearbox _gearbox = new SimulatedGearbox();
        private IKart _kart;
        private IKartInput _input;
        private AudioSource[] _sources;
        private float _throttle;

        private void Awake()
        {
            _kart = GetComponentInParent<IKart>();
            _input = GetComponentInParent<IKartInput>();
            _sources = new AudioSource[_layers.Length];
            for (int i = 0; i < _layers.Length; i++)
            {
                var source = gameObject.AddComponent<AudioSource>();
                source.clip = _layers[i];
                source.loop = true;
                source.playOnAwake = false;
                source.volume = 0f;
                source.spatialBlend = _spatialBlend;
                source.rolloffMode = AudioRolloffMode.Logarithmic;
                source.minDistance = _minDistance;
                source.maxDistance = _maxDistance;
                source.dopplerLevel = 0.3f;
                source.time = Random.Range(0f, _layers[i] != null ? _layers[i].length : 0f);   // 여러 대가 같은 박자로 울리지 않게
                _sources[i] = source;
            }
        }

        private void OnEnable()
        {
            if (_sources == null) return;
            foreach (var source in _sources) if (source.clip != null) source.Play();
        }

        private void OnDisable()
        {
            if (_sources == null) return;
            foreach (var source in _sources) source.Stop();
        }

        private void Update()
        {
            if (_kart == null || _sources.Length == 0) return;
            _gearbox.Update(_kart.CurrentSpeed, _kart.MaxSpeed);
            float load = _gearbox.Load01;
            float targetThrottle = _input != null ? Mathf.Clamp01(_input.Throttle) : 0f;
            _throttle = Mathf.MoveTowards(_throttle, targetThrottle, Time.deltaTime * 4f);
            float master = _volume * Mathf.Lerp(_offThrottleVolume, 1f, _throttle) * AudioVolumes.Sfx;

            // 층 i의 중심 = i / (n-1). 가까운 두 층이 삼각형 가중치로 섞인다.
            int last = _sources.Length - 1;
            for (int i = 0; i <= last; i++)
            {
                float center = last == 0 ? 0f : (float)i / last;
                float width = last == 0 ? 1f : 1f / last;
                float weight = Mathf.Clamp01(1f - Mathf.Abs(load - center) / width);
                if (i == 0 && load < center) weight = 1f;
                if (i == last && load > center) weight = 1f;
                _sources[i].volume = master * weight;
                _sources[i].pitch = 1f + (load - center) * _pitchRange / width * 0.5f;
            }
        }
    }
}
