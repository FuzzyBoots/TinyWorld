using System;
using UnityEditor;
using UnityEngine;

namespace JamTrackerItchio.Editor.UI
{
    /// <summary>
    /// View for search and filter controls
    /// </summary>
    public class SearchFilterView : IJamTrackerView
    {
        private readonly JamListManager _jamListManager;

        public SearchFilterView(JamListManager jamListManager)
        {
            _jamListManager = jamListManager;
        }

        public void Draw(DateTime now)
        {
            DrawSearchField();
            DrawFilterButtons();
        }

        private void DrawSearchField()
        {
            GUILayout.Label("Search and Filter", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Search:", GUILayout.Width(50));
            string newQuery = EditorGUILayout.TextField(_jamListManager.SearchQuery);
            if (newQuery != _jamListManager.SearchQuery)
            {
                _jamListManager.SearchQuery = newQuery;
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawFilterButtons()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Filter by status:", GUILayout.Width(100));

            // Create styles for the filter buttons
            GUIStyle normalButtonStyle = new GUIStyle(GUI.skin.button);
            GUIStyle selectedButtonStyle = new GUIStyle(GUI.skin.button);
            selectedButtonStyle.normal.background = selectedButtonStyle.active.background;

            // All button
            if (
                GUILayout.Button(
                    "All",
                    _jamListManager.CurrentFilter == JamFilterState.All
                        ? selectedButtonStyle
                        : normalButtonStyle
                )
            )
            {
                _jamListManager.CurrentFilter = JamFilterState.All;
            }

            // Active button (green circle)
            if (
                GUILayout.Button(
                    "🟢 Active",
                    _jamListManager.CurrentFilter == JamFilterState.Active
                        ? selectedButtonStyle
                        : normalButtonStyle
                )
            )
            {
                _jamListManager.CurrentFilter = JamFilterState.Active;
            }

            // Voting button (ballot box)
            if (
                GUILayout.Button(
                    "🗳️ Voting",
                    _jamListManager.CurrentFilter == JamFilterState.Voting
                        ? selectedButtonStyle
                        : normalButtonStyle
                )
            )
            {
                _jamListManager.CurrentFilter = JamFilterState.Voting;
            }

            // Upcoming button (blue circle)
            if (
                GUILayout.Button(
                    "🔵 Upcoming",
                    _jamListManager.CurrentFilter == JamFilterState.Upcoming
                        ? selectedButtonStyle
                        : normalButtonStyle
                )
            )
            {
                _jamListManager.CurrentFilter = JamFilterState.Upcoming;
            }

            // Ended button (red circle)
            if (
                GUILayout.Button(
                    "🔴 Ended",
                    _jamListManager.CurrentFilter == JamFilterState.Ended
                        ? selectedButtonStyle
                        : normalButtonStyle
                )
            )
            {
                _jamListManager.CurrentFilter = JamFilterState.Ended;
            }

            EditorGUILayout.EndHorizontal();
        }
    }
}
