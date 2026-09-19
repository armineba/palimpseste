using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Palimpseste.Game.Service
{
    internal static class WindowsInvitationPicker
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct OpenFileName
        {
            public int lStructSize;
            public IntPtr hwndOwner;
            public IntPtr hInstance;
            [MarshalAs(UnmanagedType.LPWStr)] public string lpstrFilter;
            public IntPtr lpstrCustomFilter;
            public int nMaxCustFilter;
            public int nFilterIndex;
            public StringBuilder lpstrFile;
            public int nMaxFile;
            public IntPtr lpstrFileTitle;
            public int nMaxFileTitle;
            public IntPtr lpstrInitialDir;
            [MarshalAs(UnmanagedType.LPWStr)] public string lpstrTitle;
            public int Flags;
            public short nFileOffset;
            public short nFileExtension;
            public IntPtr lpstrDefExt;
            public IntPtr lCustData;
            public IntPtr lpfnHook;
            public IntPtr lpTemplateName;
            public IntPtr pvReserved;
            public int dwReserved;
            public int FlagsEx;
        }

        [DllImport("comdlg32.dll", EntryPoint = "GetOpenFileNameW", CharSet = CharSet.Unicode,
            SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetOpenFileName(ref OpenFileName name);
#endif

        public static bool TryChoose(out string path)
        {
            path = null;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            var buffer = new StringBuilder(32768);
            var dialog = new OpenFileName
            {
                lStructSize = Marshal.SizeOf<OpenFileName>(),
                lpstrFilter = "Invitations Palimpseste (*.json)\0*.json\0\0",
                lpstrFile = buffer,
                nMaxFile = buffer.Capacity,
                lpstrTitle = "Ouvrir mon invitation Palimpseste",
                Flags = 0x00080000 | 0x00001000 | 0x00000800 | 0x00000008
            };
            if (!GetOpenFileName(ref dialog)) return false;
            path = buffer.ToString();
            return !string.IsNullOrEmpty(path);
#else
            return false;
#endif
        }
    }
}
