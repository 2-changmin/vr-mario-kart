using UnityEngine;
using VRKart.Core;
using VRKart.Items;
using VRKart.Kart;

namespace VRKart.Audio
{
    // 카트 이벤트 효과음(3D): 드리프트 타이어 소리(루프), 미니 터보 충전 단계, 부스트(미니 터보·대시 패드·부스터 공통),
    // 아이템 획득·바나나 놓기·쉘 발사, 스핀아웃 피격. 플레이어 카트는 컨트롤러 진동도.
    // 카트의 자식 "Audio"에 붙이고, 카트 루트의 KartController·DriftBoost·ItemHolder 이벤트를 구독만 한다.
    public sealed class KartSfx : MonoBehaviour
    {
        [Header("드리프트")]
        [SerializeField] private AudioClip _skidLoop;
        [SerializeField, Range(0f, 1f)] private float _skidVolume = 0.45f;
        [SerializeField] private AudioClip[] _boostLevelUp = new AudioClip[2];   // 1단, 2단

        [Header("부스트 / 아이템 / 피격")]
        [SerializeField] private AudioClip _boost;
        [SerializeField] private AudioClip _itemPickup;
        [SerializeField] private AudioClip _bananaDrop;
        [SerializeField] private AudioClip _shellLaunch;
        [SerializeField] private AudioClip _spinOut;
        [SerializeField, Range(0f, 1f)] private float _volume = 0.8f;

        [Header("3D / 진동")]
        [SerializeField, Range(0f, 1f)] private float _spatialBlend = 1f;
        [SerializeField] private bool _haptics;

        private KartController _kart;
        private DriftBoost _drift;
        private ItemHolder _items;
        private AudioSource _oneShot;
        private AudioSource _skid;
        private float _skidTarget;

        private void Awake()
        {
            _kart = GetComponentInParent<KartController>();
            _drift = GetComponentInParent<DriftBoost>();
            _items = GetComponentInParent<ItemHolder>();
            _oneShot = CreateSource(false);
            _skid = CreateSource(true);
            _skid.clip = _skidLoop;
        }

        private AudioSource CreateSource(bool loop)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = _spatialBlend;
            source.minDistance = 3f;
            source.maxDistance = 50f;
            source.volume = loop ? 0f : 1f;
            return source;
        }

        private void OnEnable()
        {
            if (_kart != null) { _kart.BoostStarted += HandleBoost; _kart.SpunOut += HandleSpunOut; }
            if (_drift != null) { _drift.DriftStarted += HandleDriftStarted; _drift.DriftEnded += HandleDriftEnded; _drift.BoostLevelChanged += HandleLevel; }
            if (_items != null) { _items.ItemChanged += HandleItemChanged; _items.ItemUsed += HandleItemUsed; }
        }

        private void OnDisable()
        {
            if (_kart != null) { _kart.BoostStarted -= HandleBoost; _kart.SpunOut -= HandleSpunOut; }
            if (_drift != null) { _drift.DriftStarted -= HandleDriftStarted; _drift.DriftEnded -= HandleDriftEnded; _drift.BoostLevelChanged -= HandleLevel; }
            if (_items != null) { _items.ItemChanged -= HandleItemChanged; _items.ItemUsed -= HandleItemUsed; }
            if (_skid != null) _skid.Stop();
        }

        private void Update()
        {
            // 드리프트 소리는 부드럽게 커지고 작아진다
            float target = _skidTarget * _skidVolume * AudioVolumes.Sfx;
            _skid.volume = Mathf.MoveTowards(_skid.volume, target, Time.deltaTime * 3f);
            if (_skid.volume <= 0.001f && _skidTarget <= 0f && _skid.isPlaying) _skid.Stop();
        }

        private void HandleDriftStarted(int direction)
        {
            _skidTarget = 1f;
            if (_skidLoop != null && !_skid.isPlaying) _skid.Play();
        }

        private void HandleDriftEnded() => _skidTarget = 0f;

        private void HandleLevel(int level)
        {
            if (level <= 0 || level > _boostLevelUp.Length) return;
            Play(_boostLevelUp[level - 1], 0.6f);
            Pulse(0.2f * level, 0.05f);
        }

        private void HandleBoost(float power, float duration)
        {
            Play(_boost, Mathf.Lerp(0.7f, 1f, power / 0.4f), Random.Range(0.95f, 1.05f));
            Pulse(0.35f, 0.15f);
        }

        private void HandleSpunOut()
        {
            Play(_spinOut, 1f);
            Pulse(1f, 0.35f);
        }

        // 빈손 → 아이템 = 획득
        private void HandleItemChanged(ItemType item)
        {
            if (item != ItemType.None) Play(_itemPickup, 0.7f);
        }

        // 부스터는 HandleBoost가 소리를 낸다
        private void HandleItemUsed(ItemType item)
        {
            if (item == ItemType.Banana) Play(_bananaDrop, 0.8f);
            else if (item == ItemType.Shell) Play(_shellLaunch, 0.9f);
        }

        private void Play(AudioClip clip, float volume, float pitch = 1f)
        {
            if (clip == null) return;
            _oneShot.pitch = pitch;
            _oneShot.PlayOneShot(clip, volume * _volume * AudioVolumes.Sfx);
        }

        private void Pulse(float amplitude, float duration)
        {
            if (_haptics) ControllerHaptics.Pulse(amplitude, duration);
        }
    }
}
