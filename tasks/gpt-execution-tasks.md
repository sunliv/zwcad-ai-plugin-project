# GPT Execution Tasks

本文件把项目拆成适合 GPT/Codex 执行的工程任务。每个任务都应按 `tasks/task-template.md` 单独派发，避免一次性要求模型完成整套系统。

## Execution Rules For GPT

- 每次只执行一个任务 ID。
- 开始前先读取任务的 Context Files。
- 不要越权修改任务未列出的文件。
- 所有实现必须有可验证输出。
- 不确定时把问题写入 `docs/open-questions.md`，不要默默假设。
- 涉及 CAD 写入、文件覆盖、外部服务调用时必须有安全边界。

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

Deliverables:

- 至少 10 个 DrawingSpec 样例。
- 期望几何摘要。
- 回归运行说明。

Acceptance Criteria:

- 所有样例可批量渲染。
- 几何摘要可批量比对。
- 失败报告包含样例名和实体 id。

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
- 非法 JSON、网络失败、导出失败都有清晰错误。
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
