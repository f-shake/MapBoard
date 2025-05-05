using FzLib;
using MapBoard.Mapping.Model;
using MapBoard.Model;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace MapBoard.ViewModels
{
    public class LayerViewViewModel : INotifyPropertyChanged
    {
        private MapLayerCollection layers;
        public event PropertyChangedEventHandler PropertyChanged;

        public ObservableCollection<LayerGroupList> Groups { get; } = new ObservableCollection<LayerGroupList>();

        public MapLayerCollection Layers
        {
            get => layers;
            set => this.SetValueAndNotify(ref layers, value, nameof(Layers));
        }
        public string[] ViewTypes { get; } = { "按层序", "按分组" };
        /// <summary>
        /// 生成分组多选框
        /// </summary>
        public void Update()
        {
            try
            {
                Groups.Clear();
                if(Layers.Count==0)
                {
                    return;
                }
                if (Layers.Any(p => string.IsNullOrEmpty(p.Group)))
                {
                    Groups.Add(new LayerGroupList("（无）",
                        GetGroupVisible(Layers.Where(p => string.IsNullOrEmpty(p.Group))),
                        true,
                        Layers.Where(p => string.IsNullOrEmpty(p.Group)).Cast<IMapLayerInfo>()));
                }
                foreach (var layers in Layers
                    .Where(p => !string.IsNullOrEmpty(p.Group))
                   .GroupBy(p => p.Group))
                {
                    Groups.Add(new LayerGroupList(layers.Key, GetGroupVisible(layers), false,
                        layers.Cast<IMapLayerInfo>()));
                }
            }
            catch (Exception ex)
            {


            }
            this.Notify(nameof(Layers));
        }
        /// <summary>
        /// 获取分组可见情况
        /// </summary>
        /// <param name="layers"></param>
        /// <returns>true为可见，false为不可见，null为部分可见</returns>
        private bool? GetGroupVisible(IEnumerable<ILayerInfo> layers)
        {
            int count = layers.Count();
            int visibleCount = layers.Where(p => p.LayerVisible).Count();
            if (visibleCount == 0)
            {
                return false;
            }
            if (count == visibleCount)
            {
                return true;
            }
            return null;
        }
    }

    public class LayerGroupList
    {
        public LayerGroupList(string name, bool? visible, bool isNull, IEnumerable<IMapLayerInfo> layers) 
        {
            Name = name;
            Visible = visible;
            IsNull = isNull;
            Layers = new ObservableCollection<IMapLayerInfo>(layers);
        }

        public ObservableCollection<IMapLayerInfo> Layers { get; private set; }

        /// <summary>
        /// 是否为默认组（未分组的图层所在的组）
        /// </summary>
        public bool IsNull { get; private set; }

        /// <summary>
        /// 组名
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// 是否可见。null表示部分可见
        /// </summary>
        public bool? Visible { get; set; }
    }

}