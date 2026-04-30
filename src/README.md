# Source Layout

This source tree follows `docs/architecture.md`.

| Project | Responsibility | Boundary |
|---|---|---|
| `ZwcadAiPlugin` | ZWCAD command host, user confirmation, CAD document transaction entry points | Does not own prompt repair or model internals |
| `ZwcadAi.Core` | DrawingSpec models, validation result types, domain constants, shared errors | Must not reference ZWCAD runtime assemblies |
| `ZwcadAi.Renderer` | Renderer contracts and deterministic render result types | Does not interpret natural language |
| `ZwcadAi.AiService` | AI request/response contracts and model adapter interfaces | Must not mutate DWG documents |
| `ZwcadAi.Ui` | UI-facing panel state and preview summary mapping | Must not reference ZWCAD runtime assemblies or own CAD writes |
| `ZwcadAi.Tests` | Lightweight architecture and contract tests without production secrets | Does not load ZWCAD, but can verify configured ZWCAD project references |

## P1-02 ZWCAD 2025 plugin build

`ZwcadAiPlugin` is the only project that references ZWCAD runtime assemblies. The default P1-02 reference path is resolved from `ZWCAD2025_DIR`, then from `$(ProgramW6432)\ZWSOFT\ZWCAD 2025`.

Override the local install path without committing machine-specific state by creating an ignored `Directory.Build.local.props` file:

```xml
<Project>
  <PropertyGroup>
    <Zwcad2025InstallDir>D:\Apps\ZWSOFT\ZWCAD 2025</Zwcad2025InstallDir>
  </PropertyGroup>
</Project>
```

Build the loadable plugin:

```powershell
dotnet build ZwcadAi.sln -p:Platform=x64
```

Manual POC load path:

1. Start ZWCAD 2025.
2. Run `NETLOAD`.
3. Select `src\ZwcadAiPlugin\bin\x64\Debug\net48\ZwcadAiPlugin.dll`.
4. Run `AIDRAW`.
5. Confirm the command line prints the minimal readiness message and no DWG entities are written.
