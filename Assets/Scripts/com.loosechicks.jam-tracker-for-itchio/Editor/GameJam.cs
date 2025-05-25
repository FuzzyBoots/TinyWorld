using System;
using UnityEngine;

namespace JamTrackerItchio.Editor
{
    [Serializable]
    public class GameJam
    {
        public string Title;
        public string Url;
        public DateTime StartDate;
        public DateTime EndDate;
        public DateTime? VotingEndDate;
        public int JoinedCount;
        public bool IsHighlighted;
        public int JamId;

        // Cache time values to reduce DateTime.Now calls when not selected
        private TimeSpan _cachedTimeRemaining;
        private TimeSpan _cachedVotingTimeRemaining;
        private bool _isSelected;
        private DateTime CurrentTime => DateTime.Now;

        // Time-based calculations that accept a time parameter to support both real-time and cached scenarios
        public bool IsActiveAt(DateTime currentTime) =>
            currentTime >= StartDate && currentTime <= EndDate;

        public bool IsVotingPeriodAt(DateTime currentTime) =>
            currentTime > EndDate && VotingEndDate.HasValue && currentTime <= VotingEndDate.Value;

        public float GetProgressPercentage(DateTime currentTime) =>
            Mathf.Clamp01(
                (float)(currentTime - StartDate).TotalSeconds
                    / (float)(EndDate - StartDate).TotalSeconds
            );

        public bool IsActive => IsActiveAt(CurrentTime);
        public bool IsVotingPeriod => IsVotingPeriodAt(CurrentTime);
        public float ProgressPercentage => GetProgressPercentage(CurrentTime);

        public TimeSpan GetTimeRemainingAt(DateTime currentTime) =>
            _isSelected ? EndDate - currentTime : _cachedTimeRemaining;

        public TimeSpan GetVotingTimeRemainingAt(DateTime currentTime) =>
            _isSelected
                ? (VotingEndDate.HasValue ? VotingEndDate.Value - currentTime : TimeSpan.Zero)
                : _cachedVotingTimeRemaining;

        public TimeSpan TimeRemaining => GetTimeRemainingAt(CurrentTime);
        public TimeSpan VotingTimeRemaining => GetVotingTimeRemainingAt(CurrentTime);

        // Update cached values to reduce DateTime.Now calls for non-selected jams
        public void UpdateCachedTimesAt(DateTime currentTime)
        {
            _cachedTimeRemaining = EndDate - currentTime;
            _cachedVotingTimeRemaining = VotingEndDate.HasValue
                ? VotingEndDate.Value - currentTime
                : TimeSpan.Zero;
        }

        public void UpdateCachedTimes()
        {
            UpdateCachedTimesAt(CurrentTime);
        }

        public void SetSelected(bool isSelected)
        {
            _isSelected = isSelected;
            if (_isSelected)
            {
                UpdateCachedTimes();
            }
        }
    }
}
