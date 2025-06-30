using FzLib;
using MapBoard.Mapping.Model;
using MapBoard.Views;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;

namespace MapBoard.ViewModels
{
    public class AttributeTableViewModel : INotifyPropertyChanged
    {
        private ObservableCollection<FeatureAttributeCollection> attributes;
        private int currentPage = 1;
        private ObservableCollection<FeatureAttributeCollection> currentPageItems;
        private int itemsPerPage = 50;
        private int totalPages;

        public AttributeTableViewModel()
        {
            NextPageCommand = new Command(() =>
            {
                if (HasNextPage)
                {
                    CurrentPage++;
                    UpdateCurrentPageItems();
                }
            });

            PreviousPageCommand = new Command(() =>
            {
                if (HasPreviousPage)
                {
                    CurrentPage--;
                    UpdateCurrentPageItems();
                }
            });


            GoToPageCommand = new Command(async () => await ShowPageSelectionAsync());

        }

        public event PropertyChangedEventHandler PropertyChanged;

        public ObservableCollection<FeatureAttributeCollection> Attributes
        {
            get => attributes;
            set
            {
                this.SetValueAndNotify(ref attributes, value, nameof(Attributes));
                UpdatePagination();
            }
        }

        public int CurrentPage
        {
            get => currentPage;
            private set
            {
                this.SetValueAndNotify(ref currentPage, value, nameof(CurrentPage));
                OnPropertyChanged(nameof(CurrentPageDisplay));
                OnPropertyChanged(nameof(HasPreviousPage));
                OnPropertyChanged(nameof(HasNextPage));
            }
        }

        public string CurrentPageDisplay => $"{CurrentPage}/{TotalPages}";
        public ObservableCollection<FeatureAttributeCollection> CurrentPageItems
        {
            get => currentPageItems;
            private set => this.SetValueAndNotify(ref currentPageItems, value, nameof(CurrentPageItems));
        }

        public ICommand GoToPageCommand { get; private set; }
        public bool HasNextPage => CurrentPage < TotalPages;
        public bool HasPreviousPage => CurrentPage > 1;
        public int ItemsPerPage
        {
            get => itemsPerPage;
            set
            {
                this.SetValueAndNotify(ref itemsPerPage, value, nameof(ItemsPerPage));
                UpdatePagination();
            }
        }

        public ICommand NextPageCommand { get; private set; }
        public ObservableCollection<int> PageSizeOptions { get; } = new ObservableCollection<int> { 10, 20, 50, 100, 200, 500, 1000 };
        public ICommand PreviousPageCommand { get; private set; }

        public ICommand SelectFeatureCommand { get; set; }

        public int TotalPages
        {
            get => totalPages;
            private set
            {
                this.SetValueAndNotify(ref totalPages, value, nameof(TotalPages));
                OnPropertyChanged(nameof(CurrentPageDisplay));
                OnPropertyChanged(nameof(HasPreviousPage));
                OnPropertyChanged(nameof(HasNextPage));
            }
        }
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }


        private async Task ShowPageSelectionAsync()
        {
            if (TotalPages <= 1) return;

            // 生成页码选项列表
            var pageOptions = Enumerable.Range(1, TotalPages).Select(p => $"第 {p} 页").ToArray();

            var action = await MainPage.Current.DisplayActionSheet("选择页码", "取消", null, pageOptions);

            // 提取页码数字
            if (int.TryParse(action.Replace("第 ", "").Replace(" 页", ""), out int selectedPage))
            {
                CurrentPage = selectedPage;
                UpdateCurrentPageItems();
            }
        }
        private void UpdateCurrentPageItems()
        {
            if (Attributes == null || Attributes.Count == 0)
            {
                CurrentPageItems = new ObservableCollection<FeatureAttributeCollection>();
                return;
            }

            var startIndex = (CurrentPage - 1) * ItemsPerPage;
            var count = Math.Min(ItemsPerPage, Attributes.Count - startIndex);

            var pageItems = new ObservableCollection<FeatureAttributeCollection>(
                Attributes.Skip(startIndex).Take(count));

            CurrentPageItems = pageItems;
        }

        private void UpdatePagination()
        {
            if (Attributes == null || Attributes.Count == 0)
            {
                TotalPages = 0;
                CurrentPageItems = new ObservableCollection<FeatureAttributeCollection>();
                return;
            }

            TotalPages = (int)Math.Ceiling((double)Attributes.Count / ItemsPerPage);
            CurrentPage = 1;
            UpdateCurrentPageItems();
        }
    }
}