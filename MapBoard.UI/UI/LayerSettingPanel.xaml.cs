using FzLib;
using FzLib.WPF.Dialog;

using MapBoard.IO;
using MapBoard.Model;
using MapBoard.Mapping;
using MapBoard.Util;
using ModernWpf.FzExtension.CommonDialog;
using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Color = System.Drawing.Color;
using Path = System.IO.Path;
using MapBoard.Mapping.Model;
using MapBoard.UI.Component;
using System.ComponentModel;
using System.Diagnostics;
using PropertyChanged;
using Esri.ArcGISRuntime.Data;
using FzLib.WPF.Converters;
using System.Collections.Generic;
using System.Security.Cryptography;
using MapBoard.UI.Model;
using Esri.ArcGISRuntime.Symbology;
using Esri.ArcGISRuntime.Mapping;
using LayerCollection = MapBoard.Model.LayerCollection;
using ModernWpf.Controls;

namespace MapBoard.UI
{
    /// <summary>
    /// 图层设置面板
    /// </summary>
    public partial class LayerSettingPanel : UserControlBase
    {
        public LayerSettingPanel()
        {
            InitializeComponent();
            Fonts = FontFamily.FamilyNames.Values.ToArray();
        }

        /// <summary>
        /// 图层名
        /// </summary>
        public string LayerName { get; set; } = "图层名称";

        /// <summary>
        /// 图层集合
        /// </summary>
        public MapLayerCollection Layers => MapView?.Layers;

        /// <summary>
        /// 地图
        /// </summary>
        [AlsoNotifyFor(nameof(Layers))]
        public MainMapView MapView { get; set; }

        /// <summary>
        /// 初始化面板
        /// </summary>
        /// <param name="mapView"></param>
        public void Initialize(MainMapView mapView)
        {
            MapView = mapView;
            LoadStyles();
            Layers.LayerPropertyChanged += Layers_LayerPropertyChanged;
            Layers.PropertyChanged += Layers_PropertyChanged;
        }

        /// <summary>
        /// 单击展开面板
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Expander_Expanded(object sender, RoutedEventArgs e)
        {
            //手动实现手风琴效果
            Expander expander = sender as Expander;
            foreach (var ex in grdExpanders.Children.OfType<Expander>().Where(p => p != sender))
            {
                ex.IsExpanded = false;
            }
            for (int i = 0; i < grdExpanders.Children.Count; i++)
            {
                grdExpanders.RowDefinitions[i].Height = Grid.GetRow(expander) == i ? new GridLength(1, GridUnitType.Star) : GridLength.Auto;
            }
        }

        /// <summary>
        /// 如果选中的图层加载完成，重载面板
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Layers_LayerPropertyChanged(object sender, LayerCollection.LayerPropertyChangedEventArgs e)
        {
            if (e.Layer == MapView.Layers.Selected && e.PropertyName == nameof(MapLayerInfo.IsLoaded))
            {
                LoadStyles();
            }
        }

        /// <summary>
        /// 如果选择了新的图层，重载面板
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Layers_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Layers.Selected))
            {
                LoadStyles();
            }
        }

        #region 加载和保存

        /// <summary>
        /// 配置=>UI
        /// </summary>
        public void LoadStyles()
        {
            IMapLayerInfo layer = Layers.Selected;
            if (layer == null)
            {
                KeyFields = null;
                return;
            }
            else
            {
                LayerName = layer.Name;

                LoadLabels();

                LoadRenderers(layer.Renderer);
            }

            LoadLabelFieldsMenu();
        }

        /// <summary>
        /// UI=>配置
        /// </summary>
        /// <returns></returns>
        public async Task SaveStyles()
        {
            var layer = Layers.Selected;

            layer.Renderer.UseRawJson = RendererUseRawJson;
            layer.Renderer.RawJson = RendererRawJson;
            layer.Renderer.Symbols.Clear();
            layer.Renderer.KeyFieldName = string.Join('|', KeyFields.Where(p => p.IsSelected).Select(p => p.ObjectData.Name));
            foreach (var keySymbol in Keys)
            {
                if (keySymbol.Key == defaultKeyName)
                {
                    layer.Renderer.DefaultSymbol = keySymbol.Symbol;
                }
                else
                {
                    layer.Renderer.Symbols.Add(keySymbol.Key, keySymbol.Symbol);
                }
            }
            layer.Labels = [.. Labels];
            string newName = LayerName;
            if (newName != layer.Name)
            {
                //if (newName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
                //    || newName.Length > 240 || newName.Length < 1)
                //{
                //    await CommonDialog.ShowErrorDialogAsync("新文件名不合法");
                //}
                //else
                if (Layers.Any(p => p.Name == newName))
                {
                    await CommonDialog.ShowErrorDialogAsync("该名称的文件已存在");
                }
                else
                {
                    //try
                    //{
                    //    if (Layers.Selected is ShapefileMapLayerInfo s)
                    //    {
                    //        await s.ChangeNameAsync(newName, Layers.EsriLayers);
                    //    }
                    //}
                    //catch (Exception ex)
                    //{
                    //    App.Log.Error("重命名失败", ex);
                    //    await CommonDialog.ShowErrorDialogAsync(ex, "重命名失败");
                    //}
                    //Layers.Selected = layer;
                    layer.Name = newName;
                }
            }
            try
            {
                layer.ApplyStyle();
            }
            catch (Exception ex)
            {
                string error = (string.IsNullOrWhiteSpace(layer.Name) ? "图层" + layer.Name : "图层") + "样式加载失败";
                App.Log.Error(error, ex);
                await CommonDialog.ShowErrorDialogAsync(ex, error);
            }
        }

        private void LoadLabels()
        {
            var layer = Layers.Selected;
            Labels = layer.Labels == null ?
                new ObservableCollection<LabelInfo>() :
                new ObservableCollection<LabelInfo>(layer.Labels);
            Label = Labels.Count > 0 ? Labels[0] : null;
        }

        private void LoadRenderers(UniqueValueRendererInfo renderer)
        {
            InitializeKeys(false, renderer);
            RendererUseRawJson = renderer.UseRawJson;
            RendererRawJson = renderer.RawJson;
            foreach (var symbol in renderer.Symbols)
            {
                Keys.Add(new KeySymbolPair(symbol.Key, symbol.Value));
            }
            SelectedKey = Keys.First(p => p.Key == defaultKeyName);
            var keys = renderer.KeyFieldName?.Split('|', StringSplitOptions.RemoveEmptyEntries).ToList() ?? [];

            KeyFields = Layers.Selected?.Fields
                ?.Where(p => p.CanBeRendererKey())
                ?.OrderBy(p =>
                {
                    if (keys.Contains(p.Name))
                    {
                        return keys.IndexOf(p.Name);
                    }
                    return int.MaxValue;
                })
                ?.Select(p => new SelectableObject<FieldInfo>(p, keys.Contains(p.Name)))
                ?.ToList();

            btnClasses.IsEnabled = Layers.Selected is IMapLayerInfo;
        }

        #endregion

        #region 标注
        /// <summary>
        /// 所有支持的字体
        /// </summary>
        public string[] Fonts { get; }

        /// <summary>
        /// 当前选择的标注信息
        /// </summary>
        public LabelInfo Label { get; set; }

        /// <summary>
        /// 所有标注
        /// </summary>
        public ObservableCollection<LabelInfo> Labels { get; set; }

        /// <summary>
        /// 单击新增标注按钮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void AddLabelButton_Click(object sender, RoutedEventArgs e)
        {
            Labels.Add(new LabelInfo());
            Label = Labels[^1];
        }

        /// <summary>
        /// 加载标注中字段名菜单
        /// </summary>
        private void LoadLabelFieldsMenu()
        {
            menuFields.Items.Clear();
            foreach (var field in Layers.Selected.Fields)
            {
                var item = new MenuItem()
                {
                    Tag = field.Name,
                    Header = $"{field.DisplayName}（{field.Name}）"
                };
                item.Click += (s, e) =>
                {
                    txtExpression.SelectedText = $"$feature.{(s as MenuItem).Tag as string}";
                    txtExpression.SelectionStart = txtExpression.SelectionStart + txtExpression.SelectionLength;
                };
                menuFields.Items.Add(item);
            }
        }

        /// <summary>
        /// 单击移除标注按钮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RemoveLabelButton_Click(object sender, RoutedEventArgs e)
        {
            Debug.Assert(Label != null);
            Labels.Remove(Label);
            if (Labels.Count > 0)
            {
                Label = Labels[0];
            }
        }

        /// <summary>
        /// 单击设置显示比例按钮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SetScaleButton_Click(object sender, RoutedEventArgs e)
        {
            switch ((sender as Button).Tag as string)
            {
                case "label":
                    Label.MinScale = MapView.MapScale;
                    break;

                case "min":
                    Layers.Selected.Display.MinScale = MapView.MapScale;
                    break;

                case "max":
                    Layers.Selected.Display.MaxScale = MapView.MapScale;
                    break;
            }
        }

        /// <summary>
        /// 标注的设置项和JSON互转
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ConvertLabelButton_Click(object sender, RoutedEventArgs e)
        {
            IMapLayerInfo layer = Layers.Selected;
            if (Label == null)
            {
                throw new Exception("不存在选中的Label");
            }
            if (Label.UseRawJson)
            {
                try
                {
                    var newLabel = LabelDefinition.FromJson(Label.RawJson).ToLabelInfo();
                    int index = Labels.IndexOf(Label);
                    Debug.Assert(index >= 0);
                    Labels[index] = newLabel;
                    Label = newLabel;
                }
                catch (Exception ex)
                {
                    CommonDialog.ShowErrorDialogAsync(ex, "转换失败");
                }
            }
            else
            {
                Label.RawJson = Label.ToLabelDefinition().ToJson();
                Label.UseRawJson = true;
            }
        }

        #endregion

        #region 符号系统

        /// <summary>
        /// 默认字段的显示名
        /// </summary>
        private const string defaultKeyName = "（默认）";

        /// <summary>
        /// 唯一值所有字段
        /// </summary>
        private List<SelectableObject<FieldInfo>> keyFields;

        /// <summary>
        /// 随机数生成器
        /// </summary>
        private Random r = new Random();

        /// <summary>
        /// 是否显示新增唯一值Key按钮
        /// </summary>
        public bool IsAddKeyButtonEnabled => KeyFields != null && KeyFields.Any(p => p.IsSelected);

        /// <summary>
        /// 是否显示唯一值Key的修改删除等按钮
        /// </summary>
        public bool IsChangeOrDeleteKeyButtonEnabled => SelectedKey != null && SelectedKey.Key != defaultKeyName;

        /// <summary>
        /// 唯一值所有字段
        /// </summary>
        [AlsoNotifyFor(nameof(KeyFieldsComboBoxText))]
        public List<SelectableObject<FieldInfo>> KeyFields
        {
            get => keyFields;
            private set
            {
                keyFields = value;
                if (value != null)
                {
                    foreach (var item in value)
                    {
                        item.PropertyChanged += (s, e) =>
                        {
                            if (e.PropertyName == nameof(SelectableObject<FieldInfo>.IsSelected))
                            {
                                this.Notify(nameof(IsAddKeyButtonEnabled));
                            }
                        };
                    }
                }
            }
        }

        /// <summary>
        /// 唯一值Key字段组合框显示内容
        /// </summary>
        public string KeyFieldsComboBoxText => KeyFields == null ? "（未设置）" : string.Join(", ", KeyFields.Where(p => p.IsSelected).Select(p => p.ObjectData.DisplayName));

        /// <summary>
        /// 唯一值Key集合
        /// </summary>
        public ObservableCollection<KeySymbolPair> Keys { get; private set; } = new ObservableCollection<KeySymbolPair>();

        /// <summary>
        /// 选择的唯一值Key
        /// </summary>
        [AlsoNotifyFor(nameof(IsChangeOrDeleteKeyButtonEnabled))]
        public KeySymbolPair SelectedKey { get; set; }

        public string RendererRawJson { get; set; }

        public bool RendererUseRawJson { get; set; }

        /// <summary>
        /// 生成多个集合的笛卡尔积，由New Bing编写
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="sequences"></param>
        /// <returns></returns>
        public static IEnumerable<IEnumerable<T>> CartesianProduct<T>(IEnumerable<IEnumerable<T>> sequences)
        {
            // base case: 
            IEnumerable<IEnumerable<T>> result = new[] { Enumerable.Empty<T>() };
            foreach (var sequence in sequences)
            {
                // don't close over the loop variable (fixed in C# 5 BTW):
                var s = sequence;
                // recursive case: use SelectMany to build the new product out of the old one
                result =
                    from seq in result
                    from item in s
                    select seq.Concat(new[] { item });
            }
            return result;
        }

        /// <summary>
        /// 单击修改唯一值Key按钮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void ChangeKeyButton_Click(object sender, RoutedEventArgs e)
        {
            var key = await CommonDialog.ShowInputDialogAsync("请输入分类名"
                + (KeyFields.Count(p => p.IsSelected) > 1 ? "，多个字段的分类名之间使用“|”隔开" : ""),
                SelectedKey.Key);
            if (key != null)
            {
                if (Keys.Any(p => p.Key == key))
                {
                    await CommonDialog.ShowErrorDialogAsync("该分类已存在");
                    return;
                }

                SelectedKey.Key = key;
            }
        }

        /// <summary>
        /// 单击清除唯一值Key按钮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ClearKeyFieldButton_Click(object sender, RoutedEventArgs e)
        {
            KeyFields.ForEach(p => p.IsSelected = false);
            InitializeKeys(true);
        }

        /// <summary>
        /// 唯一值字段如果被选择，就取消所有选择，因为这是一个多选框
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            (sender as ComboBox).SelectedItem = null;
        }

        /// <summary>
        /// 渲染器的设置项和JSON互转
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ConvertRendererButton_Click(object sender, RoutedEventArgs e)
        {
            IMapLayerInfo layer = Layers.Selected;
            if (RendererUseRawJson)
            {
                try
                {
                    var renderer = Renderer.FromJson(RendererRawJson).ToRendererInfo();
                    LoadRenderers(renderer);
                }
                catch (Exception ex)
                {
                    CommonDialog.ShowErrorDialogAsync(ex, "转换失败");
                }
            }
            else
            {
                RendererRawJson = layer.Layer.Renderer.ToJson();
                RendererUseRawJson = true;
            }
        }

        /// <summary>
        /// 单击创建唯一值Key按钮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void CreateKeyButton_Click(object sender, RoutedEventArgs e)
        {
            var key = await CommonDialog.ShowInputDialogAsync("请输入分类名"
                                + (KeyFields.Count(p => p.IsSelected) > 1 ? "，多个字段的分类名之间使用“|”隔开" : ""));
            if (key != null)
            {
                if (Keys.Any(p => p.Key == key))
                {
                    await CommonDialog.ShowErrorDialogAsync("该分类已存在");
                    return;
                }
                //try
                //{
                //    switch (KeyField.Type)
                //    {
                //        case FieldInfoType.Integer when !int.TryParse(key, out _):
                //        case FieldInfoType.Float when !double.TryParse(key, out _):
                //        case FieldInfoType.Date when !DateTime.TryParse(key, out _):
                //            throw new FormatException();
                //        case FieldInfoType.Time:
                //            //if (DateTime.TryParseExact(key, Parameters.TimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime time))
                //            //{
                //            //    key = time.ToString(Parameters.TimeFormat);
                //            //}
                //            //else if (DateTime.TryParse(key, out DateTime time2))
                //            //{
                //            //    key = time2.ToString(Parameters.TimeFormat);
                //            //}
                //            //else
                //            //{
                //            //    throw new FormatException();
                //            //}
                //            throw new Exception("无法使用时间类型作为Key");
                //    }
                //}
                //catch (FormatException)
                //{
                //    await CommonDialog.ShowErrorDialogAsync($"{key} 无法转换为字段 {KeyField.DisplayName} 的类型：{DescriptionConverter.GetDescription(KeyField.Type)}");
                //    return;
                //}

                var keySymbol = new KeySymbolPair(key, MapView.Layers.Selected.GetDefaultSymbol());
                Keys.Add(keySymbol);
                SelectedKey = keySymbol;
            }
        }

        /// <summary>
        /// 单击删除唯一值Key按钮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DeleteKeyButton_Click(object sender, RoutedEventArgs e)
        {
            Keys.Remove(SelectedKey);
            SelectedKey = Keys[0];
        }

        /// <summary>
        /// 自动生成全部Key
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void GenerateKeyButton_Click(object sender, RoutedEventArgs e)
        {
            var layer = Layers.Selected;
            Debug.Assert(KeyFields.Any(p => p.IsSelected));
            InitializeKeys(true);

            //生成每个字段的所有出现的属性值
            List<List<string>> keyses = new List<List<string>>();
            foreach (var key in KeyFields.Where(p => p.IsSelected))
            {
                var keysOfThisField = (await layer.GetUniqueAttributeValues(key.ObjectData.Name))
                    .Where(p => p != null)
                    .Select(p => p.ToString())
                    .ToList();
                for (int i = 0; i < keysOfThisField.Count; i++)
                {
                    if (keysOfThisField[i] == "")
                    {
                        keysOfThisField[i] = "（空）";
                    }
                }
                keyses.Add(keysOfThisField.Select(p => p.ToString()).ToList());
            }

            //生成笛卡尔积
            int count = keyses.Aggregate(1, (p, l) => p * l.Count);
            if (count > 100)
            {
                await CommonDialog.ShowErrorDialogAsync("唯一值的组合超过了100项，请手动设置");
                return;
            }
            var keys = CartesianProduct(keyses).Select(x => string.Join("|", x)).ToList();

            //分配Symbol
            foreach (var key in keys)
            {
                if (!Keys.Any(p => p.Key == key.ToString()))
                {
                    SymbolInfo symbol = layer.GetDefaultSymbol();
                    Keys.Add(new KeySymbolPair(key.ToString(), symbol));
                }
            }
        }

        /// <summary>
        /// 单击为每个唯一值生成随机颜色按钮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void GenerateRandomColorButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (var key in Keys)
            {
                key.Symbol.LineColor = GetRandomColor();
                key.Symbol.FillColor = GetRandomColor();
            }
        }

        /// <summary>
        /// 获取一个随机颜色
        /// </summary>
        /// <returns></returns>
        private Color GetRandomColor()
        {
            int R = r.Next(255);
            int G = r.Next(255);
            int B = r.Next(255);
            B = (R + G > 400) ? R + G - 400 : B;//0 : 380 - R - G;
            B = (B > 255) ? 255 : B;
            return Color.FromArgb(255, R, G, B);
        }

        /// <summary>
        /// 初始化所有Key，恢复只有默认Key的初始状态
        /// </summary>
        /// <param name="canKeepDefaultKey"></param>
        private void InitializeKeys(bool canKeepDefaultKey, UniqueValueRendererInfo layerRenderer = null)
        {
            layerRenderer ??= Layers.Selected.Renderer;
            KeySymbolPair defaultSymbol = null;
            if (canKeepDefaultKey)
            {
                defaultSymbol = Keys.FirstOrDefault(p => p.Key == defaultKeyName)//优先级1：本来的默认
                ?? new KeySymbolPair(defaultKeyName, layerRenderer.DefaultSymbol//优先级2：定义的默认
                ?? Layers.Selected.GetDefaultSymbol());//优先级3：类型默认
            }
            else
            {
                defaultSymbol = new KeySymbolPair(defaultKeyName, layerRenderer.DefaultSymbol ?? Layers.Selected.GetDefaultSymbol());
            }
            Keys.Clear();
            Keys.Add(defaultSymbol);
            SelectedKey = Keys[0];
        }

        /// <summary>
        /// 唯一值Key字段组合框下拉选择该百年
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void KeyFieldCheckBox_CheckedOrUnchecked(object sender, RoutedEventArgs e)
        {
            this.Notify(nameof(KeyFieldsComboBoxText));
        }
        #endregion


        private async void SymbolMoreButtonMenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Button btn = (((sender as MenuItem).Parent as MenuFlyoutPresenter)
                .Parent as System.Windows.Controls.Primitives.Popup)
                .PlacementTarget as Button;

                var parentGrid = btn.Parent as Grid;
                var leftControl = parentGrid.Children.OfType<Control>()
                    .Where(p => Grid.GetRow(p) == Grid.GetRow(btn) && Grid.GetColumn(p) == 2)
                    .FirstOrDefault();
                if (leftControl != null)
                {
                    DependencyProperty controlProperty = leftControl switch
                    {
                        TextBox => TextBox.TextProperty,
                        ColorPickerTextBox => ColorPickerTextBox.ColorBrushProperty,
                        ComboBox => ComboBox.SelectedIndexProperty,
                        _ => throw new NotImplementedException()
                    };

                    var selectedSymbol = leftControl.DataContext as SymbolInfo;
                    string propertyName = leftControl.GetBindingExpression(controlProperty).ResolvedSourcePropertyName;

                    var dataProperty = typeof(SymbolInfo).GetProperty(propertyName);

                    await Task.Run(() =>
                    {
                        foreach (var key in Keys.Where(p => p != SelectedKey))
                        {
                            dataProperty.SetValue(key.Symbol, dataProperty.GetValue(selectedSymbol));
                        }
                    });
                }
                else
                {
                    throw new Exception("找不到左侧控件");
                }
            }
            catch (Exception ex)
            {
                await CommonDialog.ShowErrorDialogAsync(ex, "应用属性失败");
            }
        }


    }
}