using MapBoard.Model;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using static MapBoard.Mapping.Model.FeaturesChangedSource;
using MapBoard.Mapping.Model;
using Esri.ArcGISRuntime.Data;
using System.Diagnostics;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace MapBoard.Util
{
    public static class AttributeUtility
    {
        /// <summary>
        /// 将一个字段的属性赋值给另一个字段
        /// </summary>
        /// <param name="layer">操作的图层</param>
        /// <param name="fieldSource">作为模板的字段</param>
        /// <param name="fieldTarget">需要更改的字段</param>
        /// <param name="dateFormat">日期格式</param>
        /// <returns></returns>
        public static async Task<ItemsOperationErrorCollection> CopyAttributesAsync(IMapLayerInfo layer, IEnumerable<Feature> features, FieldInfo fieldSource, FieldInfo fieldTarget)
        {
            if (fieldTarget.Name.Equals(Parameters.CreateTimeFieldName))
            {
                throw new ArgumentException("不可为“创建时间”字段赋值");
            }

            Debug.Assert(features.All(p => p.FeatureTable.Layer == layer.Layer));
            ItemsOperationErrorCollection errors = new ItemsOperationErrorCollection();
            List<UpdatedFeature> newFeatures = new List<UpdatedFeature>();
            await Task.Run(() =>
            {
                foreach (var feature in features)
                {
                    newFeatures.Add(new UpdatedFeature(feature, feature.Geometry, new Dictionary<string, object>(feature.Attributes)));
                    object oldValue = feature.GetAttributeValue(fieldTarget.Name);
                    object value = feature.GetAttributeValue(fieldSource.Name);
                    if (value is DateTimeOffset dto)
                    {
                        value = dto.UtcDateTime;
                    }
                    if (fieldTarget.Type == fieldSource.Type)//同类型
                    {
                        feature.SetAttributeValue(fieldTarget.Name, feature.GetAttributeValue(fieldSource.Name));
                    }
                    else//异类型
                    {
                        object result = null;
                        try
                        {
                            if (!FieldInfo.IsCompatibleType(fieldTarget.Type, value, out result))
                            {
                                throw new InvalidCastException($"无法将类型{value.GetType().Name}或值{value}转换为对应的类型{fieldTarget.Type}");
                            }
                            feature.SetAttributeValue(fieldTarget.Name, result);
                        }
                        catch (Exception ex)
                        {
                            errors.Add(new ItemsOperationError($"{feature.GetID()}: {oldValue}=>{value}", ex));
                        }
                    }
                }
            });
            await layer.UpdateFeaturesAsync(newFeatures, FeatureOperation);
            return errors;
        }

        /// <summary>
        /// 为图层所有要素批量设置属性
        /// </summary>
        /// <param name="layer"></param>
        /// <param name="field"></param>
        /// <param name="text"></param>
        /// <param name="includeField"></param>
        /// <param name="dateFormat"></param>
        /// <returns></returns>
        public static async Task<ItemsOperationErrorCollection> SetAttributesAsync(IMapLayerInfo layer, FieldInfo field, string text, bool includeField)
        {
            var features = await layer.GetAllFeaturesAsync();
            return await SetAttributesAsync(layer, features, field, text, includeField);
        }

        /// <summary>
        ///为要素批量设置属性
        /// </summary>
        /// <param name="layer">操作的图层</param>
        /// <param name="features">所需要修改的要素</param>
        /// <param name="field">需要更改的字段</param>
        /// <param name="text">目标字符串</param>
        /// <param name="includeField">是否包含其它字段，字段包含在[]中</param>
        /// <returns></returns>
        public static async Task<ItemsOperationErrorCollection> SetAttributesAsync(IMapLayerInfo layer, IEnumerable<Feature> features, FieldInfo field, string text, bool includeField)
        {
            if (field.Name.Equals(Parameters.CreateTimeFieldName))
            {
                throw new ArgumentException("不可为“创建时间”字段赋值");
            }
            Debug.Assert(features.All(p => p.FeatureTable.Layer == layer.Layer));

            ItemsOperationErrorCollection errors = new ItemsOperationErrorCollection();
            List<UpdatedFeature> newFeatures = new List<UpdatedFeature>();
            await Task.Run(() =>
            {
                //查询需要替换的字段
                List<FieldInfo> sourceFields = includeField ?
                layer.Fields.Where(p => text.Contains($"[{p.Name}]")).ToList() : new List<FieldInfo>();

                foreach (var feature in features)
                {
                    newFeatures.Add(new UpdatedFeature(feature, feature.Geometry, new Dictionary<string, object>(feature.Attributes)));
                    object oldValue = feature.GetAttributeValue(field.Name);
                    string textValue = text;
                    //进行字段替换
                    if (includeField && sourceFields.Count > 0)
                    {
                        foreach (var f in sourceFields)
                        {
                            textValue = textValue.Replace($"[{f.Name}]", feature.GetAttributeValue(f.Name)?.ToString() ?? "");
                        }
                    }

                    object result = null;
                    try
                    {
                        if (!FieldInfo.IsCompatibleType(field.Type, textValue, out result))
                        {
                            throw new InvalidCastException($"无法将类型{textValue.GetType().Name}或值{textValue}转换为对应的类型{field.Type}");
                        }
                        feature.SetAttributeValue(field.Name, result);
                    }
                    catch (Exception ex)
                    {
                        errors.Add(new ItemsOperationError($"{feature.GetID()}: {oldValue}=>{result}", ex));
                    }

                }
            });
            await layer.UpdateFeaturesAsync(newFeatures, FeatureOperation);
            return errors;
        }
    }
}