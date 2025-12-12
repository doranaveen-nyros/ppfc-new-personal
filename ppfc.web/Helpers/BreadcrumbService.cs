namespace ppfc.web.Helpers
{
    public class BreadcrumbService
    {
        public event Action OnChange;

        private List<string> _items = new();

        public IReadOnlyList<string> Items => _items;

        public void Set(params string[] crumbs)
        {
            _items = crumbs.ToList();
            NotifyStateChanged();
        }

        private void NotifyStateChanged() => OnChange?.Invoke();
    }

}
