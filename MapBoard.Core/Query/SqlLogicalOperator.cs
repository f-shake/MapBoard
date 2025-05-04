using System.ComponentModel;

namespace MapBoard.Query
{
    public enum SqlLogicalOperator
    {
        [Description("且")]
        And,
        [Description("或")]
        Or,
    }
}