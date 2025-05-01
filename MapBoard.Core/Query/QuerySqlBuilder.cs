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
                    sb.Append($" {item.LogicalOperator} ");
                }

                // 处理特殊NULL判断
                if (IsNullOperator(item.ValueOperator))
                {
                    sb.Append(BuildNullCheck(item));
                    continue;
                }

                // 构建常规条件
                sb.Append(BuildCondition(item));
            }

            return sb.ToString();
        }

        private static bool IsNullOperator(Enum op)
        {
            return op is StringSqlOperator { } sOp && (sOp == StringSqlOperator.IsNull || sOp == StringSqlOperator.IsNotNull)
                || op is DateTimeOperator { } dOp && (dOp == DateTimeOperator.IsNull || dOp == DateTimeOperator.IsNotNull);
        }

        private static string BuildNullCheck(SqlWhereClauseItem item)
        {
            var isNotNull = item.ValueOperator.ToString().EndsWith("NotNull");
            return $"{item.Field.Name} IS {(isNotNull ? "NOT " : "")}NULL";
        }

        private static string BuildCondition(SqlWhereClauseItem item)
        {
            return item.ValueOperator switch
            {
                StringSqlOperator => BuildStringCondition(item),
                NumberSqlOperator => BuildSimpleCondition(item),
                DateTimeOperator => BuildDateTimeCondition(item),
                _ => throw new NotSupportedException("不支持的运算符类型")
            };
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

        private static string BuildDateTimeCondition(SqlWhereClauseItem item)
        {
            var op = (DateTimeOperator)item.ValueOperator;
            return $"{item.Field.Name} {GetDateTimeOperator(op)} {FormatDateTime(item.Value)}";
        }

        private static string BuildSimpleCondition(SqlWhereClauseItem item)
        {
            return $"{item.Field.Name} {GetComparisonOperator(item.ValueOperator)} {FormatValue(item.Value)}";
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
                _ => throw new ArgumentException("不支持的运算符")
            };
        }

        private static string GetDateTimeOperator(DateTimeOperator op)
        {
            return op switch
            {
                DateTimeOperator.Before => "<",
                DateTimeOperator.After => ">",
                DateTimeOperator.OnOrBefore => "<=",
                DateTimeOperator.OnOrAfter => ">=",
                DateTimeOperator.EqualTo => "=",
                DateTimeOperator.NotEqualTo => "<>",
                _ => throw new ArgumentException("不支持的日期运算符")
            };
        }

        private static string EscapeString(string value)
        {
            return value?.Replace("'", "''") ?? "";
        }

        private static string FormatValue(object value)
        {
            return value switch
            {
                null => "NULL",
                DateTime dt => FormatDateTime(dt),
                string s => $"'{EscapeString(s)}'",
                _ => value.ToString()
            };
        }

        private static string FormatDateTime(object value)
        {
            return value is DateTime dt
                ? $"'{dt:yyyy-MM-dd HH:mm:ss}'"
                : "NULL";
        }
    }
}