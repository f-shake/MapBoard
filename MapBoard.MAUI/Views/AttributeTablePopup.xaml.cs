using AutoMapper.Features;
using CommunityToolkit.Maui.Views;
using Esri.ArcGISRuntime.Data;
using MapBoard.Mapping;
using MapBoard.Mapping.Model;
using MapBoard.Model;
using MapBoard.Util;
using MapBoard.ViewModels;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System.Collections.ObjectModel;
using System.Dynamic;
using System.Windows.Input;

namespace MapBoard.Views;

public partial class AttributeTablePopup : Popup
{
    public const double CellWidth = 120;
    public AttributeTablePopup(IMapLayerInfo layer, IList<Feature> features = null)
    {
        InitializeComponent();

        var viewModel = new AttributeTableViewModel();
        BindingContext = viewModel;

        Layer = layer;
        Features = features;

        viewModel.SelectFeatureCommand = new Command<Feature>(feature =>
        {
            MainMapView.Current.SelectFeature(feature);
            Close();
        });

        //同步滚动
        tableScrollView.Scrolled += (s, e) =>
        {
            headerScrollView.ScrollToAsync(e.ScrollX, 0, false);
        };
    }

    public IList<Feature> Features { get; }
    public IMapLayerInfo Layer { get; }

    private void CancelButton_Clicked(object sender, EventArgs e)
    {
        Close();
    }

    private async Task LoadAsync()
    {
        var p = ProgressPopup.Show("正在加载属性表");
        try
        {
            var features = Features;

            // 设置表头
            headerCollectionView.ItemsSource = Layer.Fields;

            // 创建动态数据模板
            tableCollectionView.ItemTemplate = CreateDataTemplate();

            var vm = BindingContext as AttributeTableViewModel;
            vm.Attributes = new ObservableCollection<FeatureAttributeCollection>();

            // 先加载所有数据到内存（后台线程）
            await Task.Run(async () =>
            {
                features ??= [.. (await Layer.QueryFeaturesAsync(new QueryParameters()))];
            });

            p.Close();
            p = null;
            loadingCancellationTokenSource = new CancellationTokenSource();

            // 改为在主线程上分批添加，确保UI能响应
            foreach (var f in features.Select(p => FeatureAttributeCollection.FromFeature(Layer, p)))
            {
                if (loadingCancellationTokenSource.Token.IsCancellationRequested)
                {
                    return;
                }

                // 在主线程添加单个项
                vm.Attributes.Add(f);

                // 让UI有机会渲染（在主线程上等待）
                await Task.Delay(16); // ~60fps (1000ms/60 ≈ 16ms)
            }
        }
        catch (Exception ex)
        {
            await MainPage.Current.DisplayAlert("加载属性失败", ex.Message, "关闭");
            Close();
        }
        finally
        {
            p?.Close();
            btnCancelLoading.IsVisible = false;
        }
    }

    private DataTemplate CreateDataTemplate()
    {
        return new DataTemplate(() =>
        {
            var innerGrid = new Grid { Padding = new Thickness(0, 5, 0, 5) };

            // 添加选择列
            innerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = 50 });

            var selectLabel = new Label
            {
                Text = "选择",
                TextDecorations = TextDecorations.Underline,
                VerticalOptions = LayoutOptions.Center,
                HorizontalOptions = LayoutOptions.Center
            };

            var tapGesture = new TapGestureRecognizer();
            tapGesture.SetBinding(TapGestureRecognizer.CommandProperty,
                new Binding(source: new RelativeBindingSource(
                    RelativeBindingSourceMode.FindAncestor,
                    typeof(AttributeTablePopup),
                    1),
                    path: "BindingContext.SelectFeatureCommand"));
            tapGesture.SetBinding(TapGestureRecognizer.CommandParameterProperty,
                new Binding("Feature"));

            selectLabel.GestureRecognizers.Add(tapGesture);
            innerGrid.Add(selectLabel);

            // 添加动态字段列
            for (int i = 0; i < Layer.Fields.Length; i++)
            {
                innerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = CellWidth });

                var label = new Label
                {
                    Margin = new Thickness(2),
                    VerticalOptions = LayoutOptions.Center,
                    HorizontalOptions = LayoutOptions.Center
                };

                label.SetBinding(Label.TextProperty,
                    new Binding($"Attributes[{i}].Value", stringFormat: "{0}"));

                Grid.SetColumn(label, i + 1);
                innerGrid.Add(label);
            }

            return innerGrid;
        });
    }

    private async void ContentView_Loaded(object sender, EventArgs e)
    {
        await LoadAsync();
    }

    private CancellationTokenSource loadingCancellationTokenSource;
    private void CancelLoadingButton_Clicked(object sender, EventArgs e)
    {
        loadingCancellationTokenSource?.Cancel();
    }
}