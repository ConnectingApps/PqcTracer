using System.Net.Security;
using System.Reflection;
using System.Runtime.InteropServices;

namespace ConnectingApps.PqcTracer.WebApi;

public static class TlsInspector
{
    // --- OpenSSL Imports ---
    private const string LibSsl = "libssl.so.3";
    private const string LibCrypto = "libcrypto.so.3";

    // 1. The ideal function (might be missing if it's a macro)
    [DllImport(LibSsl, EntryPoint = "SSL_get_negotiated_group")]
    private static extern int SSL_get_negotiated_group(IntPtr ssl);

    // 2. The fallback function (The "Universal Remote" for OpenSSL)
    // long SSL_ctrl(SSL *s, int cmd, long larg, void *parg);
    [DllImport(LibSsl, EntryPoint = "SSL_ctrl")]
    private static extern long SSL_ctrl(IntPtr ssl, int cmd, long larg, IntPtr parg);

    [DllImport(LibCrypto, EntryPoint = "OBJ_nid2sn")]
    private static extern IntPtr OBJ_nid2sn(int n);

    // Constants from OpenSSL headers (ssl.h)
    private const int SSL_CTRL_GET_NEGOTIATED_GROUP = 134;

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
                int nid = 0;

                try
                {
                    // Try the direct function first
                    nid = SSL_get_negotiated_group(sslPtr);
                }
                catch (EntryPointNotFoundException)
                {
                    // Fallback: If it's a macro, use SSL_ctrl
                    // effectively: #define SSL_get_negotiated_group(s) SSL_ctrl(s, 134, 0, NULL)
                    long result = SSL_ctrl(sslPtr, SSL_CTRL_GET_NEGOTIATED_GROUP, 0, IntPtr.Zero);
                    nid = (int)result;
                }

                if (nid == 0) return "Unknown (NID=0)";

                // Convert NID to Text
                IntPtr namePtr = OBJ_nid2sn(nid);
                return Marshal.PtrToStringAnsi(namePtr) ?? "Decode Error";
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

    private static (SafeHandle? Handle, List<FieldInfo> Path) FindOpenSslHandle(object target, int depth, List<FieldInfo> currentPath)
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
            if (!field.FieldType.FullName.StartsWith("System.Net")) continue;

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
