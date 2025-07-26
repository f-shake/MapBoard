using Esri.ArcGISRuntime.Data;
using MapBoard.Mapping;
using MapBoard.UI.Bar;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Documents;
using System.Collections.Generic;
using System.Linq;
using MapBoard.Mapping.Model;
using FzLib;
using System.Runtime.CompilerServices;
using System.IO;
using System.Windows.Input;
using MapBoard.Util;
using System.ComponentModel;
using MapBoard.IO.Formats;

namespace MapBoard.UI.Dialog
{
    /// <summary>
    /// 显示缩略图对话框
    /// </summary>
    public partial class ShowImageDialog : RightBottomFloatDialogBase
    {
        /// <summary>
        /// 可能需要转换才能显示的文件类型
        /// </summary>
        public static readonly string[] needConvertExtensions = new string[] { ".heif", ".heic", ".avif" };

        /// <summary>
        /// 已经经过转换的源文件和转换后文件的映射
        /// </summary>
        private static Dictionary<string, string> convertedImages = new Dictionary<string, string>();

        /// <summary>
        /// 本窗口为单例模式，以保证窗口大小和位置不变
        /// </summary>
        private static ShowImageDialog instance = null;

        /// <summary>
        /// 当前显示的图片的路径
        /// </summary>
        private string currentImagePath;

        /// <summary>
        /// 一个用于记录当前加载的图片是否为最后一张选择的图片的版本值
        /// </summary>
        private volatile int version = 0;

        private ShowImageDialog(Window owner, MainMapView mapView) : base(owner)
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            InitializeComponent();
            PropertyChanged += ShowImageDialog_PropertyChanged;
            mapView.BoardTaskChanged += MapView_BoardTaskChanged;
        }

        /// <summary>
        /// 图片URI
        /// </summary>
        public Uri ImageUri { get; set; }

        /// <summary>
        /// 是否正在加载
        /// </summary>
        public bool Loading { get; set; }

        /// <summary>
        /// 垂直偏移
        /// </summary>
        protected override int OffsetY => 248;

        /// <summary>
        /// 显示窗口，如果还未创建则先创建
        /// </summary>
        /// <param name="owner"></param>
        /// <param name="mapView"></param>
        /// <returns></returns>
        public static ShowImageDialog CreateAndShow(Window owner, MainMapView mapView)
        {
            if (instance == null)
            {
                instance = new ShowImageDialog(owner, mapView);
                owner.Closing += (s, e) => instance = null;
            }
            instance.Show();
            return instance;
        }

        /// <summary>
        /// 设置图像路径
        /// </summary>
        /// <param name="imagePath"></param>
        /// <returns></returns>
        public async Task SetImageAsync(string imagePath)
        {
            currentImagePath = imagePath;
            ImageUri = null;
            if (imagePath == null)
            {
                return;
            }
            Loading = true;

            version++;
            int currentVersion = version;
            try
            {
                if (Config.Instance.ThumbnailCompatibilityMode
                    && needConvertExtensions.Contains(Path.GetExtension(imagePath).ToLower()))
                {
                    string convertedImagePath = null;
                    //查询缓存
                    if (convertedImages.ContainsKey(imagePath) && File.Exists(convertedImages[imagePath]))
                    {
                        convertedImagePath = convertedImages[imagePath];
                    }
                    else
                    {
                        PresentationSource source = PresentationSource.FromVisual(this);
                        convertedImagePath = await Photo.GetDisplayableImage(imagePath,
                           (uint)Math.Max(ActualHeight * source.CompositionTarget.TransformToDevice.M22,
                           ActualWidth * source.CompositionTarget.TransformToDevice.M11));
                        convertedImages.Add(imagePath, convertedImagePath);
                    }

                    //由于转换时间比较长，如果在转换时又选择了另一张图片，就会导致问题，所以需要保证没有选择新的图片
                    if (currentVersion == version)
                    {
                        ImageUri = new Uri(convertedImagePath);
                    }
                }
                else
                {
                    ImageUri = new Uri(imagePath);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"无法加载{imagePath}：{ex.Message}");
            }
            finally
            {
                if (currentVersion == version)
                {
                    Loading = false;
                }
            }
        }

        /// <summary>
        /// 鼠标双击打开图片
        /// </summary>
        /// <param name="e"></param>
        protected override void OnMouseDoubleClick(MouseButtonEventArgs e)
        {
            base.OnPreviewMouseDoubleClick(e);
            if (currentImagePath != null)
            {
                IOUtility.TryOpenInShellAsync(currentImagePath);
            }
        }

        /// <summary>
        /// 渲染完成，重置缩放
        /// </summary>
        /// <param name="sizeInfo"></param>
        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            zb.Reset();
        }

        /// <summary>
        /// 如果不在选择状态，则隐藏窗口
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MapView_BoardTaskChanged(object sender, BoardTaskChangedEventArgs e)
        {
            if (e.NewTask is not BoardTask.Select && Visibility == Visibility.Visible)
            {
                Hide();
            }
        }
        /// <summary>
        /// 图片改变，重置缩放
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ShowImageDialog_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ImageUri))
            {
                zb.Reset();
            }
        }
    }
}