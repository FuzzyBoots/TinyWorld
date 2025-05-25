using System;

namespace JamTrackerItchio.Editor
{
    public static class DependencyUtils
    {
        private static bool? _isToolbarExtenderInstalled;

        /// <summary>
        /// Checks if the Unity Toolbar Extender package is installed
        /// </summary>
        public static bool IsToolbarExtenderInstalled
        {
            get
            {
                if (!_isToolbarExtenderInstalled.HasValue)
                {
                    _isToolbarExtenderInstalled = IsTypeAvailable(
                        "Paps.UnityToolbarExtenderUIToolkit.MainToolbarElement"
                    );
                }
                return _isToolbarExtenderInstalled.Value;
            }
        }

        /// <summary>
        /// Checks if a type is available by name
        /// </summary>
        private static bool IsTypeAvailable(string typeName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    if (assembly.GetType(typeName) != null)
                        return true;
                }
                catch
                {
                    // Ignore assembly load failures
                }
            }
            return false;
        }
    }
}
