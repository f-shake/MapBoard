﻿using EGIS.ShapeFileLib;
using Esri.ArcGISRuntime.Data;
using Esri.ArcGISRuntime.Geometry;
using MapBoard.Model;
using MapBoard.Mapping;
using MapBoard.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MapBoard.Mapping.Model;
using System.Text.RegularExpressions;
using FzLib;
using FzLib.DataAnalysis;
using System.Diagnostics;

namespace MapBoard.IO
{
    public static class MobileGeodatabase
    {
        public const string MgdbFileName = "layers.geodatabase";

        public readonly static string MgdbFilePath = Path.Combine(FolderPaths.DataPath, MgdbFileName);

        public static Geodatabase Current { get; private set; }

        public static async Task ClearAsync()
        {
            foreach (var table in Current.GeodatabaseFeatureTables.ToList())
            {
                await Current.DeleteTableAsync(table.TableName);
            }
        }

        public static Task CopyToDirAsync(string directory)
        {
            return Task.Run(() =>
            {
                File.Copy(MgdbFilePath, Path.Combine(directory, MgdbFileName));
            });
        }

        public static async Task<GeodatabaseFeatureTable> CreateMgdbLayerAsync(GeometryType type, string name, string folder = null, IEnumerable<FieldInfo> fields = null)
        {
            //排除由ArcGIS自动创建的临时字段，判断字段合法性
            fields = fields
                .Where(p => !p.IsIdField())//ID
                .Where(p => p.Name.ToLower() != "shape_leng")//长度
                .Where(p => p.Name.ToLower() != "shape_area");//面积
            if (fields.Any(field => string.IsNullOrEmpty(field.Name)
            || !Regex.IsMatch(field.Name[0].ToString(), "[a-zA-Z]")
                  || !Regex.IsMatch(field.Name, "^[a-zA-Z0-9_]+$")))
            {
                throw new ArgumentException($"存在不合法的字段名");
            }

            TableDescription td = new TableDescription(name, SpatialReferences.Wgs84, type);
            foreach (var field in fields)
            {
                td.FieldDescriptions.Add(field.ToFieldDescription());
            }
            var table = await Current.CreateTableAsync(td);
            await table.LoadAsync();
            return table;
        }

        public static async Task InitializeAsync()
        {
            if (Current != null)
            {
                return;
                //throw new InvalidOperationException("已经初始化，无法再次初始化");
            }
            if (!Directory.Exists(FolderPaths.DataPath))
            {
                Directory.CreateDirectory(FolderPaths.DataPath);
            }
            var gdbFile = Path.Combine(FolderPaths.DataPath, MgdbFileName);
            if (!File.Exists(gdbFile))
            {
                Current = await Geodatabase.CreateAsync(gdbFile);
            }
            else
            {
                Current = await Geodatabase.OpenAsync(gdbFile);
            }
        }

        public static async Task ReplaceFromMGDBAsync(string mgdbPath)
        {
            Current.Close();
            Current = null;
            await Task.Run(() =>
            {
                File.Copy(mgdbPath, MgdbFilePath, true);
            });
            await InitializeAsync();
        }
    }
}