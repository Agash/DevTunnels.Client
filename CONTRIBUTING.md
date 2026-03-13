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
