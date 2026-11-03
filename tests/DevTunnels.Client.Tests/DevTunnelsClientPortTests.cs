using DevTunnels.Client.Internal.Cli;
using DevTunnels.Client.Internal.Process;
using DevTunnels.Client.Ports;
using Microsoft.Extensions.Logging.Abstractions;

namespace DevTunnels.Client.Tests;

[TestClass]
public sealed class DevTunnelsClientPortTests
{
    private const string Anonymous =
        """{ "accessControlEntries": [{ "type": "Anonymous", "subjects": [], "scopes": ["connect"] }] }""";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task CreateOrReplacePortAsync_EnvelopedReply_ReadsThePort()
    {
        FakeProcessExecutor executor = new();
        executor.EnqueueRunResult(
            new ProcessExecutionResult(
                0,
                """{ "port": { "tunnelId": "sw-1.uks1", "portNumber": 7770, "protocol": "http" } }""",
                string.Empty
            )
        );

        DevTunnelPortStatus port = await Client(executor)
            .CreateOrReplacePortAsync(
                "sw-1.uks1",
                7770,
                new DevTunnelPortOptions { Protocol = "http" },
                TestContext.CancellationToken
            );

        Assert.AreEqual(7770, port.PortNumber);
        Assert.AreEqual("sw-1.uks1", port.TunnelId);
    }

    [TestMethod]
    public async Task CreateOrReplacePortAsync_MatchingPortExists_KeepsIt()
    {
        FakeProcessExecutor executor = new();
        executor.EnqueueRunResult(
            new ProcessExecutionResult(
                DevTunnelCli.ResourceConflictsWithExistingExitCode,
                string.Empty,
                "Conflict with existing entity."
            )
        );
        executor.EnqueueRunResult(
            new ProcessExecutionResult(
                0,
                """{ "ports": [{ "portNumber": 7770, "protocol": "http" }] }""",
                string.Empty
            )
        );
        executor.EnqueueRunResult(new ProcessExecutionResult(0, Anonymous, string.Empty));

        DevTunnelPortStatus port = await Client(executor)
            .CreateOrReplacePortAsync(
                "sw-1.uks1",
                7770,
                new DevTunnelPortOptions { Protocol = "http", AllowAnonymous = true },
                TestContext.CancellationToken
            );

        Assert.AreEqual(7770, port.PortNumber);
        Assert.HasCount(3, executor.RunInvocations);
        Assert.IsFalse(executor.RunInvocations.Any(static run => run.Arguments.Contains("delete")));
    }

    [TestMethod]
    public async Task CreateOrReplacePortAsync_PortWithOtherProtocol_ReplacesIt()
    {
        FakeProcessExecutor executor = new();
        executor.EnqueueRunResult(
            new ProcessExecutionResult(
                DevTunnelCli.ResourceConflictsWithExistingExitCode,
                string.Empty,
                "Conflict with existing entity."
            )
        );
        executor.EnqueueRunResult(
            new ProcessExecutionResult(
                0,
                """{ "ports": [{ "portNumber": 7770, "protocol": "https" }] }""",
                string.Empty
            )
        );
        executor.EnqueueRunResult(new ProcessExecutionResult(0, "{}", string.Empty));
        executor.EnqueueRunResult(
            new ProcessExecutionResult(
                0,
                """{ "port": { "tunnelId": "sw-1.uks1", "portNumber": 7770, "protocol": "http" } }""",
                string.Empty
            )
        );

        DevTunnelPortStatus port = await Client(executor)
            .CreateOrReplacePortAsync(
                "sw-1.uks1",
                7770,
                new DevTunnelPortOptions { Protocol = "http" },
                TestContext.CancellationToken
            );

        Assert.AreEqual("http", port.Protocol);
        Assert.IsTrue(executor.RunInvocations.Any(static run => run.Arguments.Contains("delete")));
    }

    private static DevTunnelsClient Client(FakeProcessExecutor executor) =>
        new(new DevTunnelsClientOptions(), NullLogger<DevTunnelsClient>.Instance, executor);
}
