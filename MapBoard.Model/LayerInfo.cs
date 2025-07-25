using FzLib;

using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;

namespace MapBoard.Model
{
    /// <summary>
    /// 图层信息
    /// </summary>
    [DebuggerDisplay("{Name}")]
    public class LayerInfo : ICloneable, ILayerInfo
    {
        private FieldInfo[] fields;

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// 筛选表达式。设置后，根据SQL语句筛选需要显示的图形
        /// </summary>
        public string DefinitionExpression { get; set; } = "";

        /// <summary>
        /// 显示设置
        /// </summary>
        public LayerDisplay Display { get; set; } = new LayerDisplay();

        /// <summary>
        /// 字段
        /// </summary>
        public virtual FieldInfo[] Fields
        {
            get
            {
                fields ??= [];
                return fields;
            }
            set => fields = value;
        }

        /// <summary>
        /// 所属分组名
        /// </summary>
        public string Group { get; set; }

        /// <summary>
        /// 交互设置
        /// </summary>
        public LayerInteraction Interaction { get; set; } = new LayerInteraction();

        /// <summary>
        /// 标签信息
        /// </summary>
        public LabelInfo[] Labels { get; set; }

        /// <summary>
        /// 图层可见性
        /// </summary>
        public virtual bool LayerVisible { get; set; } = true;

        /// <summary>
        /// 图层名
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 渲染器/符号系统设置
        /// </summary>
        public UniqueValueRendererInfo Renderer { get; set; } = new UniqueValueRendererInfo();

        public string SourceName { get; set; } = $"T{Guid.NewGuid():N}";

        protected void GenerateSourceName()
        {
            SourceName = $"T{Guid.NewGuid():N}";
        }

        /// <summary>
        /// 建立副本
        /// </summary>
        /// <returns></returns>
        public virtual object Clone()
        {
            LayerInfo layer = MemberwiseClone() as LayerInfo;
            foreach (var key in Renderer.Symbols.Keys.ToList())
            {
                layer.Renderer.Symbols[key] = Renderer.Symbols[key].Clone() as SymbolInfo;
            }
            layer.fields = fields?.Select(p => p.Clone() as FieldInfo).ToArray();
            layer.Labels = Labels?.Select(p => p.Clone() as LabelInfo).ToArray();
            return layer;
        }
    }
}