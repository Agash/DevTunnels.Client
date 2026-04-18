# DevTunnels.Client

Async-first .NET 10 client library for [Azure Dev Tunnels](https://aka.ms/devtunnels), wrapping the `devtunnel` CLI with a typed, cancellation-aware API.

[![Build](https://img.shields.io/github/actions/workflow/status/Agash/DevTunnels.Client/build.yml?branch=main&style=flat-square&logo=github&logoColor=white)](https://github.com/Agash/DevTunnels.Client/actions)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg?style=flat-square)](https://opensource.org/licenses/MIT)

## Packages

| Package | Description |
|---------|-------------|
| `DevTunnels.Client` | Core typed CLI wrapper |
| `DevTunnels.Client.DependencyInjection` | `IServiceCollection` registration helpers |
| `DevTunnels.Client.Installer` | Platform-aware devtunnel CLI installer (winget / Homebrew / curl / wget) |

## Requirements

The `devtunnel` CLI must be installed. The library searches the following locations in order:

1. `DevTunnelsClientOptions.CliPathOverride` — explicit path set in code
2. `DEVTUNNEL_CLI_PATH` environment variable
3. Well-known install paths (checked by default, no PATH entry required):
   - **Windows** — `%LOCALAPPDATA%\Microsoft\DevTunnels\`, `~/.devtunnels/bin/`, `~/bin/`
   - **macOS** — `/opt/homebrew/bin/` (Homebrew), `/usr/local/bin/`, `~/bin/`
   - **Linux** — `~/.local/bin/`, `~/bin/`, `/usr/local/bin/`
4. System `PATH`

**Minimum CLI version: 1.0.1435**

The library enforces this at startup. If the installed CLI is older, `ProbeCliAsync()` returns
`MeetsMinimumVersion = false` and operations that require the CLI will throw.

Check your installed version:

```bash
devtunnel --version
# expected output: Tunnel CLI version: 1.0.1435+<commit>
```

> The library was developed and integration-tested against **1.0.1435**. Later versions are
> expected to be compatible; earlier versions are not supported.
>
> The minimum can be overridden per-instance via `DevTunnelsClientOptions.MinimumSupportedVersion`
> if you need to pin a different floor.

**Install / update:**

```bash
# Windows
winget install Microsoft.DevTunnel
# or update an existing install:
winget upgrade Microsoft.DevTunnel

# macOS
brew install devtunnel
# or update:
brew upgrade devtunnel

# Linux / other
curl -sL https://aka.ms/DevTunnelCliInstall | bash
```

Or follow the [official install guide](https://learn.microsoft.com/azure/developer/dev-tunnels/get-started).

## Install

```bash
dotnet add package DevTunnels.Client
dotnet add package DevTunnels.Client.DependencyInjection  # optional
```

## Quick Start

```csharp
using DevTunnels.Client;

var client = new DevTunnelsClient(new DevTunnelsClientOptions
{
    CommandTimeout = TimeSpan.FromSeconds(15)
});

// Check CLI is available
var probe = await client.ProbeCliAsync();
Console.WriteLine($"devtunnel {probe.Version} installed: {probe.IsInstalled}");

// Login
await client.LoginAsync(LoginProvider.GitHub);

// Create a persistent tunnel with a port
await client.CreateOrUpdateTunnelAsync("my-tunnel", new DevTunnelOptions
{
    Description = "My development tunnel",
    AllowAnonymous = true,
});
await client.CreateOrReplacePortAsync("my-tunnel", 5000, new DevTunnelPortOptions
{
    Protocol = "http",  // devtunnel terminates TLS; local service speaks plain HTTP
});

// Host the tunnel and wait for it to be ready
using var session = await client.StartHostSessionAsync(
    new DevTunnelHostStartOptions { TunnelId = "my-tunnel" },
    CancellationToken.None);

await session.WaitForReadyAsync(CancellationToken.None);
Console.WriteLine($"Public URL: {session.PublicUrl}");

// Keep alive until cancelled
await session.WaitForExitAsync(CancellationToken.None);
```

## Dependency Injection

```csharp
using DevTunnels.Client.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddDevTunnelsClient(options =>
{
    options.CommandTimeout = TimeSpan.FromSeconds(15);
});
```

## CLI Installer

`DevTunnels.Client.Installer` provides `IDevTunnelCliInstaller` / `DevTunnelCliInstaller` to
detect the platform-appropriate package manager and install the `devtunnel` CLI automatically.

```csharp
using DevTunnels.Client.Installer;

var installer = new DevTunnelCliInstaller();

string? detected = await installer.DetectInstallerAsync();
Console.WriteLine($"Installer: {detected ?? "none"}");

DevTunnelCliInstallResult result = await installer.InstallAsync();
if (result.Success)
{
    Console.WriteLine($"Installed via {result.InstallerUsed}.");
}
else
{
    Console.WriteLine($"Install failed: {result.FailureReason}");
}
```

Supported installers per platform:

| Platform | Installer | Install command |
|----------|-----------|-----------------|
| Windows | `winget` | `winget install Microsoft.devtunnel` |
| macOS | `brew` | `brew install --cask devtunnel` |
| Linux | `curl` | `curl -sL https://aka.ms/DevTunnelCliInstall \| bash` |
| Linux | `wget` | `wget -qO- https://aka.ms/DevTunnelCliInstall \| bash` |

Install it:

```bash
dotnet add package DevTunnels.Client.Installer
```

## API Surface

### Authentication

| Method | Description |
|--------|-------------|
| `ProbeCliAsync()` | Check that `devtunnel` is installed and get its version |
| `GetLoginStatusAsync()` | Check current login state |
| `EnsureLoggedInAsync(provider?)` | Login if not already, coalescing concurrent callers |
| `LoginAsync(provider)` | Interactive browser login (GitHub or Microsoft) |
| `LogoutAsync()` | Sign out |

### Tunnels

| Method | Description |
|--------|-------------|
| `CreateOrUpdateTunnelAsync(id, options)` | Create or update a named tunnel |
| `GetTunnelAsync(id)` | Retrieve tunnel metadata |
| `ListTunnelsAsync()` | List all tunnels for the account |
| `DeleteTunnelAsync(id)` | Delete a tunnel |

### Ports

| Method | Description |
|--------|-------------|
| `CreateOrReplacePortAsync(tunnelId, port, options)` | Create or replace a port |
| `UpdatePortAsync(tunnelId, port, options)` | Update port settings in-place |
| `GetPortListAsync(tunnelId)` | List ports on a tunnel |
| `DeletePortAsync(tunnelId, port)` | Remove a port |

### Access

| Method | Description |
|--------|-------------|
| `GetAccessAsync(tunnelId, port?)` | Get access policies |
| `CreateAccessAsync(tunnelId, anonymous, deny?, port?)` | Add an access entry |
| `ResetAccessAsync(tunnelId, port?)` | Reset to default access |
| `GetAccessTokenAsync(tunnelId, scopes?)` | Issue a scoped access token |

### Host Session

| Member | Description |
|--------|-------------|
| `StartHostSessionAsync(options, ct)` | Start a `devtunnel host` process |
| `IDevTunnelHostSession.WaitForReadyAsync(ct)` | Wait until the public URL is available |
| `IDevTunnelHostSession.WaitForExitAsync(ct)` | Wait until the session ends |
| `IDevTunnelHostSession.PublicUrl` | Live public HTTPS URL (standard-port, no explicit port in URL) |
| `IDevTunnelHostSession.State` | Current session state |
| `IDevTunnelHostSession.OutputReceived` | Raw CLI output event stream |
| `IDevTunnelHostSession.StopAsync(ct)` | Gracefully stop the session |

### Escape Hatch

```csharp
// Run any devtunnel command and capture stdout/stderr
var result = await client.ExecuteRawAsync(["user", "show", "--json", "--nologo"]);
Console.WriteLine(result.StandardOutput);
```

## Repository Layout

```
src/
  DevTunnels.Client/                      # core library
  DevTunnels.Client.DependencyInjection/  # IServiceCollection helpers
tests/
  DevTunnels.Client.Tests/                # unit tests
examples/
  DevTunnels.Client.Example/              # interactive CLI sample (Spectre.Console)
```

## Build

```bash
dotnet restore DevTunnels.Client.slnx
dotnet build DevTunnels.Client.slnx -c Release
dotnet test DevTunnels.Client.slnx -c Release --no-build
```

## License

MIT. See [`LICENSE.txt`](LICENSE.txt).
