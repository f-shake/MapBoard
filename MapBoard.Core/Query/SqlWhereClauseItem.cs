using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MapBoard.Query
{
    public class SqlWhereClauseItem : INotifyPropertyChanged
    {
        private string _fieldName;
        private SqlLogicalOperator _logicalOperator = SqlLogicalOperator.AND;
        private Enum _operator;
        private object _value;

        public SqlWhereClauseItem() { }

        public SqlWhereClauseItem(SqlLogicalOperator logicalOperator, string fieldName, Enum @operator, object value)
        {
            LogicalOperator = logicalOperator;
            FieldName = fieldName;
            Operator = @operator;
            Value = value;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public string FieldName
        {
            get => _fieldName;
            set
            {
                if (_fieldName == value) return;
                _fieldName = value;
                OnPropertyChanged();
            }
        }

        public SqlLogicalOperator LogicalOperator
        {
            get => _logicalOperator;
            set
            {
                if (_logicalOperator == value) return;
                _logicalOperator = value;
                OnPropertyChanged();
            }
        }
        public Enum Operator
        {
            get => _operator;
            set
            {
                if (Equals(_operator, value)) return;

                ValidateOperatorType(value?.GetType());
                ValidateOperatorValuePair(value, _value);

                _operator = value;
                OnPropertyChanged();

                // 当操作符变更时，重新验证值
                if (_value != null)
                {
                    ValidateValueType(value?.GetType(), _value);
                }
            }
        }

        public object Value
        {
            get => _value;
            set
            {
                if (Equals(_value, value)) return;

                ValidateValueType(_operator?.GetType(), value);
                ValidateOperatorValuePair(_operator, value);

                _value = value;
                OnPropertyChanged();
            }
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
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