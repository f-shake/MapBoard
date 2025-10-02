#if !RELEASEWITHOUTGDAL
using OSGeo.OGR;
using System;
using System.Linq;

namespace MapBoard.IO.Gdb;
public static class DriverExtensions
{
    /// <summary>
    /// 复制数据源(解决写入中文乱码)
    /// </summary>
    /// <param name="driver">驱动</param>
    /// <param name="srcDataSource">原始数据源</param>
    /// <param name="path">目录</param>
    /// <param name="options">可选项</param>
    /// <returns>新的数据源</returns>
    public static DataSource CopyDataSourceUTF8(this Driver driver, DataSource srcDataSource, string path, string[] options)
    {
        DataSource destDataSource = null;
        if (driver != null && srcDataSource != null && !string.IsNullOrEmpty(path))
        {
            string[] destOptions = GetOptionsWithUTF8(options);
            if (srcDataSource == null)
            {
                destDataSource = driver.CreateDataSource(path, destOptions);
            }
            else
            {
                destDataSource = driver.CopyDataSource(srcDataSource, path, destOptions);
            }
        }
        return destDataSource;
    }
    /// <summary>
    /// 获取包含UTF8编码的配置
    /// </summary>
    /// <param name="options">原有配置</param>
    /// <returns>新的配置</returns>
    public static string[] GetOptionsWithUTF8(this string[] options)
    {
        string encodingStr = "ENCODING=UTF-8";//配置增加编码，添加.cpg文件，以解决写入中文乱码
        string[] destOptions = options;
        if (destOptions == null)
        {
            destOptions = new string[] { encodingStr };
        }
        else
        {
            if (!destOptions.Contains(encodingStr))
            {
                destOptions = new string[options.Length + 1];
                Array.Copy(options, destOptions, options.Length);
                destOptions[destOptions.Length - 1] = encodingStr;
            }
        }
        return destOptions;
    }
    /// <summary>
    /// 创建数据源(解决写入中文乱码)
    /// </summary>
    /// <param name="driver">驱动</param>
    /// <param name="path">目录</param>
    /// <param name="options">可选项</param>
    /// <returns>新的数据源</returns>
    public static DataSource CreateDataSourceUTF8(this Driver driver, string path, string[] options)
    {
        DataSource destDataSource = null;
        if (driver != null && !string.IsNullOrEmpty(path))
        {
            var destOptions = GetOptionsWithUTF8(options);
            destDataSource = driver.CreateDataSource(path, destOptions);
        }
        return destDataSource;
    }
}

//https://blog.csdn.net/lc156845259/article/details/122700578
//https://github.com/lucas-repo/EM.GIS
#endif