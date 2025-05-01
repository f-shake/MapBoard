using System.ComponentModel;

namespace MapBoard.Query
{
    public enum DateTimeOperator
    {
        [Description("早于")]
        Before,

        [Description("晚于")]
        After,

        [Description("早于或等于")]
        OnOrBefore,

        [Description("晚于或等于")]
        OnOrAfter,

        [Description("等于")]
        EqualTo,

        [Description("不等于")]
        NotEqualTo,

        [Description("为空")]
        IsNull,

        [Description("不为空")]
        IsNotNull
    }
}