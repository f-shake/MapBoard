using FzLib;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;

namespace MapBoard.Model
{
    /// <summary>
    /// 字段信息
    /// </summary>
    [DebuggerDisplay("Name={Name} Disp={DisplayName} Type={Type}")]
    public class FieldInfo : INotifyPropertyChanged, ICloneable
    {
        public const int MaxFieldNameLength = 200;

        public FieldInfo(string name, string displayName, FieldInfoType type)
        {
            Name = name;
            DisplayName = displayName;
            Type = type;
        }

        public FieldInfo()
        {
        }

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// 显示名（别名）
        /// </summary>
        public string DisplayName { get; set; } = "";

        /// <summary>
        /// 字段名
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 字段类型
        /// </summary>
        public FieldInfoType Type { get; set; }

        public static bool IsCompatibleType(FieldInfoType type, object propertyValue, out object value)
        {
            value = null;
            if (propertyValue == null)
            {
                return true;
            }
            switch (type)
            {
                case FieldInfoType.Integer:
                    {
                        if (propertyValue is long)
                        {
                            value = propertyValue;
                            return true;
                        }
                        if (propertyValue is double d && d <= long.MaxValue)
                        {
                            value = Convert.ToInt64(d);
                            return true;
                        }
                        if (CanConvertToLong(propertyValue))
                        {
                            value = Convert.ToInt64(propertyValue);
                            return true;
                        }
                        if (propertyValue is string str && long.TryParse(str, out long l))
                        {
                            value = l;
                            return true;
                        }
                        return false;
                    }
                case FieldInfoType.Float:
                    {
                        if (propertyValue is double)
                        {
                            value = propertyValue;
                            return true;
                        }
                        if (CanConvertToDouble(propertyValue))
                        {
                            value = Convert.ToDouble(propertyValue);
                            return true;
                        }
                        if (propertyValue is string str && double.TryParse(str, out double d))
                        {
                            value = d;
                            return true;
                        }
                        return propertyValue is double;
                    }
                case FieldInfoType.Date:
                    {
                        if (propertyValue is DateOnly date)
                        {
                            value = date;
                            return true;
                        }
                        if (propertyValue is DateTime dt)
                        {
                            value = DateOnly.FromDateTime(dt);
                            return true;
                        }
                        if (propertyValue is DateTimeOffset dto)
                        {
                            value = DateOnly.FromDateTime(dto.DateTime);
                            return true;
                        }
                        if (propertyValue is string str && DateTime.TryParse(str, out DateTime dt2))
                        {
                            value = DateOnly.FromDateTime(dt2);
                            return true;
                        }
                        return false;
                    }
                case FieldInfoType.Text:
                    {
                        if (propertyValue is string str)
                        {
                            value = str;
                        }
                        else
                        {
                            value = propertyValue.ToString();
                        }
                        return true;
                    }
                case FieldInfoType.DateTime:
                    {
                        if (propertyValue is DateOnly date)
                        {
                            value = date.ToDateTime(TimeOnly.MinValue);
                            return true;
                        }
                        if (propertyValue is DateTime)
                        {
                            value = propertyValue;
                            return true;
                        }
                        if (propertyValue is DateTimeOffset dto)
                        {
                            value = dto.DateTime;
                            return true;
                        }
                        if (propertyValue is string str && DateTime.TryParse(str, out DateTime dt2))
                        {
                            value = dt2;
                            return true;
                        }
                        return false;
                    }
                default:
                    throw new InvalidEnumArgumentException();
            }
        }

        public static bool IsValidFieldName(string name)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            if (name.Length > MaxFieldNameLength)
            {
                return false;
            }
            if ((name[0] is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or '_')
                && name.Skip(1).All(p => p is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '_'))
            {
                return true;
            }
            return false;
        }

        public object Clone()
        {
            return MemberwiseClone();
        }

        /// <summary>
        /// 判断属性值是否与字段类型对应
        /// </summary>
        /// <param name="propertyValue"></param>
        /// <returns></returns>
        /// <exception cref="InvalidEnumArgumentException"></exception>
        public bool IsCompatibleType(object propertyValue, out object value)
        {
            return IsCompatibleType(Type, propertyValue, out value);
        }

        /// <summary>
        /// 判断属性值是否与字段类型对应
        /// </summary>
        /// <param name="propertyValue"></param>
        /// <returns></returns>
        /// <exception cref="InvalidEnumArgumentException"></exception>
        public bool IsCompatibleType(ref object propertyValue)
        {
            if (IsCompatibleType(Type, propertyValue, out object newValue))
            {
                propertyValue = newValue;
                return true;
            }
            return false;
        }
        private static bool CanConvertToDouble(object o)
        {
            return System.Type.GetTypeCode(o.GetType()) switch
            {
                TypeCode.Byte or TypeCode.SByte or TypeCode.UInt16
                or TypeCode.UInt32 or TypeCode.UInt64 or TypeCode.Int16
                or TypeCode.Int32 or TypeCode.Int64 or TypeCode.Decimal
                or TypeCode.Double or TypeCode.Single => true,
                _ => false,
            };
        }

        private static bool CanConvertToLong(object o)
        {
            return System.Type.GetTypeCode(o.GetType()) switch
            {
                TypeCode.Byte or TypeCode.SByte or TypeCode.UInt16
                or TypeCode.UInt32 or TypeCode.Int16
                or TypeCode.Int32 or TypeCode.Int64 => true,
                _ => false,
            };
        }
    }
}