using System;
using UnityEditor;
using UnityEngine;

namespace JamTrackerItchio.Editor.UI
{
    /// <summary>
    /// View for displaying selected jam information
    /// </summary>
    public class SelectedJamView : IJamTrackerView
    {
        private readonly JamListManager _jamListManager;
        private readonly Action _onJamSaved;

        public SelectedJamView(JamListManager jamListManager, Action onJamSaved)
        {
            _jamListManager = jamListManager;
            _onJamSaved = onJamSaved;
        }

        public void Draw(DateTime now)
        {
            GameJam selectedJam = _jamListManager.SelectedJam;

            if (selectedJam == null)
                return;

            GUILayout.Label("Currently Selected Jam", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label(selectedJam.Title, EditorStyles.largeLabel);

            DrawJamStatus(selectedJam, now);
            DrawDisplayOptions();

            if (GUILayout.Button("Deselect"))
            {
                _jamListManager.DeselectJam();
                _onJamSaved?.Invoke();
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(10);
        }

        private void DrawJamStatus(GameJam selectedJam, DateTime now)
        {
            if (selectedJam.IsActiveAt(now))
            {
                // Show progress bar
                EditorGUILayout.LabelField("Progress:");
                float progressPercentage = selectedJam.GetProgressPercentage(now);
                EditorGUI.ProgressBar(
                    EditorGUILayout.GetControlRect(false, 20),
                    progressPercentage,
                    $"{progressPercentage * 100:F1}%"
                );

                // Time remaining
                string timeRemaining = JamTrackerUtils.FormatTimeSpan(
                    selectedJam.GetTimeRemainingAt(now)
                );
                EditorGUILayout.LabelField("Time Remaining:", timeRemaining);
            }
            else if (selectedJam.IsVotingPeriodAt(now))
            {
                EditorGUILayout.HelpBox(
                    "Submission period has ended. Voting is now open!",
                    MessageType.Info
                );
                string votingTimeRemaining = JamTrackerUtils.FormatTimeSpan(
                    selectedJam.GetVotingTimeRemainingAt(now)
                );
                EditorGUILayout.LabelField("Voting Ends In:", votingTimeRemaining);
            }
            else if (now < selectedJam.StartDate)
            {
                EditorGUILayout.HelpBox("This jam hasn't started yet.", MessageType.Info);
                string timeToStart = JamTrackerUtils.FormatTimeSpan(selectedJam.StartDate - now);
                EditorGUILayout.LabelField("Starts In:", timeToStart);
            }
            else
            {
                EditorGUILayout.HelpBox("This jam has ended.", MessageType.Warning);
            }

            if (GUILayout.Button("Open Jam Page"))
            {
                Application.OpenURL(selectedJam.Url);
            }
        }

        private void DrawDisplayOptions()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Display Options", EditorStyles.boldLabel);

            var settings = JamTrackerSettings.LoadFromEditorPrefs();
            JamTrackerSettings.DisplayLocation displayLocation = settings.DisplayIn;

            // Draw custom enum popup with disabled options
            JamTrackerSettings.DisplayLocation newDisplayLocation = DrawDisplayLocationDropdown(
                displayLocation
            );

            if (newDisplayLocation != displayLocation)
            {
                settings.DisplayIn = newDisplayLocation;
                settings.SaveToEditorPrefs();
            }
        }

        private JamTrackerSettings.DisplayLocation DrawDisplayLocationDropdown(
            JamTrackerSettings.DisplayLocation currentValue
        )
        {
            bool isToolbarExtenderInstalled = DependencyUtils.IsToolbarExtenderInstalled;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Show Jam Tracker In:", GUILayout.Width(140));

            // Store the original GUI enabled state
            bool originalEnabled = GUI.enabled;

            // Create array of options
            var options =
                Enum.GetValues(typeof(JamTrackerSettings.DisplayLocation))
                as JamTrackerSettings.DisplayLocation[];
            var displayNames = new string[options.Length];

            for (int i = 0; i < options.Length; i++)
            {
                string name = options[i].ToString();

                // Add suffix for options requiring toolbar extender
                if (
                    (
                        options[i] == JamTrackerSettings.DisplayLocation.MainToolbar
                        || options[i] == JamTrackerSettings.DisplayLocation.Both
                    ) && !isToolbarExtenderInstalled
                )
                {
                    name += " (requires Toolbar Extender)";
                }

                displayNames[i] = name;
            }

            int selectedIndex = Array.IndexOf(options, currentValue);

            // For each option, check if it should be enabled
            for (int i = 0; i < options.Length; i++)
            {
                // Skip initial iteration, we just want to draw the popup
                if (i == 0)
                    continue;

                // Get the next option
                var option = options[i];

                // If we're about to draw the currently selected index
                if (i == selectedIndex)
                {
                    // If we're showing a disabled option, show it but disable the control
                    if (
                        (
                            option == JamTrackerSettings.DisplayLocation.MainToolbar
                            || option == JamTrackerSettings.DisplayLocation.Both
                        ) && !isToolbarExtenderInstalled
                    )
                    {
                        GUI.enabled = false;
                    }
                }
            }

            int newSelectedIndex = EditorGUILayout.Popup(selectedIndex, displayNames);

            // Restore the original enabled state
            GUI.enabled = originalEnabled;

            EditorGUILayout.EndHorizontal();

            // If toolbar extender is not installed, validate the selection
            JamTrackerSettings.DisplayLocation result = options[newSelectedIndex];
            if (!JamTrackerSettings.IsDisplayLocationValid(result))
            {
                // If an invalid option was selected, revert to scene view toolbar
                result = JamTrackerSettings.DisplayLocation.SceneViewToolbar;

                // Show a helpful message
                EditorGUILayout.HelpBox(
                    "Unity Toolbar Extender is required for this display option. Please install it first.",
                    MessageType.Warning
                );
            }

            return result;
        }
    }
}
