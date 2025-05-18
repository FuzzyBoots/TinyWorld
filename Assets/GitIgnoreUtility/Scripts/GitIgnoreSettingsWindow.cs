using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GitIgnoreUtility
{
    public class GitIgnoreSettingsWindow : EditorWindow
    {
        private string gitIgnoreFilePath = ".gitignore";

        [MenuItem("Tools/GitIgnore Settings")]
        public static void ShowWindow()
        {
            GetWindow<GitIgnoreSettingsWindow>("GitIgnore Settings");
        }

        private void OnEnable()
        {
            gitIgnoreFilePath = EditorPrefs.GetString("GitignoreFilePath", ".gitignore");
        }

        private void OnGUI()
        {
            GUILayout.Label("Gitignore Settings", EditorStyles.boldLabel);
            DrawGitignoreFilePathField();
            DrawBrowseButton();
            DrawSaveSettingsButton();
            DrawCreateNewDefaultGitIgnoreButton();
            DrawOpenGitignoreFileButton();
        }

        private void DrawGitignoreFilePathField()
        {
            GUILayout.Label("Gitignore File Path", EditorStyles.label);
            gitIgnoreFilePath = GUILayout.TextField(gitIgnoreFilePath);
        }

        private void DrawBrowseButton()
        {
            if (GUILayout.Button("Browse"))
            {
                var projectRootPath = Application.dataPath.Substring(0, Application.dataPath.LastIndexOf("/Assets", StringComparison.Ordinal));
                var path = EditorUtility.OpenFilePanel("Select .gitignore file", projectRootPath, "");
                if (!string.IsNullOrEmpty(path)) gitIgnoreFilePath = path;
            }
        }

        private void DrawCreateNewDefaultGitIgnoreButton()
        {
            if (GUILayout.Button(new GUIContent("Reset .gitignore File", "This will reset the .gitignore file to a default state.")))
            {
                if (!File.Exists(gitIgnoreFilePath))
                    CreateNewGitIgnoreFile();
                else
                    HandleExistingGitIgnoreFile();
            }
        }

        private void CreateNewGitIgnoreFile()
        {
            try
            {
                File.WriteAllText(gitIgnoreFilePath, File.ReadAllText(GitIgnoreUtilities.DefaultGitIgnoreFilePath));
            }
            catch (Exception e)
            {
                Debug.LogError(
                    "Error creating new .gitignore file: <b>If you moved the Git Ignore Utility folder, you need to update the DefaultGitIgnoreFilePath!</b> \nException: " +
                    e.Message);
            }

            Debug.Log("New .gitignore file created successfully.");
        }

        private void HandleExistingGitIgnoreFile()
        {
            var userConfirmedOverwrite = EditorUtility.DisplayDialog(
                "Attention!! Resetting .gitignore file to default",
                ".gitignore file already exists for this project. Do you want to reset it? \nYou will lose all of your custom entries inside the .gitIgnore File.",
                "Yes", "No"
            );

            if (!userConfirmedOverwrite) return;

            CreateNewGitIgnoreFile();
            GitIgnoreUtilities.OnGitignoreUpdated?.Invoke();
        }

        private void DrawSaveSettingsButton()
        {
            if (GUILayout.Button(new GUIContent("Save New Path", "Save the new .gitignore file path")))
                SaveSettings();
        }

        private void DrawOpenGitignoreFileButton()
        {
            if (GUILayout.Button("Open .gitignore File"))
            {
                if (File.Exists(gitIgnoreFilePath))
                    EditorUtility.OpenWithDefaultApp(gitIgnoreFilePath);
                else
                    EditorUtility.DisplayDialog("Error", "The specified .gitignore file does not exist.", "OK");
            }
        }

        private void SaveSettings()
        {
            EditorPrefs.SetString("GitignoreFilePath", gitIgnoreFilePath);
            GitIgnoreUtilities.UpdateSettings(gitIgnoreFilePath);
        }
    }
}