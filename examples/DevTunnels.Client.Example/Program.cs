using System.Text;
using System.Text.Json;
using DevTunnels.Client;
using DevTunnels.Client.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Spectre.Console;

// ─────────────────────────────────────────────────────────────────────────────
// Bootstrap
// ─────────────────────────────────────────────────────────────────────────────
HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
builder.Logging.SetMinimumLevel(LogLevel.Warning);
builder.Services.AddDevTunnelsClient(options =>
{
    // Reasonable timeout for one-shot CLI commands.
    options.CommandTimeout = TimeSpan.FromSeconds(15);
    options.PreferredLoginProvider = LoginProvider.GitHub;
});

using IHost host = builder.Build();
IDevTunnelsClient client = host.Services.GetRequiredService<IDevTunnelsClient>();

// ─────────────────────────────────────────────────────────────────────────────
// Header
// ─────────────────────────────────────────────────────────────────────────────
AnsiConsole.Clear();
AnsiConsole.Write(
    new FigletText("DevTunnels")
        .Centered()
        .Color(Color.DodgerBlue1));

AnsiConsole.Write(
    new Rule("[grey]Azure Dev Tunnels Client — interactive demo[/]")
        .RuleStyle(Style.Parse("grey")));

AnsiConsole.WriteLine();

// ─────────────────────────────────────────────────────────────────────────────
// Main menu loop
// ─────────────────────────────────────────────────────────────────────────────
const string OptProbe = "probe    — Check CLI installation and version";
const string OptLogin = "login    — Check login status";
const string OptEnsure = "ensure   — Ensure the CLI is logged in";
const string OptList = "list     — List your tunnels";
const string OptTunnel = "tunnel   — Create, inspect, and delete a tunnel";
const string OptPort = "port     — Manage ports on a tunnel";
const string OptAccess = "access   — Inspect access policies";
const string OptToken = "token    — Issue a connect-scoped access token";
const string OptWebhook = "webhook  — Full webhook ingress setup (end-to-end)";
const string OptRaw = "raw      — Execute a raw CLI command";
const string OptExit = "exit     — Quit";

while (true)
{
    string choice = AnsiConsole.Prompt(
        new SelectionPrompt<string>()
            .Title("[bold yellow]Select a scenario:[/]")
            .HighlightStyle(Style.Parse("dodgerblue1 bold"))
            .PageSize(12)
            .AddChoices([
                OptProbe, OptLogin, OptEnsure, OptList,
                OptTunnel, OptPort, OptAccess, OptToken,
                OptWebhook, OptRaw, OptExit,
            ]));

    AnsiConsole.WriteLine();

    if (choice == OptExit)
    {
        break;
    }

    try
    {
        switch (choice)
        {
            case OptProbe: await DemoProbeCliAsync(client); break;
            case OptLogin: await DemoCheckLoginAsync(client); break;
            case OptEnsure: await DemoEnsureLoggedInAsync(client); break;
            case OptList: await DemoListTunnelsAsync(client); break;
            case OptTunnel: await DemoManageTunnelAsync(client); break;
            case OptPort: await DemoManagePortAsync(client); break;
            case OptAccess: await DemoInspectAccessAsync(client); break;
            case OptToken: await DemoIssueAccessTokenAsync(client); break;
            case OptWebhook: await DemoWebhookSetupAsync(client); break;
            case OptRaw: await DemoRawCommandAsync(client); break;
        }
    }
    catch (Exception ex)
    {
        WriteError(ex.Message);
    }

    AnsiConsole.WriteLine();
    AnsiConsole.Write(new Rule().RuleStyle(Style.Parse("grey dim")));
    AnsiConsole.WriteLine();
}

AnsiConsole.MarkupLine("[grey]Bye![/]");
return;

// ─────────────────────────────────────────────────────────────────────────────
// Scenario 1: Probe CLI
// ─────────────────────────────────────────────────────────────────────────────
static async Task DemoProbeCliAsync(IDevTunnelsClient client)
{
    AnsiConsole.MarkupLine("[bold]Probing for devtunnel CLI...[/]");
    AnsiConsole.WriteLine();

    DevTunnelCliProbeResult result = await AnsiConsole
        .Status()
        .Spinner(Spinner.Known.Dots)
        .SpinnerStyle(Style.Parse("dodgerblue1"))
        .StartAsync("Searching known install paths and PATH...", _ => client.ProbeCliAsync().AsTask());

    Table table = new Table()
        .Border(TableBorder.Rounded)
        .BorderStyle(Style.Parse("grey"))
        .AddColumn("[grey]Property[/]")
        .AddColumn("[grey]Value[/]");

    _ = table.AddRow("Installed", result.IsInstalled ? "[green]Yes[/]" : "[red]No[/]");
    _ = table.AddRow("Path", result.ResolvedPath is not null ? $"[grey]{Markup.Escape(result.ResolvedPath)}[/]" : "[grey dim]<not found>[/]");
    _ = table.AddRow("Version", result.Version?.ToString() ?? "[grey dim]<unknown>[/]");
    _ = table.AddRow("Meets minimum", result.MeetsMinimumVersion ? "[green]Yes[/]" : "[yellow]No[/]");

    if (!string.IsNullOrWhiteSpace(result.FailureReason))
    {
        _ = table.AddRow("Failure reason", $"[red]{Markup.Escape(result.FailureReason)}[/]");
    }

    AnsiConsole.Write(table);

    if (!result.IsInstalled)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Write(
            new Panel(
                "[yellow]The devtunnel CLI was not found.\n\n" +
                "Install from: [link]https://aka.ms/devtunnel/install[/]\n" +
                "Then authenticate: [bold]devtunnel user login[/][/]")
            .Header("[yellow] CLI not installed [/]")
            .BorderStyle(Style.Parse("yellow")));
    }
    else if (!result.MeetsMinimumVersion)
    {
        WriteWarning($"Version {result.Version} is below the minimum supported version. Please update.");
    }
    else
    {
        AnsiConsole.MarkupLine("[green]CLI is ready.[/]");
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Scenario 2: Check login status
// ─────────────────────────────────────────────────────────────────────────────
static async Task DemoCheckLoginAsync(IDevTunnelsClient client)
{
    AnsiConsole.MarkupLine("[bold]Checking CLI login status...[/]");
    AnsiConsole.WriteLine();

    DevTunnelLoginStatus status = await AnsiConsole
        .Status()
        .Spinner(Spinner.Known.Dots)
        .SpinnerStyle(Style.Parse("dodgerblue1"))
        .StartAsync("Running devtunnel user show...", _ => client.GetLoginStatusAsync().AsTask());

    RenderLoginStatus(status);
}

// ─────────────────────────────────────────────────────────────────────────────
// Scenario 3: Ensure logged in
// ─────────────────────────────────────────────────────────────────────────────
static async Task DemoEnsureLoggedInAsync(IDevTunnelsClient client)
{
    AnsiConsole.MarkupLine("[bold]Ensuring the CLI is logged in...[/]");
    AnsiConsole.WriteLine();

    LoginProvider provider = AnsiConsole.Prompt(
        new SelectionPrompt<LoginProvider>()
            .Title("[grey]Preferred identity provider:[/]")
            .HighlightStyle(Style.Parse("dodgerblue1"))
            .AddChoices(LoginProvider.GitHub, LoginProvider.Microsoft));

    AnsiConsole.WriteLine();

    // Check current status first — avoid opening a browser if already logged in.
    DevTunnelLoginStatus current = await AnsiConsole
        .Status()
        .Spinner(Spinner.Known.Dots)
        .SpinnerStyle(Style.Parse("dodgerblue1"))
        .StartAsync("Checking current login state...", _ => client.GetLoginStatusAsync().AsTask());

    if (current.IsLoggedIn && (current.Provider is null || current.Provider == provider))
    {
        AnsiConsole.MarkupLine("[green]Already logged in — no action needed.[/]");
        AnsiConsole.WriteLine();
        RenderLoginStatus(current);
        return;
    }

    AnsiConsole.MarkupLine("[yellow]Not logged in (or different provider).[/]");
    AnsiConsole.MarkupLine("[grey]A browser window will open. Complete authentication and return here.[/]");
    AnsiConsole.WriteLine();

    if (!AnsiConsole.Confirm("Open login browser now?"))
    {
        AnsiConsole.MarkupLine("[grey]Login skipped.[/]");
        return;
    }

    DevTunnelLoginStatus result = await client.LoginAsync(provider);
    AnsiConsole.WriteLine();
    RenderLoginStatus(result);
}

// ─────────────────────────────────────────────────────────────────────────────
// Scenario 4: List tunnels
// ─────────────────────────────────────────────────────────────────────────────
static async Task DemoListTunnelsAsync(IDevTunnelsClient client)
{
    AnsiConsole.MarkupLine("[bold]Listing tunnels for the current account...[/]");
    AnsiConsole.WriteLine();

    DevTunnelList tunnelList = await AnsiConsole
        .Status()
        .Spinner(Spinner.Known.Dots)
        .SpinnerStyle(Style.Parse("dodgerblue1"))
        .StartAsync("Running devtunnel list...", _ => client.ListTunnelsAsync().AsTask());

    if (tunnelList.Tunnels.Count == 0)
    {
        AnsiConsole.MarkupLine("[grey]No tunnels found for this account.[/]");
        return;
    }

    Table table = new Table()
        .Border(TableBorder.Rounded)
        .BorderStyle(Style.Parse("grey"))
        .Title("[bold]Your tunnels[/]")
        .AddColumn("[grey]Tunnel ID[/]")
        .AddColumn("[grey]Description[/]")
        .AddColumn("[grey]Host connections[/]")
        .AddColumn("[grey]Labels[/]");

    foreach (DevTunnelStatus t in tunnelList.Tunnels)
    {
        string labels = t.Labels.Count > 0 ? string.Join(", ", t.Labels) : "[grey dim]—[/]";
        string desc = !string.IsNullOrWhiteSpace(t.Description) ? Markup.Escape(t.Description) : "[grey dim]—[/]";

        _ = table.AddRow(
            $"[bold]{Markup.Escape(t.TunnelId)}[/]",
            desc,
            t.HostConnections > 0 ? $"[green]{t.HostConnections}[/]" : $"[grey]{t.HostConnections}[/]",
            labels);
    }

    AnsiConsole.Write(table);
}

// ─────────────────────────────────────────────────────────────────────────────
// Scenario 5: Manage a tunnel (create / show / delete)
// ─────────────────────────────────────────────────────────────────────────────
static async Task DemoManageTunnelAsync(IDevTunnelsClient client)
{
    AnsiConsole.MarkupLine("[bold]Tunnel lifecycle demo[/]");
    AnsiConsole.WriteLine();

    string tunnelId = AnsiConsole.Ask<string>(
        "Tunnel ID [grey](e.g. my-demo-tunnel-01)[/]:",
        "my-demo-tunnel-01");

    if (!DevTunnelValidation.IsValidTunnelId(tunnelId))
    {
        WriteError($"'{tunnelId}' is not a valid tunnel ID. Use lowercase letters, numbers, and hyphens (3–60 chars).");
        return;
    }

    AnsiConsole.WriteLine();

    // Create or update
    DevTunnelStatus status = await AnsiConsole
        .Status()
        .Spinner(Spinner.Known.Dots)
        .SpinnerStyle(Style.Parse("dodgerblue1"))
        .StartAsync($"Creating or updating tunnel '{tunnelId}'...",
            _ => client.CreateOrUpdateTunnelAsync(tunnelId, new DevTunnelOptions
            {
                Description = "DevTunnels.Client demo tunnel",
                AllowAnonymous = false,
                Labels = ["demo"],
            }).AsTask());

    AnsiConsole.MarkupLine($"[green]Tunnel ready:[/] [bold]{Markup.Escape(status.TunnelId)}[/]");
    RenderTunnelStatus(status);
    AnsiConsole.WriteLine();

    // Show (re-fetch)
    DevTunnelStatus fetched = await AnsiConsole
        .Status()
        .Spinner(Spinner.Known.Dots)
        .SpinnerStyle(Style.Parse("dodgerblue1"))
        .StartAsync($"Re-fetching tunnel '{tunnelId}'...",
            _ => client.GetTunnelAsync(tunnelId).AsTask());

    AnsiConsole.MarkupLine("[grey]Re-fetched from CLI:[/]");
    RenderTunnelStatus(fetched);
    AnsiConsole.WriteLine();

    // Offer to delete
    if (AnsiConsole.Confirm($"[yellow]Delete tunnel '{tunnelId}' now?[/]", defaultValue: false))
    {
        DevTunnelDeleteResult deleteResult = await AnsiConsole
            .Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("red"))
            .StartAsync($"Deleting tunnel '{tunnelId}'...",
                _ => client.DeleteTunnelAsync(tunnelId).AsTask());

        AnsiConsole.MarkupLine($"[green]Deleted:[/] [grey]{Markup.Escape(deleteResult.DeletedTunnel)}[/]");
    }
    else
    {
        AnsiConsole.MarkupLine("[grey]Tunnel left in place.[/]");
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Scenario 6: Manage ports
// ─────────────────────────────────────────────────────────────────────────────
static async Task DemoManagePortAsync(IDevTunnelsClient client)
{
    AnsiConsole.MarkupLine("[bold]Port management demo[/]");
    AnsiConsole.WriteLine();

    string tunnelId = AnsiConsole.Ask<string>("Tunnel ID:", "my-demo-tunnel-01");

    if (!DevTunnelValidation.IsValidTunnelId(tunnelId))
    {
        WriteError($"'{tunnelId}' is not a valid tunnel ID.");
        return;
    }

    int portNumber = AnsiConsole.Ask("Local port number to expose:", 5000);

    if (portNumber is <= 0 or > 65535)
    {
        WriteError("Port must be between 1 and 65535.");
        return;
    }

    AnsiConsole.WriteLine();

    // List existing ports first
    DevTunnelPortList portList = await AnsiConsole
        .Status()
        .Spinner(Spinner.Known.Dots)
        .SpinnerStyle(Style.Parse("dodgerblue1"))
        .StartAsync("Listing existing ports...",
            _ => client.GetPortListAsync(tunnelId).AsTask());

    if (portList.Ports.Count > 0)
    {
        Table portTable = new Table()
            .Border(TableBorder.Simple)
            .BorderStyle(Style.Parse("grey"))
            .Title("[grey]Existing ports[/]")
            .AddColumn("[grey]Port[/]")
            .AddColumn("[grey]Protocol[/]")
            .AddColumn("[grey]Public URI[/]");

        foreach (DevTunnelPort p in portList.Ports)
        {
            _ = portTable.AddRow(
                p.PortNumber.ToString(),
                p.Protocol,
                p.PortUri?.ToString() ?? "[grey dim]—[/]");
        }

        AnsiConsole.Write(portTable);
        AnsiConsole.WriteLine();
    }

    bool allowAnon = AnsiConsole.Confirm("Allow anonymous (public) access on this port?", defaultValue: true);

    DevTunnelPortStatus portStatus = await AnsiConsole
        .Status()
        .Spinner(Spinner.Known.Dots)
        .SpinnerStyle(Style.Parse("dodgerblue1"))
        .StartAsync($"Creating port {portNumber} on tunnel '{tunnelId}'...",
            _ => client.CreateOrReplacePortAsync(tunnelId, portNumber, new DevTunnelPortOptions
            {
                Protocol = "https",
                Description = "Webhook ingress port",
                AllowAnonymous = allowAnon,
            }).AsTask());

    AnsiConsole.MarkupLine($"[green]Port {portStatus.PortNumber} created on tunnel [bold]{Markup.Escape(portStatus.TunnelId)}[/].[/]");
    AnsiConsole.MarkupLine($"  Protocol: [grey]{portStatus.Protocol}[/]");
    AnsiConsole.MarkupLine($"  Anonymous access: [grey]{(allowAnon ? "allowed" : "denied")}[/]");
}

// ─────────────────────────────────────────────────────────────────────────────
// Scenario 7: Inspect access policies
// ─────────────────────────────────────────────────────────────────────────────
static async Task DemoInspectAccessAsync(IDevTunnelsClient client)
{
    AnsiConsole.MarkupLine("[bold]Access policy inspection[/]");
    AnsiConsole.WriteLine();

    string tunnelId = AnsiConsole.Ask<string>("Tunnel ID:", "my-demo-tunnel-01");
    string portInput = AnsiConsole.Ask<string>("Port number [grey](leave empty for tunnel-level)[/]:", string.Empty);
    int? portNumber = string.IsNullOrWhiteSpace(portInput) ? null : int.Parse(portInput);

    AnsiConsole.WriteLine();

    DevTunnelAccessStatus access = await AnsiConsole
        .Status()
        .Spinner(Spinner.Known.Dots)
        .SpinnerStyle(Style.Parse("dodgerblue1"))
        .StartAsync("Fetching access policies...",
            _ => client.GetAccessAsync(tunnelId, portNumber).AsTask());

    string target = portNumber.HasValue
        ? $"port {portNumber} on tunnel '{tunnelId}'"
        : $"tunnel '{tunnelId}'";

    if (access.AccessControlEntries.Count == 0)
    {
        AnsiConsole.MarkupLine($"[grey]No access control entries found for {Markup.Escape(target)}.[/]");
        return;
    }

    Table table = new Table()
        .Border(TableBorder.Rounded)
        .BorderStyle(Style.Parse("grey"))
        .Title($"[bold]Access policy: {Markup.Escape(target)}[/]")
        .AddColumn("[grey]Type[/]")
        .AddColumn("[grey]Deny[/]")
        .AddColumn("[grey]Inherited[/]")
        .AddColumn("[grey]Scopes[/]");

    foreach (DevTunnelAccessEntry entry in access.AccessControlEntries)
    {
        _ = table.AddRow(
            entry.Type,
            entry.IsDeny ? "[red]Yes[/]" : "[green]No[/]",
            entry.IsInherited ? "[grey]Yes[/]" : "No",
            entry.Scopes.Count > 0 ? string.Join(", ", entry.Scopes) : "[grey dim]—[/]");
    }

    AnsiConsole.Write(table);
}

// ─────────────────────────────────────────────────────────────────────────────
// Scenario 8: Full webhook ingress setup (end-to-end)
//
//   1. Probe the CLI — block if not installed or version too old
//   2. Ensure the CLI is logged in — open browser if needed
//   3. Create or reuse a stable named tunnel
//   4. Create or replace the webhook port with anonymous access
//   5. Start a long-running devtunnel host session
//   6. Wait for the public URL
//   7. Display the webhook base URL for provider dashboard registration
//   8. Run until Ctrl+C, then stop cleanly
// ─────────────────────────────────────────────────────────────────────────────
static async Task DemoWebhookSetupAsync(IDevTunnelsClient client)
{
    AnsiConsole.Write(
        new Panel(
            "[bold]This scenario walks through a full managed tunnel setup[/]\n" +
            "to expose a local webhook ingress endpoint to providers\n" +
            "like [cyan]Ko-fi[/], [cyan]Patreon[/], [cyan]Fourthwall[/], and [cyan]Kick[/].")
        .Header("[bold dodgerblue1] Webhook Ingress Setup [/]")
        .BorderStyle(Style.Parse("dodgerblue1")));

    AnsiConsole.WriteLine();

    // ── Step 1: Probe CLI ────────────────────────────────────────────────────
    WriteStep(1, "Probe CLI");

    DevTunnelCliProbeResult probe = await AnsiConsole
        .Status()
        .Spinner(Spinner.Known.Dots)
        .SpinnerStyle(Style.Parse("dodgerblue1"))
        .StartAsync("Probing for devtunnel CLI...", _ => client.ProbeCliAsync().AsTask());

    if (!probe.IsInstalled)
    {
        AnsiConsole.Write(
            new Panel(
                "[yellow]devtunnel CLI not found.\n\n" +
                "Download: [link]https://aka.ms/devtunnel/install[/]\n" +
                "After installing, restart this demo.[/]")
            .Header("[yellow] CLI not installed [/]")
            .BorderStyle(Style.Parse("yellow")));
        return;
    }

    if (!probe.MeetsMinimumVersion)
    {
        WriteWarning($"CLI version {probe.Version} is below minimum. Please update.");
        return;
    }

    AnsiConsole.MarkupLine($"  [green]✓[/] v[bold]{probe.Version}[/] at [grey]{Markup.Escape(probe.ResolvedPath ?? string.Empty)}[/]");
    AnsiConsole.WriteLine();

    // ── Step 2: Ensure logged in ─────────────────────────────────────────────
    WriteStep(2, "Verify login");

    DevTunnelLoginStatus loginStatus = await AnsiConsole
        .Status()
        .Spinner(Spinner.Known.Dots)
        .SpinnerStyle(Style.Parse("dodgerblue1"))
        .StartAsync("Checking login state...", _ => client.GetLoginStatusAsync().AsTask());

    if (!loginStatus.IsLoggedIn)
    {
        AnsiConsole.MarkupLine("  [yellow]Not logged in.[/]");

        LoginProvider loginProvider = AnsiConsole.Prompt(
            new SelectionPrompt<LoginProvider>()
                .Title("  Log in with:")
                .HighlightStyle(Style.Parse("dodgerblue1"))
                .AddChoices(LoginProvider.GitHub, LoginProvider.Microsoft));

        AnsiConsole.MarkupLine("  [grey]A browser window will open. Complete authentication and return here.[/]");

        if (!AnsiConsole.Confirm("  Open browser now?"))
        {
            AnsiConsole.MarkupLine("[grey]Login skipped — cannot continue.[/]");
            return;
        }

        loginStatus = await client.LoginAsync(loginProvider);
    }

    if (!loginStatus.IsLoggedIn)
    {
        WriteError("Login did not succeed. Cannot continue.");
        return;
    }

    AnsiConsole.MarkupLine($"  [green]✓[/] Logged in as [bold]{Markup.Escape(loginStatus.Username ?? "unknown")}[/] via [grey]{loginStatus.Provider}[/]");
    AnsiConsole.WriteLine();

    // ── Step 3: Configure tunnel ─────────────────────────────────────────────
    WriteStep(3, "Configure tunnel and port");

    string tunnelId = AnsiConsole.Ask<string>(
        "  Tunnel ID [grey](stable identity — reused across restarts)[/]:",
        "my-app-webhooks-01");

    if (!DevTunnelValidation.IsValidTunnelId(tunnelId))
    {
        WriteError($"'{tunnelId}' is not a valid tunnel ID.");
        return;
    }

    int localPort = AnsiConsole.Ask(
        "  Local HTTP port [grey](the port your webhook listener binds to)[/]:",
        5000);

    AnsiConsole.WriteLine();

    DevTunnelStatus tunnelStatus = await AnsiConsole
        .Status()
        .Spinner(Spinner.Known.Dots)
        .SpinnerStyle(Style.Parse("dodgerblue1"))
        .StartAsync($"Creating or updating tunnel '{tunnelId}'...",
            _ => client.CreateOrUpdateTunnelAsync(tunnelId, new DevTunnelOptions
            {
                // Tunnel-level anonymous access is off; port-level anonymous is set below.
                Description = "Managed webhook ingress",
                AllowAnonymous = false,
                Labels = ["webhooks"],
            }).AsTask());

    AnsiConsole.MarkupLine($"  [green]✓[/] Tunnel [bold]{Markup.Escape(tunnelStatus.TunnelId)}[/]");

    DevTunnelPortStatus portStatus = await AnsiConsole
        .Status()
        .Spinner(Spinner.Known.Dots)
        .SpinnerStyle(Style.Parse("dodgerblue1"))
        .StartAsync($"Creating or replacing port {localPort}...",
            _ => client.CreateOrReplacePortAsync(tunnelId, localPort, new DevTunnelPortOptions
            {
                // "http" = devtunnel terminates TLS on the public side and forwards
                // plain HTTP to the local port. "https" would make devtunnel open a
                // TLS connection to our local Kestrel, which speaks plain HTTP and
                // would immediately reject the TLS handshake with a 502.
                Protocol = "http",
                Description = "Webhook HTTP listener",
                AllowAnonymous = true,   // providers POST without a token
            }).AsTask());

    AnsiConsole.MarkupLine($"  [green]✓[/] Port {portStatus.PortNumber} (http→https via devtunnel TLS termination, anonymous allowed)");
    AnsiConsole.WriteLine();

    // ── Start Kestrel webhook listener ───────────────────────────────────────
    // Kestrel binds by port only and does not perform Host-header matching, so
    // requests proxied by devtunnel (which preserves the tunnel hostname as the
    // Host header) are accepted without any special configuration.
    // HttpListener/HTTP.SYS was not suitable because it rejected those requests.
    WebApplication? webhookApp = null;
    try
    {
        WebApplicationBuilder webBuilder = WebApplication.CreateSlimBuilder();
        // Explicit IPv4 loopback — "localhost" can resolve to IPv6 ::1 on some
        // Windows configurations, while devtunnel's SSH port-forwarder connects
        // to 127.0.0.1 (IPv4). Binding to 127.0.0.1 directly avoids this mismatch.
        webBuilder.WebHost.UseUrls($"http://127.0.0.1:{localPort}");
        webBuilder.Logging.ClearProviders(); // keep Kestrel startup noise off the CLI

        webhookApp = webBuilder.Build();

        webhookApp.MapMethods("/{**catchAll}",
            ["GET", "POST", "PUT", "PATCH", "DELETE"],
            async (HttpRequest req) =>
            {
                string body = string.Empty;
                if (req.ContentLength is > 0 || req.Headers.TransferEncoding.Count > 0)
                {
                    using var reader = new StreamReader(req.Body, Encoding.UTF8, leaveOpen: true);
                    body = await reader.ReadToEndAsync().ConfigureAwait(false);
                }

                var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var h in req.Headers)
                    headers[h.Key] = h.Value.ToString();

                DisplayIncomingWebhook(req.Method, req.Path.ToString(), headers, body, req.ContentType);

                return Results.Json(new { status = "ok" });
            });

        await webhookApp.StartAsync().ConfigureAwait(false);
        AnsiConsole.MarkupLine($"  [green]✓[/] Local webhook receiver on [bold]http://localhost:{localPort}/[/]");
        AnsiConsole.MarkupLine("  [grey dim]Incoming payloads will be printed as they arrive.[/]");
    }
    catch (Exception ex)
    {
        WriteWarning($"Could not start local listener on port {localPort}: {ex.Message}");
        AnsiConsole.MarkupLine("  [grey]Incoming payloads visible in the devtunnel inspect URL only.[/]");
    }

    AnsiConsole.WriteLine();

    // ── Step 4: Start host session ───────────────────────────────────────────
    WriteStep(4, "Start devtunnel host session");
    AnsiConsole.MarkupLine("  [grey]Starting devtunnel host. Waiting for the public URL...[/]");
    AnsiConsole.MarkupLine("  [grey]Press [bold]Ctrl+C[/] to stop.[/]");
    AnsiConsole.WriteLine();

    using var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

    IDevTunnelHostSession session = await client.StartHostSessionAsync(
        new DevTunnelHostStartOptions
        {
            TunnelId = tunnelId,
            ReadyTimeout = TimeSpan.FromSeconds(30),
        },
        cts.Token);

    // Stream startup output lines (capped to avoid scroll noise)
    int lineCount = 0;
    session.OutputReceived += (_, e) =>
    {
        if (lineCount++ < 15)
        {
            AnsiConsole.MarkupLine($"  [grey dim]{Markup.Escape(e.Line)}[/]");
        }
    };

    try
    {
        using var readyCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token);
        readyCts.CancelAfter(TimeSpan.FromSeconds(30));
        await session.WaitForReadyAsync(readyCts.Token);
    }
    catch (OperationCanceledException) when (!cts.IsCancellationRequested)
    {
        AnsiConsole.WriteLine();
        WriteError("Timed out waiting for the tunnel host to become ready (30 s).");
        await session.StopAsync(CancellationToken.None);
        await session.DisposeAsync();
        return;
    }
    catch (OperationCanceledException)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[grey]Interrupted before tunnel was ready.[/]");
        await session.StopAsync(CancellationToken.None);
        await session.DisposeAsync();
        return;
    }
    catch (Exception ex)
    {
        AnsiConsole.WriteLine();
        WriteError($"Host session failed: {ex.Message}");
        await session.DisposeAsync();
        return;
    }

    // ── Step 5: Display webhook URLs ─────────────────────────────────────────
    AnsiConsole.WriteLine();
    string publicBase = session.PublicUrl?.ToString().TrimEnd('/') ?? "<unknown>";
    string webhookBase = $"{publicBase}/webhooks";

    AnsiConsole.Write(
        new Panel(
            $"[bold green]{Markup.Escape(webhookBase)}/{{providerName}}/{{instanceId}}/{{webhookKey}}[/]\n\n" +
            "[grey]Copy this base URL to your provider dashboards.[/]\n\n" +
            "Example registrations:\n\n" +
            $"  [cyan]Ko-fi[/]       →  {Markup.Escape(webhookBase)}/ko-fi/ko-fi1/donation-received\n" +
            $"  [cyan]Patreon[/]     →  {Markup.Escape(webhookBase)}/patreon/patreon1/pledge-created\n" +
            $"  [cyan]Fourthwall[/]  →  {Markup.Escape(webhookBase)}/fourthwall/shop1/order-placed\n" +
            $"  [cyan]Kick[/]        →  {Markup.Escape(webhookBase)}/kick/channel1/follow\n\n" +
            $"[yellow]Tunnel identity [bold]{Markup.Escape(tunnelId)}[/] is stable and will be reused\n" +
            "on the next application restart (within the inactivity window).[/]")
        .Header("[bold green] Webhook base URL — copy to provider dashboards [/]")
        .BorderStyle(Style.Parse("green")));

    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine($"[grey]Tunnel:[/]   [bold]{Markup.Escape(session.TunnelId ?? tunnelId)}[/]");
    AnsiConsole.MarkupLine($"[grey]Public:[/]   [bold]{Markup.Escape(publicBase)}[/]");
    AnsiConsole.MarkupLine($"[grey]State:[/]    [green]{session.State}[/]");
    AnsiConsole.MarkupLine($"[grey]Port:[/]     {localPort} → {publicBase}");
    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine("[grey]Session is running. [bold]Ctrl+C[/] to stop.[/]");

    // Keep running until Ctrl+C
    try
    {
        await session.WaitForExitAsync(cts.Token);
    }
    catch (OperationCanceledException)
    {
        // User pressed Ctrl+C — clean stop
    }

    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine("[grey]Stopping session...[/]");
    if (webhookApp is not null) { await webhookApp.StopAsync(CancellationToken.None); await webhookApp.DisposeAsync(); }
    await session.StopAsync(CancellationToken.None);
    await session.DisposeAsync();
    AnsiConsole.MarkupLine("[grey]Session stopped.[/]");
}

// ─────────────────────────────────────────────────────────────────────────────
// Scenario 8b: Issue connect-scoped access token
//
// Useful for sharing tunnel access with a remote client without requiring
// them to be logged in to the Dev Tunnels service. The issued token is
// presented as the X-Tunnel-Authorization header when connecting.
// ─────────────────────────────────────────────────────────────────────────────
static async Task DemoIssueAccessTokenAsync(IDevTunnelsClient client)
{
    AnsiConsole.Write(
        new Panel(
            "[bold]Issues a connect-scoped access token for a managed tunnel.[/]\n\n" +
            "Share this token with a remote client so it can connect to the tunnel\n" +
            "without a Dev Tunnels account. Present it as [bold]X-Tunnel-Authorization[/].\n\n" +
            "[grey]CLI command: devtunnel token <tunnelId> --scopes connect --nologo[/]")
        .Header("[bold dodgerblue1] Issue Access Token [/]")
        .BorderStyle(Style.Parse("dodgerblue1")));

    AnsiConsole.WriteLine();

    string tunnelId = AnsiConsole.Ask<string>("Tunnel ID:", "my-app-webhooks-01");

    if (!DevTunnelValidation.IsValidTunnelId(tunnelId))
    {
        WriteError($"'{tunnelId}' is not a valid tunnel ID.");
        return;
    }

    string scopeInput = AnsiConsole.Ask<string>(
        "Scopes [grey](space-separated, leave blank for default 'connect')[/]:",
        string.Empty);

    string[] scopes = string.IsNullOrWhiteSpace(scopeInput)
        ? []
        : scopeInput.Split(' ', StringSplitOptions.RemoveEmptyEntries);

    AnsiConsole.WriteLine();

    string token = await AnsiConsole
        .Status()
        .Spinner(Spinner.Known.Dots)
        .SpinnerStyle(Style.Parse("dodgerblue1"))
        .StartAsync($"Issuing token for tunnel '{tunnelId}'...",
            _ => client.GetAccessTokenAsync(tunnelId, scopes.Length > 0 ? scopes : null).AsTask());

    // Truncate for display (tokens are long JWTs)
    string displayToken = token.Length > 80
        ? $"{token[..40]}…{token[^20..]}"
        : token;

    AnsiConsole.Write(
        new Panel(
            $"[bold green]{Markup.Escape(displayToken)}[/]\n\n" +
            "[grey]Full token length:[/] " + token.Length + " chars\n\n" +
            "[yellow]Share this token with the remote operator client.\n" +
            "It should be sent as the[/] [bold]X-Tunnel-Authorization[/] [yellow]header.[/]")
        .Header("[bold green] Access token issued [/]")
        .BorderStyle(Style.Parse("green")));
}

// ─────────────────────────────────────────────────────────────────────────────
// Scenario 9: Execute raw command
// ─────────────────────────────────────────────────────────────────────────────
static async Task DemoRawCommandAsync(IDevTunnelsClient client)
{
    AnsiConsole.MarkupLine("[bold]Raw CLI command (escape hatch)[/]");
    AnsiConsole.MarkupLine("[grey]Passes arguments directly to the devtunnel CLI and captures output.[/]");
    AnsiConsole.MarkupLine("[grey]Example inputs: [bold]user show --json --nologo[/]   or   [bold]list --json --nologo[/][/]");
    AnsiConsole.WriteLine();

    string input = AnsiConsole.Ask<string>("Arguments:", "user show --json --nologo");
    string[] args = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);

    AnsiConsole.WriteLine();

    DevTunnelCommandResult result = await AnsiConsole
        .Status()
        .Spinner(Spinner.Known.Dots)
        .SpinnerStyle(Style.Parse("dodgerblue1"))
        .StartAsync($"Running: devtunnel {string.Join(' ', args)}...",
            _ => client.ExecuteRawAsync(args).AsTask());

    Table table = new Table()
        .Border(TableBorder.Rounded)
        .BorderStyle(Style.Parse("grey"))
        .AddColumn("[grey]Field[/]")
        .AddColumn("[grey]Value[/]");

    _ = table.AddRow("Exit code", result.ExitCode == 0 ? "[green]0[/]" : $"[red]{result.ExitCode}[/]");
    _ = table.AddRow("stdout",
        !string.IsNullOrWhiteSpace(result.StandardOutput)
            ? $"[grey]{Markup.Escape(result.StandardOutput.Trim())}[/]"
            : "[grey dim](empty)[/]");
    _ = table.AddRow("stderr",
        !string.IsNullOrWhiteSpace(result.StandardError)
            ? $"[red]{Markup.Escape(result.StandardError.Trim())}[/]"
            : "[grey dim](empty)[/]");

    AnsiConsole.Write(table);
}

// ─────────────────────────────────────────────────────────────────────────────
// Local webhook listener helpers
// ─────────────────────────────────────────────────────────────────────────────
static void DisplayIncomingWebhook(string method, string path, Dictionary<string, string> headers, string body, string? contentType)
{
    AnsiConsole.Write(new Rule($"[bold yellow]↓ {method} {Markup.Escape(path)}[/]").RuleStyle(Style.Parse("yellow dim")));

    // Show Content-Type and any X-* headers (signatures, etc.)
    foreach (var (key, value) in headers)
    {
        if (key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase)
            || key.StartsWith("X-", StringComparison.OrdinalIgnoreCase)
            || key.Equals("User-Agent", StringComparison.OrdinalIgnoreCase))
        {
            AnsiConsole.MarkupLine($"  [grey dim]{Markup.Escape(key)}:[/] [grey]{Markup.Escape(value)}[/]");
        }
    }

    AnsiConsole.WriteLine();

    if (!string.IsNullOrWhiteSpace(body))
    {
        string display = FormatWebhookBody(body, contentType);
        AnsiConsole.MarkupLine($"[grey]{Markup.Escape(display)}[/]");
    }
    else
    {
        AnsiConsole.MarkupLine("  [grey dim](empty body)[/]");
    }

    AnsiConsole.WriteLine();
}

// Ko-fi sends application/x-www-form-urlencoded with a `data` field containing JSON.
// All other providers send application/json directly.
static string FormatWebhookBody(string body, string? contentType)
{
    if (contentType?.Contains("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase) == true)
    {
        foreach (string pair in body.Split('&'))
        {
            int eq = pair.IndexOf('=');
            if (eq <= 0) continue;
            string key = Uri.UnescapeDataString(pair[..eq].Replace('+', ' '));
            string value = Uri.UnescapeDataString(pair[(eq + 1)..].Replace('+', ' '));
            if (key.Equals("data", StringComparison.OrdinalIgnoreCase))
            {
                return $"[form field: data]\n{TryPrettyPrintJson(value) ?? value}";
            }
        }
        return body;
    }

    return TryPrettyPrintJson(body) ?? body;
}

static string? TryPrettyPrintJson(string input)
{
    try
    {
        using JsonDocument doc = JsonDocument.Parse(input.Trim());
        return JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions { WriteIndented = true });
    }
    catch
    {
        return null;
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Shared rendering helpers
// ─────────────────────────────────────────────────────────────────────────────
static void RenderLoginStatus(DevTunnelLoginStatus status)
{
    Table table = new Table()
        .Border(TableBorder.Simple)
        .BorderStyle(Style.Parse("grey"))
        .HideHeaders()
        .AddColumn(string.Empty)
        .AddColumn(string.Empty);

    _ = table.AddRow("Status", status.IsLoggedIn ? "[green]Logged in[/]" : "[red]Not logged in[/]");
    _ = table.AddRow("Provider", status.Provider?.ToString() ?? "[grey dim]—[/]");
    _ = table.AddRow("Username", status.Username ?? "[grey dim]—[/]");

    AnsiConsole.Write(table);
}

static void RenderTunnelStatus(DevTunnelStatus status)
{
    Table table = new Table()
        .Border(TableBorder.Simple)
        .BorderStyle(Style.Parse("grey"))
        .HideHeaders()
        .AddColumn(string.Empty)
        .AddColumn(string.Empty);

    _ = table.AddRow("Tunnel ID", $"[bold]{Markup.Escape(status.TunnelId)}[/]");
    _ = table.AddRow("Description", !string.IsNullOrWhiteSpace(status.Description) ? Markup.Escape(status.Description) : "[grey dim]—[/]");
    _ = table.AddRow("Labels", status.Labels.Count > 0 ? string.Join(", ", status.Labels) : "[grey dim]—[/]");
    _ = table.AddRow("Host conns", status.HostConnections.ToString());
    _ = table.AddRow("Client conns", status.ClientConnections.ToString());
    _ = table.AddRow("Ports", status.Ports.Count > 0 ? string.Join(", ", status.Ports.Select(p => p.PortNumber)) : "[grey dim]—[/]");

    AnsiConsole.Write(table);
}

static void WriteStep(int number, string description) =>
    AnsiConsole.MarkupLine($"[bold]Step {number}:[/] {Markup.Escape(description)}");

static void WriteError(string message) =>
    AnsiConsole.MarkupLine($"[bold red]Error:[/] [red]{Markup.Escape(message)}[/]");

static void WriteWarning(string message) =>
    AnsiConsole.MarkupLine($"[bold yellow]Warning:[/] [yellow]{Markup.Escape(message)}[/]");
