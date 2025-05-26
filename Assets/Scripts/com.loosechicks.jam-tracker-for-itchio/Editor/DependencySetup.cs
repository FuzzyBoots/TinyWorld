using System.Threading.Tasks;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace JamTrackerItchio.Editor
{
    /// <summary>
    /// Handles the installation of required dependencies for the Jam Tracker for Itch.io package.
    /// </summary>
    public static class DependencySetup
    {
        private const string ToolbarExtenderPackageName =
            "com.paps.unity-toolbar-extender-ui-toolkit";
        private const string ToolbarExtenderPackageUrl =
            "https://github.com/Sammmte/unity-toolbar-extender-ui-toolkit.git?path=/Assets/Package";
        private static ListRequest _listRequest;
        private static AddRequest _addRequest;

        public static async Task CheckAndInstallDependenciesAsync()
        {
            try
            {
                bool isToolbarExtenderInstalled = await IsPackageInstalledAsync(
                    ToolbarExtenderPackageName
                );

                if (!isToolbarExtenderInstalled)
                {
                    Debug.Log(
                        $"[Jam Tracker for Itch.io] Installing dependency: {ToolbarExtenderPackageName}"
                    );
                    await InstallPackageAsync(ToolbarExtenderPackageUrl);
                    Debug.Log(
                        $"[Jam Tracker for Itch.io] Successfully installed: {ToolbarExtenderPackageName}"
                    );
                }
                else
                {
                    Debug.Log(
                        $"[Jam Tracker for Itch.io] Dependency already installed: {ToolbarExtenderPackageName}"
                    );
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError(
                    $"[Jam Tracker for Itch.io] Error checking/installing dependencies: {ex.Message}"
                );
                throw; // Re-throw to allow the calling method to handle the error
            }
        }

        private static async Task<bool> IsPackageInstalledAsync(string packageName)
        {
            _listRequest = Client.List(offlineMode: true, includeIndirectDependencies: true);

            while (!_listRequest.IsCompleted)
                await Task.Delay(100);

            if (_listRequest.Status == StatusCode.Success)
            {
                foreach (var package in _listRequest.Result)
                {
                    if (package.name == packageName)
                        return true;
                }
                return false;
            }
            else
            {
                Debug.LogError(
                    $"[Jam Tracker for Itch.io] Failed to check for package {packageName}: {_listRequest.Error.message}"
                );
                return false;
            }
        }

        private static async Task InstallPackageAsync(string packageUrl)
        {
            Debug.Log($"[Jam Tracker for Itch.io] Installing package from URL: {packageUrl}");
            _addRequest = Client.Add(packageUrl);

            while (!_addRequest.IsCompleted)
                await Task.Delay(100);

            if (_addRequest.Status == StatusCode.Failure)
            {
                Debug.LogError(
                    $"[Jam Tracker for Itch.io] Failed to install package from {packageUrl}: {_addRequest.Error.message}"
                );
            }
            else
            {
                Debug.Log(
                    $"[Jam Tracker for Itch.io] Successfully added package from {packageUrl}"
                );
            }
        }
    }
}
