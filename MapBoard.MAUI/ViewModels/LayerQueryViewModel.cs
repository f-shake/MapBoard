using FzLib;
using MapBoard.IO;
using MapBoard.IO.Gpx;
using MapBoard.Model;
using MapBoard.Models;
using MapBoard.Query;
using MapBoard.Services;
using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace MapBoard.ViewModels
{
    public class LayerQueryViewModel : INotifyPropertyChanged
    {
        private bool useSql;

        public LayerQueryViewModel(ILayerInfo layer)
        {
            Fields = new ObservableCollection<FieldInfo>(layer.Fields);
            SqlLogicalOperators = new ObservableCollection<SqlLogicalOperator>(Enum.GetValues<SqlLogicalOperator>());
            Items = new ObservableCollection<SqlWhereClauseItem>();

            Items.CollectionChanged += Items_CollectionChanged;
            Items.Add(new SqlWhereClauseItem
            {
                IsFirstItem = true,
            });

            AddConditionCommand = new Command(AddCondition);
            RemoveItemCommand = new Command<SqlWhereClauseItem>(RemoveItem);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public ICommand AddConditionCommand { get; }

        public IList<FieldInfo> Fields { get; }

        public ObservableCollection<SqlWhereClauseItem> Items { get; }

        public ICommand RemoveItemCommand { get; }

        public string Sql { get; set; }
        public IList<SqlLogicalOperator> SqlLogicalOperators { get; }

        public bool UseSql
        {
            get => useSql;
            set => this.SetValueAndNotify(ref useSql, value);
        }
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void AddCondition()
        {
            Items.Add(new SqlWhereClauseItem
            {
                LogicalOperator = SqlLogicalOperator.And,
                Field = Fields.FirstOrDefault()
            });
        }

        private void Items_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            //可能是MAUI的BUG，Popup中的Picker，SelectedItem总是自动置为null
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
            {
                Dispatcher.GetForCurrentThread().Dispatch(() =>
                {
                    foreach (SqlWhereClauseItem item in e.NewItems)
                    {
                        item.Field = Fields.FirstOrDefault();
                        item.LogicalOperator = SqlLogicalOperator.Or;
                        item.LogicalOperator = SqlLogicalOperator.And;
                    }
                });
            }
        }
        private void RemoveItem(SqlWhereClauseItem item)
        {
            if (item != null)
            {
                Items.Remove(item);
            }
        }
    }
}
