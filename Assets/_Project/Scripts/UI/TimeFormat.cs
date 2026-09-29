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

        // 83.46 → "1:23.4" (HUD처럼 자주 바뀌는 곳용, 0.1초 단위)
        public static string FormatTenths(float seconds)
        {
            int totalTenths = Mathf.Max(0, Mathf.FloorToInt(seconds * 10f));
            int minutes = totalTenths / 600;
            int secs = totalTenths / 10 % 60;
            return $"{minutes}:{secs:00}.{totalTenths % 10}";
        }
    }
}
