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

        // 1 → "1st", 2 → "2nd", 11 → "11th"
        public static string Ordinal(int n)
        {
            int lastTwo = n % 100;
            if (lastTwo >= 11 && lastTwo <= 13) return n + "th";
            switch (n % 10)
            {
                case 1: return n + "st";
                case 2: return n + "nd";
                case 3: return n + "rd";
                default: return n + "th";
            }
        }
    }
}
