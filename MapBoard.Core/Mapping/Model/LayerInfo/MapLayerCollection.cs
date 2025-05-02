using Esri.ArcGISRuntime;
using Esri.ArcGISRuntime.Data;
using Esri.ArcGISRuntime.Mapping;
using FzLib;
using MapBoard.Model;
using MapBoard.Util;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;   
using MLayerCollection = MapBoard.Model.LayerCollection;
using ELayerCollection = Esri.ArcGISRuntime.Mapping.LayerCollection;
using MapBoard.IO;
using Mapster;

namespace MapBoard.Mapping.Model
{
    /// <summary>
    /// 包含ArcGIS类型的图层集合
    /// </summary>
    public class MapLayerCollection : MLayerCollection
    {
        public const string LayersFileName = "layers.json";

        public const string LayerFileName = "style.json";

        private IMapLayerInfo selected;

        public MapLayerCollection()
        {
        }

        private MapLayerCollection(ELayerCollection esriLayers)
        {
            EsriLayers = esriLayers;
            SetLayers(new ObservableCollection<ILayerInfo>());
        }

        /// <summary>
        /// 获取其中可编辑的图层
        /// </summary>
        public IEnumerable<MapLayerInfo> EditableLayers => this.OfType<MapLayerInfo>().Where(p => p.CanEdit);

        /// <summary>
        /// 对应的ArcGIS图层
        /// </summary>
        public ELayerCollection EsriLayers { get; private set; }

        /// <summary>
        /// 当前选中的图层
        /// </summary>
        public IMapLayerInfo Selected
        {
            get
            {
                if (Parameters.AppType == AppType.MAUI)
                {
                    throw new NotSupportedException("MAUI平台不存在当前图层机制");
                }
                return selected;
            }
            set
            {
                SelectedIndex = value != null ? IndexOf(value) : -1;
                selected = value;
            }
        }

        /// <summary>
        /// 从本地加载配置文件，生成图层
        /// </summary>
        /// <param name="esriLayers"></param>
        /// <returns></returns>
        public static async Task<MapLayerCollection> GetInstanceAsync(ELayerCollection esriLayers)
        {
            string path = Path.Combine(FolderPaths.DataPath, LayersFileName);
            if (!File.Exists(path))
            {
                return new MapLayerCollection(esriLayers);
            }
            MapLayerCollection instance = null;

            //读取到临时对象
            var tempLayers = FromFile(path);
            instance = new MapLayerCollection(esriLayers);
            //将临时变量映射到新的对象中
            tempLayers.Adapt(instance);

            //将临时变量中的图层添加到新的MapLayerCollection对象中
            foreach (var layer in tempLayers)
            {
                await instance.AddAndLoadAsync(layer);
            }
            //如果选定了某个图层，则将其设置为选定图层
            if (instance.SelectedIndex >= 0
                && instance.SelectedIndex < instance.Count)
            {
                instance.Selected = instance[instance.SelectedIndex] as MapLayerInfo;
            }
            return instance;
        }

        /// <summary>
        /// 根据图层信息，创建新图层并插入到最后
        /// </summary>
        /// <param name="layer"></param>
        /// <returns></returns>
        /// <exception cref="NotSupportedException"></exception>
        public async Task<ILayerInfo> AddAndLoadAsync(ILayerInfo layer)
        {
            if (layer is not MapLayerInfo)
            {
                layer = new MgdbMapLayerInfo(layer);
            }
            await AddAndLoadLayerAsync(layer as MapLayerInfo, 0);
            (layer as MapLayerInfo).PropertyChanged += OnLayerPropertyChanged;
            LayerList.Add(layer);
            return layer;
        }
        /// <summary>
        /// 清空所有图层
        /// </summary>
        public void Clear()
        {
            foreach (var layer in EsriLayers.ToArray())
            {
                EsriLayers.Remove(layer);
            }
            foreach (MapLayerInfo layer in LayerList)
            {
                layer.Dispose();
            }
            LayerList.Clear();
        }

        /// <summary>
        /// 根据ArcGIS的图层，找到对应的<see cref="MapLayerInfo"/>
        /// </summary>
        /// <param name="layer"></param>
        /// <returns></returns>
        public MapLayerInfo Find(ILayerContent layer)
        {
            if (layer is FeatureLayer l)
            {
                return LayerList.Cast<MapLayerInfo>().FirstOrDefault(p => p.Layer == layer);
            }
            else if (layer is FeatureCollectionLayer cl)
            {
                foreach (var cll in cl.Layers)
                {
                    var result = Find(cll);
                    if (result != null)
                    {
                        return result;
                    }
                }
                return null;
            }
            throw new NotSupportedException($"未知的图层类型{layer.GetType().Name}");
        }

        public ItemsOperationErrorCollection LoadErrors { get; } = new ItemsOperationErrorCollection();

        /// <summary>
        /// 插入一个图层到最后
        /// </summary>
        /// <param name="index"></param>
        /// <param name="layer"></param>
        /// <returns></returns>
        public async Task InsertAsync(int index, MapLayerInfo layer)
        {
            await AddAndLoadLayerAsync(layer, Count - index);
            layer.PropertyChanged += OnLayerPropertyChanged;
            LayerList.Insert(index, layer);
        }

        /// <summary>
        /// 是否正在批量加载
        /// </summary>
        public bool IsBatchLoading { get; private set; }

        /// <summary>
        /// 加载所有图层
        /// </summary>
        /// <param name="esriLayers"></param>
        /// <returns></returns>
        public async Task LoadAsync(ELayerCollection esriLayers)
        {
            IsBatchLoading = true;

            try
            {
                await MobileGeodatabase.InitializeAsync();
                EsriLayers = esriLayers;
                //初始化一个新的图层列表
                SetLayers([]);
                //图层配置文件
                string path = Path.Combine(FolderPaths.DataPath, LayersFileName);
                if (!File.Exists(path))
                {
                    return;
                }
                //获取临时图层对象，并映射到当前对象。使用临时对象是因为无法将对象反序列化后直接应用到当前对象，对象类型可能不一致，属性可能被覆盖。
                var tempLayers = FromFile(path);
                tempLayers.Adapt(this);
                //将临时对象中所有图层加入当前对象
                foreach (var layer in tempLayers)
                {
                    if (layer == tempLayers[^1])
                    {
                        IsBatchLoading = false;
                    }
                    try
                    {
                        await AddAndLoadAsync(layer);
                    }
                    catch (Exception ex)
                    {
                        LoadErrors.Add(new ItemsOperationError(layer.Name, ex));
                    }
                }
                //如果选定了某个图层，则将其设置为选定图层
                if (SelectedIndex >= 0
                    && SelectedIndex < Count)
                {
                    Selected = this[SelectedIndex] as MapLayerInfo;
                }
            }
            finally
            {
                IsBatchLoading = false;
            }
        }

        /// <summary>
        /// 将索引为<paramref name="fromIndex"/>的图层插入到索引为<paramref name="toIndex"/>的项之前
        /// </summary>
        /// <param name="fromIndex"></param>
        /// <param name="toIndex"></param>
        public void Move(int fromIndex, int toIndex)
        {
            if (fromIndex == toIndex)
            {
                return;
            }
            EsriLayers.Move(Count - fromIndex - 1, Count - toIndex - 1);
            LayerList.Move(fromIndex, toIndex);
        }

        /// <summary>
        /// 将一些图层插入到指定位置
        /// </summary>
        /// <param name="fromIndex"></param>
        /// <param name="toIndex"></param>
        public void Move(IList<int> fromIndexs, int insertBeforeIndex)
        {
            //将索引转换为具体的图层
            List<IMapLayerInfo> fromItems = fromIndexs
                .OrderBy(p => p)
                .Select(p => this[p])
                .Cast<IMapLayerInfo>()
                .ToList();

            HashSet<IMapLayerInfo> fromItemsSet = fromItems.ToHashSet();

            //需要锁定待插入位置后面那个图层。
            //如果后面那个图层是需要移动的图层，那么无法作为锚点，于是就需要继续向后寻找。
            //如果一直找到了最后，那么就设为null
            while (insertBeforeIndex < Count && fromItemsSet.Contains(this[insertBeforeIndex] as IMapLayerInfo))
            {
                insertBeforeIndex++;
            }
            IMapLayerInfo insertBeforeItem = insertBeforeIndex >= Count ? null : this[insertBeforeIndex] as IMapLayerInfo;

            foreach (var item in fromItems)
            {
                int nowFromIndex = IndexOf(item);//目前状态下，需要移动的图层的索引
                int nowToIndex = insertBeforeItem == null ? Count : IndexOf(insertBeforeItem);//目前状态下，目标位置后面的图层的位置
                if (nowFromIndex <= nowToIndex)
                {
                    /*
                     * 当 nowFromIndex 小于或等于 nowToIndex 时，需要将 nowToIndex 减一，
                     * 是因为在移动过程中，源位置上的项已经被移走，
                     * 导致目标位置之前的所有项的索引都减少了1。
                     * 因此，为了将项正确地移动到目标位置之前，
                     * 需要将 nowToIndex 减一来抵消这种索引变化。

                     * 举个例子，假设我们有一个列表 [A, B, C, D]，
                     * 我们想要将 B 和 C 移动到 D 之前。
                     * 在第一次移动时，B 会被移动到 D 之前，列表变成了 [A, C, B, D]。
                     * 由于 B 被移走了，所以 C 的索引从2变成了1。
                     * 因此，在第二次移动时，我们需要将目标位置减一，
                     * 才能将 C 正确地移动到 D 之前，得到最终的列表 [A, C, B, D]。
                     */
                    nowToIndex--;
                }
                Move(nowFromIndex, nowToIndex);
            }
        }

        /// <summary>
        /// 在更新Esri图层后，进行重新插入动作以刷新画面
        /// </summary>
        /// <param name="layer"></param>
        public void RefreshEsriLayer(IMapLayerInfo layer)
        {
            if (IndexOf(layer) < 0)
            {
                throw new ArgumentException("图层不在图层集合中");
            }
            int index = Count - 1 - IndexOf(layer);
            EsriLayers.RemoveAt(index);
            EsriLayers.Insert(index, layer.GetLayerForLayerList());
        }
        public async Task RemoveAsync(IMapLayerInfo layer)
        {
            try
            {
                EsriLayers.Remove(layer.GetLayerForLayerList());
                await layer.DeleteAsync();
                layer.Dispose();
            }
            catch
            {
            }
            layer.PropertyChanged -= OnLayerPropertyChanged;
            LayerList.Remove(layer);
        }

        /// <summary>
        /// 保存图层配置
        /// </summary>
        public void Save()
        {
            Save(Path.Combine(FolderPaths.DataPath, LayersFileName));
        }

        /// <summary>
        /// 插入图层到指定位置
        /// </summary>
        /// <param name="layer"></param>
        /// <param name="index"></param>
        /// <returns></returns>
        private async Task AddAndLoadLayerAsync(IMapLayerInfo layer, int index)
        {
            try
            {
                //加载图层
                if (!layer.IsLoaded)
                {
                    await layer.LoadAsync();
                }

                //添加ArcGIS图层到ArcGIS图层列表
                Layer fl = layer.GetLayerForLayerList();
                Debug.Assert(fl != null);
                if (index == -1)
                {
                    EsriLayers.Add(fl);
                }
                else
                {
                    EsriLayers.Insert(index, fl);
                }
            }
            catch (Exception ex)
            {
                try
                {
                    layer.Dispose();
                    if (layer.Layer != null)
                    {
                        EsriLayers.Remove(layer.Layer);
                    }
                }
                catch
                {
                }
                throw;
            }
        }

        private void OnLayerPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            Save();
        }
    }
}