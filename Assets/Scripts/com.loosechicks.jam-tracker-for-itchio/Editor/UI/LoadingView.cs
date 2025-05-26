using System;
using UnityEditor;
using UnityEngine;

namespace JamTrackerItchio.Editor.UI
{
    /// <summary>
    /// View for displaying loading/recompiling state
    /// </summary>
    public class LoadingView : IJamTrackerView
    {
        private readonly double _recompileStartTime;
        private readonly Action _onForceRefresh;
        public const double RECOMPILE_TIMEOUT = 10.0; // Force initialization after 10 seconds

        // Reference to the last window that drew this view
        private EditorWindow _lastWindow;

        public LoadingView(double recompileStartTime, Action onForceRefresh)
        {
            _recompileStartTime = recompileStartTime;
            _onForceRefresh = onForceRefresh;

            // Store the current focused window when created
            _lastWindow = EditorWindow.focusedWindow;
        }

        public void Draw(DateTime now)
        {
            // Create a more visually appealing "loading" UI
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.HelpBox(
                "Editor is recompiling scripts or initializing. The window will automatically refresh when complete.",
                MessageType.Info
            );

            // Show the elapsed time since recompilation started
            double elapsedTime = EditorApplication.timeSinceStartup - _recompileStartTime;
            EditorGUILayout.LabelField($"Time elapsed: {elapsedTime:F1} seconds");

            // Show a progress indicator
            Rect progressRect = EditorGUILayout.GetControlRect(false, 20);
            float pulseValue = Mathf.PingPong(
                (float)EditorApplication.timeSinceStartup * 0.5f,
                1.0f
            );
            EditorGUI.ProgressBar(
                progressRect,
                pulseValue,
                "Waiting for compilation to complete..."
            );

            // Add a manual retry button that's more visible
            if (GUILayout.Button("Force Refresh Now", GUILayout.Height(30)))
            {
                _onForceRefresh?.Invoke();
            }

            EditorGUILayout.EndVertical();

            // Force repaint to update the progress bar animation
            if (Event.current.type == EventType.Repaint)
            {
                // Store the window that's drawing this view
                _lastWindow = EditorWindow.focusedWindow;

                // Schedule a repaint of this window
                EditorApplication.delayCall += () =>
                {
                    if (_lastWindow != null)
                    {
                        _lastWindow.Repaint();
                    }
                };
            }
        }
    }
}
