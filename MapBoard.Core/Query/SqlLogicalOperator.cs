using System.ComponentModel;

namespace MapBoard.Query
{
    public enum SqlLogicalOperator
    {
        [Description("并且")]
        And,
        [Description("或者")]
        Or,
    }
}