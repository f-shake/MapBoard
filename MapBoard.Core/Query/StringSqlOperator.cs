using System.ComponentModel;

namespace MapBoard.Query
{
    public enum StringSqlOperator
    {
        [Description("等于")]
        EqualTo,

        [Description("不等于")]
        NotEqualTo,

        [Description("开始于")]
        StartWith,

        [Description("不开始于")]
        NotStartWith,

        [Description("结束于")]
        EndWith,

        [Description("不结束于")]
        NotEndWith,

        [Description("包含")]
        Include,

        [Description("不包含")]
        NotInclude,

        [Description("为空")]
        IsNull,

        [Description("不为空")]
        IsNotNull
    }
}