#if HAS_TOOLBAR_EXTENDER
using System;
using Paps.UnityToolbarExtenderUIToolkit;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace JamTrackerItchio.Editor
{
    [MainToolbarElement(id: "JamTrackerToolbar", ToolbarAlign.Right)]
    public class JamToolbarExtension : VisualElement
    {
        [Serialize]
        private int _currentJamId = -1;

        private GameJam _currentJam;
        private VisualElement _jamInfoContainer;
        private ProgressBar _progressBar;
        private Label _elapsedTimeLabel;
        private Label _remainingTimeLabel;
        private Label _jamTitleLabel;

        public void InitializeElement()
        {
            try
            {
                var settings = JamTrackerSettings.LoadFromEditorPrefs();
                _currentJam = settings.ToGameJam();

                if (_currentJam != null)
                {
                    _currentJamId = _currentJam.JamId;
                }

                // Create a container for all jam-related information with consistent styling
                _jamInfoContainer = new VisualElement();
                _jamInfoContainer.style.flexDirection = FlexDirection.Row;
                _jamInfoContainer.style.alignItems = Align.Center;
                _jamInfoContainer.style.maxWidth = 500;
                _jamInfoContainer.style.height = 24;
                _jamInfoContainer.style.justifyContent = Justify.FlexStart;

                // Show/hide based on user preferences
                bool shouldDisplay =
                    _currentJam != null
                    && (
                        settings.DisplayIn == JamTrackerSettings.DisplayLocation.MainToolbar
                        || settings.DisplayIn == JamTrackerSettings.DisplayLocation.Both
                    );

                _jamInfoContainer.style.display = shouldDisplay
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;

                // Create clickable jam title that opens the jam page
                _jamTitleLabel = new Label();
                _jamTitleLabel.style.marginRight = 5;
                _jamTitleLabel.style.maxWidth = 200;
                _jamTitleLabel.style.overflow = Overflow.Hidden;
                _jamTitleLabel.style.textOverflow = TextOverflow.Ellipsis;
                _jamTitleLabel.style.whiteSpace = WhiteSpace.NoWrap;
                _jamTitleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                _jamTitleLabel.style.color = new Color(0.4f, 0.6f, 1.0f);
                _jamTitleLabel.style.alignSelf = Align.Center;
                _jamTitleLabel.style.unityTextAlign = TextAnchor.MiddleLeft;

                // Visual feedback for clickable title
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

                _jamInfoContainer.Add(_jamTitleLabel);

                // Container for progress tracking elements
                var infoContainer = new VisualElement();
                infoContainer.style.flexDirection = FlexDirection.Row;
                infoContainer.style.flexGrow = 1;
                _jamInfoContainer.Add(infoContainer);

                var progressBarContainer = new VisualElement();
                progressBarContainer.style.flexGrow = 1;
                progressBarContainer.style.marginRight = 10;
                infoContainer.Add(progressBarContainer);

                _progressBar = new ProgressBar();
                _progressBar.style.height = 16;
                _progressBar.style.minWidth = 100;
                progressBarContainer.Add(_progressBar);

                // Container for elapsed and remaining time information
                var timeLabelsContainer = new VisualElement();
                timeLabelsContainer.style.flexDirection = FlexDirection.Column;
                timeLabelsContainer.style.minWidth = 180;
                timeLabelsContainer.style.justifyContent = Justify.Center;
                infoContainer.Add(timeLabelsContainer);

                _elapsedTimeLabel = new Label();
                _elapsedTimeLabel.style.fontSize = 10;
                _elapsedTimeLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
                timeLabelsContainer.Add(_elapsedTimeLabel);

                _remainingTimeLabel = new Label();
                _remainingTimeLabel.style.fontSize = 10;
                _remainingTimeLabel.style.display = DisplayStyle.None;
                _remainingTimeLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
                timeLabelsContainer.Add(_remainingTimeLabel);

                Add(_jamInfoContainer);

                EditorApplication.update += Update;
                UpdateUI();
            }
            catch (Exception)
            {
                var errorLabel = new Label("Jam Tracker initializing...");
                Add(errorLabel);

                EditorApplication.delayCall += () =>
                {
                    try
                    {
                        Clear();
                        InitializeElement();
                    }
                    catch { }
                };
            }
        }

        ~JamToolbarExtension()
        {
            EditorApplication.update -= Update;
        }

        private void Update()
        {
            var settings = JamTrackerSettings.LoadFromEditorPrefs();
            var newJam = settings.ToGameJam();

            bool jamChanged = newJam?.JamId != _currentJam?.JamId;
            bool shouldDisplay =
                newJam != null
                && (
                    settings.DisplayIn == JamTrackerSettings.DisplayLocation.MainToolbar
                    || settings.DisplayIn == JamTrackerSettings.DisplayLocation.Both
                );

            if (jamChanged)
            {
                _currentJam = newJam;
                _currentJamId = _currentJam?.JamId ?? -1;
            }

            _jamInfoContainer.style.display = shouldDisplay ? DisplayStyle.Flex : DisplayStyle.None;

            if (DateTime.Now.Second % 1 == 0)
            {
                UpdateUI();
            }
        }

        private void UpdateUI()
        {
            try
            {
                if (_currentJam == null)
                    return;

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

                    Color progressColor = GetProgressGradientColor(progress);
                    var progressBarFill = _progressBar?.Q(null, "unity-progress-bar__progress");
                    if (progressBarFill != null)
                    {
                        progressBarFill.style.backgroundColor = progressColor;
                    }

                    _elapsedTimeLabel.text =
                        $"⏱️ {FormatTimeSpan(elapsed)} • {FormatTimeSpan(_currentJam.TimeRemaining)} left";
                    _remainingTimeLabel.style.display = DisplayStyle.None;
                }
                else if (now < _currentJam.StartDate)
                {
                    _progressBar.style.display = DisplayStyle.None;

                    TimeSpan timeToStart = _currentJam.StartDate - now;
                    _elapsedTimeLabel.text = $"⌛ Starts in {FormatTimeSpan(timeToStart)}";
                    _remainingTimeLabel.style.display = DisplayStyle.None;
                }
                else
                {
                    _progressBar.style.display = DisplayStyle.None;

                    if (_currentJam.IsVotingPeriod)
                    {
                        _elapsedTimeLabel.text =
                            $"🗳️ Voting ends in {FormatTimeSpan(_currentJam.VotingTimeRemaining)}";
                        _remainingTimeLabel.style.display = DisplayStyle.None;
                    }
                    else
                    {
                        _elapsedTimeLabel.text = "✨ Jam Complete!";
                        _remainingTimeLabel.style.display = DisplayStyle.None;
                    }
                }
            }
            catch (Exception)
            {
                // Fail silently and try again next frame
            }
        }

        private Color GetProgressGradientColor(float progress)
        {
            float hue = Mathf.Lerp(0.3f, 0.0f, progress);
            return Color.HSVToRGB(hue, 0.7f, 0.7f);
        }

        private string FormatTimeSpan(TimeSpan timeSpan)
        {
            if (timeSpan.TotalDays >= 1)
            {
                return $"{timeSpan.Days}d {timeSpan.Hours}h";
            }
            else if (timeSpan.TotalHours >= 1)
            {
                return $"{timeSpan.Hours}h {timeSpan.Minutes}m";
            }
            else
            {
                return $"{timeSpan.Minutes}m {timeSpan.Seconds}s";
            }
        }
    }
}
#endif
