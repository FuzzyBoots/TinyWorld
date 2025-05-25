#if HAS_TOOLBAR_EXTENDER
using Paps.UnityToolbarExtenderUIToolkit;
using UnityEditor;
using UnityEngine.UIElements;

namespace JamTrackerItchio.Editor
{
    [MainToolbarElement(id: "SelectJamButton", ToolbarAlign.Right)]
    public class SelectJamButton : Button
    {
        public void InitializeElement()
        {
            try
            {
                text = "Select a jam 🕹️";
                clicked += () => JamTrackerWindow.ShowWindow();

                UpdateVisibility(JamTrackerWindow.SelectedJam);

                JamTrackerWindow.OnSelectedJamChanged += UpdateVisibility;
            }
            catch
            {
                text = "Jam Tracker...";

                EditorApplication.delayCall += () =>
                {
                    try
                    {
                        text = "Select a jam 🕹️";
                        clicked += () => JamTrackerWindow.ShowWindow();
                        UpdateVisibility(JamTrackerWindow.SelectedJam);
                        JamTrackerWindow.OnSelectedJamChanged += UpdateVisibility;
                    }
                    catch { }
                };
            }
        }

        ~SelectJamButton()
        {
            try
            {
                JamTrackerWindow.OnSelectedJamChanged -= UpdateVisibility;
            }
            catch
            {
                // Ignore errors during cleanup
            }
        }

        private void UpdateVisibility(GameJam selectedJam)
        {
            try
            {
                style.display = selectedJam == null ? DisplayStyle.Flex : DisplayStyle.None;
            }
            catch
            {
                style.display = DisplayStyle.Flex;
            }
        }
    }
}
#endif
