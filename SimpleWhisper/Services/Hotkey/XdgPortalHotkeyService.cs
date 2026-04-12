using Tmds.DBus.Protocol;
using Microsoft.Extensions.Logging;

namespace SimpleWhisper.Services.Hotkey;

/// <summary>
/// Push-to-talk global hotkey via the org.freedesktop.portal.GlobalShortcuts XDG portal.
/// Works on KDE Wayland, GNOME, and any portal-supporting compositor.
/// </summary>
public sealed class XdgPortalHotkeyService(IAppSettingsService settings, ILogger<XdgPortalHotkeyService>? logger = null) : IGlobalHotkeyService
{
    private const string PortalService = "org.freedesktop.portal.Desktop";
    private const string PortalPath = "/org/freedesktop/portal/desktop";
    private const string ShortcutId = "record";
    private static readonly TimeSpan PortalRequestTimeout = TimeSpan.FromSeconds(10);

    public event EventHandler? RecordingStartRequested;
    public event EventHandler? RecordingStopRequested;

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _sync = new();
    private DBusConnection? _dbus;
    private GlobalShortcutsProxy? _proxy;
    private IDisposable? _activatedSub;
    private IDisposable? _deactivatedSub;
    private string? _sessionHandle;

    public async Task StartAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            await InitializeSessionAsync(settings.PreferredHotkey, ct);
        }
        catch
        {
            TearDown();
            throw;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task RebindAsync(string newTrigger, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            TearDown();
            try
            {
                await InitializeSessionAsync(newTrigger, ct);
            }
            catch
            {
                TearDown();
                throw;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task InitializeSessionAsync(string trigger, CancellationToken ct)
    {
        var dbus = new DBusConnection(DBusAddress.Session!);
        await dbus.ConnectAsync();

        var proxy = new GlobalShortcutsProxy(dbus, PortalService, new ObjectPath(PortalPath));

        var activatedSub = await proxy.WatchActivatedAsync(
            (ex, data) => OnShortcutSignal(ex, data, RecordingStartRequested),
            emitOnCapturedContext: false);
        var deactivatedSub = await proxy.WatchDeactivatedAsync(
            (ex, data) => OnShortcutSignal(ex, data, RecordingStopRequested),
            emitOnCapturedContext: false);

        lock (_sync)
        {
            _dbus = dbus;
            _proxy = proxy;
            _activatedSub = activatedSub;
            _deactivatedSub = deactivatedSub;
        }

        var sessionHandle = await PortalRequestAsync(
            "sw_create",
            token => proxy.CreateSessionAsync(new Dictionary<string, VariantValue>
            {
                ["handle_token"] = token,
                ["session_handle_token"] = "simple_whisper_hotkey_session",
            }),
            results => results.TryGetValue("session_handle", out var sv)
                ? sv.GetString()
                : throw new InvalidOperationException("session_handle missing from CreateSession response"),
            ct);
        lock (_sync) _sessionHandle = sessionHandle;
        logger?.LogInformation("XDG GlobalShortcuts session created: {Session}", sessionHandle);

        await PortalRequestAsync(
            "sw_bind",
            token => proxy.BindShortcutsAsync(
                new ObjectPath(sessionHandle),
                [
                    (ShortcutId, new Dictionary<string, VariantValue>
                    {
                        ["description"] = "SimpleWhisper: Toggle Recording",
                        ["preferred_trigger"] = trigger,
                    })
                ],
                "",
                new Dictionary<string, VariantValue> { ["handle_token"] = token }),
            _ => true,
            ct);
        logger?.LogInformation("GlobalShortcuts hotkey bound. Trigger: {Trigger}", trigger);
    }

    private void TearDown()
    {
        IDisposable? activatedSub, deactivatedSub;
        DBusConnection? dbus;
        lock (_sync)
        {
            activatedSub = _activatedSub;
            deactivatedSub = _deactivatedSub;
            dbus = _dbus;
            _activatedSub = null;
            _deactivatedSub = null;
            _dbus = null;
            _proxy = null;
            _sessionHandle = null;
        }
        activatedSub?.Dispose();
        deactivatedSub?.Dispose();
        dbus?.Dispose();
    }

    private void OnShortcutSignal(
        Exception? ex,
        (ObjectPath SessionHandle, string ShortcutId, ulong Timestamp, Dictionary<string, VariantValue> Options) data,
        EventHandler? handler)
    {
        if (ex != null || data.ShortcutId != ShortcutId) return;
        string? currentSession;
        lock (_sync) currentSession = _sessionHandle;
        if (currentSession is null || data.SessionHandle.ToString() != currentSession) return;
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

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(PortalRequestTimeout);

        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var ctReg = timeoutCts.Token.Register(() =>
            tcs.TrySetException(ct.IsCancellationRequested
                ? new OperationCanceledException(ct)
                : new TimeoutException($"Portal request '{tokenPrefix}' timed out after {PortalRequestTimeout.TotalSeconds}s")));

        using var sub = await WatchResponseAsync(requestPath,
            results =>
            {
                try { tcs.TrySetResult(extractResult(results)); }
                catch (Exception ex) { tcs.TrySetException(ex); }
            },
            ex => tcs.TrySetException(ex));

        try
        {
            await callPortal(handleToken);
        }
        catch (Exception ex)
        {
            tcs.TrySetException(ex);
        }
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
            static (m, _) =>
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

    public async ValueTask DisposeAsync()
    {
        await _gate.WaitAsync();
        try { TearDown(); }
        finally { _gate.Release(); }
        _gate.Dispose();
    }

    // ─── Embedded proxy ──────────────────────────────────────────────────────
    // Adapted from dotnet-dbus v0.90.3 generated code for org.freedesktop.portal.GlobalShortcuts

    private sealed class GlobalShortcutsProxy(DBusConnection connection, string destination, ObjectPath path) : DBusObject(connection, destination, path)
    {
        private const string Interface = "org.freedesktop.portal.GlobalShortcuts";

        public Task<ObjectPath> CreateSessionAsync(Dictionary<string, VariantValue> options)
        {
            return Connection.CallMethodAsync(CreateMessage(), static (m, _) => ReadObjectPath(m), this);

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
            return Connection.CallMethodAsync(CreateMessage(), static (m, _) => ReadObjectPath(m), this);

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