using UnityEngine;
using System.Runtime.InteropServices; // 用来调用 Windows 窗口
using System;

// 这个类专门负责弹出 Windows 文件夹选择框
public class FileBrowserHandler
{
    [DllImport("comdlg32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool GetOpenFileName([In, Out] OpenFileName ofn);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public class OpenFileName
    {
        public int structSize = Marshal.SizeOf(typeof(OpenFileName));
        public IntPtr dlgOwner = IntPtr.Zero;
        public IntPtr instance = IntPtr.Zero;
        public string filter = "Unity Assets (*.fbx, *.prefab)\0*.fbx;*.prefab\0All Files (*.*)\0*.*\0";
        public string customFilter = null;
        public int maxCustFilter = 0;
        public int filterIndex = 0;
        public string file = new string(new char[256]);
        public int maxFile = 256;
        public string fileTitle = new string(new char[64]);
        public int maxFileTitle = 64;
        public string initialDir = null;
        public string title = "选择模型文件进行审计";
        public int flags = 0;
        public short fileOffset = 0;
        public short fileExtension = 0;
        public string defExt = "fbx";
        public IntPtr custData = IntPtr.Zero;
        public IntPtr hook = IntPtr.Zero;
        public string templateName = null;
        public IntPtr reservedPtr = IntPtr.Zero;
        public int reservedInt = 0;
        public int flagsEx = 0;
    }

    public static string OpenFile()
    {
        OpenFileName ofn = new OpenFileName();
        if (GetOpenFileName(ofn)) return ofn.file;
        return null;
    }
}