#if UNITY_STANDALONE_WIN
using System;
using System.Runtime.InteropServices;

namespace MeshyWorkspace
{
    public static class MeshyWindowsFileDialog
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct OpenFileName
        {
            public int lStructSize;
            public IntPtr hwndOwner;
            public IntPtr hInstance;
            public string lpstrFilter;
            public string lpstrCustomFilter;
            public int nMaxCustFilter;
            public int nFilterIndex;
            public string lpstrFile;
            public int nMaxFile;
            public string lpstrFileTitle;
            public int nMaxFileTitle;
            public string lpstrInitialDir;
            public string lpstrTitle;
            public int Flags;
            public short nFileOffset;
            public short nFileExtension;
            public string lpstrDefExt;
            public IntPtr lCustData;
            public IntPtr lpfnHook;
            public string lpTemplateName;
            public IntPtr pvReserved;
            public int dwReserved;
            public int flagsEx;
        }

        [DllImport("comdlg32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool GetOpenFileName(ref OpenFileName ofn);

        public static string PickImageFile()
        {
            var ofn = new OpenFileName();
            ofn.lStructSize = Marshal.SizeOf(ofn);
            ofn.lpstrFilter = "Image Files\0*.png;*.jpg;*.jpeg;*.webp\0All Files\0*.*\0";
            ofn.lpstrFile = new string('\0', 512);
            ofn.nMaxFile = ofn.lpstrFile.Length;
            ofn.lpstrTitle = "Select reference image";
            ofn.Flags = 0x00080000 | 0x00001000;
            if (!GetOpenFileName(ref ofn))
            {
                return string.Empty;
            }
            var end = ofn.lpstrFile.IndexOf('\0');
            return end < 0 ? ofn.lpstrFile : ofn.lpstrFile.Substring(0, end);
        }
    }
}
#endif
