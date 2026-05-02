# GPT Execution Tasks

本文件把项目拆成适合 GPT/Codex 执行的工程任务。每个任务都应按 `tasks/task-template.md` 单独派发，避免一次性要求模型完成整套系统。

## Execution Rules For GPT

- 每次只执行一个任务 ID。
- 开始前先读取任务的 Context Files。
- 不要越权修改任务未列出的文件。
- 所有实现必须有可验证输出。
- 不确定时把问题写入 `docs/open-questions.md`，不要默默假设。
- 涉及 CAD 写入、文件覆盖、外部服务调用时必须有安全边界。

## Current Development Flow

当前后续开发主线从“插件内部模型直接生成完整 DrawingSpec”调整为“粘贴 JSON 优先、本地编译和校验优先”：

1. P5A-01 定义 `CadIntentSpec v1` 上层输入合同和校验器。
2. P5A-02 实现 `mechanical_plate` 模板/组合领域包，把高频标准件 intent 本地编译为 DrawingSpec。
3. P5A-03 实现 `generic_2d_mechanical` 受控草图领域包，支持非标准二维机械新建图。
4. P5A-04 在插件面板加入“粘贴 JSON / 校验预览”默认路径，保持写入 DWG 前必须预览确认。
5. P5A-05 输出外部 AI Skill 模板，让 AI 默认生成 CadIntent JSON，而不是完整 DrawingSpec。
6. P5A-06 建立 intent 样例和性能回归门槛，再进入 P6 几何摘要、批量回归和稳定性验证。

内部模型 API 作为后续增强能力保留，但不再作为 P5A 验收主路径；完整 DrawingSpec 粘贴保留为高级调试入口。

## Phase P0: Scope And Environment

### P0-01 Confirm MVP Domain

Goal: 明确第一版插件只服务一个可控领域。

Context Files:

- `docs/capability-contract.md`
- `docs/open-questions.md`

Deliverables:

- 更新 `docs/capability-contract.md` 的 MVP 场景。
- 更新 `docs/open-questions.md`，删除已确认问题。

Acceptance Criteria:

- 明确首个领域，例如机械矩形板件。
- 明确非目标不被扩大。
- 明确至少 10 个样例需求类型。

### P0-02 Build CAD Environment Matrix

Goal: 确认 ZWCAD、SDK、Windows、.NET 版本矩阵。

Context Files:

- `docs/architecture.md`
- `checklists/feasibility-checklist.md`

Deliverables:

- 新建或更新 `docs/environment-matrix.md`。

Acceptance Criteria:

- 列出目标 ZWCAD 版本。
- 列出 SDK 来源和版本。
- 列出插件加载方式。
- 标出未验证环境。

### P0-03 Collect CAD Standards

Goal: 收集企业图层、标注、文字、图框、块库标准。

Context Files:

- `docs/capability-contract.md`

Deliverables:

- 新建 `docs/cad-standards.md`。
- 新建配置草案路径说明。

Acceptance Criteria:

- 至少定义图层、颜色、线型、线宽。
- 至少定义文字样式和标注样式。
- 明确图框模板路径和块库来源。

### P0 Scope And Environment Closure

Date: 2026-05-02

Status:

- P0-01 已完成：MVP 领域锁定为 `mechanical_plate`，并在 `docs/capability-contract.md` 中列出 12 类样例需求。
- P0-02 已完成：`docs/environment-matrix.md` 已锁定 ZWCAD 2025 x64 本机基线、ZWCAD 2025 managed assemblies、ZWCAD 2026 后续兼容目标和未验证环境。
- P0-03 已完成：`docs/cad-standards.md` 已定义 `enterprise-default-v1` 图层、线型、文字、标注、图框路径和块库来源草案。
- P0 后续非阻塞缺口：尚未收集 20 个真实需求样例；正式企业 DWT/DWG、CTB/STB 和块库文件仍待管理员提供；联网、云端模型和日志保留策略仍需部署侧决策。

## Phase P1: Technical POC

### P1-01 Create Solution Skeleton

Goal: 创建 .NET 解决方案骨架，分离插件、核心、渲染器、AI 服务和测试。

Context Files:

- `docs/architecture.md`

Deliverables:

- `src/ZwcadAiPlugin/`
- `src/ZwcadAi.Core/`
- `src/ZwcadAi.Renderer/`
- `src/ZwcadAi.AiService/`
- `src/ZwcadAi.Tests/`

Acceptance Criteria:

- 解决方案可构建。
- 各项目职责与架构文档一致。
- 核心项目不引用 ZWCAD 运行时。

### P1-02 Implement Minimal Plugin Command

Goal: 实现可由 ZWCAD 加载的最小插件，并注册 `AIDRAW` 命令。

Context Files:

- `docs/architecture.md`
- `docs/environment-matrix.md`

Deliverables:

- 插件命令入口。
- 最小日志输出。
- 插件加载说明。

Acceptance Criteria:

- 插件可加载。
- 运行 `AIDRAW` 后可显示确认信息。
- 插件异常不会导致 ZWCAD 崩溃。

### P1-03 Implement Fixed Drawing POC

Goal: 不接 AI，使用固定 DrawingSpec 绘制一个矩形板件。

Context Files:

- `specs/drawing-spec-v1.schema.json`
- `examples/rectangular-plate.example.json`

Deliverables:

- 固定样例加载代码。
- 基础实体渲染：polyline、circle、centerline、dimension。

Acceptance Criteria:

- 可生成一个 100x60 矩形板件。
- 孔中心和半径正确。
- 实体在正确图层。
- 操作支持 Undo 或事务回滚。

### P1-04 Export POC

Goal: 验证 DWG 保存副本和 PDF 导出路径。

Context Files:

- `docs/implementation-flows.md`

Deliverables:

- `AIEXPORT` 命令初稿。
- 导出日志。

Acceptance Criteria:

- 可导出 DWG 副本。
- 可导出 PDF 或明确记录当前版本不支持的原因。
- 失败时不破坏当前图纸。

## Phase P2: DrawingSpec Protocol

### P2-01 Finalize DrawingSpec v1

Goal: 完成 DrawingSpec v1 的字段、实体、标注和错误码定义。

Context Files:

- `specs/drawing-spec-v1.schema.json`
- `examples/rectangular-plate.example.json`

Deliverables:

- 更新 JSON Schema。
- 新增 5 个样例 DrawingSpec。
- 新增协议说明文档 `specs/drawing-spec-v1.md`。

Acceptance Criteria:

- Schema 可校验所有样例。
- 每个实体都有稳定 `id`。
- 单位、坐标系、角度规则明确。

### P2-02 Implement Schema Validation

Goal: 在核心层实现 DrawingSpec Schema 校验。

Context Files:

- `specs/drawing-spec-v1.schema.json`

Deliverables:

- 校验服务。
- 错误报告结构。
- 单元测试。

Acceptance Criteria:

- 合法样例通过。
- 非法实体类型失败。
- 缺失必填字段失败。
- 错误报告能定位字段路径。

### P2-03 Implement Business Rule Validation

Goal: 实现领域业务规则校验。

Context Files:

- `docs/capability-contract.md`
- `docs/cad-standards.md`

Deliverables:

- 尺寸范围校验。
- 图层白名单校验。
- 坐标范围校验。
- 实体数量上限校验。

Acceptance Criteria:

- 不合规图层被拒绝。
- 关键尺寸缺失被拒绝。
- 超大坐标或实体数量被拒绝。

## Phase P3: Deterministic CAD Renderer

### P3-01 Render Basic Entities

Goal: 渲染 line、polyline、circle、arc、text、mtext。

Context Files:

- `specs/drawing-spec-v1.schema.json`
- `docs/architecture.md`

Deliverables:

- Entity renderer。
- Spec entity id 到 CAD object id 的映射。
- 单元或集成测试。

Acceptance Criteria:

- 每种实体类型至少有一个样例。
- 图层、颜色、线型按标准应用。
- 渲染失败可定位实体 id。

### P3-02 Render Dimensions And Center Marks

Goal: 支持常用标注和中心线/中心标记。

Context Files:

- `docs/cad-standards.md`

Deliverables:

- 线性标注。
- 对齐标注。
- 半径/直径标注。
- 中心线或中心标记工具。

Acceptance Criteria:

- 标注值和几何尺寸一致。
- 标注图层正确。
- 标注样式可配置。

### P3-03 Implement Transaction And Rollback

Goal: 所有自动绘制都必须可回滚。

Context Files:

- `docs/architecture.md`

Deliverables:

- 事务封装。
- 错误回滚。
- 用户取消处理。

Acceptance Criteria:

- 渲染中途异常不会残留半成品。
- 用户取消不会写入正式实体。
- Undo 行为可用。

### P3 Renderer Baseline Closure

Date: 2026-04-28

Status:

- 图层标准已固化为可验证的企业默认值：`OUTLINE` / `CENTER` / `DIM` / `TEXT` / `HIDDEN` / `CONSTRUCTION` / `TITLE`。
- 业务校验会拒绝图层颜色、线型、线宽与 `enterprise-default-v1` 不一致的 DrawingSpec。
- ZWCAD writer 会复用并更新已存在图层，创建缺失图层，并优先从标准线型文件加载 `Center` / `Hidden` 等必需线型；仍缺失时返回稳定错误。
- text/mtext 已解析默认文字样式；dimension 已解析默认标注样式，并保留集中标准入口供后续配置化。
- `RenderResult` 已输出最小几何摘要：状态、entity/dimension 数量、类型计数、图层计数、bounding box、`specEntityId -> cadObjectId`、失败/取消 issue、导出占位状态。
- 已在 ZWCAD 2025 内完成 `NETLOAD` + `AIDRAW` 视觉验收，图层/样式基线通过。
- P4 开始前保留一条轻量回归入口：固定样例 `AIDRAW`、图层/线型/文字/标注样式检查、P3 关闭时的 44 项自动测试，作为 AI 接入防回退基线。
- P6 仍保留 DWG 反向提取、批量回归和关键尺寸比对的完整 GeometrySummary 工作。

## Phase P4: AI Service Integration

### P4-01 Implement Model Prompt Contract

Goal: 让模型稳定输出 DrawingSpec 或澄清问题。

Context Files:

- `prompts/gpt-system-prompt.md`
- `prompts/model-prompt-contract-v1.md`
- `specs/drawing-spec-v1.schema.json`
- `src/ZwcadAi.AiService/IAiDrawingSpecService.cs`

Deliverables:

- Prompt 版本文件。
- 模型输入输出结构。
- DrawingSpec Schema 边界说明。
- 失败 issue 映射。
- 失败重试策略。

Acceptance Criteria:

- 模型不输出自由 CAD 命令。
- 模型缺参时返回澄清问题。
- 模型输出可被 Schema 校验。
- Schema / business / renderer / service 失败能映射为稳定 issue。
- 修复循环只修复 DrawingSpec JSON，重试次数有限。
- 企业标准配置化只作为 P4/P5 接口设计项，不在 P4-01 改动标准加载实现。

### P4-02 Implement Local AI Service Adapter

Goal: 插件通过本地适配层调用模型服务。

Context Files:

- `docs/architecture.md`

Deliverables:

- 服务配置。
- 超时与重试。
- 错误码映射。

Acceptance Criteria:

- 网络失败不会修改 DWG。
- API Key 不写死。
- 调用日志不默认记录敏感图纸全文。

### P4-03 Implement Spec Repair Loop

Goal: 校验失败时只修复 DrawingSpec，不重新发散生成。

Context Files:

- `docs/implementation-flows.md`

Deliverables:

- 错误反馈格式。
- 最大重试次数。
- 修复结果校验。

Acceptance Criteria:

- 修复输入包含具体错误路径。
- 重试次数有限。
- 修复失败时可向用户解释。

### P4-04 Implement HTTP AI Model Provider

Goal: 先接入一条真实 `IAiModelClient` 主线，通过 HTTP 调用本地模型服务或私有模型网关。

Context Files:

- `src/ZwcadAi.AiService/IAiDrawingSpecService.cs`
- `src/ZwcadAi.AiService/LocalAiDrawingSpecAdapter.cs`
- `docs/capability-contract.md`

Deliverables:

- HTTP `IAiModelClient` 实现。
- 初始生成请求体。
- DrawingSpec-only repair 请求体。
- API key 环境变量读取。
- timeout、retry、cancellation、HTTP/service failure 映射测试。

Acceptance Criteria:

- 只实现 HTTP/私有网关这一条 provider 主线，不同时铺开本地 SDK、云厂商 SDK 或多个 provider。
- API key 只从配置指定的环境变量读取，不写死、不进入请求体。
- 初始请求允许包含自然语言用户意图和确定性上下文。
- repair 请求只包含 invalid DrawingSpec JSON、mapped issues、attempt 和 repair strategy，不回传完整用户请求、DWG、截图或插件上下文。
- timeout、cancellation、provider/HTTP failure 最终稳定映射为 `AiModelIssueSource.Service` 的非 repairable issue。
- adapter 的 bounded retry 仍由 `LocalAiDrawingSpecAdapter` 统一控制。

### P4-05 Implement Clarification Follow-up Loop

Goal: 在 P5 UI 前补齐缺参追问闭环，避免把用户补充回答送进 repair loop。

Context Files:

- `src/ZwcadAi.AiService/IAiDrawingSpecService.cs`
- `src/ZwcadAi.AiService/LocalAiDrawingSpecAdapter.cs`
- `docs/implementation-flows.md`

Deliverables:

- `NeedsClarification -> 用户回答 -> 新一轮 CreateDrawingSpec` 的服务层流程。
- 澄清问题和用户回答的最小状态模型。
- 防止 clarification 被误接入 `RepairDrawingSpec` 的测试。

Acceptance Criteria:

- 缺少关键工程参数时返回 `NeedsClarification`，不触发 repair。
- 用户回答后发起新的 `CreateDrawingSpec` 请求，而不是调用 repair loop。
- 新一轮 create 请求只携带必要的用户意图/回答和确定性上下文，不携带 DWG、截图或任意插件上下文。

### P4-06 Implement Redacted AI Call Logging

Goal: 单独实现 AI 调用日志脱敏，默认不记录完整用户需求和 DrawingSpec 全文。

Context Files:

- `docs/capability-contract.md`
- `src/ZwcadAi.AiService/LocalAiDrawingSpecAdapter.cs`
- `src/ZwcadAi.AiService/HttpAiModelClient.cs`

Deliverables:

- AI 调用日志事件模型。
- 默认脱敏日志 writer 或接口。
- 明确的 opt-in 敏感内容记录开关。
- 日志字段测试。

Acceptance Criteria:

- 默认只记录 request id、prompt version、response kind、issue code/path/source、耗时和 attempt 数。
- 默认不记录完整用户需求、完整 DrawingSpec JSON、DWG、截图、API key 或任意插件上下文。
- 即使开启敏感内容记录，也不能记录 API key。
- 日志实现不改变 provider 请求边界和 repair loop 边界。

### P4 AI Service Integration Closure

Date: 2026-05-02

Status:

- P4-01 至 P4-06 均已完成并在 `docs/environment-matrix.md` 留有 evidence。
- 当前 P4 主线包含 prompt 合同、本地 adapter、DrawingSpec-only repair loop、HTTP/私有网关 provider、澄清追问服务闭环和默认脱敏 AI 调用日志。
- P4 请求边界保持不上传完整 DWG、截图或任意插件上下文；API key 只从配置指定环境变量读取，且日志层强制脱敏。
- P4 剩余事项不阻塞阶段关闭：云端/私有网关/本地模型的企业部署策略、日志保留周期和访问权限仍作为部署决策保留在 `docs/open-questions.md`。
- 本阶段不声明 Codex config fallback、DashScope provider、`AISETTINGS` 或模型配置文件启动器已经进入主线；这些属于开发分支代码或后续增强范围。

## Phase P5: Plugin UI

### P5-01 Build AI Drawing Panel

Goal: 提供自然语言输入、参数表、预览摘要和确认按钮。

Context Files:

- `docs/implementation-flows.md`

Deliverables:

- Dock 面板。
- 输入区。
- 状态区。
- 预览摘要区。

Acceptance Criteria:

- 用户可输入需求。
- 用户能看到缺参、错误和校验状态。
- 写入 DWG 前必须确认。

### P5-02 Implement History View

Goal: 用户可查看上次请求、DrawingSpec、校验结果和输出路径。

Context Files:

- `docs/capability-contract.md`

Deliverables:

- 历史记录存储。
- 历史记录面板。

Acceptance Criteria:

- 可按时间查看记录。
- 可定位 request id。
- 敏感字段可脱敏。

### P5 Plugin UI Checkpoint

Date: 2026-05-02

Status:

- P5-01 已完成代码层实现：`AIDRAW` 打开 ZWCAD `PaletteSet` Dock 面板，支持自然语言输入、澄清回答、状态/错误显示、参数/预览摘要和确认后写入 DWG。
- P5-01 已保持 P4 服务边界：初始生成调用 `CreateDrawingSpec`，澄清回答调用 `ContinueDrawingSpecAfterClarification`，UI 不调用 `RepairDrawingSpec`。
- P5-01 自动化基线已覆盖 UI 状态映射、预览确认门槛和服务错误映射；成功模型服务端到端链路仍需在 ZWCAD 进程内手工验收。
- P5A 默认主路径尚未实现：粘贴 CadIntent/DrawingSpec JSON 后本地校验预览、再确认写入。
- P5-02 仍未完成：尚无历史记录存储、历史记录面板或 `AIHISTORY` 命令实现。

## Phase P5A: Production JSON Input And CadIntent

### P5A-01 Define CadIntentSpec v1 Contract

Goal: 定义比 DrawingSpec 更适合用户和外部 AI 输入的上层 JSON 合同，支持模板、组合特征和受控草图三种新建图输入。

Context Files:

- `docs/capability-contract.md`
- `docs/implementation-flows.md`
- `specs/drawing-spec-v1.md`
- `specs/drawing-spec-v1.schema.json`

Deliverables:

- `CadIntentSpec v1` 文档或 schema 草案。
- intent 输入类型识别规则：`TemplateIntent`、`CompositeIntent`、`SketchIntent`、`DrawingSpec`。
- 稳定 issue code：`missing_required_parameter`、`unsupported_domain_pack`、`unsupported_template`、`unsupported_feature_type`、`unsupported_segment_type`、`profile_not_closed`、`unsupported_json_contract`。
- clarify 规则：关键尺寸、位置、半径、角度、闭合关系缺失时返回 clarification，不猜测。

Acceptance Criteria:

- 合法 `TemplateIntent`、`CompositeIntent`、`SketchIntent` 可被识别。
- 缺少关键尺寸返回稳定 issue，而不是进入 DrawingSpec renderer。
- 未知领域、模板、feature、segment 返回稳定 issue。
- 直接粘贴 DrawingSpec 仍可被识别为高级入口。
- 合同明确第一阶段不支持任意 DWG 自动理解、建筑、电气、管线、3D 或钣金展开。

### P5A-02 Implement Mechanical Plate Domain Pack

Goal: 让高频机械板件输入不调用模型，直接通过本地领域包编译为 DrawingSpec。

Context Files:

- `docs/capability-contract.md`
- `docs/cad-standards.md`
- `examples/rectangular-plate.example.json`
- `src/ZwcadAi.Core/RectangularPlateSample.cs`

Deliverables:

- `mechanical_plate` 领域包接口实现。
- `TemplateIntent` 编译器：至少支持 `rectangular_plate`。
- `CompositeIntent` 编译器：支持 rectangle base profile、hole、slot、fillet、基础 overall/feature dimensions 和 center marks。
- 自动补齐标准图层、稳定 id、常规尺寸文本和默认标注偏移。

Acceptance Criteria:

- `1200x300 rectangular_plate` 从 intent 编译为合法 DrawingSpec，且不创建模型客户端、不读取 API key、不联网。
- 组合孔、槽、圆角生成的 DrawingSpec 通过 schema、业务规则和 renderer plan 校验。
- 重复输入生成一致 DrawingSpec，稳定 id 不依赖时间戳或随机数。
- 简单模板路径目标预览耗时小于 2 秒。

### P5A-03 Implement Generic 2D Mechanical Sketch Pack

Goal: 支持非标准二维机械新建图，不要求套固定模板，但仍限制在受控草图 JSON 内。

Context Files:

- `specs/drawing-spec-v1.md`
- `docs/cad-standards.md`
- `src/ZwcadAi.Renderer/DrawingRenderPlan.cs`

Deliverables:

- `generic_2d_mechanical` 领域包接口实现。
- `SketchIntent` 编译器：支持 line、arc、circle/hole、slot、profile、text/mtext、linear/aligned/radius/diameter/angular dimension intent。
- profile 闭合、segment 连续性、feature target、dimension target 校验。
- 非标准件预览摘要字段：轮廓是否闭合、segment 数量、feature 数量、标注数量、未解析问题。

Acceptance Criteria:

- line/arc 混合闭合 profile 可编译为 DrawingSpec 并通过 renderer plan。
- 未闭合 profile 返回 `profile_not_closed`。
- 未知 segment/feature/dimension 类型返回 `unsupported_*`。
- SketchIntent 到预览目标耗时小于 5 秒，前提是实体数量低于 MVP 限制。
- 第一阶段不读取或上传完整 DWG；已有 DWG 修改需求明确转入后续 EditSpec 任务。

### P5A-04 Add Pasted JSON Preview Path To Plugin UI

Goal: 把“粘贴 JSON / 校验预览”作为默认用户路径，绕开内部模型等待和 token 成本。

Context Files:

- `src/ZwcadAiPlugin/AiDrawingPanelControl.cs`
- `src/ZwcadAiPlugin/AiDrawingPluginServices.cs`
- `src/ZwcadAi.Ui/AiDrawingPanelState.cs`
- `docs/implementation-flows.md`

Deliverables:

- 面板输入模式：`意图 JSON`、`DrawingSpec JSON`、`模型生成`。
- “校验预览”按钮，直接走本地 intent/DrawingSpec 解析、校验、renderer plan 和预览摘要。
- 输入类型显示：`TemplateIntent`、`CompositeIntent`、`SketchIntent`、`DrawingSpec`。
- 校验失败显示可复制 `code/path/message`。

Acceptance Criteria:

- 粘贴 intent 后可预览，预览成功后才允许确认写入。
- 粘贴 JSON 路径不创建模型客户端、不读取 API key、不联网。
- 粘贴 DrawingSpec 仍走现有 schema/business/renderer 校验。
- 预览计划与确认写入计划保持一致。
- 内部模型路径保留但标记为高级增强，不阻塞 P5A 验收。

### P5A-05 Create External AI Skill Templates

Goal: 为外部 AI 软件提供标准 skill 模板，让 AI 默认输出 CadIntent JSON，而不是完整 DrawingSpec。

Context Files:

- `prompts/gpt-system-prompt.md`
- `prompts/model-prompt-contract-v1.md`
- `specs/drawing-spec-v1.md`
- `docs/capability-contract.md`

Deliverables:

- `zwcad-mechanical-plate-intent` skill 模板。
- `zwcad-generic-2d-mechanical-intent` skill 模板。
- 3 类输出示例：`TemplateIntent`、`CompositeIntent`、`SketchIntent`。
- 修错提示模板：把插件返回的 `code/path/message` 贴回 AI 后，只修 intent JSON。

Acceptance Criteria:

- Skill 明确只输出 JSON，不输出 Markdown、CAD 命令、脚本或插件操作。
- 标准件输出 `TemplateIntent`，模板组合输出 `CompositeIntent`，非标准二维机械轮廓输出 `SketchIntent`。
- 缺少关键工程参数时输出 `clarifications`。
- 只有用户明确要求高级调试时才输出完整 DrawingSpec。

### P5A-06 Build CadIntent Regression And Performance Gate

Goal: 在进入 P6 前建立 intent 层回归，防止本地编译器和 UI 预览路径退化。

Context Files:

- `examples/`
- `src/ZwcadAi.Tests/Program.cs`
- `checklists/execution-checklist.md`

Deliverables:

- 不少于 10 个 `mechanical_plate` intent 样例。
- 不少于 5 个 `generic_2d_mechanical` sketch intent 样例。
- 每个样例编译后的 DrawingSpec 进入现有 schema/business/renderer plan 回归。
- 简单模板、组合模板、非标准 sketch 的耗时记录。

Acceptance Criteria:

- 所有 intent 样例可批量编译和校验。
- 简单矩形路径模型调用次数为 0。
- `TemplateIntent` 预览小于 2 秒，`CompositeIntent` 预览小于 3 秒，`SketchIntent` 预览小于 5 秒。
- 失败报告包含样例名、输入类型、issue code 和 path。

## Phase P6: Verification And Regression

### P6-01 Build Geometry Summary

Goal: 从 DWG 中提取可比较的几何摘要。

Context Files:

- `docs/architecture.md`

Deliverables:

- GeometrySummary 类型。
- 实体数量、类型、图层、关键尺寸提取。

Acceptance Criteria:

- 可对矩形板件输出宽、高、孔径、孔位。
- 可对比预期和实际。
- 误差容差可配置。

### P6-02 Build Regression Sample Suite

Goal: 建立样例回归测试，防止渲染逻辑退化。

Context Files:

- `examples/rectangular-plate.example.json`
- `examples/`

Deliverables:

- 至少 10 个 DrawingSpec 样例。
- 至少 10 个 CadIntent 样例。
- 期望几何摘要。
- 回归运行说明。

Acceptance Criteria:

- 所有样例可批量渲染。
- 所有 CadIntent 样例可先批量编译为 DrawingSpec。
- 几何摘要可批量比对。
- 失败报告包含样例名、输入类型、issue code、path 和实体 id。

### P6-03 Stability Test

Goal: 验证连续出图和异常场景稳定性。

Context Files:

- `checklists/execution-checklist.md`

Deliverables:

- 连续生成测试。
- 异常输入测试。
- 性能报告。

Acceptance Criteria:

- 连续生成 100 张样例图不崩溃。
- 非法 JSON、缺参 intent、未闭合 sketch、网络失败、导出失败都有清晰错误。
- 单张标准图渲染耗时满足目标。

## Phase P7: Packaging And Deployment

### P7-01 Build Installer

Goal: 生成可交付的插件安装包。

Context Files:

- `docs/environment-matrix.md`

Deliverables:

- 安装包。
- 安装说明。
- 卸载说明。

Acceptance Criteria:

- 新机器可安装。
- 插件可加载。
- 配置目录和日志目录明确。

### P7-02 Write User And Admin Guides

Goal: 编写用户手册和管理员部署手册。

Context Files:

- `docs/implementation-flows.md`
- `docs/architecture.md`

Deliverables:

- `docs/user-guide.md`
- `docs/deployment-guide.md`

Acceptance Criteria:

- 用户能完成一次自然语言绘图。
- 管理员能配置模型服务和企业标准。
- 常见错误有处理说明。
