using System;

namespace Sleet
{
    /// <summary>
    /// Performance log message
    /// </summary>
    public abstract class PerfEntryBase : IComparable<PerfEntryBase>
    {
        /// <summary>
        /// Time the event took.
        /// </summary>
        public TimeSpan ElapsedTime { get; }

        /// <summary>
        /// Cutoff time. Only show the event if the time was greater than this time.
        /// </summary>
        public TimeSpan MinTimeToShow { get; }

        public PerfEntryBase(TimeSpan elapsedTime, TimeSpan minTimeToShow)
        {
            ElapsedTime = elapsedTime;
            MinTimeToShow = minTimeToShow;
        }

        /// <summary>
        /// Construct the message to display.
        /// </summary>
        public abstract string GetMessage(TimeSpan timeSpan);

        /// <summary>
        /// Key to merge on.
        /// </summary>
        public abstract string Key
        {
            get;
        }

        /// <summary>
        /// True if the event took long enough that it should be displayed.
        /// </summary>
        public bool ShouldShow()
        {
            return ElapsedTime >= MinTimeToShow;
        }

        public override string ToString()
        {
            return GetMessage(ElapsedTime);
        }

        public int CompareTo(PerfEntryBase? other)
        {
            if (other == null)
            {
                return 1;
            }
            return ElapsedTime.CompareTo(other.ElapsedTime);
        }

        public override bool Equals(object? obj)
        {
            if (obj is PerfEntryBase other)
            {
                return ElapsedTime == other.ElapsedTime && Key == other.Key;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(ElapsedTime, Key);
        }

        public static bool operator ==(PerfEntryBase? left, PerfEntryBase? right)
        {
            if (left is null)
            {
                return right is null;
            }
            return left.Equals(right);
        }

        public static bool operator !=(PerfEntryBase? left, PerfEntryBase? right)
        {
            return !(left == right);
        }

        public static bool operator <(PerfEntryBase? left, PerfEntryBase? right)
        {
            return left is null ? right is not null : left.CompareTo(right) < 0;
        }

        public static bool operator <=(PerfEntryBase? left, PerfEntryBase? right)
        {
            return left is null || left.CompareTo(right) <= 0;
        }

        public static bool operator >(PerfEntryBase? left, PerfEntryBase? right)
        {
            return left is not null && left.CompareTo(right) > 0;
        }

        public static bool operator >=(PerfEntryBase? left, PerfEntryBase? right)
        {
            return left is null ? right is null : left.CompareTo(right) >= 0;
        }
    }
}
