# DevTunnels.Client

A .NET client for [Microsoft Dev Tunnels](https://aka.ms/devtunnels). It drives the `devtunnel` CLI through a
typed, async, cancellable API: sign in, create a tunnel with a stable address, host it, and follow the host
through disconnects, reconnects and takeovers.

[![Build](https://img.shields.io/github/actions/workflow/status/Agash/DevTunnels.Client/build.yml?branch=main&style=flat-square&logo=github&logoColor=white)](https://github.com/Agash/DevTunnels.Client/actions)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg?style=flat-square)](https://opensource.org/licenses/MIT)

## Packages

| Package | Contents |
| --- | --- |
| `DevTunnels.Client` | The client, host sessions and the host monitor |
| `DevTunnels.Client.DependencyInjection` | `AddDevTunnelsClient` for `IServiceCollection` |
| `DevTunnels.Client.Installer` | Installs the `devtunnel` CLI (winget, Homebrew, or the official script) |

Targets .NET 9, 10 and 11. Native AOT compatible.

```bash
dotnet add package DevTunnels.Client
```

## Quick start

A tunnel with an ID you choose keeps its address for as long as it exists. Create it once, host it on every
run, and the public URL never changes.

```csharp
using DevTunnels.Client;
using DevTunnels.Client.Hosting;
using DevTunnels.Client.Ports;
using DevTunnels.Client.Tunnels;

DevTunnelsClient client = new();

DevTunnelCliProbeResult probe = await client.ProbeCliAsync();
if (!probe.IsInstalled || !probe.MeetsMinimumVersion)
{
    throw new InvalidOperationException(probe.FailureReason);
}

await client.EnsureLoggedInAsync();

// Create-or-update is idempotent: an existing tunnel keeps its address.
DevTunnelStatus tunnel = await client.CreateOrUpdateTunnelAsync(
    "my-app",
    new DevTunnelOptions { AllowAnonymous = true, ExpirationHours = 720 });

// Use the ID the service returns from here on: it carries the region.
await client.CreateOrReplacePortAsync(
    tunnel.TunnelId,
    5000,
    new DevTunnelPortOptions { Protocol = "http", AllowAnonymous = true });

await using IDevTunnelHostSession session = await client.StartHostSessionAsync(
    new DevTunnelHostStartOptions { TunnelId = tunnel.TunnelId });
await session.WaitForReadyAsync();
Console.WriteLine(session.PublicUrl);

await using DevTunnelHostMonitor monitor = new(client, session);
monitor.StatusChanged += (_, e) =>
    Console.WriteLine($"{e.Current.Connectivity}, taken over: {e.Current.IsTakenOver}");

await session.WaitForExitAsync();
```

## How tunnels behave

**Addresses.** A port's public URL is `https://<tunnel-id>-<port>.<region>.devtunnels.ms/`. The tunnel ID is
yours to choose; the service picks the region and reports the full ID (`my-app.usw2`). Pass that reported ID
to port and host calls. A tunnel belongs to the signed-in account, so two machines hosting the same ID
compete for it: derive the ID from the machine when each machine needs its own address.

**Expiration.** The service deletes a tunnel after a period without activity: 30 days unless
`ExpirationHours` says otherwise (1 to 720 hours, which is 30 days). The window slides, and every host
connection renews it. `ExpirationHours = null` leaves the service's value, and an existing tunnel's, alone.

**Access.** Tunnels and ports are private to their owner by default. `AllowAnonymous` on the tunnel or on a
port lets anyone with the URL connect. A browser opening the URL for the first time sees Microsoft's
anti-phishing page once and continues from there; non-`GET` requests, requests that do not accept
`text/html`, and requests with an `X-Tunnel-Skip-AntiPhishing-Page` header skip it, so webhooks and API
clients are unaffected.

**Protocols.** A port is `http`, `https` or `auto` (the default). The relay terminates TLS, so a local
plain-HTTP server uses `http`. The host rewrites the `Host` header of forwarded requests to `localhost`.

**Sign-in.** Microsoft Entra ID, Microsoft accounts and GitHub accounts. The CLI caches the login in the
system keychain for several days; `GetLoginStatusAsync` tells you when it has lapsed.

**Restarts.** `CreateOrUpdateTunnelAsync` and `CreateOrReplacePortAsync` leave a tunnel, port and access
that already match alone, so restarting an app does not drop anonymous access for a moment or change
anything a client depends on.

## Following a host

`IDevTunnelHostSession` is the `devtunnel host` process. `DevTunnelHostMonitor` follows it for as long as it
runs and reports a `DevTunnelHostStatus`:

| `Connectivity` | Meaning |
| --- | --- |
| `Starting` | The host has not connected yet |
| `Connected` | This host holds the relay and serves |
| `Disconnected` | This host lost the relay. The CLI reconnects by itself, so this can recover |
| `Stopped` | The host process ended. Start a new session |

`IsTakenOver` is true when this host is disconnected while the service still counts a host on the tunnel:
another machine or process on the account holds it. `Ports` maps each hosted port to its public URL, and
`HostConnections` is the service's count across the whole account.

The monitor reads the host's output first, because that is immediate and local, and asks the service every
`ReconcileInterval` (30 seconds by default) and whenever the output is not understood.
`WaitForConnectedAsync` completes on the first `Connected`; `ReconcileAsync` asks the service now.

Disposing the monitor leaves the session running. A host process also outlives a killed parent and keeps a
new host off the tunnel while it holds the relay, so record `IDevTunnelHostSession.ProcessId` and end a
leftover host before starting the next one.

## Requirements

The `devtunnel` CLI, version 1.0.1435 or newer. `ProbeCliAsync` reports whether it is installed and new
enough. The client looks for it in this order:

1. `DevTunnelsClientOptions.CliPathOverride`
2. The `DEVTUNNEL_CLI_PATH` environment variable
3. Known install locations, which need no `PATH` entry:
   - Windows: `%LOCALAPPDATA%\Microsoft\DevTunnels\`, `%LOCALAPPDATA%\Microsoft\WinGet\Links\`,
     `~/.devtunnels/bin/`, `~/bin/`
   - macOS and Linux: `~/bin/`, `~/.local/bin/`, `/usr/local/bin/`, `/opt/homebrew/bin/`
4. The system `PATH`

## Installing the CLI

```csharp
using DevTunnels.Client.Installer;

DevTunnelCliInstaller installer = new();
DevTunnelCliInstallResult result = await installer.InstallAsync();
Console.WriteLine(result.Success
    ? $"Installed with {result.InstallerUsed} at {result.InstalledPath}"
    : $"Install failed: {result.FailureReason}");
```

| Platform | Installer | Command |
| --- | --- | --- |
| Windows | `winget` | `winget install Microsoft.devtunnel` (user scope, no elevation) |
| macOS | `brew` | `brew install --cask devtunnel` |
| macOS without Homebrew, Linux | `curl` or `wget` | `curl -sL https://aka.ms/DevTunnelCliInstall \| bash` |

Every location an installer writes to is in the client's search list, so `ProbeCliAsync` finds a fresh
install right away without a restart or a `PATH` change. `UninstallAsync` removes it again.

## Dependency injection

```csharp
using DevTunnels.Client.DependencyInjection;

builder.Services.AddDevTunnelsClient(options => options.CommandTimeout = TimeSpan.FromSeconds(15));
```

`IDevTunnelsClient` is registered as a singleton.

## API

| Area | Members |
| --- | --- |
| CLI | `ProbeCliAsync`, `GetVersionAsync` |
| Sign-in | `GetLoginStatusAsync`, `EnsureLoggedInAsync`, `LoginAsync`, `LogoutAsync` |
| Tunnels | `CreateOrUpdateTunnelAsync`, `GetTunnelAsync`, `ListTunnelsAsync`, `DeleteTunnelAsync` |
| Ports | `CreateOrReplacePortAsync`, `UpdatePortAsync`, `GetPortListAsync`, `DeletePortAsync` |
| Access | `GetAccessAsync`, `CreateAccessAsync`, `ResetAccessAsync`, `GetAccessTokenAsync` |
| Hosting | `StartHostSessionAsync`, `IDevTunnelHostSession`, `DevTunnelHostMonitor` |
| Anything else | `ExecuteRawAsync(["user", "show", "--json", "--nologo"])` runs any CLI command |

`DevTunnelsClientOptions` sets the CLI path, the command timeout (10 seconds), retries (3 attempts, 2
seconds apart), the minimum CLI version and the preferred sign-in provider. CLI failures throw
`DevTunnelsCliException`.

## Building

```bash
dotnet build DevTunnels.Client.slnx -c Release
dotnet test DevTunnels.Client.slnx -c Release --no-build
dotnet csharpier check .
```

`examples/DevTunnels.Client.Example` is an interactive sample.

## License

MIT. See [`LICENSE.txt`](LICENSE.txt).
