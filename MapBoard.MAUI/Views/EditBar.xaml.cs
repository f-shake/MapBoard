using CommunityToolkit.Maui.Views;
using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.UI.Editing;
using MapBoard.Mapping;
using MapBoard.ViewModels;

namespace MapBoard.Views;

public partial class EditBar : ContentView, ISidePanel
{
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
    }

    private void DeleteButton_Click(object sender, EventArgs e)
    {
        MainMapView.Current.DeleteSelectedFeatureAsync();
    }

    private void EditButton_Click(object sender, EventArgs e)
    {
        MainMapView.Current.Editor.StartEditSelection();
    }

    private void GeometryEditor_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        var editor = sender as GeometryEditor;
        if (e.PropertyName is nameof(GeometryEditor.CanUndo) or nameof(GeometryEditor.CanRedo) or nameof(GeometryEditor.Geometry) or nameof(GeometryEditor.SelectedElement))
        {
            UpdateButtonsVisible();
        }
        if (e.PropertyName == nameof(GeometryEditor.Geometry))
        {
            var geometry = editor.Geometry;
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
        if (e.PropertyName == nameof(GeometryEditor.SelectedElement) && editor.SelectedElement != null)
        {
            //�������������ֺ��ֵ�������Ԫ�أ���ô��Ϊ��Ҫȡ���������ֲ���
            requestNewPart = false;
        }
    }

    private void RedoButton_Click(object sender, EventArgs e)
    {
        MainMapView.Current.GeometryEditor.Redo();
    }

    private async void SaveEdit_Click(object sender, EventArgs e)
    {
        await MainMapView.Current.Editor.SaveAsync();
    }

    private void UndoButton_Click(object sender, EventArgs e)
    {
        MainMapView.Current.GeometryEditor.Undo();
    }

    private void UpdateButtonsVisible()
    {
        stkSelection.IsVisible = MainMapView.Current.CurrentTask == BoardTask.Select;

        stkEdition.IsVisible = MainMapView.Current.CurrentTask == BoardTask.Draw;

        btnUndo.IsEnabled = MainMapView.Current.GeometryEditor.CanUndo;
        btnRedo.IsEnabled = MainMapView.Current.GeometryEditor.CanRedo;

        btnDeleteVertex.IsEnabled = MainMapView.Current.GeometryEditor.SelectedElement is GeometryEditorVertex;
        btnPart.IsEnabled = MainMapView.Current.GeometryEditor.Geometry is Multipart;
    }

    private void DeleteVertexButton_Click(object sender, EventArgs e)
    {
        MainMapView.Current.GeometryEditor.DeleteSelectedElement();
    }

    private void AttributeTableButton_Click(object sender, EventArgs e)
    {
        AttributeTablePopup popup = new AttributeTablePopup(MainMapView.Current.Editor.EditingFeature, MainMapView.Current.Editor.IsCreating);
        MainPage.Current.ShowPopup(popup);
    }

    private bool requestNewPart = false;

    private async void PartButton_Click(object sender, EventArgs e)
    {
        var editor = MainMapView.Current.GeometryEditor;
        var items = new MenuItem[] {
            new MenuItem(){Text="��������"},
            new MenuItem()
            {
                Text = "ɾ����ǰ����",
                IsEnabled = (editor.Geometry as Multipart).Parts.Count > 1
            }
        };
        var result = await (sender as View).PopupMenuAsync(items);
        if (result == 0)
        {
            requestNewPart = true;
            editor.ClearSelection();//���ѡ�еĽڵ㣬���ܿ�ʼ��һ������
        }
        else if (result == 1)
        {
            var geometry = editor.Geometry;
            var selectedElement = editor.SelectedElement;
            if (geometry is not Multipart m)
            {
                throw new NotSupportedException("ֻ֧�ֶԶಿ��ͼ�����Ӳ���");
            }
            if (m.Parts.Count <= 1)
            {
                throw new Exception("��Ҫ�������������ϵĲ��ֲſ�ɾ��");
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
                throw new NotSupportedException("δ֪��ѡ��PartIndex");
            }
            var parts = new List<IEnumerable<Segment>>(m.Parts);
            parts.RemoveAt((int)partIndex);
            editor.ReplaceGeometry(m is Polyline ? new Polyline(parts) : new Polygon(parts));
        }

    }
}