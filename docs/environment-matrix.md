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
6. P1-02 执行 `AIDRAW` 后只显示最小确认信息和日志，不写入 DWG。
7. P1-03 执行 `AIDRAW` 后使用固定 DrawingSpec 在当前 DWG 中写入 100x60 矩形板件、孔、中心线和标注。

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
| ZWCAD 2025 `25.0.301.11526` + installed managed assemblies + .NET Framework 4.8 + current Windows x64 | P1 local test baseline | Build verified; `NETLOAD` + `AIDRAW` manually verified through P1-03 on 2026-04-26; `AIEXPORT` DWG/PDF manually verified on 2026-04-26 | P1-04 complete for the P1 local baseline. |
| ZWCAD 2026 + ZWCAD 2026 ZRX SDK + .NET Framework 4.8 + Windows 11 x64 | Later compatibility target | SDK acquired; CAD not installed | 不阻塞 P1-02，本机安装 2026 后再做兼容验证。 |
| ZWCAD 2026 + Windows 10 22H2 x64 | Later compatibility target | Not verified | P7 前安排兼容性验证。 |
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

## P1-03 Manual Validation Evidence

Date: 2026-04-26

Validated on the local ZWCAD 2025 POC baseline after rebuilding the default x64 output:

- `dotnet build ZwcadAi.sln -p:Platform=x64` succeeds after closing the previously loaded ZWCAD process that locked the old plugin DLL.
- `NETLOAD` accepts `src\ZwcadAiPlugin\bin\x64\Debug\net48\ZwcadAiPlugin.dll`.
- `AIDRAW` renders the fixed P1-03 DrawingSpec into the current DWG.
- The generated plate outer profile is a 100x60 closed rectangular polyline on `OUTLINE`.
- The generated hole center and radius match the sample DrawingSpec: center `(30, 30)`, radius `6`.
- Centerline entities are written on `CENTER`; dimension entities are written on `DIM`.
- CAD writes are wrapped in a ZWCAD database transaction, so render exceptions fail before commit and do not leave partial POC output.

## P1-04 Export POC Implementation Evidence

Date: 2026-04-26

Implemented and build-verified against the local ZWCAD 2025 POC baseline:

- `AIEXPORT` is registered through `CommandMethod(PluginCommandCatalog.AiExport)`.
- The command writes an export log for the DWG copy path and the PDF result path or PDF-unavailable reason.
- DWG copy export uses `Database.Wblock()` to create an independent database copy, then `SaveAs(dwgPath, DwgVersion.Current)` on that copy, and does not call `Database.Save()`.
- Export outputs are placed under a `ZwcadAiExports` directory beside the active drawing when the drawing has a filesystem directory, otherwise under the user's Documents folder.
- PDF export uses the ZWCAD managed plotting path from the local SDK sample: `PlotFactory.CreatePublishEngine()`, current layout plot settings, PDF plot-device selection, and `BeginDocument(..., true, pdfPath)`.
- `dotnet build ZwcadAi.sln -p:Platform=x64` succeeds with 0 warnings and 0 errors.
- The test harness includes 13 checks and covers `AIEXPORT` command registration, the DWG SaveAs copy path, and the PDF plot-to-file path.

## P1-04 Manual Validation Evidence

Date: 2026-04-26

Validated in the ZWCAD 2025 CAD process after loading the current plugin build:

- `AIEXPORT` is registered and executable from the ZWCAD command line.
- `AIEXPORT` generated a DWG copy at `C:\Users\chenguang\Documents\ZwcadAiExports\Drawing1-aiexport-20260426-151206.dwg`.
- `AIEXPORT` generated a PDF at `C:\Users\chenguang\Documents\ZwcadAiExports\Drawing1-aiexport-20260426-151206.pdf`.
- CAD command output:

```text
命令: AIEXPORT
AIEXPORT DWG copy: C:\Users\chenguang\Documents\ZwcadAiExports\Drawing1-aiexport-20260426-151206.dwg
AIEXPORT PDF: C:\Users\chenguang\Documents\ZwcadAiExports\Drawing1-aiexport-20260426-151206.pdf
```

A non-interactive `Database` construction attempt outside the ZWCAD process previously crashed in unmanaged ZWCAD code with an access violation, so CAD-process execution is the validation source of truth for this export path.

## P3-03 Transaction And Rollback Evidence

Date: 2026-04-28

Implemented and build/test verified against the local source baseline:

- CAD writes now enter a shared writer transaction boundary before any entity or dimension append.
- The ZWCAD adapter holds `DocumentLock` and one database `Transaction` for the full layer/entity/dimension write.
- The writer commits only after all entities, dimensions, cancellation checks, and failure-injection checks complete.
- Entity writer failures resolve to stable paths such as `$.entities[relief-arc]`.
- Dimension writer failures resolve to stable paths such as `$.dimensions[dim-angle-between-edges]`.
- Failure injection supports after-entity, after-dimension, and before-commit checkpoints.
- Automated fake-transaction tests verify that injected entity failures, injected dimension failures, and cancellation do not commit or return CAD object mappings.
- Cancellation is represented as `RenderStatus.Canceled` and `render_canceled` at `$`; it is not treated as a partial success.

Manual ZWCAD Undo validation completed in the ZWCAD 2025 CAD process:

- Rebuilt `src\ZwcadAiPlugin\bin\x64\Debug\net48\ZwcadAiPlugin.dll`.
- Loaded the rebuilt DLL with `NETLOAD`.
- Ran `AIDRAW` to create the automatic DrawingSpec output.
- Ran one `UNDO` operation.
- Confirmed the generated automatic drawing content was removed successfully in one undo step.

## P3 Renderer Layer Style And Summary Evidence

Date: 2026-04-28

Implemented and build/test verified against the local source baseline:

- `CadLayerStandards` is the shared source for the seven `enterprise-default-v1` layers: `OUTLINE`, `CENTER`, `DIM`, `TEXT`, `HIDDEN`, `CONSTRUCTION`, and `TITLE`.
- Business validation rejects DrawingSpec layer color, line type, and line weight drift from the enterprise defaults before planning.
- The ZWCAD writer now opens existing managed layers for write and reapplies the planned enterprise color, line type, line weight, and plot behavior.
- `CONSTRUCTION` is marked non-plot by default when the layer is created or updated.
- Missing required non-`Continuous` line types are loaded from standard ZWCAD line type files before failing with `missing_linetype` at stable paths such as `$.layers[CENTER].lineType`.
- `DBText` and `MText` receive deterministic enterprise text styles from `CadTextStyleStandards`.
- Dimensions receive deterministic enterprise dimension styles from `CadDimensionStyleStandards`; the centralized standards are the configuration entry point for future profile loading.
- `RenderResult.Summary` now reports render status, entity and dimension counts, type counts, layer counts, bounding box, `specEntityId -> cadObjectId`, validation issues for failed/canceled renders, and export placeholders.
- Automated verification: `dotnet .\src\ZwcadAi.Tests\bin\x64\Debug\net8.0\ZwcadAi.Tests.dll` passed 44 tests.
- Build verification: `dotnet build ZwcadAi.sln -p:Platform=x64` passed and compiled `ZwcadAiPlugin.dll` against the local ZWCAD 2025 managed assemblies.

Manual ZWCAD 2025 visual validation completed in the CAD process after the line type loading fix:

- Loaded the rebuilt plugin with `NETLOAD`.
- Ran `AIDRAW`.
- Confirmed the render completed without the previous `missing_linetype` failure for `CENTER`.
- Confirmed the generated output visually matched the expected P3 layer/style baseline for layer colors, line types, line weights, text styles, and dimension styles.

## P4-01 Model Prompt Contract Evidence

Date: 2026-04-28

Implemented and build/test verified against the local source baseline:

- Added prompt contract version `p4-01-model-prompt-contract-v1` in `prompts/model-prompt-contract-v1.md` and `prompts/gpt-system-prompt.md`.
- `AiDrawingSpecRequest` now carries the natural-language request boundary, prompt version, DrawingSpec version, allowed entity/dimension types, and `enterprise-default-v1` profile id.
- `AiDrawingSpecResponse` now separates `DrawingSpec`, `NeedsClarification`, and `Rejected` response kinds.
- `AiModelIssue` maps low-level validation/render/service failures into stable `code`, `path`, `message`, `severity`, `source`, and `repairable` fields.
- `AiDrawingSpecRepairRequest` defines the bounded repair loop: previous invalid DrawingSpec JSON, mapped issues, `RepairDrawingSpecOnly`, and max 2 attempts.
- Enterprise CAD standards remain centralized in `CadLayerStandards`, `CadTextStyleStandards`, and `CadDimensionStyleStandards`; P4/P5 profile loading stays an interface design item.
- P3 lightweight regression gate remains fixed `AIDRAW`, layer/linetype/text/dimension style checks, and the 44-test automated suite. P6 still owns DWG reverse extraction, batch regression, key dimension comparison, and export artifact verification.
- Automated verification: `dotnet .\src\ZwcadAi.Tests\bin\x64\Debug\net8.0\ZwcadAi.Tests.dll` passed 44 tests.
- Build verification: `dotnet build ZwcadAi.sln -p:Platform=x64` passed and compiled `ZwcadAiPlugin.dll`.

## P4-02 Local AI Service Adapter Evidence

Date: 2026-04-28

Implemented and build/test verified against the local source baseline:

- Added a deterministic `LocalAiDrawingSpecAdapter` behind `IAiDrawingSpecService`; it consumes raw model-client text and maps it to `AiDrawingSpecResponseKind.DrawingSpec`, `NeedsClarification`, or `Rejected`.
- Added a local `IAiModelClient` seam and `LocalAiServiceOptions`/`AiModelCallOptions` skeleton for timeout, bounded retry, endpoint, API-key environment variable name, and default non-sensitive logging behavior.
- The adapter enforces JSON-only DrawingSpec root-object output; non-JSON and Markdown fenced responses are rejected before schema/business validation.
- Free CAD commands or script-like non-JSON output are rejected as `unsafe_cad_command`; valid DrawingSpec JSON text content is not treated as executable output.
- Model timeout and service failures map to non-repairable service issues after bounded retry handling.
- Schema and business validation failures are mapped to stable `AiModelIssue` values with `SchemaValidation` or `BusinessValidation` sources and repairability derived from `ModelPromptContract`.
- Clarification JSON with non-empty `clarifications` maps to `NeedsClarification`; the full UI follow-up workflow remains P5/P4 follow-on work.
- Repair calls enforce valid attempt numbers and the configured max repair attempts before calling the model client; the full P4-03 repair loop is still pending.
- P3 lightweight regression boundary remains fixed: the original 44-test AIDRAW/layer/linetype/text/dimension baseline still passes. P4-02 adds 12 local adapter tests, for 56 tests total in the combined harness.
- Automated verification: `dotnet .\src\ZwcadAi.Tests\bin\x64\Debug\net8.0\ZwcadAi.Tests.dll` passed 56 tests.
- Build verification: `dotnet build src\ZwcadAi.Tests\ZwcadAi.Tests.csproj -p:Platform=x64 -t:Rebuild` passed with 0 warnings and 0 errors.

## Open Environment Gaps

- ZWCAD 2026 and Windows 10 compatibility validation remain pending for later compatibility work.
