using System;
using UnityEngine;
using VRKart.Core;
using VRKart.Kart;

namespace VRKart.Items
{
    // 카트가 가진 아이템 1개 (FR-ITEM-01, 03). 카트 루트(KartController·RaceProgress와 같은 오브젝트)에 붙인다.
    // 같은 카트의 IKartInput.UseItem(플레이어 B / PC E, AI는 AIKartInput)이 눌리면 사용한다.
    // HUD는 CurrentItem + ItemChanged, 사운드는 ItemUsed를 쓰면 된다.
    [RequireComponent(typeof(KartController))]
    public sealed class ItemHolder : MonoBehaviour
    {
        [SerializeField] private Banana _bananaPrefab;
        [SerializeField] private Shell _shellPrefab;

        [Header("부스터")]
        [SerializeField, Min(0f)] private float _boosterPower = 0.4f;      // 최고 속도 +40%
        [SerializeField, Min(0f)] private float _boosterDuration = 1.5f;

        [Header("소환 위치 (카트 피벗 기준)")]
        [SerializeField, Min(0f)] private float _bananaDropDistance = 1.6f;   // 뒤로
        [SerializeField, Min(0f)] private float _shellSpawnDistance = 1.8f;   // 앞으로

        private KartController _kart;
        private IKartInput _input;
        private ItemType _current = ItemType.None;

        public ItemType CurrentItem => _current;

        public event Action<ItemType> ItemChanged;   // 획득·사용으로 바뀔 때 (사용하면 None)
        public event Action<ItemType> ItemUsed;      // 사용한 아이템

        // 아이템을 받는다. 이미 들고 있으면 무시하고 false.
        public bool TryGive(ItemType item)
        {
            if (_current != ItemType.None || item == ItemType.None) return false;
            SetItem(item);
            return true;
        }

        private void Awake()
        {
            _kart = GetComponent<KartController>();
            _input = GetComponent<IKartInput>();
        }

        // UseItem은 "이번 프레임에 눌렸는지"라서 Update에서 읽는다
        private void Update()
        {
            if (_current == ItemType.None || _input == null || !_input.UseItem) return;
            if (!_kart.IsControlEnabled || _kart.IsSpinningOut) return;
            Use();
        }

        private void Use()
        {
            var item = _current;
            SetItem(ItemType.None);

            var flatForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            switch (item)
            {
                case ItemType.Booster:
                    _kart.ApplyBoost(_boosterPower, _boosterDuration);
                    break;
                case ItemType.Banana:
                    var banana = Instantiate(_bananaPrefab, transform.position - flatForward * _bananaDropDistance + Vector3.up * 0.5f,
                        Quaternion.LookRotation(flatForward));
                    banana.Place(_kart);
                    break;
                case ItemType.Shell:
                    var shell = Instantiate(_shellPrefab, transform.position + flatForward * _shellSpawnDistance + Vector3.up * 0.4f,
                        Quaternion.LookRotation(flatForward));
                    shell.Launch(flatForward, _kart);
                    break;
            }
            ItemUsed?.Invoke(item);
        }

        private void SetItem(ItemType item)
        {
            _current = item;
            ItemChanged?.Invoke(item);
        }
    }
}
