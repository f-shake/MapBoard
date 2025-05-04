using MapBoard.Model;
using System;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using FieldInfo = MapBoard.Model.FieldInfo;

namespace MapBoard.Query
{
    public class SqlWhereClauseItem : INotifyPropertyChanged
    {
        private FieldInfo field;
        private SqlLogicalOperator logicalOperator = SqlLogicalOperator.And;
        private object value;
        private Enum valueOperator;

        public event PropertyChangedEventHandler PropertyChanged;

        public FieldInfo Field
        {
            get => field;
            set
            {
                field = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ValueType));
                ValueOperator = GetDefaultOperatorForField(field);
            }
        }

        public bool IsFirstItem { get; set; }

        public SqlLogicalOperator LogicalOperator
        {
            get => logicalOperator;
            set
            {
                logicalOperator = value;
                OnPropertyChanged();
            }
        }

        public object Value
        {
            get => value;
            set
            {
                if (Equals(this.value, value)) return;

                ValidateValueType(valueOperator?.GetType(), value);
                ValidateOperatorValuePair(valueOperator, value);

                this.value = value;
                OnPropertyChanged();
            }
        }

        public Enum ValueOperator
        {
            get => valueOperator;
            set
            {
                if (Equals(valueOperator, value)) return;

                ValidateOperatorType(value?.GetType());
                ValidateOperatorValuePair(value, this.value);

                valueOperator = value;
                OnPropertyChanged();

                // 当操作符变更时，重新验证值
                if (this.value != null)
                {
                    ValidateValueType(value?.GetType(), this.value);
                }
            }
        }

        public SqlWhereClauseItemValueType ValueType => Field?.Type switch
        {
            FieldInfoType.Integer or FieldInfoType.Float => SqlWhereClauseItemValueType.Number,
            FieldInfoType.Date or FieldInfoType.DateTime => SqlWhereClauseItemValueType.Datetime,
            _ => SqlWhereClauseItemValueType.String,
        };

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private Enum GetDefaultOperatorForField(FieldInfo field)
        {
            return ValueType switch
            {
                SqlWhereClauseItemValueType.Number => NumberSqlOperator.EqualTo,
                SqlWhereClauseItemValueType.Datetime => DateTimeOperator.EqualTo,
                SqlWhereClauseItemValueType.String => StringSqlOperator.EqualTo,
                _ => throw new NotImplementedException(),
            };
        }
        private bool IsNullCheckOperator(Enum op)
        {
            return op is StringSqlOperator sOp && (sOp == StringSqlOperator.IsNull || sOp == StringSqlOperator.IsNotNull)
                || op is DateTimeOperator dOp && (dOp == DateTimeOperator.IsNull || dOp == DateTimeOperator.IsNotNull);
        }

        private bool IsNumeric(object value)
        {
            return value is int
                || value is double
                || value is decimal
                || value is float
                || value is long;
        }

        private void ValidateOperatorType(Type operatorType)
        {
            if (operatorType == null) return;

            var validTypes = new[]
            {
                typeof(NumberSqlOperator),
                typeof(StringSqlOperator),
                typeof(DateTimeOperator)
            };

            if (Array.IndexOf(validTypes, operatorType) < 0)
            {
                throw new ArgumentException($"无效的操作符类型: {operatorType.Name}");
            }
        }

        private void ValidateOperatorValuePair(Enum @operator, object value)
        {
            if (@operator == null || value == null) return;

            if (IsNullCheckOperator(@operator))
            {
                throw new ArgumentException(
                    $"IsNull/IsNotNull操作符不应设置Value，当前操作符: {@operator}");
            }
        }

        private void ValidateValueType(Type operatorType, object value)
        {
            if (operatorType == null || value == null) return;

            if (operatorType == typeof(NumberSqlOperator))
            {
                if (!IsNumeric(value))
                {
                    throw new ArgumentException("数值类型操作符需要int/double等数值类型的Value");
                }
            }
            else if (operatorType == typeof(StringSqlOperator))
            {
                if (!(value is string))
                {
                    throw new ArgumentException("字符串操作符需要string类型的Value");
                }
            }
            else if (operatorType == typeof(DateTimeOperator))
            {
                if (!(value is DateTime))
                {
                    throw new ArgumentException("时间操作符需要DateTime类型的Value");
                }
            }
        }
    }
}