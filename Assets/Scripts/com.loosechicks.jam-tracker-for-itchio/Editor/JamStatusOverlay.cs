using System;
using UnityEditor;
using UnityEditor.Overlays;
using UnityEngine;
using UnityEngine.UIElements;

namespace JamTrackerItchio.Editor
{
    [Overlay(typeof(SceneView), "Itch.io Jam Status", true)]
    public class JamStatusOverlay : Overlay
    {
        private GameJam _currentJam;
        private Label _jamTitleLabel;
        private Label _timeInfoLabel;
        private ProgressBar _progressBar;
        private VisualElement _root;

        public override VisualElement CreatePanelContent()
        {
            try
            {
                var settings = JamTrackerSettings.LoadFromEditorPrefs();
                _currentJam = settings.ToGameJam();

                _root = new VisualElement();
                _root.style.flexDirection = FlexDirection.Column;
                _root.style.minWidth = 200;
                _root.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
                _root.style.alignItems = Align.Center;

                _jamTitleLabel = new Label();
                _jamTitleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                _jamTitleLabel.style.color = new Color(0.4f, 0.6f, 1.0f);
                _jamTitleLabel.style.marginBottom = 5;

                _jamTitleLabel.RegisterCallback<MouseEnterEvent>(evt =>
                    _jamTitleLabel.style.color = new Color(0.6f, 0.8f, 1.0f)
                );
                _jamTitleLabel.RegisterCallback<MouseLeaveEvent>(evt =>
                    _jamTitleLabel.style.color = new Color(0.4f, 0.6f, 1.0f)
                );

                _jamTitleLabel.RegisterCallback<ClickEvent>(evt =>
                {
                    if (_currentJam != null && !string.IsNullOrEmpty(_currentJam.Url))
                    {
                        Application.OpenURL(_currentJam.Url);
                    }
                });

                _root.Add(_jamTitleLabel);

                _progressBar = new ProgressBar();
                _progressBar.style.height = 16;
                _progressBar.style.width = Length.Percent(90);
                _progressBar.style.minWidth = 180;
                _progressBar.style.marginBottom = 5;
                _root.Add(_progressBar);

                _timeInfoLabel = new Label();
                _timeInfoLabel.style.fontSize = 11;
                _root.Add(_timeInfoLabel);

                EditorApplication.update += Update;

                UpdateUI();

                return _root;
            }
            catch (Exception)
            {
                // Create a simple fallback element if initialization fails
                var fallbackRoot = new VisualElement();
                var errorLabel = new Label("Jam Tracker is initializing...");
                errorLabel.style.color = Color.yellow;
                fallbackRoot.Add(errorLabel);

                EditorApplication.delayCall += () =>
                {
                    try
                    {
                        if (_root != null)
                        {
                            UpdateUI();
                        }
                    }
                    catch { }
                };

                return fallbackRoot;
            }
        }

        private void Update()
        {
            var settings = JamTrackerSettings.LoadFromEditorPrefs();
            var newJam = settings.ToGameJam();

            if (newJam?.JamId != _currentJam?.JamId)
            {
                _currentJam = newJam;
                UpdateUI();
            }

            if (DateTime.Now.Second % 1 == 0)
            {
                UpdateUI();
            }
        }

        private void UpdateUI()
        {
            try
            {
                if (_root == null)
                    return;

                if (_currentJam == null)
                {
                    _root.style.display = DisplayStyle.None;
                    return;
                }

                _root.style.display = DisplayStyle.Flex;
                _currentJam.UpdateCachedTimes();
                _jamTitleLabel.text = _currentJam.Title;

                DateTime now = DateTime.Now;

                if (_currentJam.IsActive)
                {
                    _progressBar.style.display = DisplayStyle.Flex;

                    TimeSpan elapsed = now - _currentJam.StartDate;
                    TimeSpan total = _currentJam.EndDate - _currentJam.StartDate;

                    float progress = _currentJam.ProgressPercentage;
                    _progressBar.value = progress * 100;
                    _progressBar.title = $"{progress * 100:F1}%";

                    // Set progress bar color based on progress (green to red gradient)
                    Color progressColor = GetProgressGradientColor(progress);

                    // Get the progress bar fill element safely
                    var progressBarFill = _progressBar?.Q(null, "unity-progress-bar__progress");
                    if (progressBarFill != null)
                    {
                        progressBarFill.style.backgroundColor = progressColor;
                    }

                    // Update time info
                    _timeInfoLabel.text =
                        $"Elapsed: {FormatTimeSpan(elapsed)} • Remaining: {FormatTimeSpan(_currentJam.TimeRemaining)}";
                }
                else if (now < _currentJam.StartDate)
                {
                    // Jam hasn't started yet - hide progress bar
                    _progressBar.style.display = DisplayStyle.None;

                    TimeSpan timeToStart = _currentJam.StartDate - now;
                    _timeInfoLabel.text = $"⌛Starts in: {FormatTimeSpan(timeToStart)}";
                }
                else
                {
                    // Jam has ended - ensure progress bar is visible
                    _progressBar.style.display = DisplayStyle.Flex;

                    // Jam has ended
                    _progressBar.value = 100;
                    _progressBar.title = "Completed";

                    // Set color to red for completed jams
                    var progressBarFill = _progressBar?.Q(null, "unity-progress-bar__progress");
                    if (progressBarFill != null)
                    {
                        progressBarFill.style.backgroundColor = new Color(0.9f, 0.2f, 0.2f); // Red
                    }

                    if (_currentJam.IsVotingPeriod)
                    {
                        _timeInfoLabel.text =
                            $"Jam completed • Voting: {FormatTimeSpan(_currentJam.VotingTimeRemaining)}";
                    }
                    else
                    {
                        _timeInfoLabel.text = "Jam completed";
                    }
                }
            }
            catch (Exception ex)
            {
                // Silently catch exceptions during UI updates
                Debug.LogWarning($"Error updating JamStatusOverlay: {ex.Message}");
            }
        }

        // Returns a color along a green-yellow-orange-red gradient based on progress (0-1)
        private Color GetProgressGradientColor(float progress)
        {
            // Define gradient key points
            Color green = new Color(0.2f, 0.8f, 0.2f); // Green at start
            Color yellow = new Color(0.9f, 0.9f, 0.2f); // Yellow at ~33%
            Color orange = new Color(0.9f, 0.6f, 0.1f); // Orange at ~66%
            Color red = new Color(0.9f, 0.2f, 0.2f); // Red at end

            if (progress < 0.33f)
            {
                // Interpolate between green and yellow
                return Color.Lerp(green, yellow, progress / 0.33f);
            }
            else if (progress < 0.66f)
            {
                // Interpolate between yellow and orange
                return Color.Lerp(yellow, orange, (progress - 0.33f) / 0.33f);
            }
            else
            {
                // Interpolate between orange and red
                return Color.Lerp(orange, red, (progress - 0.66f) / 0.34f);
            }
        }

        private string FormatTimeSpan(TimeSpan timeSpan)
        {
            if (timeSpan.TotalDays > 1)
            {
                return $"{(int)timeSpan.TotalDays}d {timeSpan.Hours}h";
            }
            else if (timeSpan.TotalHours > 1)
            {
                return $"{(int)timeSpan.TotalHours}h {timeSpan.Minutes}m";
            }
            else
            {
                return $"{timeSpan.Minutes}m {timeSpan.Seconds}s";
            }
        }

        public override void OnWillBeDestroyed()
        {
            // Unsubscribe from update
            EditorApplication.update -= Update;
            base.OnWillBeDestroyed();
        }
    }
}
