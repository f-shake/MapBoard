using Esri.ArcGISRuntime.Data;
using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Mapping;
using MapBoard.IO;
using MapBoard.Model;
using MapBoard.Util;
using Mapster;
using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection.Emit;
using System.Threading.Tasks;

namespace MapBoard.Mapping.Model
{
    /// <summary>
    /// 包含ArcGIS类型的Shapefile图层
    /// </summary>
    public class MgdbMapLayerInfo : MapLayerInfo
    {
        public MgdbMapLayerInfo() : base()
        {
        }

        public MgdbMapLayerInfo(ILayerInfo layer) : base(layer)
        {
        }

        public MgdbMapLayerInfo(string name) : base(name)
        {
        }

        public MgdbMapLayerInfo(MapLayerInfo template, string newName, bool includeFields)
        {
            template.Adapt(this);
            GenerateSourceName();
            Name = newName;
            IsLoaded = false;

            if (!includeFields)
            {
                Fields = [];
            }
        }

        [JsonIgnore]
        public GeodatabaseFeatureTable Table => table as GeodatabaseFeatureTable;

        public override object Clone()
        {
            var result = this.Adapt<MgdbMapLayerInfo>();
            result.IsLoaded = false;
            result.GenerateSourceName();
            return result;
        }

        public override Task DeleteAsync()
        {
            return (table as GeodatabaseFeatureTable).Geodatabase.DeleteTableAsync(SourceName);
        }

        public override void Dispose()
        {
            table = null;
            base.Dispose();
        }

        /// <summary>
        /// 通过将要素复制到新建的Shapefile中的方式，修改字段
        /// </summary>
        /// <param name="newFields"></param>
        /// <param name="layers"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public async Task ModifyFieldsAsync(FieldInfo[] newFields, Esri.ArcGISRuntime.Mapping.LayerCollection layers)
        {
            //检查图层是否在集合中
            if (!layers.Contains(Layer))
            {
                throw new ArgumentException("本图层不在给定的图层集合中");
            }
            int index = layers.IndexOf(Layer);

            var oldFeatures = (await QueryFeaturesAsync(new QueryParameters())).ToList();
            (table as ShapefileFeatureTable).Close();
            //重命名
            await LayerUtility.DeleteLayerAsync(this, null);

            await LayerUtility.CreateLayerAsync(GeometryType, null, this, false, newFields, Name);

            await LoadAsync();
            layers[index] = Layer;
            try
            {
                await AddFeaturesAsync(oldFeatures, FeaturesChangedSource.Import);
            }
            catch (Exception ex)
            {
            }
        }

        protected override FeatureTable GetTable()
        {
            if (!MobileGeodatabase.Current.GeodatabaseFeatureTables.Any(p => p.TableName == SourceName))
            {
                throw new Exception($"在MGDB中找不到要素类{SourceName}");
            }
            var table = MobileGeodatabase.Current.GetGeodatabaseFeatureTable(SourceName);
            table.DisplayName = Name;
            PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(Name))
                {
                    table.DisplayName = Name;
                }
            };
            return table;
        }
    }
}