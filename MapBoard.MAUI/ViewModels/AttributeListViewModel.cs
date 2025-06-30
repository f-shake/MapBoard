using MapBoard.Mapping.Model;
using System.ComponentModel;

namespace MapBoard.ViewModels
{
    public class AttributeListViewModel : INotifyPropertyChanged
    {

        public event PropertyChangedEventHandler PropertyChanged;

        public FeatureAttributeCollection Attributes { get; set; }  
    }
}
