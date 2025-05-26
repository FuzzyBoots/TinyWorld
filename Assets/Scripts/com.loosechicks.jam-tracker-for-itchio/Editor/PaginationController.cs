using UnityEngine;

namespace JamTrackerItchio.Editor
{
    public class PaginationController
    {
        private int _pageSize = 10;
        private int _currentPage = 0;
        private int _totalPages = 0;
        private int _itemCount = 0;

        public int PageSize
        {
            get => _pageSize;
            set
            {
                _pageSize = Mathf.Max(1, value);
                RecalculateTotalPages();
            }
        }

        public int CurrentPage
        {
            get => _currentPage;
            set => _currentPage = Mathf.Clamp(value, 0, Mathf.Max(0, _totalPages - 1));
        }

        public int TotalPages => _totalPages;

        public int StartIndex => _currentPage * _pageSize;

        public int EndIndex => Mathf.Min(StartIndex + _pageSize, _itemCount);

        public bool CanGoToPreviousPage => _currentPage > 0;

        public bool CanGoToNextPage => _currentPage < _totalPages - 1;

        public PaginationController(int pageSize = 10)
        {
            _pageSize = Mathf.Max(1, pageSize);
        }

        public void UpdateItemCount(int itemCount)
        {
            _itemCount = Mathf.Max(0, itemCount);
            RecalculateTotalPages();
        }

        public void NextPage()
        {
            if (CanGoToNextPage)
            {
                _currentPage++;
            }
        }

        public void PreviousPage()
        {
            if (CanGoToPreviousPage)
            {
                _currentPage--;
            }
        }

        private void RecalculateTotalPages()
        {
            _totalPages = Mathf.Max(1, Mathf.CeilToInt((float)_itemCount / _pageSize));
            _currentPage = Mathf.Clamp(_currentPage, 0, Mathf.Max(0, _totalPages - 1));
        }
    }
}
