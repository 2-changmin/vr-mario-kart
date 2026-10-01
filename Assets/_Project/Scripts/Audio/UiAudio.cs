using UnityEngine;
using UnityEngine.UI;

namespace VRKart.Audio
{
    // 씬의 모든 버튼(꺼져 있는 것 포함)에 클릭음을 붙인다. 씬에 1개. 일시정지 중에도 들린다.
    [RequireComponent(typeof(AudioSource))]
    public sealed class UiAudio : MonoBehaviour
    {
        [SerializeField] private AudioClip _click;
        [SerializeField, Range(0f, 1f)] private float _volume = 0.7f;

        private AudioSource _source;

        private void Awake()
        {
            _source = GetComponent<AudioSource>();
            _source.spatialBlend = 0f;
            _source.playOnAwake = false;
            _source.ignoreListenerPause = true;
        }

        private void Start()
        {
            foreach (var button in FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                button.onClick.AddListener(PlayClick);
        }

        public void PlayClick()
        {
            if (_click != null) _source.PlayOneShot(_click, _volume * AudioVolumes.Sfx);
        }
    }
}
