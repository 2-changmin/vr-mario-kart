using System;
using UnityEngine;
using VRKart.Core;
using VRKart.Race;

namespace VRKart.Items
{
    // 아이템 박스 (FR-ITEM-01, 02). 지나간 카트의 ItemHolder에 아이템 1개를 준다(이미 들고 있으면 무시).
    // 획득되면 _respawnTime초 동안 사라졌다가 다시 생긴다. 트리거는 Ignore Raycast 레이어.
    [RequireComponent(typeof(BoxCollider))]
    public sealed class ItemBox : MonoBehaviour
    {
        [SerializeField] private GameObject _visual;
        [SerializeField, Min(0f)] private float _respawnTime = 3f;
        [SerializeField] private float _spinSpeed = 90f;   // 도/초 (박스 모양만 돈다)

        private RaceManager _race;
        private BoxCollider _trigger;
        private bool _available = true;
        private float _respawnAt;

        public event Action<ItemHolder, ItemType> PickedUp;   // 효과음용

        private void Awake()
        {
            _trigger = GetComponent<BoxCollider>();
            _trigger.isTrigger = true;
            _race = FindAnyObjectByType<RaceManager>();
        }

        private void Update()
        {
            if (!_available && Time.time >= _respawnAt) SetAvailable(true);
            if (_available && _visual != null) _visual.transform.Rotate(0f, _spinSpeed * Time.deltaTime, 0f, Space.World);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!_available) return;
            var holder = other.GetComponentInParent<ItemHolder>();
            if (holder == null || holder.CurrentItem != ItemType.None) return;

            // 참가자 수는 획득할 때만 센다 (GetResults는 리스트를 새로 만든다)
            int count = _race != null ? _race.GetResults().Count : 1;
            var item = ItemRoll.Roll(holder.GetComponent<IRaceParticipant>(), count);
            if (!holder.TryGive(item)) return;

            SetAvailable(false);
            _respawnAt = Time.time + _respawnTime;
            PickedUp?.Invoke(holder, item);
        }

        private void SetAvailable(bool available)
        {
            _available = available;
            _trigger.enabled = available;
            if (_visual != null) _visual.SetActive(available);
        }
    }
}
