using DevTunnels.Client.Authentication;
using DevTunnels.Client.Hosting;
using DevTunnels.Client.Internal;
using DevTunnels.Client.Internal.Cli;
using DevTunnels.Client.Internal.Process;
using DevTunnels.Client.Ports;
using DevTunnels.Client.Tunnels;
using Microsoft.Extensions.Logging.Abstractions;

namespace DevTunnels.Client.Tests;

[TestClass]
public sealed class DevTunnelsClientBehaviorTests
{
    // Expected CLI argument sequences extracted to static readonly fields (CA1861).
    private static readonly string[] s_expectedCreateArgs =
        ["create", "streamweaver-webhooks-01", "--description", "updated", "--allow-anonymous", "--labels", "sw", "--json", "--nologo"];

    private static readonly string[] s_expectedUpdateArgs =
        ["update", "streamweaver-webhooks-01", "--description", "updated", "--add-labels", "sw", "--json", "--nologo"];

    private static readonly string[] s_expectedPortUpdateArgs =
        ["port", "update", "sw-01", "--port-number", "5000", "--description", "updated desc", "--add-labels", "v2", "--json", "--nologo"];

    private static readonly string[] s_expectedTokenArgs =
        ["token", "sw-01", "--scopes", "connect", "--nologo"];

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task CreateOrUpdateTunnelAsync_WhenTunnelExists_UpdatesAndReconcilesAccess()
    {
        FakeProcessExecutor executor = new();
        executor.EnqueueRunResult(new ProcessExecutionResult(DevTunnelCli.ResourceConflictsWithExistingExitCode, string.Empty, "exists"));
        executor.EnqueueRunResult(new ProcessExecutionResult(0, """
            { "tunnel": { "tunnelId": "streamweaver-webhooks-01", "hostConnections": 0, "clientConnections": 0, "description": "updated", "labels": ["sw"] } }
            """, string.Empty));
        executor.EnqueueRunResult(new ProcessExecutionResult(0, """{ "accessControlEntries": [] }""", string.Empty));
        executor.EnqueueRunResult(new ProcessExecutionResult(0, """{ "accessControlEntries": [{ "type": "Anonymous", "isDeny": false, "isInherited": false, "subjects": [], "scopes": ["connect"] }] }""", string.Empty));

        DevTunnelsClient client = new(
            new DevTunnelsClientOptions(),
            NullLogger<DevTunnelsClient>.Instance,
            executor);

        DevTunnelStatus result = await client.CreateOrUpdateTunnelAsync(
            "streamweaver-webhooks-01",
            new DevTunnelOptions
            {
                Description = "updated",
                AllowAnonymous = true,
                Labels = ["sw"]
            },
            TestContext.CancellationToken);

        Assert.AreEqual("streamweaver-webhooks-01", result.TunnelId);
        Assert.HasCount(4, executor.RunInvocations);
        CollectionAssert.AreEqual(s_expectedCreateArgs, executor.RunInvocations[0].Arguments.ToArray());
        CollectionAssert.AreEqual(s_expectedUpdateArgs, executor.RunInvocations[1].Arguments.ToArray());
    }

    [TestMethod]
    public async Task EnsureLoggedInAsync_WhenAlreadyLoggedIn_DoesNotInvokeLogin()
    {
        FakeProcessExecutor executor = new();
        executor.EnqueueRunResult(new ProcessExecutionResult(0, """{ "status": "Logged in", "provider": "GitHub", "username": "agash" }""", string.Empty));

        DevTunnelsClient client = new(
            new DevTunnelsClientOptions { PreferredLoginProvider = LoginProvider.GitHub },
            NullLogger<DevTunnelsClient>.Instance,
            executor);

        DevTunnelLoginStatus status = await client.EnsureLoggedInAsync(cancellationToken: TestContext.CancellationToken);

        Assert.IsTrue(status.IsLoggedIn);
        Assert.AreEqual(LoginProvider.GitHub, status.Provider);
        Assert.HasCount(1, executor.RunInvocations);
    }

    [TestMethod]
    public async Task UpdatePortAsync_WhenCalled_InvokesPortUpdateCommand()
    {
        FakeProcessExecutor executor = new();
        executor.EnqueueRunResult(new ProcessExecutionResult(0, """{ "tunnelId": "sw-01", "portNumber": 5000, "protocol": "https", "clientConnections": 0 }""", string.Empty));

        DevTunnelsClient client = new(
            new DevTunnelsClientOptions(),
            NullLogger<DevTunnelsClient>.Instance,
            executor);

        DevTunnelPortStatus result = await client.UpdatePortAsync(
            "sw-01",
            5000,
            new DevTunnelPortOptions { Description = "updated desc", Labels = ["v2"] },
            TestContext.CancellationToken);

        Assert.AreEqual(5000, result.PortNumber);
        Assert.HasCount(1, executor.RunInvocations);
        CollectionAssert.AreEqual(s_expectedPortUpdateArgs, executor.RunInvocations[0].Arguments.ToArray());
    }

    [TestMethod]
    public async Task GetAccessTokenAsync_WhenTokenPresentInOutput_ReturnsToken()
    {
        FakeProcessExecutor executor = new();
        executor.EnqueueRunResult(new ProcessExecutionResult(0,
            "Token tunnel ID: sw-01\nToken: eyJhbGciOiJSUzI1NiJ9.test.sig\n",
            string.Empty));

        DevTunnelsClient client = new(
            new DevTunnelsClientOptions(),
            NullLogger<DevTunnelsClient>.Instance,
            executor);

        string token = await client.GetAccessTokenAsync("sw-01", cancellationToken: TestContext.CancellationToken);

        Assert.AreEqual("eyJhbGciOiJSUzI1NiJ9.test.sig", token);
        Assert.HasCount(1, executor.RunInvocations);
        CollectionAssert.AreEqual(s_expectedTokenArgs, executor.RunInvocations[0].Arguments.ToArray());
    }

    [TestMethod]
    public async Task StartHostSessionAsync_WhenOutputContainsUrlAndTunnelId_BecomesReady()
    {
        FakeProcessExecutor executor = new();
        FakeRunningProcess runningProcess = new();
        executor.EnqueueRunningProcess(runningProcess);

        DevTunnelsClient client = new(
            new DevTunnelsClientOptions(),
            NullLogger<DevTunnelsClient>.Instance,
            executor);

        IDevTunnelHostSession session = await client.StartHostSessionAsync(
            new DevTunnelHostStartOptions { TunnelId = "streamweaver-webhooks-01" },
            TestContext.CancellationToken);

        runningProcess.EmitStdOut("Tunnel ID: streamweaver-webhooks-01");
        runningProcess.EmitStdOut("Hosting at https://abc123.devtunnels.ms");

        await session.WaitForReadyAsync(TestContext.CancellationToken);

        Assert.AreEqual(DevTunnelHostState.Running, session.State);
        Assert.AreEqual("streamweaver-webhooks-01", session.TunnelId);
        Assert.AreEqual(new Uri("https://abc123.devtunnels.ms"), session.PublicUrl);
    }

    [TestMethod]
    public async Task StartHostSessionAsync_WhenRealCliOutputFormat_ParsesConnectUrlNotInspectUrl()
    {
        // Regression: real devtunnel CLI emits two URLs on the "Connect via browser:" line
        // separated by ", " — the \S+ regex consumed the trailing comma, causing Uri.TryCreate
        // to fail on the first URL, then the inspect URL was incorrectly stored as PublicUrl.
        FakeProcessExecutor executor = new();
        FakeRunningProcess runningProcess = new();
        executor.EnqueueRunningProcess(runningProcess);

        DevTunnelsClient client = new(
            new DevTunnelsClientOptions(),
            NullLogger<DevTunnelsClient>.Instance,
            executor);

        IDevTunnelHostSession session = await client.StartHostSessionAsync(
            new DevTunnelHostStartOptions { TunnelId = "streamweaver-webhooks-01" },
            TestContext.CancellationToken);

        runningProcess.EmitStdOut("Hosting port: 5000");
        runningProcess.EmitStdOut("Connect via browser: https://jndfqj07.euw.devtunnels.ms:5000, https://jndfqj07-5000.euw.devtunnels.ms");
        runningProcess.EmitStdOut("Inspect network activity: https://jndfqj07-5000-inspect.euw.devtunnels.ms");
        runningProcess.EmitStdOut("Ready to accept connections for tunnel: streamweaver-webhooks-01.euw");

        await session.WaitForReadyAsync(TestContext.CancellationToken);

        Assert.AreEqual(DevTunnelHostState.Running, session.State);
        // Must be the standard-port hostname-embedded URL, NOT the explicit-port or inspect URL.
        // Webhook providers (Ko-fi, Patreon, etc.) reject URLs with explicit non-standard ports.
        Assert.AreEqual(new Uri("https://jndfqj07-5000.euw.devtunnels.ms"), session.PublicUrl);
        Assert.DoesNotContain("-inspect.", session.PublicUrl!.ToString(), "PublicUrl must not be the inspect URL");
    }
}
