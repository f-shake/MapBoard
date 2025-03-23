using Esri.ArcGISRuntime.Data;
using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.UI.Controls;
using MapBoard.Mapping.Model;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using MapBoard.UI;
using MapBoard.Util;
using System.ComponentModel;

namespace MapBoard.Mapping
{
    /// <summary>
    /// 图形选择帮助类
    /// </summary>
    public class SelectionHelper
    {
        private bool isClearing = false;

        private bool isSelecting = false;

        /// <summary>
        /// 选中的要素，以ID为Key
        /// </summary>
        private Dictionary<long, Feature> selectedFeatures = new Dictionary<long, Feature>();

        public SelectionHelper(MainMapView map)
        {
            map.GeoViewTapped += MapviewTapped;
            CollectionChanged += SelectedFeatures_CollectionChanged;
            MapView = map;
            Editor.EditorStatusChanged += Editor_EditorStatusChanged;
        }

        public event EventHandler<SelectedFeaturesChangedEventArgs> CollectionChanged;

        /// <summary>
        /// 关联的编辑器
        /// </summary>
        public EditorHelper Editor => MapView.Editor;

        /// <summary>
        /// 图层
        /// </summary>
        public MapLayerCollection Layers => MapView.Layers;

        /// <summary>
        /// 关联的地图
        /// </summary>
        public MainMapView MapView { get; }

        /// <summary>
        /// 选中的要素的ID值
        /// </summary>
        public Dictionary<long, Feature>.KeyCollection SelectedFeatureIDs => selectedFeatures.Keys;

        /// <summary>
        /// 选中的要素
        /// </summary>
        public Dictionary<long, Feature>.ValueCollection SelectedFeatures => selectedFeatures.Values;
        /// <summary>
        /// 清除选择
        /// </summary>
        public void ClearSelection()
        {
            ClearSelection(true);
        }

        /// <summary>
        /// 选择指定的要素
        /// </summary>
        /// <param name="feature">要素</param>
        /// <param name="clearAll">是否在选择前清除选择</param>
        /// <returns></returns>
        public bool Select(Feature feature, bool clearAll = false)
        {
            return Select(new[] { feature }, clearAll) == 1;
        }

        /// <summary>
        /// 选取一些要素
        /// </summary>
        /// <param name="features"></param>
        /// <param name="clearAll">是否在选择前清除选择</param>
        /// <returns>新增选取的个数</returns>
        public int Select(IEnumerable<Feature> features, bool clearAll = false)
        {
            if (features == null || !features.Any())
            {
                throw new ArgumentException("要选择的图形为空");
            }
            Debug.Assert(features.Select(p => p.FeatureTable.Layer).Distinct().Count() == 1);
            var layer = Layers.Find(features.First().FeatureTable.Layer as FeatureLayer);
            if (layer == null)
            {
                throw new ArgumentException("找不到图层");
            }
            //如果图层不匹配，那么要先把之前图层给清除选择，保证只有一个图层被选择
            if (Layers.Selected != layer)
            {
                ClearSelection();
                Layers.Selected = layer;
            }
            List<Feature> add = new List<Feature>();
            List<Feature> remove = new List<Feature>();

            //清除选择
            if (clearAll && SelectedFeatures.Count > 0)
            {
                remove.AddRange(SelectedFeatures);
                layer.Layer.ClearSelection();
                selectedFeatures.Clear();
            }
            add.AddRange(features);
            layer.Layer.SelectFeatures(features);
            foreach (var feature in features)
            {
                selectedFeatures.TryAdd(feature.GetID(), feature);
            }
            CollectionChanged?.Invoke(this, new SelectedFeaturesChangedEventArgs(layer, add, remove));
            return add.Count;
        }

        /// <summary>
        /// 框选
        /// </summary>
        /// <returns></returns>
        public async Task SelectRectangleAsync()
        {
            ClearSelection();
            var envelope = await Editor.GetEmptyRectangleAsync();
            if (envelope != null)
            {
                envelope = GeometryEngine.Project(envelope, SpatialReferences.Wgs84) as Envelope;
                bool allLayers = Keyboard.Modifiers.HasFlag(ModifierKeys.Alt);
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) //按下Ctrl，则为相交；否则为包含
                {
                    await SelectAsync(envelope, null, SpatialRelationship.Intersects, SelectionMode.New, allLayers);
                }
                else
                {
                    await SelectAsync(envelope, null, SpatialRelationship.Contains, SelectionMode.New, allLayers);
                }
            }
        }

        /// <summary>
        /// 取消选取一些要素
        /// </summary>
        /// <param name="features"></param>
        /// <param name="clearAll"></param>
        /// <returns>新增选取的个数</returns>
        public int Unselect(IEnumerable<Feature> features)
        {
            if (features == null || !features.Any())
            {
                throw new ArgumentException("要取消选择的图形为空");
            }
            Debug.Assert(features.Select(p => p.FeatureTable.Layer).Distinct().Count() == 1);
            var layer = Layers.Find(features.First().FeatureTable.Layer as FeatureLayer);
            if (layer == null)
            {
                throw new ArgumentException("找不到图层");
            }
            if (Layers.Selected != layer)
            {
                Layers.Selected = layer;
            }
            List<Feature> remove = new List<Feature>();

            layer.Layer.UnselectFeatures(features);
            foreach (var feature in features)
            {
                if (selectedFeatures.Remove(feature.GetID()))
                {
                    remove.Add(feature);
                }
            }
            CollectionChanged?.Invoke(this, new SelectedFeaturesChangedEventArgs(layer, null, remove));
            return remove.Count;
        }

        /// <summary>
        /// 取消选择一个要素
        /// </summary>
        /// <param name="feature"></param>
        /// <returns></returns>
        public bool UnSelect(Feature feature)
        {
            if (!selectedFeatures.ContainsKey(feature.GetID()))
            {
                return false;
            }
            return Unselect(new[] { feature }) == 1;
        }

        /// <summary>
        /// 清除选择
        /// </summary>
        /// <param name="raiseEvent">是否通知选择的要素集合改变<see cref="CollectionChanged"/></param>
        private void ClearSelection(bool raiseEvent)
        {
            isClearing = true;
            Editor.Cancel();
            if (selectedFeatures.Count == 0)
            {
                return;
            }
            var layers = selectedFeatures.Select(p => p.Value.FeatureTable.Layer as FeatureLayer).Distinct().ToList();
            Debug.Assert(layers.Count == 1);
            layers[0].ClearSelection();
            SelectedFeaturesChangedEventArgs e = new SelectedFeaturesChangedEventArgs(Layers.Find(layers[0]), null, selectedFeatures.Values);
            selectedFeatures.Clear();
            isClearing = false;
            if (raiseEvent)
            {
                CollectionChanged?.Invoke(this, e);
            }
        }
        private void Editor_EditorStatusChanged(object sender, EditorStatusChangedEventArgs e)
        {
            if (!isClearing && !e.IsRunning)
            {
                ClearSelection();
            }
        }

        /// <summary>
        /// 点击选择
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void MapviewTapped(object sender, GeoViewInputEventArgs e)
        {
            if (MapView.CurrentTask == BoardTask.Draw //正在绘制
                || MapView.CurrentTask != BoardTask.Select//当前不在选择状态，
                    && !Keyboard.Modifiers.HasFlag(ModifierKeys.Control)
                    && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)
                    && !Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)
                    && !Config.Instance.TapToSelect
                    )
            {
                //没有按下任何修饰键，并且未开启单击选择
                return;
            }
            MapPoint point = GeometryEngine.Project(e.Location, SpatialReferences.Wgs84) as MapPoint;
            double tolerance = MapView.MapScale / 1e8;
            Envelope envelope = new Envelope(point.X - tolerance, point.Y - tolerance, point.X + tolerance, point.Y + tolerance, SpatialReferences.Wgs84);
            //在“继续选择”的情况下：
            //按Ctrl表示先清除选择然后再选择
            //按Alt表示从已选择的图形中删去
            SelectionMode mode = Keyboard.Modifiers.HasFlag(ModifierKeys.Control) ?
               SelectionMode.New
               : (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt) ?
               SelectionMode.Subtract
               : SelectionMode.Add);
            //在“新建选择”的情况下：
            //按Ctrl表示从当前图层中点选
            //按Shift表示从所有图层中点选
            //按Alt表示从选择后立刻进入编辑模式
            bool allLayers =
                MapView.CurrentTask != BoardTask.Select && (Keyboard.Modifiers == ModifierKeys.Shift
                || Config.Instance.TapToSelect && Config.Instance.TapToSelectAllLayers);
            bool edit = MapView.CurrentTask != BoardTask.Select && Keyboard.Modifiers == ModifierKeys.Alt;
            await SelectAsync(envelope, e.Position, SpatialRelationship.Intersects, mode, allLayers, edit);
        }
        /// <summary>
        /// 根据范围或点，查询要素并选择
        /// </summary>
        /// <param name="envelope">提供范围</param>
        /// <param name="point">提供点</param>
        /// <param name="relationship">空间查询关系</param>
        /// <param name="mode">选择模式</param>
        /// <param name="allLayers">是否从所有图层中查询</param>
        /// <param name="startEdit">选中后是否直接编辑</param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        private async Task SelectAsync(Envelope envelope,
            Point? point,
            SpatialRelationship relationship,
             SelectionMode mode,
             bool allLayers = false,
             bool startEdit = false)
        {
            if (allLayers && !point.HasValue)
            {
                throw new ArgumentException("需要选取多图层，但是没有提供鼠标位置");
            }

            if (envelope == null || isSelecting)
            {
                return;
            }
            isSelecting = true;
            try
            {
                QueryParameters query = new QueryParameters
                {
                    Geometry = envelope,
                    SpatialRelationship = relationship
                };
                bool first = SelectedFeatures.Count == 0; //是否为新建选择
                List<Feature> features = null;
                IMapLayerInfo layer = null;
                if (allLayers) //若从所有图层中查询
                {
                    var result = (await MapView.IdentifyLayersAsync(point.Value, 8, false))//容差为8像素
                        .Where(p => p.LayerContent.IsVisible)//仅可见图层
                        .Select(p => new
                        {
                            Layer = Layers.Find(p.LayerContent),//图层
                            Elements = p.GeoElements,//要素
                            SubLayer = p.SublayerResults.Any() ? p.SublayerResults[0] : null//子图层（仅临时图层）
                        })
                        .Where(p => p.Layer.Interaction.CanSelect)//筛选支持选择的图层
                        .FirstOrDefault();


                    if (result == null)
                    {
                        return;
                    }
                    layer = result.Layer;
                    Debug.Assert(layer != null);
                    MapView.Layers.Selected = layer;
                    IReadOnlyList<GeoElement> elements = result.SubLayer?.GeoElements ?? result.Elements;
                    features = elements.Cast<Feature>().ToList();
                    layer.Layer.SelectFeatures(features);
                }
                else//仅当前图层
                {
                    layer = Layers.Selected;

                    if (layer == null
                        || !layer.Interaction.CanSelect
                        || !layer.LayerVisible)//可见、可选图层
                    {
                        return;
                    }
                    FeatureLayer fLayer = layer.Layer;
                    FeatureQueryResult result = await fLayer.SelectFeaturesAsync(query, mode);

                    await Task.Run(() =>
                    {
                        features = result.ToList();
                    });
                }
                if (features.Count == 0)
                {
                    return;
                }
                List<Feature> add = new List<Feature>();
                List<Feature> remove = new List<Feature>();

                if (first)//首次选择
                {
                    if (startEdit && layer is IMapLayerInfo w)//直接编辑
                    {
                        ClearSelection();
                        await MapView.Editor.EditAsync(w, features[0]).ConfigureAwait(false);
                    }
                    else//加入选择
                    {
                        foreach (var feature in features)
                        {
                            selectedFeatures.Add(feature.GetID(), feature);
                            add.Add(feature);
                        }
                    }
                }
                else//继续选择
                {
                    switch (mode)
                    {
                        case SelectionMode.Add:
                            //找到新增部分，加入
                            foreach (var feature in features)
                            {
                                long fid = feature.GetID();
                                if (!selectedFeatures.ContainsKey(fid))
                                {
                                    selectedFeatures.Add(fid, feature);
                                    add.Add(feature);
                                }
                            }
                            break;

                        case SelectionMode.New:
                            //删除之前的，加入所有新增
                            remove.AddRange(selectedFeatures.Values);
                            selectedFeatures.Clear();
                            foreach (var feature in features)
                            {
                                selectedFeatures.Add(feature.GetID(), feature);
                            }
                            break;

                        case SelectionMode.Subtract:
                            //找到和之前的相交部分，移除
                            foreach (var feature in features)
                            {
                                long fid = feature.GetID();
                                if (selectedFeatures.ContainsKey(fid))
                                {
                                    selectedFeatures.Remove(fid);
                                    remove.Add(feature);
                                }
                            }
                            break;

                        default:
                            throw new InvalidEnumArgumentException();
                    }
                }
                CollectionChanged?.Invoke(this, new SelectedFeaturesChangedEventArgs(layer, add, remove));
            }
            finally
            {
                isSelecting = false;
            }
        }

        private void SelectedFeatures_CollectionChanged(object sender, SelectedFeaturesChangedEventArgs e)
        {
            if (SelectedFeatures.Count == 0 && MapView.CurrentTask == BoardTask.Select)
            {
                MapView.CurrentTask = BoardTask.Ready;
            }
            else if (SelectedFeatures.Count != 0 && MapView.CurrentTask != BoardTask.Select)
            {
                MapView.CurrentTask = BoardTask.Select;
            }
        }
    }
}