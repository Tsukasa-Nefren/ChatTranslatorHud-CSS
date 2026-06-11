using System.Runtime.InteropServices;
using System.Text;

namespace ChatTranslatorHud.Services;

public readonly record struct ClientConVarResponse(int Cookie, int StatusCode, string Name, string Value);

public static class ClientConVarResponseReader
{
    private const int MaxStringBytes = 512;

    private static readonly object _bridgeLock = new();
    private static bool _disposed;
    private static Exception? _nativeBridgeError;
    private static NativeBridge? _nativeBridge;
    private static string? _nativeBridgeDirectory;

    public static void DisposeBridge()
    {
        lock (_bridgeLock)
        {
            _disposed = true;
            var bridge = _nativeBridge;
            _nativeBridge = null;
            _nativeBridgeError = null;
            bridge?.Dispose();
        }
    }

    public static void ResetForReload(string? directory)
    {
        lock (_bridgeLock)
        {
            _disposed = false;
            _nativeBridgeError = null;
            _nativeBridgeDirectory = directory;
        }
    }

    public static bool IsNativeBridgeAvailable(out Exception? error)
    {
        lock (_bridgeLock)
        {
            if (_disposed)
            {
                error = new ObjectDisposedException(nameof(ClientConVarResponseReader));
                return false;
            }

            if (_nativeBridge != null)
            {
                error = null;
                return true;
            }

            NativeBridge? loadedBridge = null;
            try
            {
                loadedBridge = NativeBridge.Load(_nativeBridgeDirectory);
                if (loadedBridge.Version() < 4)
                    throw new InvalidOperationException("ChatTranslatorHud.Native returned an unsupported version.");

                _nativeBridge = loadedBridge;
                _nativeBridgeError = null;
            }
            catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException or InvalidOperationException)
            {
                // If we loaded the DLL but version check failed, free it
                if (loadedBridge != null && _nativeBridge != loadedBridge)
                    loadedBridge.Dispose();

                _nativeBridgeError = ex;
            }

            error = _nativeBridgeError;
            return _nativeBridge != null;
        }
    }

    public static bool TryRead(IntPtr cNetMessage, IEnumerable<int> expectedCookies, out ClientConVarResponse response)
    {
        response = default;

        if (cNetMessage == IntPtr.Zero)
            return false;

        NativeBridge? bridge;
        lock (_bridgeLock)
        {
            if (_disposed || _nativeBridge == null)
                return false;
            bridge = _nativeBridge;
        }

        if (!IsNativeBridgeAvailable(out _))
            return false;

        var cookies = expectedCookies as int[] ?? expectedCookies.ToArray();
        if (cookies.Length == 0)
            return false;

        var nativeResponse = NativeConVarResponse.Create();
        if (!bridge.ReadRespondCvarValue(cNetMessage, cookies, cookies.Length, ref nativeResponse))
            return false;

        if (nativeResponse.StatusCode is < 0 or > 3)
            return false;

        var name = ReadUtf8(nativeResponse.Name, nativeResponse.NameLength);
        if (string.IsNullOrWhiteSpace(name))
            return false;

        response = new ClientConVarResponse(
            nativeResponse.Cookie,
            nativeResponse.StatusCode,
            name,
            ReadUtf8(nativeResponse.Value, nativeResponse.ValueLength));

        return true;
    }

    public static bool TryReadFromHook(IntPtr hook, IEnumerable<int> expectedCookies, out ClientConVarResponse response)
    {
        response = default;

        if (hook == IntPtr.Zero)
            return false;

        NativeBridge? bridge;
        lock (_bridgeLock)
        {
            if (_disposed || _nativeBridge == null)
                return false;
            bridge = _nativeBridge;
        }

        if (!IsNativeBridgeAvailable(out _))
            return false;

        var cookies = expectedCookies as int[] ?? expectedCookies.ToArray();
        if (cookies.Length == 0)
            return false;

        var nativeResponse = NativeConVarResponse.Create();
        if (!bridge.ReadRespondCvarValueFromHook(hook, cookies, cookies.Length, ref nativeResponse))
            return false;

        if (nativeResponse.StatusCode is < 0 or > 3)
            return false;

        var name = ReadUtf8(nativeResponse.Name, nativeResponse.NameLength);
        if (string.IsNullOrWhiteSpace(name))
            return false;

        response = new ClientConVarResponse(
            nativeResponse.Cookie,
            nativeResponse.StatusCode,
            name,
            ReadUtf8(nativeResponse.Value, nativeResponse.ValueLength));

        return true;
    }

    private static string ReadUtf8(byte[] buffer, int length)
    {
        var byteCount = Math.Clamp(length, 0, Math.Min(buffer.Length, MaxStringBytes - 1));
        var terminator = Array.IndexOf(buffer, (byte)0, 0, byteCount);
        if (terminator >= 0)
            byteCount = terminator;

        return byteCount == 0 ? string.Empty : Encoding.UTF8.GetString(buffer, 0, byteCount);
    }

    private sealed class NativeBridge : IDisposable
    {
        private readonly NativeVersionDelegate _version;
        private readonly ReadRespondCvarValueDelegate _readRespondCvarValue;
        private readonly ReadRespondCvarValueFromHookDelegate _readRespondCvarValueFromHook;
        private IntPtr _handle;

        private NativeBridge(IntPtr handle)
        {
            _handle = handle;
            _version = Marshal.GetDelegateForFunctionPointer<NativeVersionDelegate>(
                NativeLibrary.GetExport(handle, "ChatTranslatorHud_NativeVersion"));
            _readRespondCvarValue = Marshal.GetDelegateForFunctionPointer<ReadRespondCvarValueDelegate>(
                NativeLibrary.GetExport(handle, "ChatTranslatorHud_ReadRespondCvarValue"));
            _readRespondCvarValueFromHook = Marshal.GetDelegateForFunctionPointer<ReadRespondCvarValueFromHookDelegate>(
                NativeLibrary.GetExport(handle, "ChatTranslatorHud_ReadRespondCvarValueFromHook"));
        }

        public void Dispose()
        {
            var handle = Interlocked.Exchange(ref _handle, IntPtr.Zero);
            if (handle != IntPtr.Zero)
                NativeLibrary.Free(handle);
        }

        public static NativeBridge Load(string? configuredDirectory)
        {
            var path = GetCandidatePaths(configuredDirectory).FirstOrDefault(File.Exists);

            if (path == null)
            {
                var searched = string.Join(", ", GetCandidatePaths(configuredDirectory));
                throw new DllNotFoundException($"Native bridge was not found. Searched: {searched}");
            }

            var handle = NativeLibrary.Load(path);
            try
            {
                return new NativeBridge(handle);
            }
            catch
            {
                NativeLibrary.Free(handle);
                throw;
            }
        }

        private static string GetNativeLibraryName()
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? "ChatTranslatorHud.Native.dll"
                : "ChatTranslatorHud.Native.so";
        }

        private static IEnumerable<string> GetCandidatePaths(string? configuredDirectory)
        {
            var fileName = GetNativeLibraryName();

            if (!string.IsNullOrWhiteSpace(configuredDirectory))
                yield return Path.Combine(configuredDirectory, fileName);

            var assemblyDirectory = Path.GetDirectoryName(typeof(ClientConVarResponseReader).Assembly.Location);
            if (!string.IsNullOrWhiteSpace(assemblyDirectory))
                yield return Path.Combine(assemblyDirectory, fileName);

            if (!string.IsNullOrWhiteSpace(AppContext.BaseDirectory))
                yield return Path.Combine(AppContext.BaseDirectory, fileName);
        }

        public int Version()
        {
            return _version();
        }

        public bool ReadRespondCvarValue(
            IntPtr cNetMessage,
            int[] expectedCookies,
            int expectedCookieCount,
            ref NativeConVarResponse response)
        {
            return _readRespondCvarValue(cNetMessage, expectedCookies, expectedCookieCount, ref response);
        }

        public bool ReadRespondCvarValueFromHook(
            IntPtr hook,
            int[] expectedCookies,
            int expectedCookieCount,
            ref NativeConVarResponse response)
        {
            return _readRespondCvarValueFromHook(hook, expectedCookies, expectedCookieCount, ref response);
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int NativeVersionDelegate();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private delegate bool ReadRespondCvarValueDelegate(
            IntPtr cNetMessage,
            [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] int[] expectedCookies,
            int expectedCookieCount,
            ref NativeConVarResponse response);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private delegate bool ReadRespondCvarValueFromHookDelegate(
            IntPtr hook,
            [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] int[] expectedCookies,
            int expectedCookieCount,
            ref NativeConVarResponse response);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeConVarResponse
    {
        public int Cookie;
        public int StatusCode;
        public int NameLength;
        public int ValueLength;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = MaxStringBytes)]
        public byte[] Name;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = MaxStringBytes)]
        public byte[] Value;

        public static NativeConVarResponse Create()
        {
            return new NativeConVarResponse
            {
                Name = new byte[MaxStringBytes],
                Value = new byte[MaxStringBytes]
            };
        }
    }
}
