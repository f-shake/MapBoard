namespace MapBoard.Util
{
    public enum ImportMapType
    {
        //文件
        MapPackageOverwrite = 1,
        //MapPackgeAppend = 2,
        LayerPackge = 3,
        Gpx = 4,
        Shapefile = 5,
        CSV = 6,
        KML = 7,
        Mmpk = 8,

        //目录
        FgdbDir = 101,
        GpxDir = 102,
        PhotoDir = 103
    }
}