using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GitIgnoreUtility
{
    [InitializeOnLoad]
    public class GitignoreOverlay
    {
        private static readonly GUIContent FolderIgnoredIcon;
        private static readonly HashSet<string> GitignoreEntries = new HashSet<string>();
        private const string ImageName = "SceneViewVisibility@2x";

        static GitignoreOverlay()
        {
            FolderIgnoredIcon = EditorGUIUtility.IconContent(ImageName);
            UpdateGitignoreEntries();
            EditorApplication.projectWindowItemOnGUI += OnProjectWindowItemGUI;
            GitIgnoreUtilities.OnGitignoreUpdated += UpdateGitignoreEntries;
            AssetDatabase.Refresh();
        }

        private static void UpdateGitignoreEntries()
        {
            GitignoreEntries.Clear();
            if (File.Exists(".gitignore"))
            {
                var entries = File.ReadAllLines(".gitignore");
                foreach (string entry in entries)
                {
                    var trimmedEntry = entry.Trim();
                    if (!string.IsNullOrEmpty(trimmedEntry))
                    {
                        // Convert to relative path if necessary
                        if (!trimmedEntry.StartsWith("Assets/"))
                        {
                            trimmedEntry = "Assets/" + trimmedEntry;
                        }

                        GitignoreEntries.Add(trimmedEntry);
                    }
                }
            }
        }

        private static void OnProjectWindowItemGUI(string guid, Rect selectionRect)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);

            if (IsInGitignore(path))
            {
                var iconRect = new Rect(selectionRect.x + selectionRect.width - 16, selectionRect.y, 16, 16);
                GUI.Label(iconRect, FolderIgnoredIcon);
            }
        }

        private static bool IsInGitignore(string path)
        {
            foreach (var entry in GitignoreEntries)
            {
                if (entry.EndsWith("/"))
                {
                    // Handle folder entries ending with '/'
                    if (path.Equals(entry.TrimEnd('/')) || path.StartsWith(entry))
                    {
                        return true;
                    }
                }
                else
                {
                    // Handle file or other entries
                    if (path.Equals(entry) || path.StartsWith(entry))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}