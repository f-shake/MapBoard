using System.ComponentModel;

namespace MapBoard.Query
{
    public enum NumberSqlOperator
    {
        [Description("等于")]
        EqualTo,

        [Description("不等于")]
        NotEqualTo,

        [Description("大于")]
        GreaterThan,

        [Description("小于")]
        LessThan,

        [Description("大于等于")]
        GreaterThanOrEqual,

        [Description("小于等于")]
        LessThanOrEqual,

        [Description("为空")]
        IsNull,

        [Description("不为空")]
        IsNotNull
    }
}