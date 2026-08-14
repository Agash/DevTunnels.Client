# Contributing

## Ground rules

- Keep the core package transport-focused and UI-free.
- Prefer typed records and narrow abstractions over ad hoc dictionaries.
- Preserve low-level escape hatches even as higher-level helpers are added.
- Do not hand-edit generated files if code generation is introduced later.

## Before coding

1. Read `README.md` for an overview of the API surface and repo shape.
2. Keep the core package free of host-application-specific dependencies.

## Validation

```bash
dotnet restore DevTunnels.Client.slnx
dotnet build DevTunnels.Client.slnx -c Release
dotnet test DevTunnels.Client.slnx -c Release --no-build
```

## House rules

- **Warnings are errors.** `TreatWarningsAsErrors` is on. Fix the diagnostic rather than suppressing
  it; a `NoWarn` or `#pragma` needs a comment saying why the rule genuinely does not apply.
- **Nullable reference types are enabled** everywhere. No `!` without a reason.
- **All I/O is async**, with a `CancellationToken` accepted and propagated. No `.Result`,
  `.GetAwaiter().GetResult()`, or `Thread.Sleep`.
- **Public API carries XML documentation.**
- **The package is trim- and AOT-clean.** `IsAotCompatible` is set, so the trim and AOT analyzers run
  on every build. Serialization goes through a source-generated `JsonSerializerContext`, never the
  reflection-based `JsonSerializer` overloads.

## Tests

- Name tests `{Method}_{Scenario}_{ExpectedResult}`.
- Prefer the purpose-built MSTest assertions (`Assert.HasCount`, `Assert.Contains`,
  `Assert.AreSequenceEqual`) over hand-rolled equality checks — the analyzers will point you at them.
- No `Thread.Sleep`. Use `TaskCompletionSource`, channels, or a fake clock.
- New behaviour needs a test. Bug fixes need a test that fails before the fix.

## Commits and pull requests

Commit messages follow [Conventional Commits](https://www.conventionalcommits.org/en/v1.0.0/):

```
fix(webhooks): reject a signature computed over the decoded body
```

Keep the subject under 50 characters and in the imperative mood. Add a body only when the reason for
the change would not be obvious to the next reader — explain *why*, not *what*.

One logical change per commit. Rebase rather than merge when updating a branch.

## Code of conduct

This project follows the [Contributor Covenant](CODE_OF_CONDUCT.md). By participating you are
expected to uphold it.

## Reporting security issues

Please do not open a public issue. See [SECURITY.md](SECURITY.md).
