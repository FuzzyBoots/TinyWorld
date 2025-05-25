using System;
using System.Collections.Generic;
using System.Linq;

namespace JamTrackerItchio.Editor
{
    public enum JamFilterState
    {
        All,
        Active,
        Voting,
        Upcoming,
        Ended,
    }

    public class JamListManager
    {
        private List<GameJam> _allJams = new List<GameJam>();
        private List<GameJam> _filteredJams = new List<GameJam>();
        private GameJam _selectedJam;

        private string _searchQuery = "";
        private JamFilterState _currentFilter = JamFilterState.All;
        private bool _isLoading = false;

        public bool IsLoading => _isLoading;
        public List<GameJam> FilteredJams => _filteredJams;
        public GameJam SelectedJam
        {
            get => _selectedJam;
            set => _selectedJam = value;
        }
        public string SearchQuery
        {
            get => _searchQuery;
            set
            {
                if (_searchQuery != value)
                {
                    _searchQuery = value;
                    FilterJams();
                }
            }
        }

        public JamFilterState CurrentFilter
        {
            get => _currentFilter;
            set
            {
                if (_currentFilter != value)
                {
                    _currentFilter = value;
                    FilterJams();
                }
            }
        }

        public void FetchJams()
        {
            _isLoading = true;
            EditorCoroutineUtility.StartCoroutine(ItchioJamFetcher.FetchJams(OnJamsFetched), null);
        }

        private void OnJamsFetched(List<GameJam> jams)
        {
            _allJams = jams.OrderBy(j => j.EndDate).ToList();

            DateTime now = DateTime.Now;
            foreach (var jam in _allJams)
            {
                jam.UpdateCachedTimesAt(now);
            }

            FilterJams();
            _isLoading = false;

            // If we have a saved jam ID, try to find it in the fetched jams
            if (_selectedJam != null && _selectedJam.JamId != -1)
            {
                var matchingJam = _allJams.FirstOrDefault(j => j.JamId == _selectedJam.JamId);
                if (matchingJam != null)
                {
                    _selectedJam = matchingJam;
                    _selectedJam.SetSelected(true);
                }
            }
        }

        public void FilterJams()
        {
            DateTime now = DateTime.Now;

            IEnumerable<GameJam> searchFiltered;
            if (string.IsNullOrWhiteSpace(_searchQuery))
            {
                searchFiltered = _allJams;
            }
            else
            {
                string query = _searchQuery.ToLowerInvariant();
                searchFiltered = _allJams.Where(j => j.Title.ToLowerInvariant().Contains(query));
            }

            switch (_currentFilter)
            {
                case JamFilterState.Active:
                    _filteredJams = searchFiltered.Where(j => j.IsActiveAt(now)).ToList();
                    break;
                case JamFilterState.Voting:
                    _filteredJams = searchFiltered.Where(j => j.IsVotingPeriodAt(now)).ToList();
                    break;
                case JamFilterState.Upcoming:
                    _filteredJams = searchFiltered.Where(j => now < j.StartDate).ToList();
                    break;
                case JamFilterState.Ended:
                    _filteredJams = searchFiltered
                        .Where(j => now > j.EndDate && !j.IsVotingPeriodAt(now))
                        .ToList();
                    break;
                case JamFilterState.All:
                default:
                    _filteredJams = searchFiltered.ToList();
                    break;
            }

            _filteredJams = _filteredJams
                .OrderBy(j => j.IsActiveAt(now) ? 0 : 1)
                .ThenBy(j => j.IsVotingPeriodAt(now) ? 0 : 1)
                .ThenBy(j => now < j.StartDate ? 0 : 1)
                .ThenBy(j => j.EndDate)
                .ToList();
        }

        public void SelectJam(GameJam jam)
        {
            _selectedJam?.SetSelected(false);

            _selectedJam = jam;
            _selectedJam.SetSelected(true);
        }

        public void DeselectJam()
        {
            if (_selectedJam != null)
            {
                _selectedJam.SetSelected(false);
                _selectedJam = null;
            }
        }
    }
}
