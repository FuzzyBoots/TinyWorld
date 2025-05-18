#if UNITY_EDITOR

using UnityEngine;
using UnityEditor;
using System.Diagnostics;
using System.IO;
using System.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.SceneManagement;

namespace ProjectSyncer
{
    /// <summary>
    /// Represents a single entry parsed from 'git status --porcelain' output.
    /// Provides properties to interpret the status code.
    /// </summary>
    public struct GitStatusEntry
    {
        public string FilePath;
        public string StatusCode;
        public string OriginalPath;

        public bool IsStaged => !string.IsNullOrEmpty(StatusCode) &&
                                StatusCode.Length > 0 && StatusCode[0] != ' ' &&
                                StatusCode[0] != '?';
        public bool IsUnstaged => !string.IsNullOrEmpty(StatusCode) &&
                                  StatusCode.Length > 1 && StatusCode[1] != ' ';
        public bool IsUntracked =>
          !string.IsNullOrEmpty(StatusCode) && StatusCode.Trim() == "??";
        public bool IsRenamedStaged => !string.IsNullOrEmpty(StatusCode) &&
                                       StatusCode.Length > 0 && StatusCode[0] == 'R';
        public bool IsDeletedUnstaged => !string.IsNullOrEmpty(StatusCode) &&
                                         StatusCode.Length > 1 &&
                                         StatusCode[1] == 'D';
        public bool IsDeletedStaged => !string.IsNullOrEmpty(StatusCode) &&
                                       StatusCode.Length > 0 && StatusCode[0] == 'D';
        public bool IsConflicted => !string.IsNullOrEmpty(StatusCode) &&
                                    StatusCode.Contains("U"); // Covers UU, AU, DU etc.

        /// <summary>
        /// Generates a user-friendly string representation of the file's status.
        /// </summary>
        public string GetFormattedStatus()
        {
            if (IsRenamedStaged)
                return $"Renamed: {OriginalPath} -> {FilePath}";
            if (IsUntracked)
                return $"Untracked: {FilePath}";
            if (IsConflicted)
                return $"CONFLICT: {FilePath}";

            string staged = IsStaged ? StatusCharToString(StatusCode[0]) : "";
            string unstaged = IsUnstaged ? StatusCharToString(StatusCode[1]) : "";

            if (!string.IsNullOrEmpty(staged) && !string.IsNullOrEmpty(unstaged))
                return $"{staged}/{unstaged}: {FilePath}";
            if (!string.IsNullOrEmpty(staged))
                return $"{staged} (Staged): {FilePath}";
            if (!string.IsNullOrEmpty(unstaged))
            {
                if (IsDeletedUnstaged) return $"Deleted (Unstaged): {FilePath}";
                return $"{unstaged} (Unstaged): {FilePath}";
            }

            return $"Unknown ({StatusCode}): {FilePath}";
        }

        /// <summary>
        /// Converts a single status character (like 'M', 'A') to a readable string.
        /// </summary>
        private string StatusCharToString(char statusChar)
        {
            switch (statusChar)
            {
                case 'M': return "Modified";
                case 'A': return "Added";
                case 'D': return "Deleted";
                case 'R': return "Renamed";
                case 'C': return "Copied";
                case 'U': return "Unmerged";
                default: return statusChar.ToString();
            }
        }
    }

    /// <summary>
    /// Editor window providing a basic Git interface within Unity.
    /// Allows fetching, pulling, staging, committing, and pushing changes.
    /// </summary>
    public class ProjectSyncerWindow : EditorWindow
    {
        private string detectedRepositoryUrl = "";
        private bool repoFoundAndRemoteDetected = false;
        private string commitMessage = "";
        private Vector2 scrollPos;
        private bool isBusy = false; // Prevents concurrent Git operations
        private string lastStatusMessage = "Initializing...";
        private string selectedRemote = "origin";
        private List<string> availableRemotes = new List<string> { "origin" };
        private bool upstreamConfigured = false;

        private bool fetchCompletedSuccessfully = false;
        private bool isBehindRemote = false;
        private bool isAheadRemote = false;
        private bool hasUncommittedChanges = false;
        private bool hasConflicts = false;

        private List<GitStatusEntry> stagedFiles = new List<GitStatusEntry>();
        private List<GitStatusEntry> unstagedFiles = new List<GitStatusEntry>();
        private List<string> commitsToPull = new List<string>();
        private List<string> commitsToPush = new List<string>();
        private List<string> commitHistory = new List<string>();
        private Vector2 pullScrollPos;
        private Vector2 pushScrollPos;
        private Vector2 historyScrollPos;
        private Vector2 stagedScrollPos;
        private Vector2 unstagedScrollPos;

        private const int MAX_LOG_ENTRIES = 10;
        private const int GIT_COMMAND_TIMEOUT_MS = 3600 * 1000; // 1 hour

        // Tooltip Content remains useful for UI understanding
        private static readonly GUIContent refreshTooltip = new GUIContent("Refresh", "Refresh status, check remote.");
        private static readonly GUIContent fetchTooltip = new GUIContent("Fetch", "Download remote tracking information.");
        private static readonly GUIContent pullTooltip = new GUIContent("Pull", "Download & merge remote changes into your local branch. Requires upstream to be configured.");
        private static readonly GUIContent commitLabelTooltip = new GUIContent("Commit Message:", "Describe the changes being saved.");
        private static readonly GUIContent commitTooltip = new GUIContent("Commit", "Save staged changes to the local repository.");
        private static readonly GUIContent pushTooltip = new GUIContent("Push", "Upload local commits to the remote repository. Requires upstream to be configured.");
        private static readonly GUIContent repoLabelTooltip = new GUIContent("Remote URL:", "URL of the selected remote repository.");
        private static readonly GUIContent historyStatusLabelTooltip = new GUIContent("Repository History & Status");
        private static readonly GUIContent commitsToPullLabelTooltip = new GUIContent("Commits to Pull");
        private static readonly GUIContent commitsToPushLabelTooltip = new GUIContent("Commits to Push");
        private static readonly GUIContent recentHistoryLabelTooltip = new GUIContent("Recent Commit History");
        private static readonly GUIContent changesLabelTooltip = new GUIContent("Local File Changes");
        private static readonly GUIContent unstagedLabelTooltip = new GUIContent("Unstaged Changes");
        private static readonly GUIContent stagedLabelTooltip = new GUIContent("Staged Changes");
        private static readonly GUIContent discardTooltip = new GUIContent("Discard", "Revert unstaged changes for this file. Cannot be undone easily!");
        private static readonly GUIContent remotePopupTooltip = new GUIContent("Remote:", "Select the remote repository to interact with.");
        private static readonly GUIContent setUpstreamTooltip = new GUIContent("Set Upstream", "Configure the current branch to track the corresponding branch on the selected remote (e.g., 'main' -> 'origin/main'). This is required for Pull/Push.");


        [MenuItem("Window/Project Syncer Tool")]
        public static void ShowWindow() { GetWindow<ProjectSyncerWindow>("Project Syncer"); }

        void OnEnable()
        {
            ResetState();
            isBusy = false;
            EditorApplication.delayCall += InitializeRepositoryCheck;
            CheckGitVersion();
        }

        void ResetState()
        {
            fetchCompletedSuccessfully = false;
            isBehindRemote = false;
            isAheadRemote = false;
            hasUncommittedChanges = false;
            hasConflicts = false;
            commitMessage = "";
            commitsToPull.Clear();
            commitsToPush.Clear();
            commitHistory.Clear();
            stagedFiles.Clear();
            unstagedFiles.Clear();
            lastStatusMessage = "Initializing...";
            detectedRepositoryUrl = "";
            repoFoundAndRemoteDetected = false;
            selectedRemote = "origin";
            availableRemotes = new List<string> { "origin" };
            upstreamConfigured = false;
        }

        /// <summary>
        /// Performs a basic check for Git version (v2+ recommended).
        /// </summary>
        void CheckGitVersion()
        {
            StringBuilder o, e;
            if (RunGitCommand("--version", out o, out e))
            {
                string versionStr = o.ToString().Trim();
                if (!versionStr.Contains("git version 2") && !versionStr.Contains("git version 3"))
                {
                    UnityEngine.Debug.LogWarning($"Detected Git version might be older than recommended (v2+): {versionStr}");
                }
            }
        }

        /// <summary>
        /// Initial check to verify if the project is a Git repository and detect remotes.
        /// </summary>
        void InitializeRepositoryCheck()
        {
            if (isBusy) return;
            isBusy = true;

            if (IsGitRepository())
            {
                GetAvailableRemotes();
                DetectRemoteUrl();

                if (repoFoundAndRemoteDetected)
                {
                    isBusy = false;
                    RefreshStatusAndHistory();
                }
                else
                {
                    isBusy = false;
                    Repaint();
                }
            }
            else
            {
                lastStatusMessage = "Error: Project root is not a Git repository.";
                repoFoundAndRemoteDetected = false;
                isBusy = false;
                Repaint();
            }
        }

        /// <summary>
        /// Checks if the Unity project root directory contains a .git folder.
        /// </summary>
        bool IsGitRepository()
        {
            try
            {
                string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                return Directory.Exists(Path.Combine(root, ".git"));
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"Error checking for .git directory: {ex.Message}");
                return false;
            }
        }


        /// <summary>
        /// Attempts to retrieve the URL for the currently selected remote repository.
        /// Updates `repoFoundAndRemoteDetected` and `lastStatusMessage` based on outcome.
        /// </summary>
        void DetectRemoteUrl()
        {
            StringBuilder o, e;
            if (RunGitCommand($"remote get-url {selectedRemote}", out o, out e))
            {
                detectedRepositoryUrl = o.ToString().Trim();
                if (string.IsNullOrEmpty(detectedRepositoryUrl))
                {
                    lastStatusMessage = $"Error: Remote '{selectedRemote}' found, but URL is empty.";
                    repoFoundAndRemoteDetected = false;
                }
                else
                {
                    repoFoundAndRemoteDetected = true;
                }
            }
            else
            {
                detectedRepositoryUrl = "";
                repoFoundAndRemoteDetected = false;
                string err = e.ToString();
                if (err.Contains("No such remote"))
                {
                    lastStatusMessage = $"Error: No remote named '{selectedRemote}' found.";
                }
                else if (err.Contains("Err: 'git' not found"))
                {
                    lastStatusMessage = "Error: Git command not found. Is Git installed and in PATH?";
                }
                else
                {
                    lastStatusMessage = $"Error getting URL for remote '{selectedRemote}'. See console.";
                    UnityEngine.Debug.LogError($"DetectRemoteUrl Error: {e}");
                }
            }
        }

        /// <summary>
        /// Main GUI drawing method, called frequently by Unity Editor.
        /// </summary>
        void OnGUI()
        {
            GUILayout.Label("Project Syncer Tool", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            DisplayStatusMessage();
            EditorGUILayout.Space();

            bool enableUI = !isBusy && repoFoundAndRemoteDetected;

            if (IsGitRepository())
            {
                DisplayRepositoryInfo();
            }

            EditorGUI.BeginDisabledGroup(!enableUI);
            try
            {
                scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
                DisplayFetchPullPushButtons();
                DisplayChangesAndStagingUI();
                DisplayCommitUI();
                DisplayStatusAndHistory();
                EditorGUILayout.EndScrollView();
            }
            finally
            {
                EditorGUI.EndDisabledGroup();
            }

            if (!repoFoundAndRemoteDetected && !isBusy && IsGitRepository())
            {
                if (lastStatusMessage.StartsWith("Error:"))
                {
                    EditorGUILayout.HelpBox(lastStatusMessage + "\nCheck remote configuration or Git installation.", MessageType.Error);
                }
                else
                {
                    EditorGUILayout.HelpBox($"Could not get URL for remote '{selectedRemote}'. Check configuration.", MessageType.Warning);
                }
            }
            else if (!IsGitRepository() && !isBusy)
            {
                EditorGUILayout.HelpBox("Error: Project root is not a Git repository. Initialize a repository first.", MessageType.Error);
            }
        }

        /// <summary>
        /// Displays the `lastStatusMessage` in a HelpBox with appropriate message type.
        /// </summary>
        void DisplayStatusMessage()
        {
            if (!string.IsNullOrEmpty(lastStatusMessage))
            {
                MessageType t = MessageType.Info;
                if (hasConflicts) t = MessageType.Error;
                else if (lastStatusMessage.StartsWith("Err", StringComparison.OrdinalIgnoreCase) || lastStatusMessage.Contains("Fail")) t = MessageType.Error;
                else if (lastStatusMessage.StartsWith("Warn", StringComparison.OrdinalIgnoreCase)) t = MessageType.Warning;

                EditorGUILayout.HelpBox(lastStatusMessage, t);
            }
        }

        /// <summary>
        /// Displays the remote selection dropdown, the detected URL, and the Set Upstream button if needed.
        /// </summary>
        void DisplayRepositoryInfo()
        {
            string previousRemote = selectedRemote;

            int selectedIndex = availableRemotes.IndexOf(selectedRemote);
            if (selectedIndex < 0 && availableRemotes.Count > 0) selectedIndex = 0;
            else if (availableRemotes.Count == 0) selectedIndex = -1;

            if (selectedIndex >= 0)
            {
                selectedIndex = EditorGUILayout.Popup(remotePopupTooltip, selectedIndex, availableRemotes.ToArray());
                selectedRemote = availableRemotes[selectedIndex];
            }
            else
            {
                EditorGUILayout.LabelField(remotePopupTooltip.text, "No remotes found.");
            }

            if (selectedRemote != previousRemote)
            {
                DetectRemoteUrl();
                if (repoFoundAndRemoteDetected) RefreshStatusAndHistory();
                else Repaint();
                GUIUtility.ExitGUI();
            }

            EditorGUILayout.LabelField(repoLabelTooltip, EditorStyles.wordWrappedLabel);
            if (repoFoundAndRemoteDetected && !string.IsNullOrEmpty(detectedRepositoryUrl))
            {
                EditorGUILayout.SelectableLabel(detectedRepositoryUrl, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
            }
            else if (repoFoundAndRemoteDetected && string.IsNullOrEmpty(detectedRepositoryUrl))
            {
                EditorGUILayout.LabelField("URL empty for this remote.", EditorStyles.miniLabel);
            }

            if (repoFoundAndRemoteDetected && !upstreamConfigured)
            {
                EditorGUILayout.HelpBox("Current branch has no upstream configured. Push/Pull requires this.", MessageType.Warning);
                if (GUILayout.Button(setUpstreamTooltip))
                {
                    PerformSetUpstream();
                    GUIUtility.ExitGUI();
                }
            }
            EditorGUILayout.Space();
        }

        /// <summary>
        /// Fetches the list of configured remote names (e.g., "origin").
        /// </summary>
        void GetAvailableRemotes()
        {
            StringBuilder o, e;
            if (RunGitCommand("remote", out o, out e))
            {
                availableRemotes = o.ToString().Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).ToList();
                if (!availableRemotes.Any())
                {
                    availableRemotes.Add("origin");
                    lastStatusMessage = "Warning: No remotes found, defaulting to 'origin'.";
                }
                if (availableRemotes.Contains("origin") && availableRemotes[0] != "origin")
                {
                    availableRemotes.Remove("origin");
                    availableRemotes.Insert(0, "origin");
                }
                if (!availableRemotes.Contains(selectedRemote))
                {
                    selectedRemote = availableRemotes.FirstOrDefault() ?? "origin";
                }
            }
            else
            {
                availableRemotes = new List<string> { "origin" };
                selectedRemote = "origin";
                UnityEngine.Debug.LogError($"Failed to get remotes: {e}");
            }
        }

        /// <summary>
        /// Displays the main action buttons: Refresh, Fetch, Pull.
        /// </summary>
        void DisplayFetchPullPushButtons()
        {
            GUILayout.Label("Common Actions", EditorStyles.boldLabel);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(refreshTooltip)) { RefreshStatusAndHistory(); GUIUtility.ExitGUI(); }
            if (GUILayout.Button(fetchTooltip)) { PerformFetch(); GUIUtility.ExitGUI(); }

            bool canPull = fetchCompletedSuccessfully && isBehindRemote && upstreamConfigured;
            EditorGUI.BeginDisabledGroup(!canPull || hasConflicts);
            string pullTxt = canPull && commitsToPull.Count > 0 ? $"Pull ({commitsToPull.Count})" : "Pull";
            if (GUILayout.Button(new GUIContent(pullTxt, pullTooltip.tooltip))) { PerformPull(); GUIUtility.ExitGUI(); }
            EditorGUI.EndDisabledGroup();
            GUILayout.EndHorizontal();
            GUILayout.Space(10);
        }

        /// <summary>
        /// Sets the upstream branch for the *current* local branch to track the corresponding
        /// branch on the selected remote. Uses `git push --set-upstream`.
        /// </summary>
        void PerformSetUpstream()
        {
            if (isBusy || !repoFoundAndRemoteDetected) return;
            isBusy = true;
            try
            {
                StringBuilder currentBranchOutput, currentBranchError;
                string currentBranch = "main";
                if (RunGitCommand("rev-parse --abbrev-ref HEAD", out currentBranchOutput, out currentBranchError))
                {
                    currentBranch = currentBranchOutput.ToString().Trim();
                    if (string.IsNullOrEmpty(currentBranch) || currentBranch == "HEAD")
                    {
                        lastStatusMessage = "Error: Cannot set upstream in detached HEAD state or unable to determine current branch.";
                        ShowNotification(new GUIContent("Branch Error"));
                        UnityEngine.Debug.LogError($"Could not determine a valid current branch name. Result: '{currentBranch}'");
                        isBusy = false;
                        return;
                    }
                }
                else
                {
                    lastStatusMessage = "Error: Could not determine current branch name.";
                    ShowNotification(new GUIContent("Branch Error"));
                    UnityEngine.Debug.LogError($"Could not get current branch: {currentBranchError}");
                    isBusy = false;
                    return;
                }

                EditorUtility.DisplayProgressBar("Git Set Upstream", $"Setting upstream for '{currentBranch}' to {selectedRemote}/{currentBranch}...", 0.1f);
                StringBuilder o, e;
                if (RunGitCommand($"push --set-upstream {selectedRemote} {currentBranch}", out o, out e))
                {
                    lastStatusMessage = $"Upstream set for '{currentBranch}' to {selectedRemote}/{currentBranch}.";
                    ShowNotification(new GUIContent("Upstream Set"));
                    upstreamConfigured = true;
                }
                else
                {
                    lastStatusMessage = $"Error setting upstream for '{currentBranch}'. See console.";
                    ShowNotification(new GUIContent("Set Upstream Error"));
                    UnityEngine.Debug.LogError($"Failed to set upstream: {e}\nOutput: {o}");
                    if (e.ToString().Contains("does not appear to be a git repository") || e.ToString().Contains("repository not found"))
                    {
                        lastStatusMessage += " Ensure the remote repository exists and you have permissions.";
                    }
                    else if (e.ToString().Contains("src refspec") && e.ToString().Contains("does not match any"))
                    {
                        lastStatusMessage += $" The branch '{currentBranch}' might not exist locally or failed to push.";
                    }
                }
            }
            catch (Exception ex) { HandleException(ex, "Set Upstream"); }
            finally
            {
                EditorUtility.ClearProgressBar();
                isBusy = false;
                RefreshStatusAndHistory();
            }
        }


        /// <summary>
        /// Performs `git fetch` for the selected remote.
        /// </summary>
        void PerformFetch()
        {
            if (isBusy || !repoFoundAndRemoteDetected) return;
            isBusy = true;
            try
            {
                EditorUtility.DisplayProgressBar("Git Fetch", $"Fetching from '{selectedRemote}'...", 0.1f);
                StringBuilder o, e;
                if (RunGitCommand($"fetch {selectedRemote}", out o, out e))
                {
                    fetchCompletedSuccessfully = true;
                    lastStatusMessage = "Fetch successful. Refreshing status...";
                    ShowNotification(new GUIContent("Fetch OK"));
                }
                else
                {
                    fetchCompletedSuccessfully = false;
                    lastStatusMessage = "Fetch failed. See console.";
                    ShowNotification(new GUIContent("Fetch Failed"));
                    UnityEngine.Debug.LogError($"Fetch failed: {e}");
                }
            }
            catch (Exception ex) { HandleException(ex, "Fetch"); fetchCompletedSuccessfully = false; }
            finally
            {
                EditorUtility.ClearProgressBar();
                isBusy = false;
                if (repoFoundAndRemoteDetected) RefreshStatusAndHistory();
                else Repaint();
            }
        }

        /// <summary>
        /// Performs `git pull` using the configured upstream branch.
        /// Warns about uncommitted changes and handles potential conflicts.
        /// Refreshes Unity assets on successful pull without conflicts.
        /// </summary>
        void PerformPull()
        {
            if (isBusy || !repoFoundAndRemoteDetected || !isBehindRemote || !upstreamConfigured)
            {
                lastStatusMessage = !upstreamConfigured ? "Pull requires upstream branch configuration." :
                                  !isBehindRemote ? "Already up-to-date." :
                                  "Pull prerequisites not met.";
                Repaint();
                return;
            }
            if (hasConflicts)
            {
                lastStatusMessage = "Error: Cannot pull with existing merge conflicts. Resolve conflicts first.";
                ShowNotification(new GUIContent("Pull Blocked"));
                Repaint();
                return;
            }

            if (hasUncommittedChanges && !EditorUtility.DisplayDialog("Uncommitted Changes", "You have uncommitted changes.\nPulling might cause conflicts or overwrite local work.\n\nConsider Stashing or Committing first.\n\nProceed anyway?", "Proceed with Pull", "Cancel"))
            {
                lastStatusMessage = "Pull cancelled due to uncommitted changes.";
                Repaint();
                return;
            }

            isBusy = true;
            string scenePath = "";
            try { scenePath = EditorSceneManager.GetActiveScene().path; } catch { /* ignore */ }

            try
            {
                EditorUtility.DisplayProgressBar("Git Pull", $"Pulling changes from upstream...", 0.1f);
                StringBuilder o, e;
                if (RunGitCommand("pull", out o, out e))
                {
                    EditorUtility.DisplayProgressBar("Git Pull", "Checking for merge conflicts...", 0.5f);
                    StringBuilder statusOut, statusErr;
                    bool pullCausedConflicts = false;
                    // Use -uall to see conflicted files clearly after pull
                    if (RunGitCommand("status --porcelain -uall", out statusOut, out statusErr))
                    {
                        if (statusOut.ToString().Contains("U ")) // Look for unmerged status marker 'U'
                        {
                            pullCausedConflicts = true;
                            hasConflicts = true;
                        }
                    }

                    if (pullCausedConflicts)
                    {
                        lastStatusMessage = "Pull completed with MERGE CONFLICTS. Resolve conflicts manually, then stage and commit.";
                        ShowNotification(new GUIContent("Merge Conflicts!"));
                    }
                    else
                    {
                        EditorUtility.DisplayProgressBar("Git Pull", "Refreshing Unity assets...", 0.7f);
                        AssetDatabase.Refresh(); // Refresh assets only if no conflicts
                        if (!string.IsNullOrEmpty(scenePath))
                        {
                            EditorUtility.DisplayProgressBar("Git Pull", "Reloading scene...", 0.9f);
                            try { EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single); }
                            catch (Exception sceneEx) { UnityEngine.Debug.LogWarning($"Could not reload scene '{scenePath}' after pull: {sceneEx.Message}"); }
                        }
                        lastStatusMessage = "Pull successful. Assets refreshed.";
                        ShowNotification(new GUIContent("Pull OK"));
                        hasConflicts = false;
                    }
                    isBehindRemote = false;
                    commitsToPull.Clear();
                }
                else
                {
                    lastStatusMessage = "Pull failed. Check console (e.g., conflicts, network issue, authentication).";
                    ShowNotification(new GUIContent("Pull Failed"));
                    UnityEngine.Debug.LogError($"Pull failed: {e}\nOutput: {o}");
                    if (e.ToString().Contains("conflict") || o.ToString().Contains("conflict")) hasConflicts = true;
                }
            }
            catch (Exception ex) { HandleException(ex, "Pull"); }
            finally
            {
                EditorUtility.ClearProgressBar();
                isBusy = false;
                RefreshStatusAndHistory();
            }
        }


        /// <summary>
        /// Stages all unstaged changes using `git add .`.
        /// </summary>
        void PerformStageAll()
        {
            if (isBusy || !repoFoundAndRemoteDetected || !unstagedFiles.Any())
            {
                lastStatusMessage = "Nothing to stage or prerequisites not met.";
                Repaint();
                return;
            }
            isBusy = true;
            try
            {
                EditorUtility.DisplayProgressBar("Git Add All", "Staging all changes (git add .)...", 0.1f);
                StringBuilder o, e;
                if (RunGitCommand("add .", out o, out e))
                {
                    lastStatusMessage = "Staged all changes.";
                    ShowNotification(new GUIContent("All Changes Staged"));
                }
                else
                {
                    lastStatusMessage = "Error staging all changes. See console.";
                    ShowNotification(new GUIContent("Stage All Error"));
                    UnityEngine.Debug.LogError($"Failed to stage all: {e}");
                }
            }
            catch (Exception ex) { HandleException(ex, "Stage All"); }
            finally
            {
                EditorUtility.ClearProgressBar();
                isBusy = false;
                if (repoFoundAndRemoteDetected) RefreshStatusAndHistory();
                else Repaint();
            }
        }

        /// <summary>
        /// Unstages all staged changes using `git reset`.
        /// </summary>
        void PerformUnstageAll()
        {
            if (isBusy || !repoFoundAndRemoteDetected || !stagedFiles.Any())
            {
                lastStatusMessage = "Nothing to unstage or prerequisites not met.";
                Repaint();
                return;
            }
            isBusy = true;
            try
            {
                EditorUtility.DisplayProgressBar("Git Reset", "Unstaging all changes (git reset)...", 0.1f);
                StringBuilder o, e;
                if (RunGitCommand("reset", out o, out e))
                {
                    lastStatusMessage = "Unstaged all changes.";
                    ShowNotification(new GUIContent("All Changes Unstaged"));
                }
                else
                {
                    lastStatusMessage = "Error unstaging all changes. See console.";
                    ShowNotification(new GUIContent("Unstage All Error"));
                    UnityEngine.Debug.LogError($"Failed to unstage all: {e}");
                }
            }
            catch (Exception ex) { HandleException(ex, "Unstage All"); }
            finally
            {
                EditorUtility.ClearProgressBar();
                isBusy = false;
                if (repoFoundAndRemoteDetected) RefreshStatusAndHistory();
                else Repaint();
            }
        }

        /// <summary>
        /// Discards unstaged changes for a single file using `git checkout -- <file>`.
        /// Does not work for untracked or conflicted files.
        /// </summary>
        void PerformDiscardFile(string filePath)
        {
            if (isBusy || !repoFoundAndRemoteDetected) return;
            var entry = unstagedFiles.FirstOrDefault(f => f.FilePath == filePath);

            if (entry.Equals(default(GitStatusEntry)) || entry.IsUntracked || entry.IsConflicted)
            {
                lastStatusMessage = entry.IsUntracked ? "Cannot discard untracked file via checkout." :
                                    entry.IsConflicted ? "Cannot discard conflicted file. Resolve conflicts manually." :
                                    "File not found in unstaged list.";
                ShowNotification(new GUIContent("Discard N/A"));
                Repaint();
                return;
            }

            if (!EditorUtility.DisplayDialog("Discard Changes?", $"Discard all unstaged changes to:\n{filePath}\n\nThis action cannot be easily undone.", "Discard Changes", "Cancel"))
            {
                return;
            }

            isBusy = true;
            try
            {
                EditorUtility.DisplayProgressBar("Git Checkout", $"Discarding changes to {Path.GetFileName(filePath)}...", 0.1f);
                StringBuilder o, e;
                // Use 'git checkout -- <file>' to discard changes
                if (RunGitCommand($"checkout -- {SafeQuote(filePath)}", out o, out e))
                {
                    lastStatusMessage = $"Discarded changes for: {Path.GetFileName(filePath)}";
                    ShowNotification(new GUIContent("Changes Discarded"));
                    AssetDatabase.Refresh();
                }
                else
                {
                    lastStatusMessage = $"Error discarding changes for {Path.GetFileName(filePath)}. See console.";
                    ShowNotification(new GUIContent("Discard Error"));
                    UnityEngine.Debug.LogError($"Failed to discard '{filePath}': {e}");
                }
            }
            catch (Exception ex) { HandleException(ex, $"Discard File '{filePath}'"); }
            finally
            {
                EditorUtility.ClearProgressBar();
                isBusy = false;
                if (repoFoundAndRemoteDetected) RefreshStatusAndHistory();
                else Repaint();
            }
        }

        /// <summary>
        /// Displays the UI sections for unstaged and staged files, including buttons for staging/unstaging/discarding.
        /// Shows a prominent warning if merge conflicts exist.
        /// </summary>
        void DisplayChangesAndStagingUI()
        {
            if (hasConflicts)
            {
                EditorGUILayout.HelpBox("Merge conflicts detected! Resolve conflicts in your preferred Git tool or text editor, then stage the resolved files and commit.", MessageType.Error);
            }

            GUILayout.Label(changesLabelTooltip, EditorStyles.boldLabel);

            // Unstaged Changes Section
            EditorGUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label(new GUIContent($"Unstaged Changes ({unstagedFiles.Count})", unstagedLabelTooltip.tooltip));
            EditorGUI.BeginDisabledGroup(!unstagedFiles.Any() || hasConflicts);
            if (GUILayout.Button(new GUIContent("Stage All", "Stage all unstaged changes."))) { PerformStageAll(); GUIUtility.ExitGUI(); }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.Space(3);

            if (unstagedFiles.Any())
            {
                unstagedScrollPos = EditorGUILayout.BeginScrollView(unstagedScrollPos, GUILayout.Height(120));
                foreach (var entry in unstagedFiles)
                {
                    GUILayout.BeginHorizontal();
                    Color defaultColor = GUI.color;
                    if (entry.IsConflicted) GUI.color = Color.red;
                    EditorGUILayout.SelectableLabel(entry.GetFormattedStatus(), EditorStyles.label, GUILayout.Height(EditorGUIUtility.singleLineHeight), GUILayout.ExpandWidth(true));
                    GUI.color = defaultColor;

                    bool canDiscard = !entry.IsUntracked && !entry.IsConflicted;
                    EditorGUI.BeginDisabledGroup(!canDiscard || isBusy);
                    string discardBtnTooltipText = discardTooltip.tooltip;
                    if (!canDiscard) discardBtnTooltipText = entry.IsUntracked ? "Cannot discard untracked file." : "Cannot discard conflicted file.";
                    if (GUILayout.Button(new GUIContent("Discard", discardBtnTooltipText), GUILayout.Width(60)))
                    { PerformDiscardFile(entry.FilePath); GUIUtility.ExitGUI(); }
                    EditorGUI.EndDisabledGroup();

                    bool canStage = !entry.IsDeletedStaged && !entry.IsConflicted;
                    EditorGUI.BeginDisabledGroup(!canStage || isBusy);
                    string stageBtnTooltipText = $"Stage changes for '{entry.FilePath}'";
                    if (!canStage) stageBtnTooltipText = entry.IsConflicted ? "Resolve conflict before staging." : "Cannot stage this file state.";
                    if (GUILayout.Button(new GUIContent("Stage", stageBtnTooltipText), GUILayout.Width(60)))
                    { StageFile(entry.FilePath); GUIUtility.ExitGUI(); }
                    EditorGUI.EndDisabledGroup();

                    GUILayout.EndHorizontal();
                }
                EditorGUILayout.EndScrollView();
            }
            else { GUILayout.Label("No unstaged changes.", EditorStyles.miniLabel); }
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(5);

            // Staged Changes Section
            EditorGUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label(new GUIContent($"Staged Changes ({stagedFiles.Count})", stagedLabelTooltip.tooltip));
            EditorGUI.BeginDisabledGroup(!stagedFiles.Any() || hasConflicts);
            if (GUILayout.Button(new GUIContent("Unstage All", "Remove all staged changes."))) { PerformUnstageAll(); GUIUtility.ExitGUI(); }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.Space(3);

            if (stagedFiles.Any())
            {
                stagedScrollPos = EditorGUILayout.BeginScrollView(stagedScrollPos, GUILayout.Height(100));
                foreach (var entry in stagedFiles)
                {
                    GUILayout.BeginHorizontal();
                    Color defaultColor = GUI.color;
                    if (entry.IsConflicted) GUI.color = Color.red;
                    EditorGUILayout.SelectableLabel(entry.GetFormattedStatus(), EditorStyles.label, GUILayout.Height(EditorGUIUtility.singleLineHeight), GUILayout.ExpandWidth(true));
                    GUI.color = defaultColor;

                    EditorGUI.BeginDisabledGroup(hasConflicts || isBusy);
                    if (GUILayout.Button(new GUIContent("Unstage", $"Unstage changes for '{entry.FilePath}'"), GUILayout.Width(60)))
                    { UnstageFile(entry.FilePath); GUIUtility.ExitGUI(); }
                    EditorGUI.EndDisabledGroup();

                    GUILayout.EndHorizontal();
                }
                EditorGUILayout.EndScrollView();
            }
            else { GUILayout.Label("No staged changes.", EditorStyles.miniLabel); }
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(10);
        }

        /// <summary>
        /// Displays the commit message text area and the Commit/Push buttons.
        /// Disables buttons based on repository state (conflicts, behind remote, etc.).
        /// Provides context-sensitive help messages.
        /// </summary>
        void DisplayCommitUI()
        {
            GUILayout.Label("Commit Staged Changes & Push", EditorStyles.boldLabel);
            GUILayout.Label(commitLabelTooltip);
            EditorGUI.BeginDisabledGroup(hasConflicts);
            commitMessage = EditorGUILayout.TextArea(commitMessage, GUILayout.Height(EditorGUIUtility.singleLineHeight * 3));
            EditorGUI.EndDisabledGroup();

            GUILayout.BeginHorizontal();
            bool canCommit = stagedFiles.Any() && !isBehindRemote && !hasConflicts;
            EditorGUI.BeginDisabledGroup(!canCommit || string.IsNullOrWhiteSpace(commitMessage));
            if (GUILayout.Button(commitTooltip)) { PerformCommit(); GUIUtility.ExitGUI(); }
            EditorGUI.EndDisabledGroup();

            bool canPush = isAheadRemote && !isBehindRemote && upstreamConfigured && !hasConflicts;
            EditorGUI.BeginDisabledGroup(!canPush);
            string pushTxt = canPush && commitsToPush.Count > 0 ? $"Push ({commitsToPush.Count})" : "Push";
            if (GUILayout.Button(new GUIContent(pushTxt, pushTooltip.tooltip))) { PerformPush(); GUIUtility.ExitGUI(); }
            EditorGUI.EndDisabledGroup();
            GUILayout.EndHorizontal();

            // Context-sensitive help text
            if (hasConflicts)
            {
                EditorGUILayout.HelpBox("Resolve merge conflicts before committing or pushing.", MessageType.Error);
            }
            else if (isBehindRemote)
            {
                EditorGUILayout.HelpBox("Local branch is behind remote. Pull changes before committing or pushing.", MessageType.Warning);
            }
            else if (!stagedFiles.Any() && unstagedFiles.Any())
            {
                EditorGUILayout.HelpBox("Stage changes before committing.", MessageType.Info);
            }
            else if (!stagedFiles.Any() && !isAheadRemote)
            {
                EditorGUILayout.HelpBox("No changes staged for commit.", MessageType.Info);
            }
            else if (stagedFiles.Any() && string.IsNullOrWhiteSpace(commitMessage))
            {
                EditorGUILayout.HelpBox("Enter a commit message.", MessageType.Info);
            }
            else if (!canPush && isAheadRemote && !upstreamConfigured)
            {
                EditorGUILayout.HelpBox("Set upstream branch before pushing.", MessageType.Warning);
            }
            else if (!canPush && !isAheadRemote && repoFoundAndRemoteDetected && !hasConflicts && !isBehindRemote)
            {
                if (hasUncommittedChanges)
                    EditorGUILayout.HelpBox("Stage and commit local changes before pushing.", MessageType.Info);
                else
                    EditorGUILayout.HelpBox("Repository is up-to-date. Nothing to push.", MessageType.Info);
            }
            EditorGUILayout.Space(10);
        }


        /// <summary>
        /// Displays scrollable lists for commits to pull, commits to push, and recent local commit history.
        /// </summary>
        void DisplayStatusAndHistory()
        {
            GUILayout.Label(historyStatusLabelTooltip, EditorStyles.boldLabel);
            if (isBehindRemote && commitsToPull.Any())
            {
                GUILayout.Label(new GUIContent($"Commits to Pull ({commitsToPull.Count}):", commitsToPullLabelTooltip.tooltip));
                pullScrollPos = EditorGUILayout.BeginScrollView(pullScrollPos, GUILayout.Height(60));
                foreach (var c in commitsToPull) EditorGUILayout.SelectableLabel(c, EditorStyles.label, GUILayout.Height(EditorGUIUtility.singleLineHeight));
                EditorGUILayout.EndScrollView();
                EditorGUILayout.Space(5);
            }
            if (isAheadRemote && commitsToPush.Any())
            {
                GUILayout.Label(new GUIContent($"Commits to Push ({commitsToPush.Count}):", commitsToPushLabelTooltip.tooltip));
                pushScrollPos = EditorGUILayout.BeginScrollView(pushScrollPos, GUILayout.Height(60));
                foreach (var c in commitsToPush) EditorGUILayout.SelectableLabel(c, EditorStyles.label, GUILayout.Height(EditorGUIUtility.singleLineHeight));
                EditorGUILayout.EndScrollView();
                EditorGUILayout.Space(5);
            }
            if (commitHistory.Any())
            {
                int validHistoryCount = commitHistory.Count(c => !c.Contains("Error retrieving") && !c.Contains("No commits found"));
                string label = validHistoryCount > 0 ? $"Recent Commit History (Last {Math.Min(validHistoryCount, MAX_LOG_ENTRIES)}):" : "Recent Commit History:";

                GUILayout.Label(new GUIContent(label, recentHistoryLabelTooltip.tooltip));
                historyScrollPos = EditorGUILayout.BeginScrollView(historyScrollPos, GUILayout.Height(120));
                foreach (var c in commitHistory) EditorGUILayout.SelectableLabel(c, EditorStyles.label, GUILayout.Height(EditorGUIUtility.singleLineHeight));
                EditorGUILayout.EndScrollView();
                EditorGUILayout.Space(5);
            }
            else if (repoFoundAndRemoteDetected)
            {
                GUILayout.Label("Could not retrieve commit history or no commits yet.", EditorStyles.miniLabel);
            }
        }

        /// <summary>
        /// Stages a single file using `git add -- <file>`.
        /// </summary>
        void StageFile(string filePath)
        {
            if (isBusy || !repoFoundAndRemoteDetected || hasConflicts) return;
            isBusy = true;
            try
            {
                EditorUtility.DisplayProgressBar("Git Add", $"Staging {Path.GetFileName(filePath)}...", 0.1f);
                StringBuilder o, e;
                // Use -- to handle filenames that might start with dashes
                if (RunGitCommand($"add -- {SafeQuote(filePath)}", out o, out e))
                {
                    lastStatusMessage = $"Staged: {Path.GetFileName(filePath)}";
                    ShowNotification(new GUIContent("File Staged"));
                }
                else
                {
                    lastStatusMessage = $"Error staging {Path.GetFileName(filePath)}. See console.";
                    ShowNotification(new GUIContent("Stage Error"));
                    UnityEngine.Debug.LogError($"Failed to stage '{filePath}': {e}");
                }
            }
            catch (Exception ex) { HandleException(ex, $"Stage File '{filePath}'"); }
            finally
            {
                EditorUtility.ClearProgressBar();
                isBusy = false;
                if (repoFoundAndRemoteDetected) RefreshStatusAndHistory();
                else Repaint();
            }
        }

        /// <summary>
        /// Unstages a single file using `git reset HEAD -- <file>`.
        /// </summary>
        void UnstageFile(string filePath)
        {
            if (isBusy || !repoFoundAndRemoteDetected || hasConflicts) return;
            var entry = stagedFiles.FirstOrDefault(f => f.FilePath == filePath);
            if (entry.Equals(default(GitStatusEntry))) return;

            isBusy = true;
            try
            {
                EditorUtility.DisplayProgressBar("Git Reset", $"Unstaging {Path.GetFileName(filePath)}...", 0.1f);
                StringBuilder o, e;
                // Use -- to handle filenames that might start with dashes
                if (RunGitCommand($"reset HEAD -- {SafeQuote(filePath)}", out o, out e))
                {
                    lastStatusMessage = $"Unstaged: {Path.GetFileName(filePath)}";
                    ShowNotification(new GUIContent("File Unstaged"));
                }
                else
                {
                    lastStatusMessage = $"Error unstaging {Path.GetFileName(filePath)}. See console.";
                    ShowNotification(new GUIContent("Unstage Error"));
                    UnityEngine.Debug.LogError($"Failed to unstage '{filePath}': {e}");
                }
            }
            catch (Exception ex) { HandleException(ex, $"Unstage File '{filePath}'"); }
            finally
            {
                EditorUtility.ClearProgressBar();
                isBusy = false;
                if (repoFoundAndRemoteDetected) RefreshStatusAndHistory();
                else Repaint();
            }
        }

        /// <summary>
        /// Commits currently staged changes using `git commit -m "message"`.
        /// Checks prerequisites like staged files, commit message, and repository state.
        /// </summary>
        void PerformCommit()
        {
            if (isBusy || !repoFoundAndRemoteDetected || hasConflicts || isBehindRemote || !stagedFiles.Any() || string.IsNullOrWhiteSpace(commitMessage))
            {
                lastStatusMessage = hasConflicts ? "Cannot commit with merge conflicts." :
                                  isBehindRemote ? "Cannot commit while behind remote. Pull first." :
                                  !stagedFiles.Any() ? "Nothing staged to commit." :
                                  string.IsNullOrWhiteSpace(commitMessage) ? "Commit message cannot be empty." :
                                  "Commit prerequisites not met.";
                ShowNotification(new GUIContent("Commit Blocked"));
                Repaint();
                return;
            }

            isBusy = true;
            try
            {
                EditorUtility.DisplayProgressBar("Git Commit", "Committing staged changes...", 0.4f);
                StringBuilder o, e;
                string escapedMsg = commitMessage.Replace("\"", "\\\"");
                if (RunGitCommand($"commit -m \"{escapedMsg}\"", out o, out e))
                {
                    // Check output because exit code 1 for "nothing to commit" is treated as success by RunGitCommand
                    if (o.ToString().Contains("nothing to commit") || e.ToString().Contains("nothing to commit"))
                    {
                        lastStatusMessage = "Commit resulted in 'nothing to commit'.";
                        ShowNotification(new GUIContent("Nothing Committed?"));
                    }
                    else
                    {
                        lastStatusMessage = "Commit successful.";
                        ShowNotification(new GUIContent("Commit OK"));
                        commitMessage = "";
                        GUI.FocusControl(null);
                    }
                }
                else
                {
                    lastStatusMessage = "Commit failed. See console.";
                    ShowNotification(new GUIContent("Commit Failed"));
                    UnityEngine.Debug.LogError($"Commit failed: {e}");
                }
            }
            catch (Exception ex) { HandleException(ex, "Commit"); }
            finally
            {
                EditorUtility.ClearProgressBar();
                isBusy = false;
                if (repoFoundAndRemoteDetected) RefreshStatusAndHistory();
                else Repaint();
            }
        }

        /// <summary>
        /// Pushes committed changes to the configured upstream remote/branch using `git push`.
        /// Checks prerequisites like being ahead of remote and upstream configuration.
        /// </summary>
        void PerformPush()
        {
            if (isBusy || !repoFoundAndRemoteDetected || hasConflicts || isBehindRemote || !isAheadRemote || !upstreamConfigured)
            {
                lastStatusMessage = hasConflicts ? "Cannot push with merge conflicts." :
                                  isBehindRemote ? "Cannot push while behind remote. Pull first." :
                                  !upstreamConfigured ? "Cannot push: Upstream branch not configured." :
                                  !isAheadRemote ? "Nothing to push." :
                                  "Push prerequisites not met.";
                ShowNotification(new GUIContent("Push Blocked"));
                Repaint();
                return;
            }

            isBusy = true;
            try
            {
                int count = commitsToPush.Count;
                EditorUtility.DisplayProgressBar("Git Push", $"Pushing {count} commit(s) to upstream...", 0.7f);
                StringBuilder o, e;
                if (RunGitCommand("push", out o, out e))
                {
                    if (o.ToString().Contains("Everything up-to-date") || e.ToString().Contains("Everything up-to-date"))
                    {
                        lastStatusMessage = "Push successful (already up-to-date).";
                        ShowNotification(new GUIContent("Already Up-to-date"));
                    }
                    else
                    {
                        lastStatusMessage = $"Push successful ({count} commit(s)).";
                        ShowNotification(new GUIContent("Push OK"));
                    }
                }
                else
                {
                    lastStatusMessage = "Push failed. See console (e.g., rejected, auth error, network issue).";
                    ShowNotification(new GUIContent("Push Failed"));
                    UnityEngine.Debug.LogError($"Push failed: {e}\nOutput: {o}");
                }
            }
            catch (Exception ex) { HandleException(ex, "Push"); }
            finally
            {
                EditorUtility.ClearProgressBar();
                isBusy = false;
                if (repoFoundAndRemoteDetected) RefreshStatusAndHistory();
                else Repaint();
            }
        }

        /// <summary>
        /// Refreshes all Git status information: local changes, ahead/behind counts, upstream status, and history.
        /// Updates the UI state variables accordingly.
        /// </summary>
        void RefreshStatusAndHistory()
        {
            if (isBusy || !IsGitRepository())
            {
                if (!isBusy) Repaint();
                return;
            }

            isBusy = true;
            EditorUtility.DisplayProgressBar("Git Status", "Refreshing repository status...", 0.05f);

            // Clear previous state
            stagedFiles.Clear();
            unstagedFiles.Clear();
            commitsToPull.Clear();
            commitsToPush.Clear();
            commitHistory.Clear();
            isBehindRemote = false;
            isAheadRemote = false;
            hasUncommittedChanges = false;
            hasConflicts = false;

            try
            {
                // Get Local Changes & Conflicts
                EditorUtility.DisplayProgressBar("Git Status", "Checking local file changes...", 0.1f);
                StringBuilder statusOutput, statusError;
                if (RunGitCommand("status --porcelain -uall", out statusOutput, out statusError))
                {
                    ParsePorcelainStatus(statusOutput.ToString());
                    hasUncommittedChanges = stagedFiles.Any() || unstagedFiles.Any();
                    hasConflicts = stagedFiles.Any(s => s.IsConflicted) || unstagedFiles.Any(u => u.IsConflicted);
                }
                else
                {
                    UnityEngine.Debug.LogError($"Failed to get git status: {statusError}");
                    lastStatusMessage = "Error getting file status. See console.";
                }

                // Check Upstream Configuration
                EditorUtility.DisplayProgressBar("Git Status", "Checking upstream configuration...", 0.2f);
                StringBuilder upstreamCheckOutput, upstreamCheckError;
                if (!RunGitCommand($"rev-parse --abbrev-ref --symbolic-full-name @{{u}}", out upstreamCheckOutput, out upstreamCheckError))
                {
                    string errText = upstreamCheckError.ToString();
                    if (errText.Contains("no upstream configured") || errText.Contains("fatal: no upstream") || errText.Contains("unknown revision or path not in the working tree"))
                    {
                        upstreamConfigured = false;
                    }
                    else if (!string.IsNullOrEmpty(errText.Trim()))
                    {
                        upstreamConfigured = false; // Assume not configured on other errors
                        UnityEngine.Debug.LogWarning($"Could not verify upstream branch configuration: {errText.Trim()}");
                    }
                    else
                    {
                        upstreamConfigured = false; // Failed without specific error
                    }
                }
                else
                {
                    upstreamConfigured = !string.IsNullOrWhiteSpace(upstreamCheckOutput.ToString());
                    if (!upstreamConfigured) UnityEngine.Debug.LogWarning("Upstream check command succeeded but returned empty result.");
                }

                // Check Ahead/Behind (Only if upstream IS configured)
                if (upstreamConfigured)
                {
                    // Check Behind
                    EditorUtility.DisplayProgressBar("Git Status", "Checking commits to pull...", 0.3f);
                    StringBuilder behindCountOutput, behindCountError;
                    if (RunGitCommand($"rev-list --count HEAD..@{{u}}", out behindCountOutput, out behindCountError))
                    {
                        int behindCount;
                        if (int.TryParse(behindCountOutput.ToString().Trim(), out behindCount) && behindCount > 0)
                        {
                            isBehindRemote = true;
                            StringBuilder behindLogOutput, behindLogError;
                            if (RunGitCommand($"log --oneline --pretty=format:\"%h %s\" --max-count={MAX_LOG_ENTRIES} HEAD..@{{u}}", out behindLogOutput, out behindLogError))
                            { commitsToPull = behindLogOutput.ToString().Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).ToList(); }
                            else { commitsToPull.Add("Error retrieving pull log"); UnityEngine.Debug.LogError($"Failed to get pull log: {behindLogError}"); }
                        }
                    }

                    // Check Ahead
                    EditorUtility.DisplayProgressBar("Git Status", "Checking commits to push...", 0.5f);
                    StringBuilder aheadCountOutput, aheadCountError;
                    if (RunGitCommand($"rev-list --count @{{u}}..HEAD", out aheadCountOutput, out aheadCountError))
                    {
                        int aheadCount;
                        if (int.TryParse(aheadCountOutput.ToString().Trim(), out aheadCount) && aheadCount > 0)
                        {
                            isAheadRemote = true;
                            StringBuilder aheadLogOutput, aheadLogError;
                            if (RunGitCommand($"log --oneline --pretty=format:\"%h %s\" --max-count={MAX_LOG_ENTRIES} @{{u}}..HEAD", out aheadLogOutput, out aheadLogError))
                            { commitsToPush = aheadLogOutput.ToString().Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).ToList(); }
                            else { commitsToPush.Add("Error retrieving push log"); UnityEngine.Debug.LogError($"Failed to get push log: {aheadLogError}"); }
                        }
                    }
                }
                else
                {
                    isAheadRemote = false;
                    isBehindRemote = false;
                    commitsToPull.Clear();
                    commitsToPush.Clear();
                }

                // Get Local Commit History
                EditorUtility.DisplayProgressBar("Git Status", "Getting recent commit history...", 0.7f);
                StringBuilder historyOutput, historyError;
                string historyCmd = $"log --pretty=format:\"%h %ad | %s (%an)\" --date=relative --max-count={MAX_LOG_ENTRIES}";
                if (RunGitCommand(historyCmd, out historyOutput, out historyError))
                {
                    commitHistory = historyOutput.ToString().Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).ToList();
                    if (!commitHistory.Any()) { commitHistory.Add("No commits found in history."); }
                }
                else
                {
                    commitHistory.Add("Error retrieving commit history.");
                    UnityEngine.Debug.LogError($"Failed to get commit history: {historyError}");
                }

                // Update Overall Status Message
                UpdateOverallStatusMessage(!upstreamConfigured && repoFoundAndRemoteDetected);

            }
            catch (Exception ex) { HandleException(ex, "Refresh Status & History"); }
            finally
            {
                EditorUtility.ClearProgressBar();
                isBusy = false;
                Repaint();
            }
        }

        /// <summary>
        /// Parses the output of `git status --porcelain` into `stagedFiles` and `unstagedFiles` lists.
        /// Handles different status codes including renames, untracked, and conflicts.
        /// </summary>
        void ParsePorcelainStatus(string statusOutput)
        {
            stagedFiles.Clear();
            unstagedFiles.Clear();
            if (string.IsNullOrWhiteSpace(statusOutput)) return;

            var lines = statusOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                if (line.Length < 4) continue;

                string statusCode = line.Substring(0, 2);
                string pathPart = line.Substring(3);
                string filePath = pathPart;
                string originalPath = null;

                if (statusCode.StartsWith("R") || statusCode.StartsWith("C"))
                {
                    int arrowIndex = pathPart.IndexOf(" -> ");
                    if (arrowIndex > 0)
                    {
                        originalPath = pathPart.Substring(0, arrowIndex);
                        filePath = pathPart.Substring(arrowIndex + 4);
                    }
                }

                filePath = TrimQuotes(filePath);
                if (originalPath != null) originalPath = TrimQuotes(originalPath);

                var entry = new GitStatusEntry { StatusCode = statusCode, FilePath = filePath, OriginalPath = originalPath };
                char indexStatus = statusCode[0];
                char workTreeStatus = statusCode[1];
                bool isUntrackedEntry = indexStatus == '?' && workTreeStatus == '?';
                bool isConflictedEntry = statusCode.Contains("U");

                if (indexStatus != ' ' && indexStatus != '?')
                {
                    stagedFiles.Add(entry);
                }

                if (isUntrackedEntry || workTreeStatus != ' ')
                {
                    if (isUntrackedEntry || isConflictedEntry || workTreeStatus != ' ')
                    {
                        if (!unstagedFiles.Any(f => f.FilePath == entry.FilePath))
                        {
                            unstagedFiles.Add(entry);
                        }
                    }
                }
            }

            stagedFiles.Sort((a, b) => String.Compare(a.FilePath, b.FilePath, StringComparison.OrdinalIgnoreCase));
            unstagedFiles.Sort((a, b) => String.Compare(a.FilePath, b.FilePath, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Helper method to remove leading/trailing quotes from a path string if present.
        /// </summary>
        private string TrimQuotes(string path)
        {
            if (path != null && path.Length >= 2 && path.StartsWith("\"") && path.EndsWith("\""))
            {
                return path.Substring(1, path.Length - 2);
            }
            return path;
        }

        /// <summary>
        /// Updates the main `lastStatusMessage` based on the current repository state flags
        /// (conflicts, ahead/behind, changes, upstream status).
        /// </summary>
        void UpdateOverallStatusMessage(bool upstreamIssueDetected)
        {
            if (hasConflicts)
            {
                lastStatusMessage = "Error: Merge conflicts detected! Resolve conflicts, then stage and commit.";
                return;
            }

            if (upstreamIssueDetected)
            {
                lastStatusMessage = "Warning: Branch has no upstream configured. Push/Pull requires setup.";
                List<string> otherStatus = new List<string>();
                if (isBehindRemote) otherStatus.Add($"Behind ({commitsToPull.Count})");
                if (isAheadRemote) otherStatus.Add($"Ahead ({commitsToPush.Count})");
                if (stagedFiles.Any()) otherStatus.Add($"{stagedFiles.Count} staged");
                if (unstagedFiles.Any(f => !f.IsUntracked)) otherStatus.Add($"{unstagedFiles.Count(f => !f.IsUntracked)} unstaged");
                if (unstagedFiles.Any(f => f.IsUntracked)) otherStatus.Add($"{unstagedFiles.Count(f => f.IsUntracked)} untracked");
                if (otherStatus.Any()) lastStatusMessage += $" Current status: {string.Join(", ", otherStatus)}.";
                return;
            }

            List<string> statusParts = new List<string>();
            if (isBehindRemote) statusParts.Add($"Behind remote ({commitsToPull.Count})");
            if (isAheadRemote) statusParts.Add($"Ahead of remote ({commitsToPush.Count})");
            if (stagedFiles.Any()) statusParts.Add($"{stagedFiles.Count} staged");
            int modifiedUnstagedCount = unstagedFiles.Count(f => !f.IsUntracked && !f.IsConflicted);
            if (modifiedUnstagedCount > 0) statusParts.Add($"{modifiedUnstagedCount} unstaged");
            int untrackedCount = unstagedFiles.Count(f => f.IsUntracked);
            if (untrackedCount > 0) statusParts.Add($"{untrackedCount} untracked");

            if (statusParts.Any())
            {
                lastStatusMessage = string.Join(", ", statusParts) + ".";
            }
            else if (fetchCompletedSuccessfully)
            {
                lastStatusMessage = "Repository is clean and up-to-date.";
            }
            else if (repoFoundAndRemoteDetected)
            {
                lastStatusMessage = "Local repository is clean. Fetch remote status?";
            }
            else
            {
                if (string.IsNullOrEmpty(lastStatusMessage) || lastStatusMessage == "Initializing..." || lastStatusMessage.Contains("successful") || lastStatusMessage.Contains("refreshed") || lastStatusMessage.Contains("OK"))
                {
                    lastStatusMessage = "Status refreshed.";
                }
            }
        }


        /// <summary>
        /// Executes a Git command synchronously using System.Diagnostics.Process.
        /// Captures standard output and standard error. Handles common errors like timeout or Git not found.
        /// Sets environment variable LANG=en_US.UTF-8 to prevent localized Git messages.
        /// </summary>
        /// <param name="arguments">The arguments to pass to the git command.</param>
        /// <param name="output">StringBuilder to capture standard output.</param>
        /// <param name="error">StringBuilder to capture standard error.</param>
        /// <returns>True if the command exited with code 0 (or a known benign non-zero code), False otherwise.</returns>
        private bool RunGitCommand(string arguments, out StringBuilder output, out StringBuilder error)
        {
            output = new StringBuilder();
            error = new StringBuilder();
            string root;

            try
            {
                root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            }
            catch (Exception ex)
            {
                error.AppendLine($"Err:Failed to determine project root path: {ex.Message}");
                repoFoundAndRemoteDetected = false;
                return false;
            }

            if (!repoFoundAndRemoteDetected && !Directory.Exists(Path.Combine(root, ".git")))
            {
                error.AppendLine("Err:No .git directory found at project root.");
                return false;
            }


            var startInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = arguments,
                WorkingDirectory = root,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                EnvironmentVariables = { ["LANG"] = "en_US.UTF-8" }
            };

            StringBuilder processOutput = new StringBuilder();
            StringBuilder processError = new StringBuilder();
            int exitCode = -1;

            using (var process = new Process { StartInfo = startInfo })
            {
                process.OutputDataReceived += (s, e) => { if (e.Data != null) processOutput.AppendLine(e.Data); };
                process.ErrorDataReceived += (s, e) => { if (e.Data != null) processError.AppendLine(e.Data); };

                try
                {
                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    if (!process.WaitForExit(GIT_COMMAND_TIMEOUT_MS))
                    {
                        try { if (!process.HasExited) process.Kill(); } catch { /* Ignore */ }
                        error.AppendLine($"Err:Command '{arguments}' timed out after {GIT_COMMAND_TIMEOUT_MS / 1000} seconds.");
                        output.Append(processOutput);
                        error.Append(processError);
                        return false;
                    }
                    exitCode = process.ExitCode;
                }
                catch (System.ComponentModel.Win32Exception ex)
                {
                    if (ex.NativeErrorCode == 2) // ERROR_FILE_NOT_FOUND
                    {
                        error.AppendLine("Err: 'git' command not found. Ensure Git is installed and added to the system PATH environment variable.");
                        repoFoundAndRemoteDetected = false;
                    }
                    else
                    {
                        error.AppendLine($"Err:Win32Exception running git '{arguments}': {ex.Message} (Code: {ex.NativeErrorCode})");
                    }
                    output.Append(processOutput);
                    error.Append(processError);
                    return false;
                }
                catch (Exception ex)
                {
                    error.AppendLine($"Err:Exception running git '{arguments}': {ex.GetType().Name} - {ex.Message}");
                    output.Append(processOutput);
                    error.Append(processError);
                    UnityEngine.Debug.LogError($"Exception running git '{arguments}': {ex.Message}\n{ex.StackTrace}");
                    return false;
                }
            }

            output.Append(processOutput);
            error.Append(processError);

            if (exitCode == 0) return true;

            // Handle benign non-zero exit codes
            string errStr = error.ToString();
            string outStr = output.ToString();
            if (exitCode == 1)
            {
                if (arguments.StartsWith("commit") && (errStr.Contains("nothing to commit") || outStr.Contains("nothing to commit"))) return true;
                if (arguments.StartsWith("stash push") && (outStr.Contains("No local changes to save") || errStr.Contains("No local changes to save"))) return true;
                if (arguments.StartsWith("reset") && errStr.Contains("Unstaged changes after reset")) return true;
            }

            // Prepend user-friendly messages for common errors
            if (errStr.Contains("Authentication failed")) error.Insert(0, "Authentication Error: Check Git credentials (e.g., via Git Credential Manager or SSH key setup).\n---\n");
            else if (errStr.Contains("could not read Username") || errStr.Contains("could not read Password")) error.Insert(0, "Authentication Required: Git needs credentials. Try the operation in a terminal first, or configure credential helper.\n---\n");
            else if (errStr.Contains("Network is unreachable") || errStr.Contains("Could not resolve host")) error.Insert(0, "Network Error: Check internet connection and remote URL.\n---\n");
            else if (errStr.Contains("merge conflict")) error.Insert(0, "Merge Conflict Detected: Resolve conflicts manually.\n---\n");
            else if (errStr.Contains("Permission denied")) error.Insert(0, "Permission Error: Check file/repository permissions or SSH key setup.\n---\n");
            else if (errStr.Contains("non-fast-forward")) error.Insert(0, "Push Rejected (Non-Fast-Forward): Remote has changes you don't. Pull first.\n---\n");
            else if (errStr.Contains("fatal: repository") && errStr.Contains("not found")) error.Insert(0, "Repository Not Found: Check remote URL and permissions.\n---\n");
            else if (errStr.Contains("src refspec") && errStr.Contains("does not match any")) error.Insert(0, "Refspec Error: The local or remote branch name might be incorrect or not exist.\n---\n");

            return false; // Non-zero exit code not handled above is failure
        }


        /// <summary>
        /// Central handler for exceptions occurring outside of RunGitCommand (e.g., file system access, Unity API calls).
        /// Logs the error and updates the status message.
        /// </summary>
        private void HandleException(Exception ex, string operation)
        {
            UnityEngine.Debug.LogError($"Exception during '{operation}': {ex.GetType().Name} - {ex.Message}\n{ex.StackTrace}");
            lastStatusMessage = $"Error during '{operation}'. Check Unity Console for details.";
            try { EditorUtility.ClearProgressBar(); } catch { /* Ignore */ }
            isBusy = false;
            Repaint();
        }

        /// <summary>
        /// Safely encloses a string in double quotes for use as a command line argument,
        /// particularly useful for file paths that might contain spaces.
        /// ProcessStartInfo handles internal escaping when UseShellExecute is false.
        /// </summary>
        private string SafeQuote(string input)
        {
            return "\"" + input + "\"";
        }
    }
}
#endif
