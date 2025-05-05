using FzLib;
using MapBoard.Mapping.Model;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace MapBoard.ViewModels
{
    public class AttributeTableViewModel : INotifyPropertyChanged
    {
        private ObservableCollection<FeatureAttributeCollection> attributes;
        private ObservableCollection<FeatureAttributeCollection> selectedAttributes;
        public event PropertyChangedEventHandler PropertyChanged;

        public ObservableCollection<FeatureAttributeCollection> Attributes
        {
            get => attributes;
            set => this.SetValueAndNotify(ref attributes, value, nameof(Attributes));
        }
        public ObservableCollection<FeatureAttributeCollection> SelectedAttributes
        {
            get => selectedAttributes;
            set => this.SetValueAndNotify(ref selectedAttributes, value, nameof(SelectedAttributes));
        }
    }
}
