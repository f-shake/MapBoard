#if !RELEASEWITHOUTGDAL
using OSGeo.OGR;
using System;
using System.Runtime.InteropServices;
using System.Text;
using static MapBoard.IO.Gdb.GdalNativeMethods;

namespace MapBoard.IO.Gdb;
public static class GdalExtensions
{

    public const string GdalEncoding = "GBK";

    public static string GetFieldAsStringUTF8(this Feature feature, int fieldIndex) => Feature.getCPtr(feature).ToUTF8(OGR_F_GetFieldAsString, fieldIndex);

    public static string GetFieldAsStringUTF8(this Feature feature, string fieldName) => Feature.getCPtr(feature).ToUTF8(OGR_F_GetFieldAsString, fieldName);

    public static string GetFieldAliasAsStringUTF8(this FieldDefn def) => FieldDefn.getCPtr(def).ToUTF8(OGR_Fld_GetAlternativeNameRef);

    public static string GetNameUTF8(this Layer layer) => Layer.getCPtr(layer).ToUTF8(OGR_L_GetName);

    public static string GetNameUTF8(this FieldDefn fieldDefn) => FieldDefn.getCPtr(fieldDefn).ToUTF8(OGR_Fld_GetNameRef);

    private static string ToUTF8(this HandleRef handleRef, Func<HandleRef, nint> func)
    {
        IntPtr strPtr = func(handleRef);
        string value = strPtr.IntPtrToString(Encoding.UTF8);
        return value;
    }
    private static string ToUTF8(this HandleRef handleRef, Func<HandleRef, int, nint> func, int index)
    {
        IntPtr strPtr = func(handleRef, index);
        string value = strPtr.IntPtrToString(Encoding.UTF8);
        return value;
    }
    private static string ToUTF8(this HandleRef handleRef, Func<HandleRef, string, nint> func, string key)
    {
        IntPtr strPtr = func(handleRef, key);
        string value = strPtr.IntPtrToString(Encoding.UTF8);
        return value;
    }
}

//https://blog.csdn.net/lc156845259/article/details/122700578
//https://github.com/lucas-repo/EM.GIS

#endif