using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MapBoard.Query
{
    public static class QuerySqlBuilder
    {
        public static string Build(IList<SqlWhereClauseItem> items)
        {
            if (items == null || !items.Any())
            {
                return string.Empty;
            }

            var sb = new StringBuilder();

            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];

                // 处理逻辑运算符前缀
                if (i > 0)
                {
                    sb.Append(' ').Append(item.LogicalOperator.ToString().ToUpper()).Append(' ');
                }

                // 处理特殊NULL判断
                if (IsNullOperator(item.ValueOperator))
                {
                    sb.Append(BuildNullCheck(item));
                }
                else
                {
                    sb.Append(BuildCondition(item));
                }
            }

            return sb.ToString();
        }

        private static string BuildCondition(SqlWhereClauseItem item)
        {
            return item.ValueOperator switch
            {
                StringSqlOperator => BuildStringCondition(item),
                NumberSqlOperator => BuildNumberCondition(item),
                DateTimeSqlOperator => BuildDateTimeCondition(item),
                _ => throw new NotSupportedException("不支持的运算符类型")
            };
        }

        private static string BuildDateTimeCondition(SqlWhereClauseItem item)
        {
            var op = (DateTimeSqlOperator)item.ValueOperator;
            return $"{item.Field.Name} {GetComparisonOperator(op)} {FormatDateTime(item.Value)}";
        }

        private static string BuildNullCheck(SqlWhereClauseItem item)
        {
            var isNotNull = item.ValueOperator is StringSqlOperator { } sOp && sOp == StringSqlOperator.IsNotNull
                || item.ValueOperator is DateTimeSqlOperator { } dOp && dOp == DateTimeSqlOperator.IsNotNull
                || item.ValueOperator is NumberSqlOperator { } nOp && nOp == NumberSqlOperator.IsNotNull;
            return $"{item.Field.Name} IS {(isNotNull ? "NOT " : "")}NULL";
        }

        private static string BuildNumberCondition(SqlWhereClauseItem item)
        {
            return $"{item.Field.Name} {GetComparisonOperator(item.ValueOperator)} {item.Value}";
        }

        private static string BuildStringCondition(SqlWhereClauseItem item)
        {
            var op = (StringSqlOperator)item.ValueOperator;
            var value = item.Value?.ToString();

            return op switch
            {
                StringSqlOperator.Include => $"{item.Field.Name} LIKE '%{EscapeString(value)}%'",
                StringSqlOperator.NotInclude => $"{item.Field.Name} NOT LIKE '%{EscapeString(value)}%'",
                StringSqlOperator.StartWith => $"{item.Field.Name} LIKE '{EscapeString(value)}%'",
                StringSqlOperator.NotStartWith => $"{item.Field.Name} NOT LIKE '{EscapeString(value)}%'",
                StringSqlOperator.EndWith => $"{item.Field.Name} LIKE '%{EscapeString(value)}'",
                StringSqlOperator.NotEndWith => $"{item.Field.Name} NOT LIKE '%{EscapeString(value)}'",
                _ => $"{item.Field.Name} {GetComparisonOperator(op)} '{EscapeString(value)}'"
            };
        }

        private static string EscapeString(string value)
        {
            return value?.Replace("'", "''") ?? "";
        }

        private static string FormatDateTime(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return $"timestamp '{DateTime.Now:yyyy-MM-dd HH:mm:ss}'";
            }
            if (DateTime.TryParse(value, out DateTime dt))
            {
                return $"timestamp '{dt:yyyy-MM-dd HH:mm:ss}'";
            }
            return value;
        }

        private static string GetComparisonOperator(Enum op)
        {
            return op switch
            {
                NumberSqlOperator.EqualTo => "=",
                NumberSqlOperator.NotEqualTo => "<>",
                NumberSqlOperator.GreaterThan => ">",
                NumberSqlOperator.LessThan => "<",
                NumberSqlOperator.GreaterThanOrEqual => ">=",
                NumberSqlOperator.LessThanOrEqual => "<=",
                StringSqlOperator.EqualTo => "=",
                StringSqlOperator.NotEqualTo => "<>",
                DateTimeSqlOperator.Before => "<",
                DateTimeSqlOperator.After => ">",
                DateTimeSqlOperator.OnOrBefore => "<=",
                DateTimeSqlOperator.OnOrAfter => ">=",
                DateTimeSqlOperator.EqualTo => "=",
                DateTimeSqlOperator.NotEqualTo => "<>",
                _ => throw new ArgumentException("不支持的运算符")
            };
        }

        private static bool IsNullOperator(Enum op)
        {
            return op is StringSqlOperator { } sOp && (sOp == StringSqlOperator.IsNull || sOp == StringSqlOperator.IsNotNull)
                || op is DateTimeSqlOperator { } dOp && (dOp == DateTimeSqlOperator.IsNull || dOp == DateTimeSqlOperator.IsNotNull)
                || op is NumberSqlOperator { } nOp && (nOp == NumberSqlOperator.IsNull || nOp == NumberSqlOperator.IsNotNull);
        }
    }
}