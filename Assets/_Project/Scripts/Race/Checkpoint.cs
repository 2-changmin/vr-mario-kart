using UnityEngine;

namespace VRKart.Race
{
    // 트랙 진행 방향 = transform.forward. 0번은 결승선.
    [RequireComponent(typeof(BoxCollider))]
    public sealed class Checkpoint : MonoBehaviour
    {
        private const float RespawnHeight = 0.5f;

        [SerializeField, Min(0)] private int _index;

        public int Index => _index;

        public Pose RespawnPose
        {
            get
            {
                Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
                return new Pose(transform.position + Vector3.up * RespawnHeight, Quaternion.LookRotation(forward, Vector3.up));
            }
        }

        private void Reset()
        {
            GetComponent<BoxCollider>().isTrigger = true;
            gameObject.layer = 2; // Ignore Raycast — 카트의 지면 Raycast에 걸리지 않게
        }

        private void OnTriggerEnter(Collider other)
        {
            RaceProgress progress = other.GetComponentInParent<RaceProgress>();
            if (progress != null) progress.NotifyCheckpoint(this);
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            BoxCollider box = GetComponent<BoxCollider>();
            if (box == null) return;

            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = _index == 0 ? new Color(1f, 1f, 1f, 0.9f) : new Color(0.2f, 0.8f, 1f, 0.8f);
            Gizmos.DrawWireCube(box.center, box.size);
            Gizmos.DrawLine(box.center, box.center + Vector3.forward * 4f);

            Gizmos.matrix = Matrix4x4.identity;
            UnityEditor.Handles.Label(transform.position + Vector3.up * (box.size.y + 0.5f), _index == 0 ? "CP 0 (결승선)" : "CP " + _index);
        }
#endif
    }
}
