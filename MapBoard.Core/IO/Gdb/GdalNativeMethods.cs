using System;
using System.Runtime.InteropServices;

namespace MapBoard.IO.Gdb;
internal static class GdalNativeMethods
{
    public const string GdalDllName = "gdal.dll";

    [DllImport(GdalDllName, CallingConvention = CallingConvention.Cdecl)]
    public extern static IntPtr OGR_Fld_GetAlternativeNameRef(HandleRef featureHandle);

    [DllImport(GdalDllName, CallingConvention = CallingConvention.Cdecl)]
    public extern static IntPtr OGR_Fld_GetNameRef(IntPtr fieldDefn);

    [DllImport(GdalDllName, CallingConvention = CallingConvention.Cdecl)]
    public extern static IntPtr OGR_Fld_GetNameRef(HandleRef fieldDefn);

    [DllImport(GdalDllName, CallingConvention = CallingConvention.Cdecl)]
    public extern static IntPtr OGR_F_GetFieldAsString(HandleRef featureHandle, int fieldIndex);

    [DllImport(GdalDllName, CallingConvention = CallingConvention.Cdecl)]
    public extern static IntPtr OGR_F_GetFieldAsString(HandleRef featureHandle, string fieldName);

    [DllImport(GdalDllName, CallingConvention = CallingConvention.Cdecl)]
    public extern static void OGR_F_SetFieldString(HandleRef featureHandle, int fieldIndex, IntPtr value);

    [DllImport(GdalDllName, CallingConvention = CallingConvention.Cdecl)]
    public extern static IntPtr OGR_L_GetName(HandleRef layer);
}