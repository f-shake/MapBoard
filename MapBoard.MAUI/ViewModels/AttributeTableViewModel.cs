using FzLib;
using MapBoard.Mapping.Model;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;

namespace MapBoard.ViewModels
{
    public class AttributeTableViewModel : INotifyPropertyChanged
    {
        private ObservableCollection<FeatureAttributeCollection> attributes;
        public event PropertyChangedEventHandler PropertyChanged;

        public ObservableCollection<FeatureAttributeCollection> Attributes
        {
            get => attributes;
            set => this.SetValueAndNotify(ref attributes, value, nameof(Attributes));
        }

        public ICommand SelectFeatureCommand { get; set; }
    }
}