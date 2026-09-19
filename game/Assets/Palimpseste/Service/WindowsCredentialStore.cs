using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace Palimpseste.Game.Service
{
    // Generic credentials are encrypted and scoped to the signed-in Windows user.
    // The target includes a hash of the service URL, so a changed URL cannot reuse a token.
    public static class WindowsCredentialStore
    {
        private const int GenericCredential = 1;
        private const int PersistLocalMachine = 2;
        private const int NotFound = 1168;
        private const int MaximumBlobBytes = 2560;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct Credential
        {
            public int Flags;
            public int Type;
            [MarshalAs(UnmanagedType.LPWStr)] public string TargetName;
            [MarshalAs(UnmanagedType.LPWStr)] public string Comment;
            public long LastWritten;
            public int CredentialBlobSize;
            public IntPtr CredentialBlob;
            public int Persist;
            public int AttributeCount;
            public IntPtr Attributes;
            [MarshalAs(UnmanagedType.LPWStr)] public string TargetAlias;
            [MarshalAs(UnmanagedType.LPWStr)] public string UserName;
        }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CredWrite(ref Credential credential, int flags);

        [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CredRead(string target, int type, int flags, out IntPtr credential);

        [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CredDelete(string target, int type, int flags);

        [DllImport("advapi32.dll", EntryPoint = "CredFree")]
        private static extern void CredFree(IntPtr credential);
#endif

        private static string Target(string serviceUrl)
        {
            if (!Uri.TryCreate(serviceUrl, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttps && !(uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback)) ||
                !string.IsNullOrEmpty(uri.UserInfo))
                throw new ArgumentException("Adresse de service invalide");
            var normalized = uri.GetLeftPart(UriPartial.Authority).ToLowerInvariant() + uri.AbsolutePath.TrimEnd('/');
            using (var sha = SHA256.Create())
            {
                var digest = sha.ComputeHash(Encoding.UTF8.GetBytes(normalized));
                return "Palimpseste.SpellLab.ApiToken.v1." + BitConverter.ToString(digest).Replace("-", "");
            }
        }

        public static bool TryRead(string serviceUrl, out string token, out string error)
        {
            token = null;
            error = null;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            try
            {
                if (!CredRead(Target(serviceUrl), GenericCredential, 0, out var pointer))
                {
                    var code = Marshal.GetLastWin32Error();
                    if (code != NotFound) error = new Win32Exception(code).Message;
                    return false;
                }
                try
                {
                    var credential = Marshal.PtrToStructure<Credential>(pointer);
                    if (credential.CredentialBlobSize <= 0 || credential.CredentialBlobSize > MaximumBlobBytes ||
                        credential.CredentialBlob == IntPtr.Zero)
                    {
                        error = "Jeton protégé invalide";
                        return false;
                    }
                    var bytes = new byte[credential.CredentialBlobSize];
                    try
                    {
                        Marshal.Copy(credential.CredentialBlob, bytes, 0, bytes.Length);
                        token = new UTF8Encoding(false, true).GetString(bytes);
                        return !string.IsNullOrWhiteSpace(token);
                    }
                    finally { Array.Clear(bytes, 0, bytes.Length); }
                }
                finally { CredFree(pointer); }
            }
            catch (Exception ex) { error = ex.Message; return false; }
#else
            error = "Stockage protégé disponible uniquement sous Windows";
            return false;
#endif
        }

        public static bool TryWrite(string serviceUrl, string token, out string error)
        {
            error = null;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            byte[] bytes = null;
            IntPtr pointer = IntPtr.Zero;
            try
            {
                if (string.IsNullOrWhiteSpace(token)) throw new ArgumentException("Jeton vide");
                bytes = Encoding.UTF8.GetBytes(token);
                if (bytes.Length > MaximumBlobBytes) throw new ArgumentException("Jeton trop long");
                pointer = Marshal.AllocHGlobal(bytes.Length);
                Marshal.Copy(bytes, 0, pointer, bytes.Length);
                var credential = new Credential
                {
                    Type = GenericCredential,
                    TargetName = Target(serviceUrl),
                    CredentialBlobSize = bytes.Length,
                    CredentialBlob = pointer,
                    Persist = PersistLocalMachine,
                    UserName = "Palimpseste"
                };
                if (CredWrite(ref credential, 0)) return true;
                error = new Win32Exception(Marshal.GetLastWin32Error()).Message;
                return false;
            }
            catch (Exception ex) { error = ex.Message; return false; }
            finally
            {
                if (bytes != null) Array.Clear(bytes, 0, bytes.Length);
                if (pointer != IntPtr.Zero)
                {
                    for (var i = 0; i < (bytes == null ? 0 : bytes.Length); i++) Marshal.WriteByte(pointer, i, 0);
                    Marshal.FreeHGlobal(pointer);
                }
            }
#else
            error = "Stockage protégé disponible uniquement sous Windows";
            return false;
#endif
        }

        public static bool TryDelete(string serviceUrl, out string error)
        {
            error = null;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            try
            {
                if (CredDelete(Target(serviceUrl), GenericCredential, 0)) return true;
                var code = Marshal.GetLastWin32Error();
                if (code == NotFound) return true;
                error = new Win32Exception(code).Message;
                return false;
            }
            catch (Exception ex) { error = ex.Message; return false; }
#else
            error = "Stockage protégé disponible uniquement sous Windows";
            return false;
#endif
        }
    }
}
