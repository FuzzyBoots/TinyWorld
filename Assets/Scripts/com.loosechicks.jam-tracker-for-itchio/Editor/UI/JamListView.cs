using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace JamTrackerItchio.Editor.UI
{
    /// <summary>
    /// View for displaying the list of jams with pagination
    /// </summary>
    public class JamListView : IJamTrackerView
    {
        private readonly JamListManager _jamListManager;
        private readonly PaginationController _paginationController;
        private readonly JamTrackerStyles _styles;
        private readonly Action _onJamSaved;
        private Vector2 _scrollPosition;

        public JamListView(
            JamListManager jamListManager,
            PaginationController paginationController,
            JamTrackerStyles styles,
            Action onJamSaved
        )
        {
            _jamListManager = jamListManager;
            _paginationController = paginationController;
            _styles = styles;
            _onJamSaved = onJamSaved;
        }

        public void Draw(DateTime now)
        {
            if (_jamListManager.IsLoading)
            {
                EditorGUILayout.HelpBox("Loading jams from itch.io...", MessageType.Info);
                return;
            }

            List<GameJam> filteredJams = _jamListManager.FilteredJams;

            if (filteredJams.Count == 0)
            {
                EditorGUILayout.HelpBox("No jams found matching your criteria.", MessageType.Info);
                return;
            }

            DrawListHeader(filteredJams.Count);
            DrawJamItems(filteredJams, now);
            DrawPaginationControls();
        }

        private void DrawListHeader(int totalCount)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label($"Available Jams ({totalCount} total)", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            GUILayout.Label(
                $"Page {_paginationController.CurrentPage + 1} of {_paginationController.TotalPages}"
            );
            EditorGUILayout.EndHorizontal();

            // Update pagination with current list
            _paginationController.UpdateItemCount(totalCount);
        }

        private void DrawJamItems(List<GameJam> filteredJams, DateTime now)
        {
            // Get the items for the current page
            int startIndex = _paginationController.StartIndex;
            int endIndex = _paginationController.EndIndex;

            // Show jam list
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            // Display jams for current page
            for (int i = startIndex; i < endIndex; i++)
            {
                var jam = filteredJams[i];
                DrawJamItem(jam, now);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawJamItem(GameJam jam, DateTime now)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();

            // Title and status
            EditorGUILayout.BeginVertical();

            // Get the appropriate style and prefix based on jam status
            GUIStyle titleStyle;
            string prefix;

            if (jam.IsActiveAt(now))
            {
                titleStyle = _styles.ActiveTitleStyle;
                prefix = "🟢 | ";
            }
            else if (jam.IsVotingPeriodAt(now))
            {
                titleStyle = _styles.VotingTitleStyle;
                prefix = "🗳️ | ";
            }
            else if (now < jam.StartDate)
            {
                titleStyle = _styles.UpcomingTitleStyle;
                prefix = "🔵 | ";
            }
            else
            {
                titleStyle = _styles.EndedTitleStyle;
                prefix = "🔴 | ";
            }

            // Draw the title as a clickable link
            Rect titleRect = EditorGUILayout.GetControlRect();
            GUI.Label(titleRect, prefix + jam.Title, titleStyle);

            // Check if the title was clicked
            if (
                Event.current.type == EventType.MouseDown
                && Event.current.button == 0
                && titleRect.Contains(Event.current.mousePosition)
            )
            {
                // Open the jam URL
                Application.OpenURL(jam.Url);
                Event.current.Use(); // Consume the event
            }

            // Add a tooltip to show the URL on hover
            EditorGUIUtility.AddCursorRect(titleRect, MouseCursor.Link);

            // Dates
            string dateInfo;
            if (jam.IsActiveAt(now))
            {
                dateInfo =
                    $"Ends: {jam.EndDate.ToShortDateString()} ({JamTrackerUtils.FormatTimeSpan(jam.GetTimeRemainingAt(now))} left)";
            }
            else if (jam.IsVotingPeriodAt(now))
            {
                dateInfo =
                    $"Voting ends: {jam.VotingEndDate?.ToShortDateString()} ({JamTrackerUtils.FormatTimeSpan(jam.GetVotingTimeRemainingAt(now))} left)";
            }
            else if (now < jam.StartDate)
            {
                TimeSpan timeToStart = jam.StartDate - now;
                dateInfo =
                    $"Starts: {jam.StartDate.ToShortDateString()} in {JamTrackerUtils.FormatTimeSpan(timeToStart)}";
            }
            else
            {
                dateInfo = $"Ended: {jam.EndDate.ToShortDateString()}";
            }

            EditorGUILayout.LabelField(dateInfo);
            EditorGUILayout.LabelField($"Participants: {jam.JoinedCount}");
            EditorGUILayout.EndVertical();

            // Select button
            if (GUILayout.Button("Select", GUILayout.Width(60), GUILayout.Height(40)))
            {
                _jamListManager.SelectJam(jam);
                _onJamSaved?.Invoke();
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private void DrawPaginationControls()
        {
            EditorGUILayout.BeginHorizontal();

            // Previous page button
            GUI.enabled = _paginationController.CanGoToPreviousPage;
            if (GUILayout.Button("Previous Page"))
            {
                _paginationController.PreviousPage();
                _scrollPosition = Vector2.zero; // Reset scroll position when changing pages
            }

            // Page size dropdown
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Items per page:", GUILayout.Width(100));
            string[] pageSizeOptions = { "5", "10", "15", "20", "25" };
            int[] pageSizeValues = { 5, 10, 15, 20, 25 };
            int selectedPageSizeIndex = Array.IndexOf(
                pageSizeValues,
                _paginationController.PageSize
            );
            if (selectedPageSizeIndex < 0)
                selectedPageSizeIndex = 1; // Default to 10

            int newSelectedIndex = EditorGUILayout.Popup(selectedPageSizeIndex, pageSizeOptions);
            if (newSelectedIndex != selectedPageSizeIndex)
            {
                _paginationController.PageSize = pageSizeValues[newSelectedIndex];
                _scrollPosition = Vector2.zero;
            }
            EditorGUILayout.EndHorizontal();

            // Next page button
            GUI.enabled = _paginationController.CanGoToNextPage;
            if (GUILayout.Button("Next Page"))
            {
                _paginationController.NextPage();
                _scrollPosition = Vector2.zero; // Reset scroll position when changing pages
            }

            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();
        }
    }
}
