using FzLib;
using MapBoard.Model;
using MapBoard.Query;
using ModernWpf.FzExtension.CommonDialog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace MapBoard.UI.Dialog
{
    public partial class QuerySqlBuilderDialog : CommonDialog
    {
        public QuerySqlBuilderDialog(ILayerInfo layer)
        {
            Fields = layer.Fields;
            InitializeComponent();
        }

        public IList<FieldInfo> Fields { get; }
        public ObservableCollection<SqlWhereClauseItem> Items { get; } = new ObservableCollection<SqlWhereClauseItem>();

        private void AddCondition_Click(object sender, RoutedEventArgs e)
        {
            Items.Add(new SqlWhereClauseItem
            {
                // 默认设置为第一个字段
                FieldName = Fields.FirstOrDefault()?.Name,
                // 默认操作符根据字段类型决定
                Operator = GetDefaultOperatorForField(Fields.FirstOrDefault())
            });
        }

        private Enum GetDefaultOperatorForField(FieldInfo field)
        {
            if (field == null) return StringSqlOperator.EqualTo;

            switch (field.Type)
            {
                case FieldType.Number:
                    return NumberSqlOperator.EqualTo;
                case FieldType.DateTime:
                    return DateTimeOperator.EqualTo;
                default:
                    return StringSqlOperator.EqualTo;
            }
        }
    }
}