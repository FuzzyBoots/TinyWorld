using System;
using UnityEditor;
using UnityEngine;

namespace JamTrackerItchio.Editor
{
    [Serializable]
    public class JamTrackerSettings
    {
        public enum DisplayLocation
        {
            [Tooltip("Always available")]
            SceneViewToolbar,

            [Tooltip("Requires Unity Toolbar Extender")]
            MainToolbar,

            [Tooltip("Requires Unity Toolbar Extender")]
            Both,
        }

        public int SelectedJamId = -1;
        public string SelectedJamTitle = "";
        public string SelectedJamUrl = "";
        public string StartDateStr = "";
        public string EndDateStr = "";
        public string VotingEndDateStr = "";
        public DisplayLocation DisplayIn = DisplayLocation.SceneViewToolbar;

        public static bool IsDisplayLocationValid(DisplayLocation location)
        {
            if (location == DisplayLocation.SceneViewToolbar)
                return true;

            return DependencyUtils.IsToolbarExtenderInstalled;
        }

        public DisplayLocation ValidateDisplayLocation(DisplayLocation location)
        {
            if (IsDisplayLocationValid(location))
                return location;

            return DisplayLocation.SceneViewToolbar;
        }

        public void SaveToEditorPrefs()
        {
            DisplayIn = ValidateDisplayLocation(DisplayIn);

            EditorPrefs.SetInt("JamTracker_SelectedJamId", SelectedJamId);
            EditorPrefs.SetString("JamTracker_SelectedJamTitle", SelectedJamTitle);
            EditorPrefs.SetString("JamTracker_SelectedJamUrl", SelectedJamUrl);
            EditorPrefs.SetString("JamTracker_StartDate", StartDateStr);
            EditorPrefs.SetString("JamTracker_EndDate", EndDateStr);
            EditorPrefs.SetString("JamTracker_VotingEndDate", VotingEndDateStr);
            EditorPrefs.SetInt("JamTracker_DisplayIn", (int)DisplayIn);
        }

        public static JamTrackerSettings LoadFromEditorPrefs()
        {
            var settings = new JamTrackerSettings
            {
                SelectedJamId = EditorPrefs.GetInt("JamTracker_SelectedJamId", -1),
                SelectedJamTitle = EditorPrefs.GetString("JamTracker_SelectedJamTitle", ""),
                SelectedJamUrl = EditorPrefs.GetString("JamTracker_SelectedJamUrl", ""),
                StartDateStr = EditorPrefs.GetString("JamTracker_StartDate", ""),
                EndDateStr = EditorPrefs.GetString("JamTracker_EndDate", ""),
                VotingEndDateStr = EditorPrefs.GetString("JamTracker_VotingEndDate", ""),
                DisplayIn = (DisplayLocation)
                    EditorPrefs.GetInt(
                        "JamTracker_DisplayIn",
                        (int)DisplayLocation.SceneViewToolbar
                    ),
            };

            settings.DisplayIn = settings.ValidateDisplayLocation(settings.DisplayIn);

            return settings;
        }

        public GameJam ToGameJam()
        {
            if (SelectedJamId == -1)
                return null;

            return new GameJam
            {
                JamId = SelectedJamId,
                Title = SelectedJamTitle,
                Url = SelectedJamUrl,
                StartDate = ParseSavedDateTime(StartDateStr),
                EndDate = ParseSavedDateTime(EndDateStr, DateTime.MaxValue),
                VotingEndDate = string.IsNullOrEmpty(VotingEndDateStr)
                    ? (DateTime?)null
                    : ParseSavedDateTime(VotingEndDateStr),
            };
        }

        private DateTime ParseSavedDateTime(string dateStr, DateTime defaultValue = default)
        {
            if (string.IsNullOrEmpty(dateStr))
                return defaultValue;

            try
            {
                DateTime parsedDate = DateTime.Parse(dateStr);

                if (dateStr.EndsWith("Z"))
                {
                    return DateTime.SpecifyKind(parsedDate, DateTimeKind.Utc).ToLocalTime();
                }

                return parsedDate;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError(
                    $"Error parsing saved date: {dateStr}, Error: {ex.Message}"
                );
                return defaultValue;
            }
        }

        public void FromGameJam(GameJam jam)
        {
            if (jam == null)
            {
                SelectedJamId = -1;
                SelectedJamTitle = "";
                SelectedJamUrl = "";
                StartDateStr = "";
                EndDateStr = "";
                VotingEndDateStr = "";
                return;
            }

            SelectedJamId = jam.JamId;
            SelectedJamTitle = jam.Title;
            SelectedJamUrl = jam.Url;
            StartDateStr = jam.StartDate.ToString("o");
            EndDateStr = jam.EndDate.ToString("o");
            VotingEndDateStr = jam.VotingEndDate?.ToString("o") ?? "";
        }
    }
}
