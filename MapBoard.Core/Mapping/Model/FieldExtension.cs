using Esri.ArcGISRuntime.Data;
using Esri.ArcGISRuntime.Mapping;
using MapBoard.Model;
using MapBoard.Util;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MapBoard.Mapping.Model
{
    /// <summary>
    /// 字段扩展方法类
    /// </summary>
    public static class FieldExtension
    {
        /// <summary>
        /// 创建时间字段
        /// </summary>
        public static FieldInfo CreateTimeField => new FieldInfo(Parameters.CreateTimeFieldName, "创建时间", FieldInfoType.DateTime);

        /// <summary>
        /// 修改时间字段
        /// </summary>
        public static FieldInfo ModifiedTimeField => new FieldInfo(Parameters.ModifiedTimeFieldName, "修改时间", FieldInfoType.DateTime);

        /// <summary>
        /// 是否为能够作为符号系统Key的字段
        /// </summary>
        /// <param name="field"></param>
        /// <returns></returns>
        public static bool CanBeRendererKey(this FieldInfo field)
        {
            return field.Type is FieldInfoType.Text or FieldInfoType.Integer or FieldInfoType.Float;
        }

        public static void CheckInvalidFieldNames(this IEnumerable<Field> fields)
        {
            var invalidFields = fields.Where(p => FieldInfo.IsValidFieldName(p.Name) == false).ToList();
            if (invalidFields.Count > 0)
            {
                throw new ArgumentException($"存在不合法的字段名：{string.Join(",", invalidFields.Select(p => p.Name))}，字段名必须以字母开头，且只能包含字母、数字和下划线。", nameof(fields));
            }
        }

        public static void CheckInvalidFieldNames(this IEnumerable<FieldInfo> fields)
        {
            var invalidFields = fields.Where(p => FieldInfo.IsValidFieldName(p.Name) == false).ToList();
            if (invalidFields.Count > 0)
            {
                throw new ArgumentException($"存在不合法的字段名：{string.Join(",", invalidFields.Select(p => p.Name))}，字段名必须以字母开头，且只能包含字母、数字和下划线。", nameof(fields));
            }
        }
        /// <summary>
        /// 是否存在某个类型、某个名称的字段
        /// </summary>
        /// <param name="layer"></param>
        /// <param name="name"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public static bool HasField(this ILayerInfo layer, string name, FieldInfoType type)
        {
            return layer.Fields.Any(p => p.Name == name && p.Type == type);
        }

        /// <summary>
        /// 是否为ID字段
        /// </summary>
        /// <param name="field"></param>
        /// <returns></returns>
        public static bool IsIdField(this Field field)
        {
            return IsIdField(field.Name);
        }

        /// <summary>
        /// 是否为ID字段
        /// </summary>
        /// <param name="field"></param>
        /// <returns></returns>
        public static bool IsIdField(this FieldInfo field)
        {
            return IsIdField(field.Name);
        }

        /// <summary>
        /// 是否为ID字段名
        /// </summary>
        /// <param name="field"></param>
        /// <returns></returns>
        public static bool IsIdField(string field)
        {
            return field.Equals("fid", StringComparison.OrdinalIgnoreCase) ||
                   field.Equals("id", StringComparison.OrdinalIgnoreCase) ||
                   field.Equals("objectid", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 将字段信息与数据源进行同步
        /// </summary>
        /// <param name="fields"></param>
        /// <param name="table"></param>
        /// <returns></returns>
        public static IEnumerable<FieldInfo> SyncWithSource(FieldInfo[] fields, FeatureTable table)
        {
            foreach (var esriField in table.Fields.Where(p => !p.IsIdField()))
            {
                var oldField = fields.FirstOrDefault(p => p.Name == esriField.Name);
                var field = esriField.ToFieldInfo();
                if (oldField != null)
                {
                    field.DisplayName = oldField.DisplayName;
                }
                yield return field;
            }
        }
    }
}