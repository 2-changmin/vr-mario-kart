using UnityEngine;

namespace VRKart.UI
{
    public static class TimeFormat
    {
        // 83.4561 → "1:23.456"
        public static string Format(float seconds)
        {
            int totalMs = Mathf.Max(0, Mathf.RoundToInt(seconds * 1000f));
            int minutes = totalMs / 60000;
            int secs = totalMs / 1000 % 60;
            int ms = totalMs % 1000;
            return $"{minutes}:{secs:00}.{ms:000}";
        }
    }
}
