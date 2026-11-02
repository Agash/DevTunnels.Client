using System.Globalization;
using System.Text.RegularExpressions;

namespace DevTunnels.Client.Internal;

internal enum DevTunnelHostOutputKind
{
    None,
    Unrecognized,
    Port,
    Ready,
    Connected,
    Disconnected,
}

internal readonly record struct DevTunnelHostOutput(
    DevTunnelHostOutputKind Kind,
    int? Port = null,
    Uri? PortUri = null
);

/// <summary>
/// Reads what <c>devtunnel host</c> prints. The CLI's wording is not a contract: labels and values can arrive
/// on separate lines, messages can share a line, and colour codes can sit anywhere. Only a bounded, recognized
/// prefix is kept across lines; anything else is reported so the caller can fall back to asking the service.
/// </summary>
internal sealed partial class DevTunnelHostOutputParser(string tunnelId)
{
    private const int MaxPendingLength = 4096;

    private string? _pending;
    private int? _port;
    private bool _inspectUrl;
    private bool _readyWithoutId;

    /// <summary>Splits console output into messages, removing colour codes first.</summary>
    public static string[] SplitOutput(string content) =>
        MessageBoundaryRegex().Split(AnsiRegex().Replace(content, string.Empty));

    /// <summary>Whether a URL is the plain HTTPS form of a tunnel port.</summary>
    public static bool IsPortUri(Uri uri, int port) =>
        uri.IsAbsoluteUri
        && uri.Scheme == Uri.UriSchemeHttps
        && uri.IsDefaultPort
        && uri.UserInfo.Length == 0
        && uri.Query.Length == 0
        && uri.Fragment.Length == 0
        && uri.AbsolutePath == "/"
        && uri.Host.EndsWith(".devtunnels.ms", StringComparison.OrdinalIgnoreCase)
        && uri.Host.Split('.')[0]
            .EndsWith(
                "-" + port.ToString(CultureInfo.InvariantCulture),
                StringComparison.OrdinalIgnoreCase
            );

    /// <summary>Reads one message.</summary>
    public DevTunnelHostOutput Parse(string line)
    {
        string text = WhitespaceRegex()
            .Replace(AnsiRegex().Replace(line, string.Empty), " ")
            .Trim();
        if (text.Length == 0)
        {
            return default;
        }

        string words = Words(text);
        if (_pending is not null && StartsMessage(words))
        {
            // A new message supersedes an unfinished field, above all a disconnect arriving after a label.
            Clear();
        }

        if (_pending is { } pending)
        {
            text = pending + " " + text;
            words = Words(text);
            _pending = null;
        }

        if (_readyWithoutId && words.StartsWith("for tunnel", StringComparison.Ordinal))
        {
            text = "Ready to accept connections " + text;
            words = "ready to accept connections " + words;
        }

        _readyWithoutId = false;
        return words switch
        {
            _ when words.StartsWith("hosting port ", StringComparison.Ordinal) => ParseHostingPort(
                text
            ),
            _ when IsUrlMessage(text, words) => ParseUrlMessage(text, words),
            _ when words.StartsWith("ready to accept connections", StringComparison.Ordinal) =>
                ParseReady(text, words),
            _ when words.StartsWith(RelayPrefix, StringComparison.Ordinal) => ParseRelay(words),
            _ when IsPartialMessage(words) => Remember(text),
            _ => Unknown(),
        };
    }

    private const string RelayPrefix = "connection to host tunnel relay ";

    private static string Words(string text) =>
        WordsRegex().Replace(text, " ").Trim().ToLowerInvariant();

    private static bool StartsMessage(string words)
    {
        int separator = words.IndexOf(' ', StringComparison.Ordinal);
        string first = separator < 0 ? words : words[..separator];
        return first is "hosting" or "connect" or "inspect" or "ready" or "connection";
    }

    private static bool IsPrefix(string text, string phrase) =>
        phrase.Equals(text, StringComparison.Ordinal)
        || phrase.StartsWith(text + " ", StringComparison.Ordinal);

    private static bool IsPartialMessage(string words) =>
        IsPrefix(words, "hosting port")
        || IsPrefix(words, "connect via browser")
        || IsPrefix(words, "inspect network activity")
        || IsPrefix(words, "ready to accept connections for tunnel")
        || IsPrefix(words, "connection to host tunnel relay");

    private bool IsUrlMessage(string text, string words) =>
        words.StartsWith("connect via browser ", StringComparison.Ordinal)
        || words.StartsWith("inspect network activity ", StringComparison.Ordinal)
        || (_port is not null && words.StartsWith("at ", StringComparison.Ordinal))
        || Uri.TryCreate(text.Trim('<', '>', '(', ')'), UriKind.Absolute, out _);

    private DevTunnelHostOutput ParseHostingPort(string text)
    {
        Match match = PortRegex().Match(text);
        if (
            !match.Success
            || !int.TryParse(
                match.Groups["port"].ValueSpan,
                CultureInfo.InvariantCulture,
                out int port
            )
        )
        {
            return Unknown();
        }

        _port = port;
        _inspectUrl = false;
        Match url = UrlRegex().Match(text);
        return url.Success ? ParseUrl(url.Value) : default;
    }

    private DevTunnelHostOutput ParseUrlMessage(string text, string words)
    {
        _inspectUrl |= words.StartsWith("inspect network activity", StringComparison.Ordinal);
        Match match = UrlRegex().Match(text);
        return match.Success ? ParseUrl(match.Value) : Unknown();
    }

    private DevTunnelHostOutput ParseReady(string text, string words)
    {
        if (words.EndsWith("for tunnel", StringComparison.Ordinal))
        {
            return Remember(text);
        }

        Match match = ReadyRegex().Match(text);
        if (!match.Success)
        {
            return Unknown();
        }

        string reported = match.Groups["id"].Value;
        string bare = tunnelId.Split('.')[0];
        if (
            reported.Length == 0
            || reported.Equals(tunnelId, StringComparison.OrdinalIgnoreCase)
            || reported.Equals(bare, StringComparison.OrdinalIgnoreCase)
            || reported.StartsWith(bare + ".", StringComparison.OrdinalIgnoreCase)
        )
        {
            _readyWithoutId = reported.Length == 0;
            return new DevTunnelHostOutput(DevTunnelHostOutputKind.Ready);
        }

        return Unknown();
    }

    private DevTunnelHostOutput ParseRelay(string words) =>
        words[RelayPrefix.Length..].Split(' ')[0] switch
        {
            "restored" or "connected" => new DevTunnelHostOutput(DevTunnelHostOutputKind.Connected),
            "closed" or "lost" or "disconnected" => new DevTunnelHostOutput(
                DevTunnelHostOutputKind.Disconnected
            ),
            _ => Unknown(),
        };

    private DevTunnelHostOutput ParseUrl(string text)
    {
        string trimmed = text.TrimEnd('.', ',', ';', ':', ')', ']', '>');
        if (_inspectUrl)
        {
            _inspectUrl = false;
            return default;
        }

        if (
            _port is { } port
            && Uri.TryCreate(trimmed, UriKind.Absolute, out Uri? uri)
            && IsPortUri(uri, port)
        )
        {
            _port = null;
            return new DevTunnelHostOutput(DevTunnelHostOutputKind.Port, port, uri);
        }

        return Unknown();
    }

    private DevTunnelHostOutput Remember(string text)
    {
        if (text.Length > MaxPendingLength)
        {
            return Unknown();
        }

        _pending = text;
        return default;
    }

    private DevTunnelHostOutput Unknown()
    {
        Clear();
        return new DevTunnelHostOutput(DevTunnelHostOutputKind.Unrecognized);
    }

    private void Clear()
    {
        _pending = null;
        _port = null;
        _inspectUrl = false;
        _readyWithoutId = false;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(
        @"[\r\n]+|(?<=[\s;|])(?=(?:hosting\s+port|connect\s+via\s+browser|inspect\s+network\s+activity|ready\s+to\s+accept\s+connections|connection\s+to\s+host\s+tunnel\s+relay)\b)",
        RegexOptions.IgnoreCase
    )]
    private static partial Regex MessageBoundaryRegex();

    [GeneratedRegex(@"[\s\p{P}\p{S}]+")]
    private static partial Regex WordsRegex();

    [GeneratedRegex(@"\x1b\[[0-?]*[ -/]*[@-~]")]
    private static partial Regex AnsiRegex();

    [GeneratedRegex(@"hosting\s+port\s*:?\s*(?<port>\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex PortRegex();

    [GeneratedRegex(@"https?://[^\s,;]+", RegexOptions.IgnoreCase)]
    private static partial Regex UrlRegex();

    [GeneratedRegex(
        @"ready\s+to\s+accept\s+connections(?:\s+for\s+tunnel\s*:?\s*(?<id>[^\s.,;]+(?:\.[^\s,;]+?)?))?[.\s]*$",
        RegexOptions.IgnoreCase
    )]
    private static partial Regex ReadyRegex();
}
