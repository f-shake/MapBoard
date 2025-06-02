using FzLib;
using FzLib.DataStorage.Serialization;
using MapBoard.IO;
using MapBoard.Model;
using MapBoard.UI.Model;
using Newtonsoft.Json;
using PropertyChanged;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.IO;

namespace MapBoard
{
    public class Config : IJsonSerializable, INotifyPropertyChanged
    {
        public static readonly int WatermarkHeight = 72;
        private static readonly string path = FolderPaths.ConfigPath;
        private static Config instance;
        private int gpx_maxAcceptablePointDistance = 300;
        private int httpTimeOut = 1000;

        private int serverLayerLoadTimeout = 5000;

        private int theme = 0;

        public event PropertyChangedEventHandler PropertyChanged;

        public event EventHandler ThemeChanged;

        /// <summary>
        /// 配置类单例
        /// </summary>
        public static Config Instance
        {
            get
            {
                if (instance == null)
                {
                    try
                    {
                        if(File.Exists(path))
                        {
                            var json = File.ReadAllText(path);
                            instance = JsonConvert.DeserializeObject<Config>(json);
                        }
                        else
                        {
                            instance = new Config();
                        }
                    }
                    catch (Exception ex)
                    {
                        instance = new Config();
                        instance.LoadError = ex;
                    }
                }
                return instance;
            }
        }

        /// <summary>
        /// <see cref="Extension"/>的网络服务API的Token
        /// </summary>
        public List<ApiToken> ApiTokens { get; set; } = new List<ApiToken>();

        /// <summary>
        /// 绘制时，是否自动捕捉到最近的节点
        /// </summary>
        public bool AutoCatchToNearestVertex { get; set; } = true;

        /// <summary>
        /// 退出时是否自动备份
        /// </summary>
        public bool BackupWhenExit { get; set; } = false;

        /// <summary>
        /// 导入地图包以替换当前图层时是否备份
        /// </summary>
        public bool BackupWhenReplace { get; set; } = true;

        /// <summary>
        /// 底图
        /// </summary>
        public List<BaseLayerInfo> BaseLayers { get; set; } = new List<BaseLayerInfo>();

        /// <summary>
        /// 底图使用的坐标系
        /// </summary>
        public CoordinateSystem BasemapCoordinateSystem { get; set; } = CoordinateSystem.WGS84;

        /// <summary>
        /// 记忆建立缓冲区的距离
        /// </summary>
        public List<double> BufferDistances { get; set; } = new List<double>();

        /// <summary>
        /// 捕捉时，查找的范围（像素）
        /// </summary>
        public int CatchDistance { get; set; } = 12;

        /// <summary>
        /// 启用XYZ网络瓦片底图的缓存
        /// </summary>
        public bool EnableBasemapCache { get; set; } = true;

        /// <summary>
        /// GPX工具箱中，是否自动平滑
        /// </summary>
        public bool Gpx_AutoSmooth { get; set; } = false;

        /// <summary>
        /// GPX工具箱中，自动平滑的等级（窗口大小）
        /// </summary>
        public int Gpx_AutoSmoothLevel { get; set; } = 3;

        /// <summary>
        /// GPX工具箱中，是否仅自动平滑高程
        /// </summary>
        public bool Gpx_AutoSmoothOnlyZ { get; set; } = false;

        /// <summary>
        /// GPX工具箱中，浏览设置
        /// </summary>
        public BrowseInfo Gpx_BrowseInfo { get; set; } = new BrowseInfo();

        /// <summary>
        /// GPX工具箱中，速度图中是否绘制每一条记录的点
        /// </summary>
        public bool Gpx_DrawPoints { get; set; } = false;

        /// <summary>
        /// GPX工具箱中，是否绘制高程
        /// </summary>
        public bool Gpx_Height { get; set; } = false;

        /// <summary>
        /// GPX工具箱中，高程显示的夸大倍率
        /// </summary>
        public int Gpx_HeightExaggeratedMagnification { get; set; } = 5;

        /// <summary>
        /// GPX工具箱中，某点和上一个点之间的距离大于多少（米），则被认为是出现轨迹异常或信号断联，将不会连接改点和上一点
        /// </summary>
        public int Gpx_MaxAcceptablePointDistance
        {
            get => gpx_maxAcceptablePointDistance;
            set
            {
                if (value < 30)
                {
                    value = 30;
                }
                this.SetValueAndNotify(ref gpx_maxAcceptablePointDistance, value, nameof(Gpx_MaxAcceptablePointDistance));
            }
        }

        /// <summary>
        /// GPX工具箱中，是否采用相对高度
        /// </summary>
        public bool Gpx_RelativeHeight { get; set; } = false;

        /// <summary>
        /// GPX速度相关平滑的窗口大小
        /// </summary>
        public int GpxSpeedSmoothWindow { get; set; } = 5;
        /// <summary>
        /// 是否隐藏ArcGIS水印
        /// </summary>
        public bool HideWatermark { get; set; } = true;

        /// <summary>
        /// HTTP代理（无效）
        /// </summary>
        public string HttpProxy { get; set; } = "";

        /// <summary>
        /// 瓦片下载时，请求超时时间
        /// </summary>
        public int HttpTimeOut
        {
            get => httpTimeOut;
            set
            {
                if (value > 0)
                {
                    httpTimeOut = value;
                }
            }
        }

        /// <summary>
        /// 默认的HTTP访问（XYZ图层）的请求头的User-Agent
        /// </summary>
        public string HttpUserAgent { get; set; } = "Mozilla/4.0 (compatible; MSIE 8.0; Windows NT 6.1; Trident/4.0; QQWubi 133; SLCC2; .NET CLR 2.0.50727; .NET CLR 3.5.30729; .NET CLR 3.0.30729; Media Center PC 6.0; CIBA; InfoPath.2)";
        
        /// <summary>
        /// 记忆导出到GIS工具箱的FTP地址
        /// </summary>
        public string LastFTP { get; set; } = null;

        /// <summary>
        /// 记忆所选的图层列表显示类型
        /// </summary>
        public int LastLayerListGroupType { get; set; } = 0;

        /// <summary>
        /// 加载错误信息
        /// </summary>
        public Exception LoadError { get; private set; }

        /// <summary>
        /// 复制位置坐标到剪贴板时，采用的格式
        /// </summary>
        public string LocationClipboardFormat { get; set; } = "{经度},{纬度}";

        /// <summary>
        /// 最大备份数量。超过该数量后，删除最早的备份
        /// </summary>
        public int MaxBackupCount { get; set; } = 100;

        /// <summary>
        /// 地图最大缩放比例尺（无UI设置）
        /// </summary>
        public double MaxScale { get; set; } = 100;

        /// <summary>
        /// 连续绘制时，是否记忆属性值
        /// </summary>
        public bool RemainAttribute { get; set; } = false;

        /// <summary>
        /// 网路服务型图层的加载超时时间
        /// </summary>
        public int ServerLayerLoadTimeout
        {
            get => serverLayerLoadTimeout;
            set
            {
                Debug.Assert(value >= 100);
                serverLayerLoadTimeout = value;
                Parameters.LoadTimeout = TimeSpan.FromMilliseconds(value);
            }
        }

        /// <summary>
        /// 是否显示当前位置
        /// </summary>
        public bool ShowLocation { get; set; } = false;

        /// <summary>
        /// 是否显示最近点捕捉符号
        /// </summary>
        public bool ShowNearestPointSymbol { get; set; } = true;

        /// <summary>
        /// 侧边栏是否显示底图按钮
        /// </summary>
        public bool ShowSideBaseLayers { get; set; } = true;

        /// <summary>
        /// 侧边栏是否显示指南针按钮
        /// </summary>
        public bool ShowSideCompass { get; set; } = true;

        /// <summary>
        /// 侧边栏是否显示定位按钮
        /// </summary>
        public bool ShowSideLocation { get; set; } = true;

        /// <summary>
        /// 侧边栏是否显示缩放条
        /// </summary>
        public bool ShowSideScaleBar { get; set; } = true;

        /// <summary>
        /// 侧边栏是否显示放大缩小按钮
        /// </summary>
        public bool ShowSideScaleButton { get; set; } = false;

        /// <summary>
        /// 侧边栏是否显示搜索按钮
        /// </summary>
        public bool ShowSideSearch { get; set; } = true;

        /// <summary>
        /// 是否开启平滑滚动
        /// </summary>
        public bool SmoothScroll { get; set; } = true;

        /// <summary>
        /// 是否单击即可选中要素
        /// </summary>
        public bool TapToSelect { get; set; } = false;

        /// <summary>
        /// 开启单击选中要素时，是否允许选中所有图层中的要素
        /// </summary>
        public bool TapToSelectAllLayers { get; set; } = true;

        /// <summary>
        /// UI主题。0=自动/默认，1=亮色，2=暗色
        /// </summary>
        public int Theme
        {
            get => theme;
            set
            {
                theme = value;
                ThemeChanged?.Invoke(this, new EventArgs());
            }
        }

        /// <summary>
        /// 是否启用缩略图兼容模式。开启后，对于较新的格式（HEIC/AVIF）将转换为JPG后显示
        /// </summary>
        public bool ThumbnailCompatibilityMode { get; set; }
        /// <summary>
        /// 瓦片地图下载拼接器中，是否覆盖已存在的文件
        /// </summary>
        public bool Tile_CoverFile { get; set; } = false;

        /// <summary>
        /// 瓦片地图下载拼接器中，下载文件的目标路径
        /// </summary>
        public string Tile_DownloadFolder { get; set; } = FolderPaths.TileDownloadPath;

        /// <summary>
        /// 瓦片地图下载拼接器中，使用的请求头的User-Agent
        /// </summary>
        public string Tile_DownloadUserAgent { get; set; } = "Mozilla/4.0 (compatible; MSIE 8.0; Windows NT 6.1; Trident/4.0; QQWubi 133; SLCC2; .NET CLR 2.0.50727; .NET CLR 3.5.30729; .NET CLR 3.0.30729; Media Center PC 6.0; CIBA; InfoPath.2)";

        /// <summary>
        /// 瓦片地图下载拼接器中，下载的文件的扩展名
        /// </summary>
        public string Tile_FormatExtension { get; set; } = "png";

        /// <summary>
        /// 瓦片地图下载拼接器中，请求头的代理地址
        /// </summary>
        public string Tile_HttpProxy { get; set; } = "";

        /// <summary>
        /// 瓦片地图下载拼接器中，记忆上一次的下载信息
        /// </summary>
        public DownloadInfo Tile_LastDownload { get; set; } = null;

        /// <summary>
        /// 瓦片地图下载拼接器中，自建服务器的端口号
        /// </summary>
        public int Tile_ServerPort { get; set; } = 8080;

        /// <summary>
        /// 瓦片地图下载拼接器中，瓦片大小（无UI设置）
        /// </summary>
        public (int width, int height) Tile_TileSize { get; set; } = (256, 256);

        /// <summary>
        /// 瓦片地图下载拼接器中，瓦片源
        /// </summary>
        public TileSourceCollection Tile_Urls { get; set; } = new TileSourceCollection();

        /// <summary>
        /// 是否使用简约模式的图层列表
        /// </summary>
        public bool UseCompactLayerList { get; set; } = false;

        /// <summary>
        /// 保存配置到默认文件
        /// </summary>
        public void Save()
        {
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
            }
            File.WriteAllText(path, JsonConvert.SerializeObject(this, new JsonSerializerSettings() { Formatting = Formatting.Indented }));
        }
    }
}