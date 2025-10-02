using Esri.ArcGISRuntime.Geometry;
using FzLib;
using MapBoard.Model;
using MapBoard.Mapping;
using MapBoard.Util;
using ModernWpf.FzExtension.CommonDialog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using static MapBoard.Util.CoordinateTransformation;
using MapBoard.Mapping.Model;
using System.Data;
using Esri.ArcGISRuntime.Data;
using System.Globalization;
using MapBoard.UI.Model;

namespace MapBoard.UI.Dialog
{
    /// <summary>
    /// 导入图层对话框
    /// </summary>
    public partial class ImportTableDialog : AddLayerDialogBase
    {
        public MapLayerInfo editLayer = null;

        public ImportTableDialog(MapLayerCollection layers, DataTable table, string defaultName) : base(layers)
        {
            LayerName = defaultName;

            Table = table;
            LoadFields();
            InitializeComponent();
        }

        /// <summary>
        /// 日期格式
        /// </summary>
        public string DateFormat { get; set; } = Parameters.DateFormat;

        /// <summary>
        /// 
        /// </summary>
        public ObservableCollection<ImportTableFieldInfo> Fields { get; } = new ObservableCollection<ImportTableFieldInfo>();

        /// <summary>
        /// 纬度列序号
        /// </summary>
        public int LatitudeIndex { get; set; } = -1;

        /// <summary>
        /// 经度列序号
        /// </summary>
        public int LongitudeIndex { get; set; } = -1;

        /// <summary>
        /// 导入数据表
        /// </summary>
        public DataTable Table { get; }

        /// <summary>
        /// 时间格式
        /// </summary>
        public string TimeFormat { get; set; } = Parameters.TimeFormat;

        /// <summary>
        /// 检查字段是否合法
        /// </summary>
        /// <returns></returns>
        private bool CheckFields()
        {
            HashSet<string> names = new HashSet<string>();
            HashSet<string> displayNames = new HashSet<string>();
            foreach (var field in Fields.Where(p => p.Import))
            {
                if (field.Field.DisplayName.Length * field.Field.Name.Length == 0
                    || !displayNames.Add(field.Field.DisplayName)
                    || !names.Add(field.Field.Name))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// 对话框加载，马上检查字段
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CommonDialog_Loaded(object sender, RoutedEventArgs e)
        {
            CheckFields();
        }

        /// <summary>
        /// 单击确定按钮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private async void CommonDialog_PrimaryButtonClick(ModernWpf.Controls.ContentDialog sender, ModernWpf.Controls.ContentDialogButtonClickEventArgs args)
        {
            args.Cancel = true;
            if (LongitudeIndex < 0 || LatitudeIndex < 0)
            {
                Message = "请先选择经度和纬度字段";
                return;
            }
            if (!CheckFields())
            {
                Message = "请检查是否有异常字段名";
                return;
            }
            IsPrimaryButtonEnabled = false;
            IsEnabled = false;
            CloseButtonText = null;

            Hide();
            var fields = Fields.Where(p => p.Import)
                .Select(p => p.Field).ToList();
            try
            {
                var layer = await LayerUtility.CreateLayerAsync(GeometryType.Point, Layers, name: LayerName, fields: fields);
                int failedRowCount = 0;
                List<Feature> features = new List<Feature>();
                await Task.Run(async () =>
                {
                    foreach (DataRow row in Table.Rows)
                    {
                        double x = 0, y = 0;
                        try
                        {
                            x = double.Parse(row[LongitudeIndex - 1].ToString());
                            y = double.Parse(row[LatitudeIndex - 1].ToString());

                            Feature feature = layer.CreateFeature();

                            feature.Geometry = new MapPoint(x, y);
                            for (int i = 0; i < Fields.Count; i++)
                            {
                                var field = Fields[i];
                                if (!field.Import)
                                {
                                    continue;
                                }
                                string strValue = row[i]?.ToString();
                                if (string.IsNullOrEmpty(strValue))
                                {
                                    continue;
                                }
                                object value = null;
                                
                                value = field.Field.Type switch
                                {
                                    FieldInfoType.Integer => int.Parse(strValue),
                                    FieldInfoType.Float => double.Parse(strValue),
                                    FieldInfoType.Date => DateOnly.Parse(strValue),
                                    FieldInfoType.Text => strValue,
                                    FieldInfoType.DateTime => DateTime.Parse(strValue),
                                    _ => throw new NotSupportedException(),
                                };
                                feature.SetAttributeValue(field.Field.Name, value);
                            }
                            features.Add(feature);
                        }
                        catch (Exception ex)
                        {
                            App.Log.Error("保存属性失败", ex);
                            failedRowCount++;
                            continue;
                        }
                    }
                    await layer.AddFeaturesAsync(features, FeaturesChangedSource.Initialize);
                });
                Hide();
                await ShowOkDialogAsync($"导入了{features.Count}条，失败了{failedRowCount}条");
            }
            catch (Exception ex)
            {
                App.Log.Error("创建图层失败", ex);
                Hide();
                await ShowErrorDialogAsync(ex, "创建图层失败");
                return;
            }
        }

        /// <summary>
        /// 单元格编辑结束，自动给显示名称赋值
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Grid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction == DataGridEditAction.Commit)
            {
                if (e.Column.DisplayIndex == 0)
                {
                    var field = e.Row.Item as ImportTableFieldInfo;
                    if (string.IsNullOrEmpty(field.Field.DisplayName))
                    {
                        field.Field.DisplayName = field.Field.Name;
                    }
                }
            }
        }

        /// <summary>
        /// 加载字段到<see cref="Field"/>
        /// </summary>
        private void LoadFields()
        {
            int index = 0;
            foreach (DataColumn column in Table.Columns)
            {
                ImportTableFieldInfo field = new ImportTableFieldInfo()
                {
                    ColumnName = column.ColumnName,
                    ColumnIndex = ++index
                };
                field.Field.Name = new string(column.ColumnName.Take(10).ToArray());//取前10个字符为字段名
                if (field.Field.Name.Length == 0)
                {
                    field.Field.Name = $"Field{index}";
                }
                field.Field.DisplayName = column.ColumnName;
                field.Field.Type = FieldInfoType.Text;
                Fields.Add(field);
            }
        }
    }
}