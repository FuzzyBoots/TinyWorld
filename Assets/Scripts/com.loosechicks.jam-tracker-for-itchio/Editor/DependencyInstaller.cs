using System;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace JamTrackerItchio.Editor
{
    public class DependencyInstaller
    {
        private bool _isInstalling = false;

        public bool IsInstalling => _isInstalling;

        public async void CheckAndInstallDependencies()
        {
            _isInstalling = true;

            try
            {
                await CheckAndInstallDependenciesAsync();

                EditorUtility.DisplayDialog(
                    "Dependencies Installation",
                    "Dependencies have been checked and installed successfully.\n\n"
                        + "You may need to restart Unity to fully apply changes.",
                    "OK"
                );
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    $"[Jam Tracker for Itch.io] Error installing dependencies: {ex.Message}"
                );
                EditorUtility.DisplayDialog(
                    "Installation Error",
                    $"Failed to install dependencies: {ex.Message}\n\nCheck console for details.",
                    "OK"
                );
            }
            finally
            {
                _isInstalling = false;
            }
        }

        private async Task CheckAndInstallDependenciesAsync()
        {
            await DependencySetup.CheckAndInstallDependenciesAsync();
        }
    }
}
