using System;

namespace JamTrackerItchio.Editor.UI
{
    /// <summary>
    /// Interface for UI views in the Jam Tracker window
    /// </summary>
    public interface IJamTrackerView
    {
        /// <summary>
        /// Draw the view in the current GUI context
        /// </summary>
        /// <param name="now">Current time to use for calculations</param>
        void Draw(DateTime now);
    }
}
