#define ERROR

using FzLib;
using PropertyChanged;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;

namespace MapBoard.Model
{
    /// <summary>
    /// 单个要素属性
    /// </summary>
    public class FeatureAttribute : FieldInfo
    {
        private object attrValue;

        private DateTime? dateTimeValue;

        private DateOnly? dateValue;

        private double? floatValue;

        private long? intValue;

        private string textValue;
        public FeatureAttribute()
        {
        }

        /// <summary>
        /// 日期类型
        /// </summary>
        public static string DateFormat { get; set; } = "yyyy-MM-dd";

        /// <summary>
        /// 日期时间类型
        /// </summary>
        public static string DateTimeFormat { get; set; } = "yyyy-MM-dd HH:mm:ss";


        /// <summary>
        /// 日期时间型值
        /// </summary>
        [AlsoNotifyFor(nameof(Value))]
        public DateTime? DateTimeValue
        {
            get => dateTimeValue;
            set
            {
                if (Type != FieldInfoType.DateTime)
                {
                    throw new NotSupportedException();
                }
                attrValue = value;
                dateTimeValue = value;
            }
        }

        /// <summary>
        /// 日期型值
        /// </summary>
        [AlsoNotifyFor(nameof(Value))]
        public DateOnly? DateValue
        {
            get => dateValue;
            set
            {
                if (Type != FieldInfoType.Date)
                {
                    throw new NotSupportedException();
                }
                attrValue = value;
                dateValue = value;
            }
        }

        /// <summary>
        /// 浮点型值
        /// </summary>
        [AlsoNotifyFor(nameof(Value))]
        public double? FloatValue
        {
            get => floatValue;
            set
            {
                if (Type != FieldInfoType.Float)
                {
                    throw new NotSupportedException();
                }
                attrValue = value;
                floatValue = value;
            }
        }

        /// <summary>
        /// 整形值
        /// </summary>
        [AlsoNotifyFor(nameof(Value))]
        public long? IntValue
        {
            get => intValue;
            set
            {
                if (Type != FieldInfoType.Integer)
                {
                    throw new NotSupportedException();
                }
                attrValue = value;
                intValue = value;
                this.Notify(nameof(IntValue), nameof(Value));
            }
        }

        /// <summary>
        /// 文本型值
        /// </summary>
        [AlsoNotifyFor(nameof(Value))]
        public string TextValue
        {
            get => textValue;
            set
            {
                if (Type != FieldInfoType.Text)
                {
                    throw new NotSupportedException();
                }
                attrValue = value;
                textValue = value;
            }
        }

        /// <summary>
        /// 属性值
        /// </summary>
        public object Value
        {
            get => attrValue;
            set
            {
                if (IsCompatibleType(value, out object newValue))
                {
                    attrValue = newValue;
                    switch (Type)
                    {
                        case FieldInfoType.Integer: intValue = (long)attrValue; break;
                        case FieldInfoType.Float: floatValue = (double)attrValue; break;
                        case FieldInfoType.Date: dateValue = (DateOnly)attrValue; break;
                        case FieldInfoType.DateTime: dateTimeValue = (DateTime)attrValue; break;
                        case FieldInfoType.Text: textValue = (string)attrValue; break;
                        default: throw new InvalidEnumArgumentException();
                    };
                }
                else
                {
                    Debug.Assert(false);
                }
                this.Notify(nameof(Value));
            }
        }

        /// <summary>
        /// 根据ArcGIS字段和属性值，创建<see cref="FeatureAttribute"/>
        /// </summary>
        /// <param name="field"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static FeatureAttribute FromFieldAndValue(FieldInfo field, object value = null)
        {
            FeatureAttribute attribute = new FeatureAttribute()
            {
                Name = field.Name,
                DisplayName = field.DisplayName,
                Type = field.Type,
            };
            try
            {
                attribute.Value = value;
            }
            catch (ArgumentException ex)
            {
                Debug.WriteLine(ex);
            }
            return attribute;
        }

        /// <summary>
        /// 获取属性值的字符串表达
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            if (Value == null)
            {
                return "";
            }
            if (Type == FieldInfoType.Date)
            {
                return DateValue.Value.ToShortDateString();
            }
            return Value.ToString();
        }
    }
}