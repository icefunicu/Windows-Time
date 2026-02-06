using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace ScreenTimeWin.Service;

public static class JumboIconHelper
{
    // System Image List flags
    private const int SHIL_JUMBO = 0x4; // 256x256
    private const int SHIL_EXTRALARGE = 0x2; // 48x48

    // SHGetFileInfo flags
    private const int SHGFI_SYSICONINDEX = 0x000004000;
    
    // IImageList.GetIcon flags
    private const int ILD_TRANSPARENT = 0x00000001;

    /// <summary>
    /// Gets the 256x256 Jumbo icon for a file path.
    /// </summary>
    public static string? ExtractJumboIconBase64(string filePath)
    {
        try
        {
            if (string.IsNullOrEmpty(filePath)) return null;

            // 1. Get index in system image list
            var shinfo = new SHFILEINFO();
            IntPtr ret = SHGetFileInfo(filePath, 0, ref shinfo, (uint)Marshal.SizeOf(shinfo), SHGFI_SYSICONINDEX);
            
            // If failed to get info or index is 0 (which might be valid but suspicious for system icons, let's proceed if ret is valid)
            // Actually ret != 0 means success for SHGetFileInfo with SYSICONINDEX
            if (ret == IntPtr.Zero) return null;

            // 2. Get Jumbo Image List
            var iidImageList = new Guid("46EB5926-582E-4017-9FDF-E8998DAA0950");
            IImageList? iml = null;
            
            try 
            {
                int hResult = SHGetImageList(SHIL_JUMBO, ref iidImageList, out iml);
                if (hResult != 0 || iml == null) return null;

                // 3. Extract Icon
                IntPtr hIcon = IntPtr.Zero;
                hResult = iml.GetIcon(shinfo.iIcon, ILD_TRANSPARENT, out hIcon);

                if (hResult != 0 || hIcon == IntPtr.Zero) return null;

                // 4. Convert to Bitmap
                try
                {
                    using var icon = Icon.FromHandle(hIcon);
                    using var bitmap = icon.ToBitmap();
                    
                    // Resize/Compress if needed? 256x256 PNG is fine for modern UI.
                    // It might be around 10-50KB per icon.
                    using var stream = new System.IO.MemoryStream();
                    bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                    return Convert.ToBase64String(stream.ToArray());
                }
                finally
                {
                    DestroyIcon(hIcon);
                }
            }
            catch
            {
                return null;
            }
        }
        catch
        {
            return null;
        }
    }

    #region P/Invoke

    [StructLayout(LayoutKind.Sequential)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

    [DllImport("shell32.dll", EntryPoint = "#727")]
    private static extern int SHGetImageList(int iImageList, ref Guid riid, out IImageList ppv);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [ComImport]
    [Guid("46EB5926-582E-4017-9FDF-E8998DAA0950")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IImageList
    {
        [PreserveSig] int Add(IntPtr hbmImage, IntPtr hbmMask, ref int pi);
        [PreserveSig] int ReplaceIcon(int i, IntPtr hicon, ref int pi);
        [PreserveSig] int SetOverlayImage(int iImage, int iOverlay);
        [PreserveSig] int Replace(int i, IntPtr hbmImage, IntPtr hbmMask);
        [PreserveSig] int AddMasked(IntPtr hbmImage, int crMask, ref int pi);
        [PreserveSig] int Draw(IntPtr pimldp);
        [PreserveSig] int Remove(int i);
        [PreserveSig] int GetIcon(int i, int flags, out IntPtr picon);
    }

    #endregion
}