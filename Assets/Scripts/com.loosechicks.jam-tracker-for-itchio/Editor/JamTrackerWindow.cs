using System;
using JamTrackerItchio.Editor.Controllers;
using UnityEditor;

namespace JamTrackerItchio.Editor
{
    /// <summary>
    /// Main window for the jam tracker that provides a centralized view of game jam information
    /// and delegates UI management to a dedicated controller for better separation of concerns.
    /// </summary>
    public class JamTrackerWindow : EditorWindow
    {
        public static event Action<GameJam> OnSelectedJamChanged
        {
            add { JamTrackerWindowController.OnSelectedJamChanged += value; }
            remove { JamTrackerWindowController.OnSelectedJamChanged -= value; }
        }

        public static GameJam SelectedJam => JamTrackerWindowController.SelectedJam;

        private JamTrackerWindowController _controller;

        [MenuItem("Tools/🐤 loose chicks/Game Jam Tracker for Itch.io")]
        public static void ShowWindow()
        {
            GetWindow<JamTrackerWindow>("Game Jam Tracker for Itch.io");
        }

        private void OnEnable()
        {
            _controller = new JamTrackerWindowController(this);
            _controller.OnEnable();
        }

        private void OnDisable()
        {
            _controller?.OnDisable();
        }

        private void OnGUI()
        {
            _controller?.OnGUI();
        }
    }
}
