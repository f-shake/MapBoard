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
        private string value;
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
                ValueOperator = GetDefaultOperatorForField();
                Value = GetDefaultValueForField();
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

        public string Value
        {
            get => value;
            set
            {
                if (Equals(this.value, value)) return;

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

                valueOperator = value;
                OnPropertyChanged();
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

        private Enum GetDefaultOperatorForField()
        {
            return ValueType switch
            {
                SqlWhereClauseItemValueType.Number => (NumberSqlOperator)0,
                SqlWhereClauseItemValueType.Datetime => (DateTimeSqlOperator)0,
                SqlWhereClauseItemValueType.String => (StringSqlOperator)0,
                _ => throw new NotImplementedException(),
            };
        }

        private string   GetDefaultValueForField()
        {
            return ValueType switch
            {
                SqlWhereClauseItemValueType.Number => "0",
                SqlWhereClauseItemValueType.Datetime => DateTime.Now.ToString(),
                SqlWhereClauseItemValueType.String => "",
                _ => throw new NotImplementedException(),
            };
        }

        private bool IsNullCheckOperator(Enum op)
        {
            return op is StringSqlOperator sOp && (sOp == StringSqlOperator.IsNull || sOp == StringSqlOperator.IsNotNull)
                || op is DateTimeSqlOperator dOp && (dOp == DateTimeSqlOperator.IsNull || dOp == DateTimeSqlOperator.IsNotNull);
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
                typeof(DateTimeSqlOperator)
            };

            if (Array.IndexOf(validTypes, operatorType) < 0)
            {
                throw new ArgumentException($"无效的操作符类型: {operatorType.Name}");
            }
        }

    }
}