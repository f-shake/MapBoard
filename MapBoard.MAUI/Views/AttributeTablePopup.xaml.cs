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

namespace MapBoard.Views;

public partial class AttributeTablePopup : Popup
{
    public const double CellWidth = 120;
    public AttributeTablePopup(IMapLayerInfo layer, IList<Feature> features = null)
    {
        InitializeComponent();

        BindingContext = new AttributeTableViewModel();

        //dg.Loaded += Dg_Loaded;
        Layer = layer;
        Features = features;
    }

    public IList<Feature> Features { get; }

    public IMapLayerInfo Layer { get; }

    private void CancelButton_Clicked(object sender, EventArgs e)
    {
        Close();
    }

    //private async void Dg_Loaded(object sender, EventArgs e)
    //{
    //    //dg.Opacity = 0;
    //    try
    //    {
    //        await LoadAsync();
    //    }
    //    catch (Exception ex)
    //    {
    //        await MainPage.Current.DisplayAlert("加载属性失败", ex.Message, "关闭");
    //        Close();
    //    }
    //    //dg.Opacity = 1;
    //}

    private async Task LoadAsync()
    {
        var features = Features;
        await Task.Run(async () =>
        {
            features ??= (await Layer.QueryFeaturesAsync(new QueryParameters())).ToList();
            //var list = new ObservableCollection<dynamic>();
            //foreach (var feature in features)
            //{
            //    var obj = new ExpandoObject();
            //    var dic = obj as  IDictionary<string, object>;
            //    foreach (var field in feature.Attributes.Keys)
            //    {
            //        dic.Add(field, feature.Attributes[field]);
            //    }
            //    list.Add(dic);
            //}
            //new ExpandoObject();
            //(BindingContext as AttributeTableViewModel).Attributes = list;
            (BindingContext as AttributeTableViewModel).Attributes = [.. features.Select(p => FeatureAttributeCollection.FromFeature(Layer, p))];

        });


        gHeader.RowDefinitions.Add(new RowDefinition(50));
        gHeader.ColumnDefinitions.Add(new ColumnDefinition(50));
        gTable.ColumnDefinitions.Add(new ColumnDefinition(50));

        for (int i = 0; i < Layer.Fields.Length; i++)
        {
            FieldInfo field = Layer.Fields[i];
            //dg.Columns.Add(new UraniumUI.Material.Controls.DataGridColumn
            //{
            //    Title = field.DisplayName,
            //    ValueBinding = new Binding($"{nameof(FeatureAttributeCollection.Attributes)}[{index}].{nameof(FeatureAttribute.Value)}")
            //});
            gHeader.ColumnDefinitions.Add(new ColumnDefinition(CellWidth));
            gTable.ColumnDefinitions.Add(new ColumnDefinition(CellWidth));
            Label label = new Label()
            {
                Text = field.DisplayName,
                Margin = new Thickness(2),
                FontAttributes = FontAttributes.Bold,
                HorizontalOptions = LayoutOptions.Center,
            };
            Grid.SetColumn(label, i + 1);
            gHeader.Children.Add(label);
        }

        for (int i = 0; i < features.Count; i++)
        {
            var feature = features[i];
            gTable.RowDefinitions.Add(new RowDefinition(1));
            gTable.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            BoxView b = new BoxView
            {
                Color = Colors.Gray,
            };
            Grid.SetRow(b, i * 2);
            Grid.SetColumnSpan(b, Layer.Fields.Length + 1);
            gTable.Children.Add(b);

            Label lSelect = new Label
            {
                Text = "选择",
                TextDecorations = TextDecorations.Underline,
                VerticalOptions = LayoutOptions.Center
            };
            lSelect.GestureRecognizers.Add(new TapGestureRecognizer()
            {
                Command = new Command<Feature>(feature =>
                {
                    MainMapView.Current.SelectFeature(feature);
                    Close();
                }),
                CommandParameter=feature
            });

            Grid.SetRow(lSelect, 2 * i + 1);
            gTable.Children.Add(lSelect);

            for (int j = 0; j < Layer.Fields.Length; j++)
            {
                FieldInfo field = Layer.Fields[j];
                Label label = new Label()
                {
                    Text = feature.GetAttributeValue(field.Name)?.ToString() ?? "",
                    Margin = new Thickness(2),
                    VerticalOptions = LayoutOptions.Center,
                    HorizontalOptions = LayoutOptions.Center,
                };
                Grid.SetColumn(label, j + 1);
                Grid.SetRow(label, i * 2 + 1);
                gTable.Children.Add(label);
            }
        }
    }

    private async void ContentView_Loaded(object sender, EventArgs e)
    {
        var p = ProgressPopup.Show("正在加载属性表");
        try
        {
            await LoadAsync();
        }
        catch (Exception ex)
        {
            await MainPage.Current.DisplayAlert("加载属性失败", ex.Message, "关闭");
            Close();
        }
        finally
        {
            p.Close();
        }
    }

    private void scrTable_Scrolled(object sender, ScrolledEventArgs e)
    {
        scrHeader.ScrollToAsync(scrTable.ScrollX, 0, false);
    }
}