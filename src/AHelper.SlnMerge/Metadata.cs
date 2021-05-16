using System;

namespace AHelper.SlnMerge
{
    internal class Metadata
    {
        public static TimeSpan UpdateCheckInterval => TimeSpan.FromDays(7);
        public DateTime LastUpdateCheckTime { get; set; }
    }
}
