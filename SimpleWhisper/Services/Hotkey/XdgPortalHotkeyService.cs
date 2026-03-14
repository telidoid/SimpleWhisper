using Tmds.DBus.Protocol;
using Microsoft.Extensions.Logging;

namespace SimpleWhisper.Services.Hotkey;

/// <summary>
/// Push-to-talk global hotkey via the org.freedesktop.portal.GlobalShortcuts XDG portal.
/// Works on KDE Wayland, GNOME, and any portal-supporting compositor.
/// </summary>
public sealed class XdgPortalHotkeyService(ILogger<XdgPortalHotkeyService>? logger = null) : IGlobalHotkeyService
{
    private const string PortalService = "org.freedesktop.portal.Desktop";
    private const string PortalPath = "/org/freedesktop/portal/desktop";
    private const string ShortcutId = "record";
    private const string DefaultTrigger = "Meta+Alt+W";

    public event EventHandler? RecordingStartRequested;
    public event EventHandler? RecordingStopRequested;

    private DBusConnection? _dbus;
    private IDisposable? _activatedSub;
    private IDisposable? _deactivatedSub;
    private string? _sessionHandle;

    public async Task StartAsync(CancellationToken ct = default)
    {
        _dbus = new DBusConnection(DBusAddress.Session!);
        await _dbus.ConnectAsync();

        var proxy = new GlobalShortcutsProxy(_dbus, PortalService, new ObjectPath(PortalPath));

        _activatedSub = await proxy.WatchActivatedAsync(
            (ex, data) => OnShortcutSignal(ex, data, RecordingStartRequested),
            emitOnCapturedContext: false);
        _deactivatedSub = await proxy.WatchDeactivatedAsync(
            (ex, data) => OnShortcutSignal(ex, data, RecordingStopRequested),
            emitOnCapturedContext: false);

        _sessionHandle = await PortalRequestAsync(
            "sw_create",
            token => proxy.CreateSessionAsync(new Dictionary<string, VariantValue>
            {
                ["handle_token"] = token,
                ["session_handle_token"] = NewToken("sw_session"),
            }),
            results => results.TryGetValue("session_handle", out var sv)
                ? sv.GetString()!
                : throw new InvalidOperationException("session_handle missing from CreateSession response"),
            ct);
        logger?.LogInformation("XDG GlobalShortcuts session created: {Session}", _sessionHandle);

        await PortalRequestAsync<bool>(
            "sw_bind",
            token => proxy.BindShortcutsAsync(
                new ObjectPath(_sessionHandle),
                [
                    (ShortcutId, new Dictionary<string, VariantValue>
                    {
                        ["description"] = "SimpleWhisper: Toggle Recording",
                        ["preferred_trigger"] = DefaultTrigger,
                    })
                ],
                "",
                new Dictionary<string, VariantValue> { ["handle_token"] = token }),
            _ => true,
            ct);
        logger?.LogInformation("GlobalShortcuts hotkey bound. Default trigger: {Trigger}", DefaultTrigger);
    }

    private void OnShortcutSignal(
        Exception? ex,
        (ObjectPath SessionHandle, string ShortcutId, ulong Timestamp, Dictionary<string, VariantValue> Options) data,
        EventHandler? handler)
    {
        if (ex != null || data.SessionHandle.ToString() != _sessionHandle || data.ShortcutId != ShortcutId)
            return;
        handler?.Invoke(this, EventArgs.Empty);
    }

    private async Task<T> PortalRequestAsync<T>(
        string tokenPrefix,
        Func<string, Task> callPortal,
        Func<Dictionary<string, VariantValue>, T> extractResult,
        CancellationToken ct)
    {
        var handleToken = NewToken(tokenPrefix);
        var requestPath = BuildRequestPath(handleToken);

        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var ctReg = ct.Register(() => tcs.TrySetCanceled());

        using var sub = await WatchResponseAsync(requestPath,
            results => tcs.TrySetResult(extractResult(results)),
            ex => tcs.TrySetException(ex));

        await callPortal(handleToken);
        return await tcs.Task;
    }

    private static string NewToken(string prefix) =>
        prefix + "_" + Guid.NewGuid().ToString("N")[..8];

    private string BuildRequestPath(string handleToken)
    {
        var sender = _dbus!.UniqueName!.TrimStart(':').Replace('.', '_');
        return $"/org/freedesktop/portal/desktop/request/{sender}/{handleToken}";
    }

    private async Task<IDisposable> WatchResponseAsync(
        string requestPath,
        Action<Dictionary<string, VariantValue>> onSuccess,
        Action<Exception> onError)
    {
        return await _dbus!.WatchSignalAsync<(uint Code, Dictionary<string, VariantValue> Results)>(
            PortalService,
            new ObjectPath(requestPath),
            "org.freedesktop.portal.Request",
            "Response",
            static (Message m, object? _) =>
            {
                var reader = m.GetBodyReader();
                var code = reader.ReadUInt32();
                var results = reader.ReadDictionaryOfStringToVariantValue();
                return (code, results);
            },
            (ex, data) =>
            {
                if (ex != null)
                {
                    onError(ex);
                    return;
                }

                if (data.Code != 0)
                {
                    onError(new InvalidOperationException($"Portal request failed: code={data.Code}"));
                    return;
                }

                onSuccess(data.Results);
            },
            null,
            false,
            ObserverFlags.None);
    }

    public ValueTask DisposeAsync()
    {
        _activatedSub?.Dispose();
        _deactivatedSub?.Dispose();
        _dbus?.Dispose();
        return ValueTask.CompletedTask;
    }

    // ─── Embedded proxy ──────────────────────────────────────────────────────
    // Adapted from dotnet-dbus v0.90.3 generated code for org.freedesktop.portal.GlobalShortcuts

    private sealed class GlobalShortcutsProxy(DBusConnection connection, string destination, ObjectPath path) : DBusObject(connection, destination, path)
    {
        private const string Interface = "org.freedesktop.portal.GlobalShortcuts";

        public Task<ObjectPath> CreateSessionAsync(Dictionary<string, VariantValue> options)
        {
            return Connection.CallMethodAsync(CreateMessage(), static (Message m, object? _) => ReadObjectPath(m), this);

            MessageBuffer CreateMessage()
            {
                var writer = Connection.GetMessageWriter();
                writer.WriteMethodCallHeader(
                    destination: Destination, path: Path, @interface: Interface,
                    member: "CreateSession", signature: "a{sv}");
                writer.WriteDictionary(options);
                return writer.CreateMessage();
            }
        }

        public Task<ObjectPath> BindShortcutsAsync(
            ObjectPath sessionHandle,
            (string Id, Dictionary<string, VariantValue> Options)[] shortcuts,
            string parentWindow,
            Dictionary<string, VariantValue> options)
        {
            return Connection.CallMethodAsync(CreateMessage(), static (Message m, object? _) => ReadObjectPath(m), this);

            MessageBuffer CreateMessage()
            {
                var writer = Connection.GetMessageWriter();
                writer.WriteMethodCallHeader(
                    destination: Destination, path: Path, @interface: Interface,
                    member: "BindShortcuts", signature: "oa(sa{sv})sa{sv}");
                writer.WriteObjectPath(sessionHandle);
                WriteShortcutsArray(ref writer, shortcuts);
                writer.WriteString(parentWindow);
                writer.WriteDictionary(options);
                return writer.CreateMessage();
            }
        }

        public ValueTask<IDisposable> WatchActivatedAsync(
            Action<Exception?, (ObjectPath SessionHandle, string ShortcutId, ulong Timestamp, Dictionary<string, VariantValue> Options)> handler,
            bool emitOnCapturedContext = true,
            ObserverFlags flags = ObserverFlags.None)
            => Connection.WatchSignalAsync(Destination, Path, Interface, "Activated",
                static (m, _) => ReadShortcutSignal(m), handler, this, emitOnCapturedContext, flags);

        public ValueTask<IDisposable> WatchDeactivatedAsync(
            Action<Exception?, (ObjectPath SessionHandle, string ShortcutId, ulong Timestamp, Dictionary<string, VariantValue> Options)> handler,
            bool emitOnCapturedContext = true,
            ObserverFlags flags = ObserverFlags.None)
            => Connection.WatchSignalAsync(Destination, Path, Interface, "Deactivated",
                static (m, _) => ReadShortcutSignal(m), handler, this, emitOnCapturedContext, flags);

        private static ObjectPath ReadObjectPath(Message m)
        {
            var reader = m.GetBodyReader();
            return reader.ReadObjectPath();
        }

        private static (ObjectPath, string, ulong, Dictionary<string, VariantValue>) ReadShortcutSignal(Message m)
        {
            var reader = m.GetBodyReader();
            return (reader.ReadObjectPath(), reader.ReadString(), reader.ReadUInt64(), reader.ReadDictionaryOfStringToVariantValue());
        }

        private static void WriteShortcutsArray(ref MessageWriter writer, (string Id, Dictionary<string, VariantValue> Options)[] shortcuts)
        {
            var arrayStart = writer.WriteArrayStart(DBusType.Struct);
            foreach (var (id, opts) in shortcuts)
            {
                writer.WriteStructureStart();
                writer.WriteString(id);
                writer.WriteDictionary(opts);
            }

            writer.WriteArrayEnd(arrayStart);
        }
    }
}