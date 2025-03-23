using Esri.ArcGISRuntime.Data;
using FzLib.Collection;
using MapBoard.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MapBoard.Mapping.Model
{
    public static class FeatureExtension
    {
        /// <summary>
        /// 获取要素的ID，优先级分别为FID、ID、ObjectID，若均不存在，则获取要素的哈希值
        /// </summary>
        /// <param name="feature"></param>
        /// <returns></returns>
        public static long GetID(this Feature feature)
        {
            if (feature.Attributes.ContainsKey("FID"))
            {
                return (long)feature.GetAttributeValue("FID");
            }
            if (feature.Attributes.ContainsKey("ID"))
            {
                return (long)feature.GetAttributeValue("ID");
            }
            if (feature.Attributes.ContainsKey("ObjectID"))
            {
                return (long)feature.GetAttributeValue("ObjectID");
            }
            return feature.Geometry.ToJson().GetHashCode();
        }

        /// <summary>
        /// 克隆要素
        /// </summary>
        /// <param name="feature"></param>
        /// <param name="layer"></param>
        /// <returns></returns>
        public static Feature Clone(this Feature feature, IMapLayerInfo layer)
        {
            IEnumerable<FieldInfo> fields = layer.Fields;
            return feature.Clone(layer.Layer.FeatureTable, fields.ToDictionary(p => p.Name));
        }

        /// <summary>
        /// 克隆要素
        /// </summary>
        /// <param name="feature"></param>
        /// <param name="layer"></param>
        /// <param name="key2Field"></param>
        /// <returns></returns>
        public static Feature Clone(this Feature feature, FeatureTable table, Dictionary<string, FieldInfo> key2Field)
        {
            var dic = new Dictionary<string, object>();
            foreach (var attr in feature.Attributes)
            {
                string key = attr.Key;
                object value = attr.Value;

                //仅包含存在于目标图层的字段，且需要保证数据类型正确
                if (key2Field.TryGetValue(key, out FieldInfo fi) && fi.IsCompatibleType(ref value) && !fi.IsIdField())
                {
                    dic.Add(key, value);
                }
            }

            var newFeature = table.CreateFeature(dic, feature.Geometry);
            return newFeature;
        }
    }
}