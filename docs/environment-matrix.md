# CAD Environment Matrix

本文件锁定 P0-02 的 CAD 环境基线。P1 技术 POC 先使用当前工作站已安装的 ZWCAD 2025 验证最小插件加载和绘图链路；ZWCAD 2026 保留为后续兼容目标，不能在未安装前作为 P1-02 验收前提。

## Locked POC Baseline

| Item | Locked Decision | Status | Notes |
|---|---|---|---|
| CAD product | ZWCAD 2025 x64 | Locked for P1 local testing | 当前工作站安装路径：`C:\Program Files\ZWSOFT\ZWCAD 2025`；`ZWCAD.exe` 版本 `25.0.301.11526`。 |
| CAD API route | ZRX.NET | Locked, local 2025 assemblies available | 优先 .NET 插件，降低 POC 维护成本；ZRX/C++ 仅作为 ZRX.NET API 缺口时的备选。 |
| SDK/reference package | ZWCAD 2025 installation managed assemblies | Available locally | P1-02 使用本机 ZWCAD 2025 安装目录中的 `ZwManaged.dll` 和 `ZwDatabaseMgd.dll` 做加载测试。ZWCAD 2026 ZRX SDK 已下载并解包，但暂不作为 P1 本机测试基线。 |
| .NET target | .NET Framework 4.8 for CAD-facing projects | Installed and build-verified | `ZwcadAiPlugin` 和 CAD-facing renderer adapter 使用 `net48`；`ZwcadAi.Core` 不引用 ZWCAD runtime。 |
| Build platform | x64 | Locked | 不支持 AnyCPU/32-bit POC。 |
| OS primary | Windows 11 x64 | Locked, current workstation not verified for ZWCAD install | 用于主开发和加载验证。 |
| OS secondary | Windows 10 22H2 x64 | Allowed, unverified | 仅作为兼容性验证环境，不阻塞 P1 主线。 |
| IDE/tooling | .NET SDK 8.0.420 + .NET Framework 4.8 Developer Pack | Installed and build-verified | 当前已能执行 `dotnet build ZwcadAi.sln -p:Platform=x64`；仍未发现 Visual Studio 2022/MSBuild 独立安装。 |

## Plugin Loading Method

P1 POC 锁定为手动加载 ZRX.NET DLL：

1. 构建 `ZwcadAiPlugin.dll`，目标框架 `net48`，平台 `x64`。
2. 打开 ZWCAD 2025。
3. 使用 ZWCAD 命令行执行 `NETLOAD`。
4. 选择 `ZwcadAiPlugin.dll`。
5. 插件通过 ZRX.NET 命令入口注册 `AIDRAW`。
6. 执行 `AIDRAW` 后只显示最小确认信息和日志，不写入 DWG。

P1 不做安装包、注册表 demand-load、企业自动部署或多版本自动探测。这些放到 P7 Deployment。

## Reference Assemblies Policy

- P1-02 本机测试首选引用 `C:\Program Files\ZWSOFT\ZWCAD 2025` 中与运行时一致的 .NET assemblies。
- 项目文件不得把本机绝对安装路径写死到源码仓库；路径必须通过 `Directory.Build.props`、环境变量或本机未提交配置传入。
- POC 编译引用至少需要确认 `ZwManaged.dll`、`ZwDatabaseMgd.dll` 及相关 ZWCAD managed assemblies 的实际来源和版本。
- 如果后续切换到 ZWCAD 2026，必须把引用切回 ZWCAD 2026 安装目录或 ZWCAD 2026 ZRX SDK 中的同主版本 assemblies。

当前本机已确认的 ZWCAD 2025 managed assemblies：

| Assembly | File/Product Version | Assembly Version | Architecture | Local Path |
|---|---|---|---|---|
| `ZwManaged.dll` | `25.0.301.11526` | `25.0.25.0` | `Amd64` | `C:\Program Files\ZWSOFT\ZWCAD 2025\ZwManaged.dll` |
| `ZwDatabaseMgd.dll` | `25.0.301.11526` | `25.0.25.0` | `Amd64` | `C:\Program Files\ZWSOFT\ZWCAD 2025\ZwDatabaseMgd.dll` |

已缓存但暂不用于 P1 本机测试的 ZWCAD 2026 SDK managed assemblies：

| Assembly | Assembly Version | Architecture | Local Path |
|---|---|---|---|
| `ZwManaged.dll` | `26.0.26.0` | `Amd64` | `.local/zwcad-sdk/extracted/ZWCAD_2026_ZRXSDK/inc/ZwManaged.dll` |
| `ZwDatabaseMgd.dll` | `26.0.26.0` | `Amd64` | `.local/zwcad-sdk/extracted/ZWCAD_2026_ZRXSDK/inc/ZwDatabaseMgd.dll` |
| `ZwDatabaseMgdBrep.dll` | `26.0.26.0` | `Amd64` | `.local/zwcad-sdk/extracted/ZWCAD_2026_ZRXSDK/inc/ZwDatabaseMgdBrep.dll` |
| `ZwSoft.ZwCAD.Interop.dll` | `1.0.0.0` | `MSIL` | `.local/zwcad-sdk/extracted/ZWCAD_2026_ZRXSDK/inc/ZwSoft.ZwCAD.Interop.dll` |
| `ZwSoft.ZwCAD.Interop.Common.dll` | `1.0.0.0` | `MSIL` | `.local/zwcad-sdk/extracted/ZWCAD_2026_ZRXSDK/inc/ZwSoft.ZwCAD.Interop.Common.dll` |

## Compatibility Matrix

| Environment | Support Decision | Verification Status | P1 Action |
|---|---|---|---|
| ZWCAD 2025 `25.0.301.11526` + installed managed assemblies + .NET Framework 4.8 + current Windows x64 | P1 local test baseline | Build verified; `NETLOAD` + `AIDRAW` manually verified on 2026-04-26 | P1-02 complete; P1-04 covers DWG/PDF export validation. |
| ZWCAD 2026 + ZWCAD 2026 ZRX SDK + .NET Framework 4.8 + Windows 11 x64 | Later compatibility target | SDK acquired; CAD not installed | 不阻塞 P1-02，本机安装 2026 后再做兼容验证。 |
| ZWCAD 2026 + Windows 10 22H2 x64 | Later compatibility target | Not verified | P1-04 或 P7 前安排兼容性验证。 |
| ZWCAD 2024 or earlier | Not supported in MVP | Not verified | 不为 P1 做兼容分支。 |
| ZWCAD 2027 Beta | Excluded from MVP | Not verified | 等稳定版和匹配 SDK 明确后再评估。 |
| ZRX/C++ | Fallback only | Not verified | 只有 ZRX.NET 无法完成实体、标注、事务或导出能力时才开 POC。 |
| AutoCAD ObjectARX/.NET | Not supported | Not applicable | 不引用 AutoCAD runtime，不以 AutoCAD 加载成功作为验收。 |

## P1 Entry Criteria

P1-01 可以在本矩阵提交后开始。P1-02 之前必须补齐以下证据：

- ZWCAD 2025 已安装并能启动。当前已确认安装目录和文件版本，并已完成手动启动验证（2026-04-26）。
- ZWCAD 2025 managed assemblies 已确认。当前已确认 `ZwManaged.dll` 和 `ZwDatabaseMgd.dll`。
- 本机已安装 .NET Framework 4.8 Developer Pack。当前已完成，并同时存在 `v4.8` 和 `v4.8.1` reference assemblies。
- 能编译 `net48/x64` 的最小插件 DLL。当前 P1-01 骨架已完成。
- 能在 ZWCAD 2025 中通过 `NETLOAD` 选择 DLL。当前已完成手动验收（2026-04-26）。
- `AIDRAW` 命令能显示确认信息，并且异常不会导致 ZWCAD 崩溃。当前已完成手动验收（2026-04-26）。

## Source Evidence

- [ZWSOFT Developer Documentation](https://www.zwsoft.com/support/zwcad-devdoc) 页面列出 ZWCAD 2026 的 ZRX SDK、ZRX Developer Guide 和 ZRX DotNet Guide。
- [ZWSOFT ZWCAD 2026 release announcement](https://www.zwsoft.com/news/products/zwcad-2026-design-with-speed-innovate-with-ease) 确认 2026 是正式产品线。
- [ZWSOFT ZWCAD download page](https://www.zwsoft.com/product/zwcad/download/viewer) 当前显示 ZWCAD 2027 Beta，因此 2027 Beta 不作为 MVP POC 基线。
- ZWSOFT 官方 Developer Documentation 页面当前通过表单提供 SDK 下载；本地 SDK 包从 `zwcad.pl` 的 ZWCAD 2026 ZRXSDK 下载端点获取，并已验证 Authenticode 签名主体为 `ZWSOFT CO., LTD.(Guangzhou)`、状态为 `Valid`。

## P1-02 Manual Validation Evidence

Date: 2026-04-26

Validated on the local ZWCAD 2025 POC baseline:

- ZWCAD 2025 starts on the workstation.
- `NETLOAD` accepts `src\ZwcadAiPlugin\bin\x64\Debug\net48\ZwcadAiPlugin.dll`.
- The plugin loads without copying `ZwManaged.dll` or `ZwDatabaseMgd.dll` into the plugin output directory.
- `AIDRAW` is registered and executable from the ZWCAD command line.
- `AIDRAW` prints the minimal readiness confirmation message.
- `AIDRAW` does not write DWG entities in P1-02.

## Open Environment Gaps

- DWG save-copy validation is still pending for P1-04.
- PDF export validation is still pending for P1-04.
