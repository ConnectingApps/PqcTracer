using System.Net.Security;
using System.Reflection;
using System.Runtime.InteropServices;

namespace ConnectingApps.PqcTracer;

public static class TlsInspector
{
    // --- OpenSSL Imports ---
    private const string LibSsl = "libssl.so.3";
    private const string LibCrypto = "libcrypto.so.3";

    // The fallback function (The "Universal Remote" for OpenSSL)
    // long SSL_ctrl(SSL *s, int cmd, long larg, void *parg);
    [DllImport(LibSsl, EntryPoint = "SSL_ctrl")]
    private static extern long SSL_ctrl(IntPtr ssl, int cmd, long larg, IntPtr parg);

    // Get the name of a TLS group from its ID
    [DllImport(LibSsl, EntryPoint = "SSL_group_to_name")]
    private static extern IntPtr SSL_group_to_name(IntPtr ssl, int id);

    [DllImport(LibCrypto, EntryPoint = "OBJ_nid2sn")]
    private static extern IntPtr OBJ_nid2sn(int n);

    // Constants from OpenSSL headers (ssl.h)
    private const int SslCtrlGetNegotiatedGroup = 134;

    // --- Reflection Cache ---
    private static FieldInfo[]? _cachedPath;

    public static string GetNegotiatedGroup(SslStream sslStream)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return "Non-Linux";

        try
        {
            // 1. Get the Native Handle (Reuse previous logic)
            var handle = GetSslHandle(sslStream);
            if (handle == null || handle.IsInvalid) return "Err: Handle Not Found";

            // 2. Query OpenSSL
            bool success = false;
            handle.DangerousAddRef(ref success);
            try
            {
                IntPtr sslPtr = handle.DangerousGetHandle();
                long result = SSL_ctrl(sslPtr, SslCtrlGetNegotiatedGroup, 0, IntPtr.Zero);
                var groupId = (int)result;

                if (groupId == 0) return "Unknown (GroupID=0)";

                // Convert Group ID to name using SSL_group_to_name (not OBJ_nid2sn for TLS 1.3 groups)
                IntPtr namePtr = SSL_group_to_name(sslPtr, groupId);
                
                if (namePtr == IntPtr.Zero) return $"Decode Error (GroupID={groupId})";
                
                return Marshal.PtrToStringAnsi(namePtr) ?? $"Decode Error (GroupID={groupId})";
            }
            finally
            {
                if (success) handle.DangerousRelease();
            }
        }
        catch (Exception ex)
        {
            return $"Err: {ex.Message}";
        }
    }

    // --- Helper: Reflection (Same as before, condensed) ---
    private static SafeHandle? GetSslHandle(SslStream sslStream)
    {
        // 1. Fast Path
        if (_cachedPath != null)
        {
            object? current = sslStream;
            foreach (var field in _cachedPath)
            {
                current = field.GetValue(current);
                if (current == null) return null;
            }
            return current as SafeHandle;
        }

        // 2. Discovery Path (One-time cost)
        var result = FindOpenSslHandle(sslStream, 0, new List<FieldInfo>());
        if (result.Handle != null)
        {
            _cachedPath = result.Path.ToArray();
            return result.Handle;
        }
        return null;
    }

    private static (SafeHandle? Handle, List<FieldInfo> Path) FindOpenSslHandle(object? target, int depth, List<FieldInfo> currentPath)
    {
        if (depth > 4 || target == null) return (null, currentPath);

        // Found it?
        if (target is SafeHandle h && !h.IsClosed && !h.IsInvalid)
        {
            var name = target.GetType().Name;
            // "SafeDeleteContext" is the standard name in .NET 5+ for the OpenSSL handle wrapper
            if (name.Contains("Context") || name.Contains("Ssl")) return (h, currentPath);
        }

        // Scan fields
        var fields = target.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        foreach (var field in fields)
        {
            if (field.FieldType.IsPrimitive || field.FieldType == typeof(string)) continue;
            if (field.FieldType.FullName?.StartsWith("System.Net") != true) continue;

            try
            {
                var val = field.GetValue(target);
                if (val != null)
                {
                    currentPath.Add(field);
                    var res = FindOpenSslHandle(val, depth + 1, currentPath);
                    if (res.Handle != null) return res;
                    currentPath.RemoveAt(currentPath.Count - 1);
                }
            }
            catch { }
        }
        return (null, currentPath);
    }
}
