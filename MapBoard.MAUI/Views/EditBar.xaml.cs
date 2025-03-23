using CommunityToolkit.Maui.Views;
using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.UI.Editing;
using MapBoard.Mapping;
using MapBoard.Models;
using MapBoard.Util;
using MapBoard.ViewModels;
using static MapBoard.Views.PopupMenu;

namespace MapBoard.Views;

public partial class EditBar : ContentView, ISidePanel
{
    private bool requestNewPart = false;

    public EditBar()
    {
        InitializeComponent();
    }

    public SwipeDirection Direction => SwipeDirection.Up;

    public int Length => 240;

    public bool Standalone => true;

    public void OnPanelClosed()
    {
    }

    public void OnPanelOpening()
    {
        UpdateButtonsVisible();
    }

    private void AttributeTableButton_Click(object sender, EventArgs e)
    {
        AttributeTablePopup popup = new AttributeTablePopup(MainMapView.Current.Editor.EditingFeature, MainMapView.Current.Editor.Status == EditorStatus.Creating);
        MainPage.Current.ShowPopup(popup);
    }

    private void CancelEdit_Click(object sender, EventArgs e)
    {
        MainMapView.Current.Editor.Cancel();
    }

    private void CancelSelectionButton_Click(object sender, EventArgs e)
    {
        MainMapView.Current.ClearSelection();
    }

    private void ContentView_Loaded(object sender, EventArgs e)
    {
        MainMapView.Current.GeometryEditor.PropertyChanged += GeometryEditor_PropertyChanged;
        MainMapView.Current.SelectedFeatureChanged += MapView_SelectedFeatureChanged;
    }

    private void DeleteButton_Click(object sender, EventArgs e)
    {
        MainMapView.Current.DeleteSelectedFeatureAsync();
    }

    private void DeleteVertexButton_Click(object sender, EventArgs e)
    {
        MainMapView.Current.GeometryEditor.DeleteSelectedElement();
    }

    private void EditButton_Click(object sender, EventArgs e)
    {
        MainMapView.Current.Editor.StartEditSelection();
    }

    private void GeometryEditor_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        var editor = sender as GeometryEditor;
        Geometry geometry = null;
        if (e.PropertyName is nameof(GeometryEditor.CanUndo)
            or nameof(GeometryEditor.CanRedo)
            or nameof(GeometryEditor.Geometry)
            or nameof(GeometryEditor.SelectedElement))
        {
            UpdateButtonsVisible();
        }
        if (e.PropertyName == nameof(GeometryEditor.Geometry))
        {
            geometry = editor.Geometry;
            if (geometry != null)
            {
                HandleNewPart();
                HandleMeasuring(geometry);
            }

        }
        if (e.PropertyName == nameof(GeometryEditor.SelectedElement) && editor.SelectedElement != null)
        {
            //如果点击新增部分后又点了其他元素，那么认为需要取消新增部分操作
            requestNewPart = false;
        }

        void HandleNewPart()
        {
            if (requestNewPart
            && geometry is Multipart m
            && m.Parts.Count > 0
            && m.Parts[^1].PointCount > 1)
            {
                requestNewPart = false;

                if (geometry is Polyline line)
                {
                    var builder = new PolylineBuilder(line);
                    var lastPart = builder.Parts[^1];
                    builder.Parts.Remove(lastPart);
                    var oldPart = lastPart.Points.Take(lastPart.PointCount - 1);
                    dynamic newPart = new[] { lastPart.Points[^1] };
                    builder.Parts.Add(new Part(oldPart));
                    builder.Parts.Add(new Part(newPart));
                    editor.ReplaceGeometry(builder.ToGeometry());
                }
                else if (geometry is Polygon polygon)
                {
                    var builder = new PolygonBuilder(polygon);
                    var lastPart = builder.Parts[^1];
                    builder.Parts.Remove(lastPart);
                    var oldPart = lastPart.Points.Take(lastPart.PointCount - 1);
                    dynamic newPart = new[] { lastPart.Points[^1] };
                    builder.Parts.Add(new Part(oldPart));
                    builder.Parts.Add(new Part(newPart));
                    editor.ReplaceGeometry(builder.ToGeometry());
                }
            }
        }

    }
    private void HandleMeasuring(Geometry geometry)
    {
        if (geometry.GeometryType == GeometryType.Polyline)
        {
            double length = 0;
            if (geometry != null)
            {
                length = geometry.GetLength();
            }
            if (length > 1000)
            {
                lblMeasuringInfo.Text = $"长度：{length / 1000:0.00} km";
            }
            else
            {
                lblMeasuringInfo.Text = $"长度：{length:0.00} m";
            }
        }
        else if (geometry.GeometryType == GeometryType.Polygon)
        {
            double area = 0;
            if (geometry != null)
            {
                area = geometry.GetArea();
            }
            if (area > 1_000_000)
            {
                lblMeasuringInfo.Text = $"面积：{area / 1_000_000:0.00} km²";
            }
            else
            {
                lblMeasuringInfo.Text = $"面积：{area:0.0} m²";
            }
        }
        else if (geometry != null && geometry.GeometryType == GeometryType.Point)
        {
            var point = geometry as MapPoint;
            lblMeasuringInfo.Text = $"{point.X:0.00000}，{point.Y:0.00000}";
        }
        else if (geometry != null && geometry.GeometryType == GeometryType.Multipoint)
        {
            var points = geometry as Multipoint;
            lblMeasuringInfo.Text = $"数量：{points.Points.Count}";
        }
        else
        {
            lblMeasuringInfo.Text = "";
        }
    }

    private void MapView_SelectedFeatureChanged(object sender, EventArgs e)
    {
        UpdateButtonsVisible();
        if(MainMapView.Current.SelectedFeature!=null)
        {
            HandleMeasuring(MainMapView.Current.SelectedFeature.Geometry);
        }
    }
    private async void PartButton_Click(object sender, EventArgs e)
    {
        var editor = MainMapView.Current.GeometryEditor;
        var items = new PopupMenuItem[] {
            new PopupMenuItem("新增部分"),
            new PopupMenuItem("删除当前部分")
            {
                IsEnabled = (editor.Geometry as Multipart).Parts.Count > 1
            }
        };
        var result = await (sender as View).PopupMenuAsync(items);
        if (result == 0)
        {
            requestNewPart = true;
            editor.ClearSelection();//清除选中的节点，才能开始下一个部分
        }
        else if (result == 1)
        {
            var geometry = editor.Geometry;
            var selectedElement = editor.SelectedElement;
            if (geometry is not Multipart m)
            {
                throw new NotSupportedException("只支持对多部分图形增加部分");
            }
            if (m.Parts.Count <= 1)
            {
                throw new Exception("需要包含两个或以上的部分才可删除");
            }
            long partIndex;
            if (selectedElement is GeometryEditorVertex v)
            {
                partIndex = v.PartIndex;
            }
            else if (selectedElement is GeometryEditorMidVertex mv)
            {
                partIndex = mv.PartIndex;
            }
            else if (selectedElement is GeometryEditorPart p)
            {
                partIndex = p.PartIndex;
            }
            else
            {
                throw new NotSupportedException("未知的选中PartIndex");
            }
            var parts = new List<IEnumerable<Segment>>(m.Parts);
            parts.RemoveAt((int)partIndex);
            editor.ReplaceGeometry(m is Polyline ? new Polyline(parts) : new Polygon(parts));
        }

    }

    private void RedoButton_Click(object sender, EventArgs e)
    {
        MainMapView.Current.GeometryEditor.Redo();
    }

    private async void SaveEdit_Click(object sender, EventArgs e)
    {
        try
        {
            await MainMapView.Current.Editor.SaveAsync();
        }
        catch(Exception ex)
        {
            await MainPage.Current.DisplayAlert("无法保存", ex.Message, "确定");
        }
    }

    private void UndoButton_Click(object sender, EventArgs e)
    {
        MainMapView.Current.GeometryEditor.Undo();
    }

    private void UpdateButtonsVisible()
    {
        var map = MainMapView.Current;
        var editor = map.Editor;
        stkSelection.IsVisible = map.CurrentStatus == MapViewStatus.Select;
        stkEdition.IsVisible = map.CurrentStatus == MapViewStatus.Draw;

        switch (map.CurrentStatus)
        {
            case MapViewStatus.Ready:
                break;
            case MapViewStatus.Draw:
                btnUndo.IsEnabled = map.GeometryEditor.CanUndo;
                btnRedo.IsEnabled = map.GeometryEditor.CanRedo;
                btnDeleteVertex.IsEnabled = map.GeometryEditor.SelectedElement is GeometryEditorVertex;
                btnPart.IsEnabled = editor.Status is EditorStatus.Creating or EditorStatus.Editing && map.GeometryEditor.Geometry is Multipart;
                btnAttributeTable.IsEnabled = editor.Status is EditorStatus.Creating or EditorStatus.Editing;
                btnSaveEdit.IsEnabled = editor.Status is EditorStatus.Creating or EditorStatus.Editing;
                break;
            case MapViewStatus.Select:
                btnEdit.IsEnabled = map.Layers.Find(map.SelectedFeature.FeatureTable.Layer).CanEdit;
                break;
            default:
                break;
        }
    }
}