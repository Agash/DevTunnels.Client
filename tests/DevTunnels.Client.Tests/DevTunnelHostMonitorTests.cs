using System.Reflection;
using DevTunnels.Client.Hosting;
using DevTunnels.Client.Tunnels;

namespace DevTunnels.Client.Tests;

[TestClass]
public sealed class DevTunnelHostMonitorTests
{
    private const string Ready = "Ready to accept connections for tunnel: sw-1";
    private const string Lost = "Connection to host tunnel relay closed.";

    private static readonly DevTunnelHostMonitorOptions Quiet = new()
    {
        OutputTimeout = TimeSpan.FromHours(1),
        ReconcileInterval = TimeSpan.FromHours(1),
    };

    public TestContext TestContext { get; set; } = null!;

    private CancellationToken Token => TestContext.CancellationToken;

    [TestMethod]
    public async Task Output_Ready_ConnectsAndReportsPorts()
    {
        await using Fixture fixture = new();
        fixture.Session.Print("Hosting port: 7770");
        fixture.Session.Print("Connect via browser: https://abc-7770.uks1.devtunnels.ms/");
        fixture.Session.Print(Ready);

        await fixture.Monitor.WaitForConnectedAsync(Token);
        DevTunnelHostStatus status = await fixture.WaitForAsync(
            static s => s.Ports.ContainsKey(7770),
            Token
        );

        Assert.AreEqual(DevTunnelHostConnectivity.Connected, status.Connectivity);
    }

    [TestMethod]
    public async Task Output_RelayLostThenRestored_FollowsTheHost()
    {
        await using Fixture fixture = new();
        fixture.Session.Print(Ready);
        await fixture.Monitor.WaitForConnectedAsync(Token);

        fixture.Session.Print(Lost);
        _ = await fixture.WaitForAsync(
            static s => s.Connectivity == DevTunnelHostConnectivity.Disconnected,
            Token
        );

        fixture.Session.Print("Connection to host tunnel relay restored.");
        _ = await fixture.WaitForAsync(
            static s => s.Connectivity == DevTunnelHostConnectivity.Connected,
            Token
        );
    }

    [TestMethod]
    public async Task Service_HostCountWhileLocallyDisconnected_IsATakeover()
    {
        await using Fixture fixture = new();
        fixture.Client.Status = new DevTunnelStatus { TunnelId = "sw-1.uks1", HostConnections = 1 };
        fixture.Session.Print(Ready);
        await fixture.Monitor.WaitForConnectedAsync(Token);

        fixture.Session.Print(Lost);

        DevTunnelHostStatus status = await fixture.WaitForAsync(static s => s.IsTakenOver, Token);
        Assert.AreEqual(DevTunnelHostConnectivity.Disconnected, status.Connectivity);
    }

    [TestMethod]
    public async Task Service_HostCountWithoutLocalEvidence_DoesNotConnect()
    {
        await using Fixture fixture = new();
        fixture.Client.Status = new DevTunnelStatus { TunnelId = "sw-1.uks1", HostConnections = 1 };

        await fixture.Monitor.ReconcileAsync(Token);
        DevTunnelHostStatus status = await fixture.WaitForAsync(
            static s => s.HostConnections == 1,
            Token
        );

        Assert.AreEqual(DevTunnelHostConnectivity.Starting, status.Connectivity);
    }

    [TestMethod]
    public async Task Service_QualifiedId_ReplacesTheBareOne()
    {
        await using Fixture fixture = new();
        fixture.Client.Status = new DevTunnelStatus { TunnelId = "sw-1.uks1" };

        await fixture.Monitor.ReconcileAsync(Token);

        _ = await fixture.WaitForAsync(static s => s.TunnelId == "sw-1.uks1", Token);
    }

    [TestMethod]
    public async Task Service_AnswerOlderThanADisconnect_DoesNotReconnect()
    {
        await using Fixture fixture = new();
        fixture.Session.Print(Ready);
        await fixture.Monitor.WaitForConnectedAsync(Token);

        TaskCompletionSource<DevTunnelStatus> slow = new(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        fixture.Client.Pending = slow;
        Task reconcile = fixture.Monitor.ReconcileAsync(Token);
        fixture.Session.Print(Lost);
        _ = await fixture.WaitForAsync(
            static s => s.Connectivity == DevTunnelHostConnectivity.Disconnected,
            Token
        );

        slow.SetResult(new DevTunnelStatus { TunnelId = "sw-1.uks1", HostConnections = 1 });
        await reconcile;
        DevTunnelHostStatus status = await fixture.WaitForAsync(
            static s => s.HostConnections == 1,
            Token
        );

        Assert.AreEqual(DevTunnelHostConnectivity.Disconnected, status.Connectivity);
    }

    [TestMethod]
    public async Task Exit_BeforeConnecting_FailsTheWait()
    {
        await using Fixture fixture = new();
        fixture.Session.Exit();

        _ = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            fixture.Monitor.WaitForConnectedAsync(Token)
        );
        Assert.AreEqual(DevTunnelHostConnectivity.Stopped, fixture.Monitor.Status.Connectivity);
    }

    [TestMethod]
    public async Task Start_OnARunningSession_IsConnected()
    {
        await using Fixture fixture = new(DevTunnelHostState.Running);
        await fixture.Monitor.WaitForConnectedAsync(Token);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public Fixture(DevTunnelHostState state = DevTunnelHostState.Starting)
        {
            Session = new FakeSession(state);
            IDevTunnelsClient client = DispatchProxy.Create<IDevTunnelsClient, FakeClient>();
            Client = (FakeClient)(object)client;
            Monitor = new DevTunnelHostMonitor(client, Session, Quiet);
        }

        public FakeSession Session { get; }

        public FakeClient Client { get; }

        public DevTunnelHostMonitor Monitor { get; }

        public async Task<DevTunnelHostStatus> WaitForAsync(
            Func<DevTunnelHostStatus, bool> condition,
            CancellationToken cancellationToken
        )
        {
            TaskCompletionSource<DevTunnelHostStatus> reached = new(
                TaskCreationOptions.RunContinuationsAsynchronously
            );
            void Check(object? sender, DevTunnelHostStatusChangedEventArgs e)
            {
                if (condition(e.Current))
                {
                    _ = reached.TrySetResult(e.Current);
                }
            }

            Monitor.StatusChanged += Check;
            try
            {
                return condition(Monitor.Status)
                    ? Monitor.Status
                    : await reached.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
            }
            finally
            {
                Monitor.StatusChanged -= Check;
            }
        }

        public ValueTask DisposeAsync() => Monitor.DisposeAsync();
    }

    private sealed class FakeSession(DevTunnelHostState state) : IDevTunnelHostSession
    {
        private readonly TaskCompletionSource _exit = new(
            TaskCreationOptions.RunContinuationsAsynchronously
        );

        public event EventHandler<DevTunnelHostOutputReceivedEventArgs>? OutputReceived;

        public DevTunnelHostState State { get; } = state;

        public string? TunnelId => "sw-1";

        public Uri? PublicUrl => null;

        public string? LastOutputLine => null;

        public string? FailureReason => null;

        public void Print(string line) =>
            OutputReceived?.Invoke(this, new DevTunnelHostOutputReceivedEventArgs(false, line));

        public void Exit() => _exit.TrySetResult();

        public ValueTask WaitForReadyAsync(CancellationToken cancellationToken = default) =>
            default;

        public ValueTask WaitForExitAsync(CancellationToken cancellationToken = default) =>
            new(_exit.Task.WaitAsync(cancellationToken));

        public ValueTask StopAsync(CancellationToken cancellationToken = default) => default;

        public ValueTask DisposeAsync() => default;
    }

    // DispatchProxy needs a public, derivable type.
    public class FakeClient : DispatchProxy
    {
        public DevTunnelStatus Status { get; set; } = new() { TunnelId = "sw-1" };

        public TaskCompletionSource<DevTunnelStatus>? Pending { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            targetMethod?.Name == nameof(IDevTunnelsClient.GetTunnelAsync)
                ? Pending is { } pending
                    ? new ValueTask<DevTunnelStatus>(pending.Task)
                    : ValueTask.FromResult(Status)
                : throw new NotSupportedException(targetMethod?.Name);
    }
}
