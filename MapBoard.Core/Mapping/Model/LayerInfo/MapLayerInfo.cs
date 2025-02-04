using Esri.ArcGISRuntime.Data;
using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Mapping;
using FzLib;
using MapBoard.Model;
using MapBoard.Util;
using Mapster;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PropertyChanged;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using FieldInfo = MapBoard.Model.FieldInfo;

namespace MapBoard.Mapping.Model
{
    /// <summary>
    /// 包含ArcGIS类型的图层
    /// </summary>
    public abstract class MapLayerInfo : LayerInfo, IMapLayerInfo
    {
        protected FeatureTable table;

        private FeatureLayer layer;

        public MapLayerInfo()
        {
        }

        public MapLayerInfo(string name)
        {
            Name = name;
        }

        public MapLayerInfo(ILayerInfo layer)
        {
            layer.Adapt(this);

            if (Histories.Count > 0)
            {
                Histories.Clear();
            }
        }

        public event EventHandler Unattached;

        [JsonIgnore]
        public bool CanEdit => Interaction.CanEdit;

        [JsonIgnore]
        public GeometryType GeometryType => table.GeometryType;

        [JsonIgnore]
        public bool HasTable => table != null;

        [JsonIgnore]
        [AlsoNotifyFor(nameof(GeometryType))]
        public bool IsLoaded { get; set; }

        [JsonIgnore]
        public FeatureLayer Layer => layer;

        [JsonIgnore]
        public long NumberOfFeatures
        {
            get
            {
                try
                {
                    return table == null ? 0 : table.NumberOfFeatures;
                }
                catch
                {
                    return 0;
                }
            }
        }

        public override object Clone()
        {
            return this.Adapt<MapLayerInfo>();
        }

        public abstract Task DeleteAsync();
        public virtual void Dispose()
        {
            Unattached?.Invoke(this, new EventArgs());
        }

        public SymbolInfo GetDefaultSymbol()
        {
            return GeometryType switch
            {
                GeometryType.Point => SymbolInfo.DefaultPointSymbol,
                GeometryType.Multipoint => SymbolInfo.DefaultPointSymbol,
                GeometryType.Polyline => SymbolInfo.DefaultLineSymbol,
                GeometryType.Polygon => SymbolInfo.DefaultPolygonSymbol,
                _ => null
            };
        }

        /// <summary>
        /// 获取添加到MapView的图层集合中的图层
        /// </summary>
        /// <returns></returns>
        public virtual Layer GetLayerForLayerList()
        {
            return layer;
        }
        /// <summary>
        /// 加载图层
        /// </summary>
        /// <returns></returns>
        public async Task LoadAsync()
        {
            table = GetTable();
            if (table == null)
            {
                throw new Exception("获取到的要素类为空");
            }
            layer = GetLayerForLoading(table);

            try
            {
                await table.LoadAsync().TimeoutAfter(Parameters.LoadTimeout);
            }
            catch (TimeoutException)
            {
                table.CancelLoad();
                throw new TimeoutException("加载超时，通常是无法连接网络服务所致");
            }
            //应用属性
            ApplyProperties();
            //应用符号和标签
            this.ApplyStyle();
            IsLoaded = true;
            this.Notify(nameof(GeometryType));
        }

        public Task<Envelope> QueryExtentAsync(QueryParameters parameters)
        {
            return table.QueryExtentAsync(parameters);
        }

        public Task<FeatureQueryResult> QueryFeaturesAsync(QueryParameters parameters)
        {
            return table.QueryFeaturesAsync(parameters);
        }

        /// <summary>
        /// 重新加载
        /// </summary>
        /// <param name="layers"></param>
        /// <returns></returns>
        public async Task ReloadAsync(MapLayerCollection layers)
        {
            FeatureTable newTable = GetTable();
            try
            {
                await newTable.LoadAsync();
            }
            catch
            {
                IsLoaded = false;
                throw;
            }
            //如果上面加载失败，那么不会执行下面的语句
            table = newTable;
            layer = GetLayerForLoading(table);
            ApplyProperties();
            this.ApplyStyle();

            //也许是Esri的BUG，如果不重新插入，那么可能啥都不会显示
            layers.RefreshEsriLayer(this);

            IsLoaded = true;
            this.Notify(nameof(NumberOfFeatures), nameof(GeometryType));
        }

        protected virtual void ApplyProperties()
        {
            Fields = FieldExtension.SyncWithSource(base.Fields, table).ToArray();
            layer.IsVisible = LayerVisible;
            layer.MinScale = Display.MinScale;
            layer.MaxScale = Display.MaxScale;
            layer.Opacity = Display.Opacity;
            layer.DefinitionExpression = DefinitionExpression;
            layer.RenderingMode = (FeatureRenderingMode)Display.RenderingMode;
            PropertyChanged += PropertiesChanged;
            Display.PropertyChanged += PropertiesChanged;
        }

        /// <summary>
        /// 获取用于进行操作的图层
        /// </summary>
        /// <param name="table"></param>
        /// <returns></returns>
        protected virtual FeatureLayer GetLayerForLoading(FeatureTable table)
        {
            return new FeatureLayer(table);
        }

        /// <summary>
        /// 获取要素类
        /// </summary>
        /// <returns></returns>
        protected abstract FeatureTable GetTable();
        private void PropertiesChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(Display.Opacity):
                    layer.Opacity = Display.Opacity;
                    break;

                case nameof(Display.MinScale):
                    layer.MinScale = Display.MinScale;
                    break;

                case nameof(Display.MaxScale):
                    layer.MaxScale = Display.MaxScale;
                    break;

                case nameof(Display.RenderingMode):
                    layer.RenderingMode = (FeatureRenderingMode)Display.RenderingMode;
                    break;

                case nameof(LayerVisible):
                    layer.IsVisible = LayerVisible;
                    break;

                case nameof(DefinitionExpression):
                    layer.DefinitionExpression = DefinitionExpression;
                    break;

                default:
                    break;
            }
        }


        /// <summary>
        /// 要素集合发生改变，即增删改
        /// </summary>
        public event EventHandler<FeaturesChangedEventArgs> FeaturesChanged;

        /// <summary>
        /// 要素操作历史
        /// </summary>
        [JsonIgnore]
        public ObservableCollection<FeaturesChangedEventArgs> Histories { get; private set; } = new ObservableCollection<FeaturesChangedEventArgs>();

        /// <summary>
        /// 新增要素
        /// </summary>
        /// <param name="feature"></param>
        /// <param name="source"></param>
        /// <returns></returns>
        public Task AddFeatureAsync(Feature feature, FeaturesChangedSource source)
        {
            ThrowIfNotEditable(source);
            return AddFeatureAsync(feature, source, feature.FeatureTable != table);
        }

        /// <summary>
        /// 新增要素
        /// </summary>
        /// <param name="feature"></param>
        /// <param name="source"></param>
        /// <param name="rebuildFeature">根据原有要素的图形和属性，创建新的要素，而不是使用旧的要素</param>
        /// <returns></returns>
        public async Task AddFeatureAsync(Feature feature, FeaturesChangedSource source, bool rebuildFeature)
        {
            ThrowIfNotEditable(source);
            Feature newFeature = rebuildFeature ? feature.Clone(this) : feature;
            AddTimeAttributeIfExistField(newFeature, Parameters.CreateTimeFieldName, true);
            await table.AddFeatureAsync(newFeature);
            NotifyFeaturesChanged([feature], null, null, source);
        }

        /// <summary>
        /// 新增一些要素
        /// </summary>
        /// <param name="features"></param>
        /// <param name="source"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public Task<IEnumerable<Feature>> AddFeaturesAsync(IEnumerable<Feature> features, FeaturesChangedSource source)
        {
            ThrowIfNotEditable(source);
            if (features.Select(p => p.FeatureTable).Distinct().Count() != 1)
            {
                throw new ArgumentException("集合为空或要素来自不同的要素类");
            }
            return AddFeaturesAsync(features, source, features.First().FeatureTable != table);
        }

        /// <summary>
        /// 增加一些要素
        /// </summary>
        /// <param name="features"></param>
        /// <param name="source"></param>
        /// <param name="rebuildFeature">是否需要根据属性和图形新建要素。若源要素属于另一个拥有不同字段的要素类，则该值应设为True</param>
        /// <returns></returns>
        public async Task<IEnumerable<Feature>> AddFeaturesAsync(IEnumerable<Feature> features, FeaturesChangedSource source, bool rebuildFeature)
        {
            ThrowIfNotEditable(source);
            Dictionary<string, FieldInfo> key2Field = Fields.ToDictionary(p => p.Name);
            //为了避免出现问题，改成了强制重建，经测试用不了多长时间
            if (true || rebuildFeature)
            {
                List<Feature> newFeatures = new List<Feature>();
                foreach (var feature in features)
                {
                    Feature newFeature = feature.Clone(table, key2Field);
                    newFeatures.Add(newFeature);
                }
                await table.AddFeaturesAsync(newFeatures);
                NotifyFeaturesChanged(newFeatures, null, null, source);
                features = newFeatures;
            }
            else
            {
                foreach (var feature in features)
                {
                    if (!feature.Attributes.TryGetValue(Parameters.CreateTimeFieldName, out object value) || value == null) //一般Add
                    {
                        AddTimeAttributeIfExistField(feature, Parameters.CreateTimeFieldName, true);
                    }
                    else//从其他源导入或从其他图层复制
                    {
                        AddTimeAttributeIfExistField(feature, Parameters.ModifiedTimeFieldName, false);
                    }
                }
                await table.AddFeaturesAsync(features);
                NotifyFeaturesChanged(features, null, null, source);
            }
            return features;
        }

        /// <summary>
        /// 根据属性和图形创建要素
        /// </summary>
        /// <param name="attributes"></param>
        /// <param name="geometry"></param>
        /// <returns></returns>
        public Feature CreateFeature(IEnumerable<KeyValuePair<string, object>> attributes, Geometry geometry)
        {
            return table.CreateFeature(attributes, geometry);
        }

        /// <summary>
        /// 创建空要素
        /// </summary>
        /// <returns></returns>
        public Feature CreateFeature()
        {
            return table.CreateFeature();
        }

        /// <summary>
        /// 删除要素
        /// </summary>
        /// <param name="feature"></param>
        /// <param name="source"></param>
        /// <returns></returns>
        public async Task DeleteFeatureAsync(Feature feature, FeaturesChangedSource source)
        {
            ThrowIfNotEditable(source);
            await table.DeleteFeatureAsync(feature);
            NotifyFeaturesChanged(null, new[] { feature }, null, source);
        }

        /// <summary>
        /// 删除一些要素
        /// </summary>
        /// <param name="features"></param>
        /// <param name="source"></param>
        /// <returns></returns>
        public async Task DeleteFeaturesAsync(IEnumerable<Feature> features, FeaturesChangedSource source)
        {
            ThrowIfNotEditable(source);
            await table.DeleteFeaturesAsync(features);
            NotifyFeaturesChanged(null, features, null, source);
        }

        /// <summary>
        /// 更新要素
        /// </summary>
        /// <param name="feature"></param>
        /// <param name="source"></param>
        /// <returns></returns>
        public async Task UpdateFeatureAsync(UpdatedFeature feature, FeaturesChangedSource source)
        {
            ThrowIfNotEditable(source);
            AddTimeAttributeIfExistField(feature.Feature, Parameters.ModifiedTimeFieldName, false);
            await table.UpdateFeatureAsync(feature.Feature);
            NotifyFeaturesChanged(null, null, new[] { feature }, source);
        }

        /// <summary>
        /// 更新一些要素
        /// </summary>
        /// <param name="features"></param>
        /// <param name="source"></param>
        /// <returns></returns>
        public async Task UpdateFeaturesAsync(IEnumerable<UpdatedFeature> features, FeaturesChangedSource source)
        {
            ThrowIfNotEditable(source);
            features.ForEach(feature => AddTimeAttributeIfExistField(feature.Feature, Parameters.ModifiedTimeFieldName, false));
            await table.UpdateFeaturesAsync(features.Select(p => p.Feature));
            NotifyFeaturesChanged(null, null, features, source);
        }

        /// <summary>
        /// 增加创建时间字段，如果该字段存在
        /// </summary>
        /// <param name="feature"></param>
        private void AddTimeAttributeIfExistField(Feature feature, string fieldName, bool skipIfExisted)
        {
            var esriField = table.GetField(fieldName);
            //若没有该字段，跳过
            if (esriField == null)
            {
                return;
            }
            //若已有值，跳过
            if (skipIfExisted && feature.GetAttributeValue(esriField) != null)
            {
                return;
            }
            FieldInfo field = esriField.ToFieldInfo();
            //若目标字段的类型不兼容，跳过
            if (field.IsCompatibleType(DateTime.Now, out object value))
            {
                feature.SetAttributeValue(esriField, value);
            }
        }

        /// <summary>
        /// 通知要素发生改变
        /// </summary>
        /// <param name="added"></param>
        /// <param name="deleted"></param>
        /// <param name="updated"></param>
        /// <param name="source"></param>
        private void NotifyFeaturesChanged(IEnumerable<Feature> added,
            IEnumerable<Feature> deleted,
            IEnumerable<UpdatedFeature> updated,
            FeaturesChangedSource source)
        {
            this.Notify(nameof(NumberOfFeatures));
            var h = new FeaturesChangedEventArgs(this, added, deleted, updated, source);
            FeaturesChanged?.Invoke(this, h);
            Histories.Add(h);
        }

        /// <summary>
        /// 检测是否允许编辑，若不允许则抛出错误
        /// </summary>
        /// <exception cref="NotSupportedException"></exception>
        private void ThrowIfNotEditable(FeaturesChangedSource? source = null)
        {
            if (source.HasValue && source.Value == FeaturesChangedSource.Initialize)
            {
                return;
            }
            if (!Interaction.CanEdit)
            {
                throw new NotSupportedException("当前图层被禁止编辑");
            }
        }
    }
}