using UnityEditor;
using UnityEngine;

namespace JamTrackerItchio.Editor
{
    public class JamTrackerStyles
    {
        private GUIStyle _titleStyle;
        private GUIStyle _activeTitleStyle;
        private GUIStyle _votingTitleStyle;
        private GUIStyle _upcomingTitleStyle;
        private GUIStyle _endedTitleStyle;

        private bool _stylesInitialized = false;

        public GUIStyle TitleStyle
        {
            get
            {
                EnsureStylesInitialized();
                return _titleStyle ?? EditorGUIUtility.GetBuiltinSkin(EditorSkin.Inspector).label;
            }
        }

        public GUIStyle ActiveTitleStyle
        {
            get
            {
                EnsureStylesInitialized();
                return _activeTitleStyle ?? TitleStyle;
            }
        }

        public GUIStyle VotingTitleStyle
        {
            get
            {
                EnsureStylesInitialized();
                return _votingTitleStyle ?? TitleStyle;
            }
        }

        public GUIStyle UpcomingTitleStyle
        {
            get
            {
                EnsureStylesInitialized();
                return _upcomingTitleStyle ?? TitleStyle;
            }
        }

        public GUIStyle EndedTitleStyle
        {
            get
            {
                EnsureStylesInitialized();
                return _endedTitleStyle ?? TitleStyle;
            }
        }

        public JamTrackerStyles()
        {
            // No initialization in constructor - will be done when styles are accessed
        }

        private void EnsureStylesInitialized()
        {
            if (_stylesInitialized)
                return;

            if (EditorStyles.label != null)
            {
                try
                {
                    InitializeStyles();
                    _stylesInitialized = true;
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"Failed to initialize JamTrackerStyles: {e.Message}");
                }
            }
        }

        private void InitializeStyles()
        {
            _titleStyle = new GUIStyle(EditorStyles.label);
            _titleStyle.fontStyle = FontStyle.Bold;

            // Status-specific title styles
            _activeTitleStyle = new GUIStyle(_titleStyle);
            _activeTitleStyle.normal.textColor = new Color(0.2f, 0.8f, 0.4f, 1.000f);
            _activeTitleStyle.hover.textColor = new Color(0.2f, 0.9f, 0.4f, 1.000f);

            _votingTitleStyle = new GUIStyle(_titleStyle);
            _votingTitleStyle.normal.textColor = new Color(0.643f, 0.714f, 0.780f, 1.000f);
            _votingTitleStyle.hover.textColor = new Color(0.839f, 0.918f, 0.980f, 1.000f);

            _upcomingTitleStyle = new GUIStyle(_titleStyle);
            _upcomingTitleStyle.normal.textColor = new Color(0.2f, 0.6f, 1.0f);
            _upcomingTitleStyle.hover.textColor = new Color(0.3f, 0.7f, 1.0f);

            _endedTitleStyle = new GUIStyle(_titleStyle);
            _endedTitleStyle.normal.textColor = new Color(1.000f, 0.333f, 0.333f, 1.000f);
            _endedTitleStyle.hover.textColor = new Color(1.000f, 0.439f, 0.439f, 1.000f);
        }
    }
}
