using System;
using System.Collections.Generic;
using JamTrackerItchio.Editor.UI;
using UnityEditor;
using UnityEngine;

namespace JamTrackerItchio.Editor.Controllers
{
    /// <summary>
    /// Controller for the JamTrackerWindow, handling state and lifecycle
    /// </summary>
    public class JamTrackerWindowController
    {
        private EditorWindow _window;

        private JamListManager _jamListManager;
        private PaginationController _paginationController;
        private DependencyInstaller _dependencyInstaller;
        private JamTrackerStyles _styles;

        private readonly List<IJamTrackerView> _views = new List<IJamTrackerView>();
        private LoadingView _loadingView;

        private bool _isRecompiling = false;
        private bool _componentInitialized = false;
        private double _recompileStartTime;
        private bool _isWaitingForCompilationToFinish = false;
        private double _lastAutoRetryTime;
        private const double AUTO_RETRY_INTERVAL = 2.0;

        private static GameJam _currentSelectedJam;
        public static GameJam SelectedJam
        {
            get
            {
                if (_currentSelectedJam == null)
                {
                    var settings = JamTrackerSettings.LoadFromEditorPrefs();
                    _currentSelectedJam = settings.ToGameJam();
                }
                return _currentSelectedJam;
            }
        }

        public static event Action<GameJam> OnSelectedJamChanged;

        public bool IsInitialized => _componentInitialized && !_isRecompiling;

        public JamTrackerWindowController(EditorWindow window)
        {
            _window = window;
            _recompileStartTime = EditorApplication.timeSinceStartup;
            _lastAutoRetryTime = EditorApplication.timeSinceStartup;

            _loadingView = new LoadingView(_recompileStartTime, ForceRefresh);

            try
            {
                _isRecompiling = EditorStyles.label == null || EditorApplication.isCompiling;
            }
            catch
            {
                _isRecompiling = true;
            }

            if (!_isRecompiling)
            {
                InitializeComponents();
            }
            else
            {
                // During recompilation, set up the update callback so we can initialize later
                _isWaitingForCompilationToFinish = true;
            }
        }

        public void OnEnable()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.update += OnEditorUpdate;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            AssemblyReloadEvents.afterAssemblyReload += OnAfterAssemblyReload;
        }

        public void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
            AssemblyReloadEvents.afterAssemblyReload -= OnAfterAssemblyReload;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        }

        public void OnGUI()
        {
            double currentTime = EditorApplication.timeSinceStartup;
            if (
                (!_componentInitialized || _jamListManager == null || _styles == null)
                && currentTime - _lastAutoRetryTime > AUTO_RETRY_INTERVAL
            )
            {
                _lastAutoRetryTime = currentTime;

                // Only attempt auto-retry if we're not compiling
                if (!EditorApplication.isCompiling)
                {
                    EditorApplication.delayCall += () =>
                    {
                        _isRecompiling = false;
                        _isWaitingForCompilationToFinish = false;
                        InitializeComponents();
                        RepaintWindow();
                    };
                }
            }

            try
            {
                if (
                    _isRecompiling
                    || !_componentInitialized
                    || _jamListManager == null
                    || _styles == null
                )
                {
                    _loadingView.Draw(DateTime.Now);

                    // Check if we need to try initializing again (timeout case)
                    double elapsedTime = EditorApplication.timeSinceStartup - _recompileStartTime;
                    if (
                        (
                            !_isWaitingForCompilationToFinish
                            && elapsedTime > LoadingView.RECOMPILE_TIMEOUT
                        ) || (elapsedTime > LoadingView.RECOMPILE_TIMEOUT * 2)
                    ) // Force retry after double timeout
                    {
                        EditorApplication.delayCall += () =>
                        {
                            _isRecompiling = false;
                            _isWaitingForCompilationToFinish = false;
                            InitializeComponents();
                            RepaintWindow();
                        };
                    }

                    return;
                }

                DateTime now = DateTime.Now;

                foreach (var view in _views)
                {
                    view.Draw(now);
                }
            }
            catch (Exception ex)
            {
                // Fallback UI in case of error
                EditorGUILayout.HelpBox(
                    $"An error occurred while drawing the UI: {ex.Message}\n\nThe editor might be recompiling. Try closing and reopening this window if the error persists.",
                    MessageType.Error
                );

                if (GUILayout.Button("Attempt to Reinitialize Components"))
                {
                    InitializeComponents();
                }
            }
        }

        private void InitializeComponents()
        {
            if (EditorApplication.isCompiling)
            {
                _isRecompiling = true;
                _isWaitingForCompilationToFinish = true;
                return;
            }

            try
            {
                if (EditorStyles.label == null)
                {
                    EditorApplication.delayCall += () =>
                    {
                        if (!_componentInitialized && !EditorApplication.isCompiling)
                        {
                            InitializeComponents();
                        }
                    };
                    return;
                }

                _styles = new JamTrackerStyles();
                _jamListManager = new JamListManager();
                _paginationController = new PaginationController(10);
                _dependencyInstaller = new DependencyInstaller();

                var settings = JamTrackerSettings.LoadFromEditorPrefs();
                if (_jamListManager != null)
                {
                    _jamListManager.SelectedJam = settings.ToGameJam();
                    _currentSelectedJam = _jamListManager.SelectedJam;
                }

                _jamListManager?.FetchJams();

                InitializeViews();

                _componentInitialized = true;
                _isRecompiling = false;
                _isWaitingForCompilationToFinish = false;

                EditorApplication.delayCall += RepaintWindow;
            }
            catch (Exception)
            {
                _componentInitialized = false;

                EditorApplication.delayCall += () =>
                {
                    if (!_componentInitialized && !EditorApplication.isCompiling)
                    {
                        InitializeComponents();
                        RepaintWindow();
                    }
                };
            }
        }

        private void InitializeViews()
        {
            _views.Clear();

            var dependencyView = new DependencyView(_dependencyInstaller);
            var selectedJamView = new SelectedJamView(_jamListManager, SaveSelectedJam);
            var searchFilterView = new SearchFilterView(_jamListManager);
            var jamListView = new JamListView(
                _jamListManager,
                _paginationController,
                _styles,
                SaveSelectedJam
            );

            _views.Add(dependencyView);
            _views.Add(selectedJamView);
            _views.Add(searchFilterView);
            _views.Add(jamListView);
        }

        private void SaveSelectedJam()
        {
            var settings = new JamTrackerSettings();
            settings.FromGameJam(_jamListManager.SelectedJam);
            settings.SaveToEditorPrefs();

            _currentSelectedJam = _jamListManager.SelectedJam;
            OnSelectedJamChanged?.Invoke(_currentSelectedJam);
        }

        private void OnBeforeAssemblyReload()
        {
            _isRecompiling = true;
            _isWaitingForCompilationToFinish = true;
            _recompileStartTime = EditorApplication.timeSinceStartup;

            // We can't repaint during this event, so we'll do it in the next update
            EditorApplication.delayCall += RepaintWindow;
        }

        private void OnAfterAssemblyReload()
        {
            // This might not get called due to domain reload, but we'll try anyway
            // Use delayCall to ensure we're not in the middle of a layout event
            EditorApplication.delayCall += () =>
            {
                _isRecompiling = false;
                _isWaitingForCompilationToFinish = false;

                if (!_componentInitialized)
                {
                    InitializeComponents();
                }

                RepaintWindow();
            };
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            // When entering play mode or exiting play mode, we need to reinitialize
            if (
                state == PlayModeStateChange.EnteredEditMode
                || state == PlayModeStateChange.EnteredPlayMode
            )
            {
                EditorApplication.delayCall += () =>
                {
                    if (!_isRecompiling)
                    {
                        InitializeComponents();
                        RepaintWindow();
                    }
                };
            }
        }

        private void OnEditorUpdate()
        {
            // Repaint the window every minute to update the time displays
            if (DateTime.Now.Second == 0)
            {
                RepaintWindow();
            }

            // Check if we need to initialize components after compilation
            if (
                (_isRecompiling && !EditorApplication.isCompiling)
                || (!_componentInitialized && !EditorApplication.isCompiling)
            )
            {
                // Only attempt to initialize if we haven't done so recently
                double currentTime = EditorApplication.timeSinceStartup;
                if (currentTime - _lastAutoRetryTime > AUTO_RETRY_INTERVAL)
                {
                    _lastAutoRetryTime = currentTime;

                    EditorApplication.delayCall += () =>
                    {
                        _isRecompiling = false;
                        _isWaitingForCompilationToFinish = false;
                        if (!_componentInitialized || _jamListManager == null || _styles == null)
                        {
                            InitializeComponents();
                        }
                        RepaintWindow();
                    };
                }
            }

            // Also check for null components that might indicate a failed initialization
            if (_componentInitialized && (_jamListManager == null || _styles == null))
            {
                double currentTime = EditorApplication.timeSinceStartup;
                if (currentTime - _lastAutoRetryTime > AUTO_RETRY_INTERVAL)
                {
                    _lastAutoRetryTime = currentTime;

                    EditorApplication.delayCall += () =>
                    {
                        _componentInitialized = false;
                        InitializeComponents();
                        RepaintWindow();
                    };
                }
            }
        }

        private void ForceRefresh()
        {
            _isRecompiling = false;
            _isWaitingForCompilationToFinish = false;
            InitializeComponents();
            RepaintWindow();
        }

        private void RepaintWindow()
        {
            if (_window != null)
            {
                _window.Repaint();
            }
        }
    }
}
