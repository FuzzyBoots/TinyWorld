using System;
using UnityEditor;
using UnityEngine;

namespace JamTrackerItchio.Editor.UI
{
    /// <summary>
    /// View for displaying and managing dependencies
    /// </summary>
    public class DependencyView : IJamTrackerView
    {
        private readonly DependencyInstaller _dependencyInstaller;

        public DependencyView(DependencyInstaller dependencyInstaller)
        {
            _dependencyInstaller = dependencyInstaller;
        }

        public void Draw(DateTime now)
        {
            GUILayout.Label("Optional Plugin Dependencies", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            if (_dependencyInstaller.IsInstalling)
            {
                EditorGUILayout.HelpBox(
                    "Installing dependencies... Please wait.",
                    MessageType.Info
                );
            }
            else
            {
                bool isToolbarExtenderInstalled = DependencyUtils.IsToolbarExtenderInstalled;

                if (isToolbarExtenderInstalled)
                {
                    EditorGUILayout.HelpBox(
                        "Unity Toolbar Extender is installed. All display options are available.",
                        MessageType.Info
                    );
                }
                else
                {
                    EditorGUILayout.HelpBox(
                        "The Unity Toolbar Extender UI Toolkit is optional but required to show the Jam tracker at the top of Unity window next to the Play mode button.",
                        MessageType.Warning
                    );

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(
                        "Install Unity Toolbar Extender:",
                        GUILayout.Width(220)
                    );

                    if (GUILayout.Button("Install Dependencies"))
                    {
                        _dependencyInstaller.CheckAndInstallDependencies();
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUILayout.Space(10);
        }
    }
}
