using Esri.ArcGISRuntime.Data;
using FzLib;
using MapBoard.Model;
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MapBoard.Mapping.Model
{
    public static class FieldRelationship
    {
        private static Lazy<IReadOnlyDictionary<FieldType, FieldItem>> esriFieldDic = new Lazy<IReadOnlyDictionary<FieldType, FieldItem>>(() =>
        {
            Dictionary<FieldType, FieldItem> dic = new Dictionary<FieldType, FieldItem>();
            foreach (var item in items)
            {
                dic.Add(item.EsriType, item);
                if (item.CompatibleEsriTypes is { Length: > 0 })
                {
                    item.CompatibleEsriTypes.ForEach(p => dic.Add(p, item));
                }
            }
            return dic.AsReadOnly();
        });

        private static Lazy<IReadOnlyDictionary<FieldInfoType, FieldItem>> fieldInfoDic = new Lazy<IReadOnlyDictionary<FieldInfoType, FieldItem>>(() =>
        {
            return items.ToFrozenDictionary(p => p.MapBoardType);
        });

        private static List<FieldItem> items =
        [
            new FieldItem(FieldInfoType.Integer,FieldType.Int64,FieldType.Int32,FieldType.Int16,FieldType.OID),
            new FieldItem(FieldInfoType.Float,FieldType.Float64,FieldType.Float32),
            new FieldItem(FieldInfoType.Text,FieldType.Text),
            new FieldItem(FieldInfoType.Date,FieldType.DateOnly),
            new FieldItem(FieldInfoType.DateTime,FieldType.Date),
        ];

        public static FieldItem GetRelationship(this FieldInfoType type)
        {
            return fieldInfoDic.Value.TryGetValue(type, out var value) ? value : throw new ArgumentException($"找不到{type}对应的{nameof(FieldItem)}", nameof(type));
        }

        public static FieldItem GetRelationship(this FieldType type)
        {
            return esriFieldDic.Value.TryGetValue(type, out var value) ? value : throw new ArgumentException($"找不到{type}对应的{nameof(FieldItem)}", nameof(type));
        }

        /// <summary>
        /// 转为ArcGIS的字段类型
        /// </summary>
        /// <param name="field"></param>
        /// <returns></returns>
        public static Field ToEsriField(this FieldInfo field)
        {
            return new Field(field.Type.GetRelationship().EsriType, field.Name, field.DisplayName, 0);
        }

        public static FieldDescription ToFieldDescription(this FieldInfo field)
        {
            return new FieldDescription(field.Name, field.Type.GetRelationship().EsriType) { Alias = field.DisplayName };
        }
        /// <summary>
        /// 从ArcGIS的字段类型转为应用字段类型
        /// </summary>
        /// <param name="field"></param>
        /// <returns></returns>
        /// <exception cref="NotSupportedException"></exception>
        public static FieldInfo ToFieldInfo(this Field field)
        {
            var type = field.FieldType.GetRelationship().MapBoardType;
            string name = field.Name;

            name = FieldInfo.GetValidFieldName(name);

            string alias = string.IsNullOrEmpty(field.Alias) ? field.Name : field.Alias;
            return new FieldInfo(name, alias, type);
        }

        /// <summary>
        /// 从ArcGISRuntime到FiledInfo，会排除ID字段
        /// </summary>
        /// <param name="fields"></param>
        /// <returns>从原表字段名到新字段的映射</returns>
        public static IReadOnlyDictionary<string, FieldInfo> ToFieldInfos(this IEnumerable<Field> fields)
        {
            Dictionary<string, FieldInfo> result = new Dictionary<string, FieldInfo>();
            foreach (var field in fields.Where(field => !field.IsIdField()))
            {
                result.Add(field.Name, field.ToFieldInfo());
            }

            return result.AsReadOnly();
        }

        public struct FieldItem
        {
            public FieldItem(FieldInfoType fieldInfo, FieldType esriField, params FieldType[] compatibleEsriFields)
            {
                MapBoardType = fieldInfo;
                EsriType = esriField;
                CompatibleEsriTypes = compatibleEsriFields;
            }

            public FieldType[] CompatibleEsriTypes { get; }
            public FieldType EsriType { get; }
            public FieldInfoType MapBoardType { get; }
        }
    }
}
