using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GitIgnoreUtility
{
    public class GitIgnoreUtilities : Editor
    {
        private static string _gitIgnoreFilePath;
        internal static readonly string DefaultGitIgnoreFilePath = Path.Combine(
            Application.dataPath, "GitIgnoreUtility/Resources/gitignore.txt");


        static GitIgnoreUtilities()
        {
            _gitIgnoreFilePath = EditorPrefs.GetString("GitignoreFilePath", ".gitignore");
            UpdateSettings(_gitIgnoreFilePath);
        }

        internal static Action OnGitignoreUpdated;

        public static void UpdateSettings(string newGitignoreFilePath)
        {
            _gitIgnoreFilePath = newGitignoreFilePath;
        }

        [MenuItem("Assets/Add to .gitignore", false, 20)]
        private static void AddToGitignore()
        {
            var path = GetSelectedPathOrFallback();
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogError("No valid file or folder selected.");
                return;
            }

            var relativePath = GetRelativePath(path);

            EnsureGitIgnoreFileExists();

            using (StreamWriter sw = File.AppendText(_gitIgnoreFilePath))
            {
                sw.WriteLine(relativePath);
            }

            OnGitignoreUpdated?.Invoke();
            Debug.Log($"{relativePath} has been added to {_gitIgnoreFilePath}");
        }

        [MenuItem("Assets/Remove from .gitignore", false, 21)]
        private static void RemoveFromGitignore()
        {
            var path = GetSelectedPathOrFallback();
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogError("No valid file or folder selected.");
                return;
            }

            var relativePath = GetRelativePath(path);

            if (!File.Exists(_gitIgnoreFilePath))
            {
                Debug.LogError($"{_gitIgnoreFilePath} does not exist.");
                return;
            }

            var entries = new List<string>(File.ReadAllLines(_gitIgnoreFilePath));
            if (entries.Contains(relativePath))
            {
                entries.Remove(relativePath);
                File.WriteAllLines(_gitIgnoreFilePath, entries);
                Debug.Log($"{relativePath} has been removed from {_gitIgnoreFilePath}");
                OnGitignoreUpdated?.Invoke();
            }
            else
            {
                Debug.LogWarning($"{relativePath} is not in {_gitIgnoreFilePath}");
            }
        }

        private static void EnsureGitIgnoreFileExists()
        {
            if (!File.Exists(_gitIgnoreFilePath))
            {
                if (File.Exists(DefaultGitIgnoreFilePath))
                {
                    File.Copy(DefaultGitIgnoreFilePath, _gitIgnoreFilePath);
                    Debug.Log($"{_gitIgnoreFilePath} has been created using the default Unity GitIgnore template.");
                }
                else
                {
                    File.Create(_gitIgnoreFilePath).Dispose();
                    Debug.Log($"{_gitIgnoreFilePath} has been created as an empty file.");
                }
            }
        }

        private static string GetSelectedPathOrFallback()
        {
            var path = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (string.IsNullOrEmpty(path))
                path = "Assets";

            if (File.Exists(path))
                return path;

            if (Directory.Exists(path))
                return path + "/";

            return null;
        }

        private static string GetRelativePath(string path)
        {
            var projectPath = Path.GetFullPath(Application.dataPath).Replace("\\", "/");
            if (path.StartsWith(projectPath))
                path = path.Substring(projectPath.Length + 1);
            return path;
        }

        [MenuItem("Assets/Add to .gitignore", true)]
        private static bool AddToGitignoreValidation()
        {
            var path = GetSelectedPathOrFallback();
            var relativePath = GetRelativePath(path);
            return Selection.activeObject != null && !IsInGitignore(relativePath);
        }

        [MenuItem("Assets/Remove from .gitignore", true)]
        private static bool RemoveFromGitignoreValidation()
        {
            var path = GetSelectedPathOrFallback();
            var relativePath = GetRelativePath(path);
            return Selection.activeObject != null && IsInGitignore(relativePath);
        }

        private static bool IsInGitignore(string relativePath)
        {
            if (File.Exists(_gitIgnoreFilePath))
            {
                var entries = new List<string>(File.ReadAllLines(_gitIgnoreFilePath));
                return entries.Contains(relativePath);
            }
            return false;
        }
    }
}
