using FzLib;
using FzLib.WPF.Converters;
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
            // 初始添加一个条件
            Items.Add(new SqlWhereClauseItem
            {
                IsFirstItem = true,
                Field = Fields.FirstOrDefault()
            });
        }

        public IList<FieldInfo> Fields { get; }

        public ObservableCollection<SqlWhereClauseItem> Items { get; } = new ObservableCollection<SqlWhereClauseItem>();

        private void AddCondition_Click(object sender, RoutedEventArgs e)
        {
            Items.Add(new SqlWhereClauseItem
            {
                LogicalOperator = SqlLogicalOperator.And,
                Field = Fields.FirstOrDefault()
            });
        }

  
    }
}