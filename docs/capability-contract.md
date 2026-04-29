# Capability Contract: ZWCAD AI Drawing Plugin

## CAPABILITY

中望 CAD 用户可以在 CAD 内部通过自然语言或参数化表单描述标准化二维图形需求，插件将需求转换为 DrawingSpec，经过 Schema、业务规则和几何校验后，在当前 DWG 中生成可编辑、可标注、可导出的 CAD 实体。项目目标是稳定辅助工程师提高重复性出图效率，而不是替代工程审核。

## MVP DOMAIN

- 首个 MVP 业务领域锁定为 `mechanical_plate`：二维机械矩形板件/安装板自动出图。
- MVP 只处理毫米制 2D 板件，不处理建筑、电气、管线、钣金展开、3D 建模或任意 DWG 自动理解。
- 目标用户是需要快速生成标准板件草图、安装孔位图和交付检查图的机械工程师或制图员。
- 首版输出必须生成可编辑的 ZWCAD 实体、中心线/中心标记、基础尺寸标注，并能进入后续 DWG/PDF 导出验证。
- 输入方式允许自然语言和参数表并存；P1 POC 优先用固定 DrawingSpec 和参数化样例，不依赖 AI 生成。

### MVP Sample Demand Types

1. 矩形板件：给定长、宽、厚度备注和外轮廓。
2. 单孔板件：给定板件尺寸、孔中心坐标、孔直径。
3. 中心孔板件：孔位默认位于板件中心，但必须由规格显式表达。
4. 双孔安装板：给定两个孔的中心距、孔径和边距。
5. 四角安装孔板：给定边距、孔径和孔位阵列。
6. 圆角板件：给定四角圆角半径和外轮廓尺寸。
7. 倒角板件：给定一个或多个角的倒角尺寸。
8. 长圆孔板件：给定槽中心、槽长、槽宽和方向。
9. 矩形槽板件：给定槽起点/中心、宽度、高度和圆角选项。
10. 非对称孔位板件：给定多个孔的绝对坐标和直径。
11. 带中心线板件：为孔、槽或板件中心生成中心线/中心标记。
12. 带基础标注板件：生成外形尺寸、孔径/槽宽和孔位尺寸。

## CONSTRAINTS

- 首期能力限定为 `mechanical_plate` 2D 标准化机械板件，不在同一 MVP 中扩展到建筑、电气、管线或 3D 场景。
- AI 输出必须是 DrawingSpec JSON，不能直接输出并执行任意 LISP、SCR、COM 调用或 .NET 代码。
- CAD Renderer 必须是确定性代码，同一 DrawingSpec 在同一模板和配置下应生成一致结果。
- 所有坐标、单位、角度、半径、偏移、图层、标注样式必须显式化。
- 缺少关键尺寸时必须追问，不能猜测关键工程参数。
- 写入 DWG 前必须完成校验并给用户确认入口。
- 每次自动绘制必须在可回滚事务或可 Undo 命令组内完成。
- 写入取消必须发生在事务提交前；取消结果不得返回正式 CAD 对象映射。
- 文件保存、导出、覆盖、删除、批量修改必须受路径和权限限制。
- API Key、模型服务地址和企业标准配置不能写死在源码中。
- 生产日志不能默认记录敏感图纸全文或完整用户业务数据。

## IMPLEMENTATION CONTRACT

### Actors

- CAD 用户：输入需求、确认预览、保存和交付图纸。
- 插件操作者：配置模板、图层、块库、模型服务和权限。
- AI 服务：将自然语言转换为 DrawingSpec 或 EditSpec。
- CAD Renderer：把已校验规格转换为 ZWCAD 实体。
- 校验器：检查协议、业务规则、几何事实和输出质量。

### Surfaces

- ZWCAD 命令：`AIDRAW`、`AICHECK`、`AIEXPORT`、`AISETTINGS`。
- Dock 面板：自然语言输入、参数表、预览摘要、错误提示、历史记录。
- 本地服务 API：模型调用、规格修复、日志脱敏、配置管理。
- 文件接口：模板、块库、图层配置、DrawingSpec 样例、校验报告。

### States And Transitions

| State | Meaning | Allowed Next States |
|---|---|---|
| `draft_request` | 用户输入尚未解析 | `spec_generated`、`needs_clarification` |
| `needs_clarification` | 关键参数缺失 | `draft_request` |
| `spec_generated` | AI 已生成 DrawingSpec | `schema_validated`、`rejected` |
| `schema_validated` | 协议结构合法 | `rules_validated`、`rejected` |
| `rules_validated` | 业务规则合法 | `preview_ready`、`rejected` |
| `preview_ready` | 可供用户确认 | `applied_to_dwg`、`render_canceled`、`draft_request` |
| `applied_to_dwg` | 已写入 DWG | `geometry_verified`、`rolled_back` |
| `geometry_verified` | 几何复核通过 | `exported`、`completed` |
| `exported` | 已导出交付文件 | `completed` |
| `rejected` | 校验失败或用户取消 | `draft_request` |
| `render_canceled` | 用户取消或取消令牌触发，事务未提交 | `draft_request` |
| `rolled_back` | 写入失败后回滚 | `draft_request` |

### Interfaces

AI 服务输入：

```json
{
  "userRequest": "画一个100x60矩形板，四角R5，中心有直径12孔",
  "context": {
    "units": "mm",
    "domain": "mechanical_plate",
    "allowedEntityTypes": ["line", "polyline", "circle", "arc", "text", "mtext", "centerMark"],
    "allowedDimensionTypes": ["linear", "aligned", "radius", "diameter", "angular"],
    "layerStandard": "enterprise-default-v1",
    "drawingSpecVersion": "1.0"
  }
}
```

AI 服务输出：

```json
{
  "drawingSpecVersion": "1.0",
  "units": "mm",
  "entities": [],
  "dimensions": [],
  "clarifications": []
}
```

P4-01 模型提示词合同：

- Prompt 版本固定为 `p4-01-model-prompt-contract-v1`，详细合同见 `prompts/model-prompt-contract-v1.md`。
- 自然语言只进入 `AiDrawingSpecRequest.UserRequest`，不是命令通道；模型不得返回 LISP、SCR、COM、.NET、ZRX、shell 或任意 CAD 命令。
- 模型原始响应必须是 DrawingSpec v1 JSON 根对象，不额外包一层自由 envelope；服务适配层再映射为 `AiDrawingSpecResponse.Kind = DrawingSpec | NeedsClarification | Rejected`。
- Schema 失败、业务规则失败和渲染/服务失败统一映射为稳定 issue：`code`、`path`、`message`、`severity`、`source`、`repairable`。
- 修复循环只接收上一轮 `invalidDrawingSpecJson` 与已映射的稳定 issue 列表，最多 2 次，并且只能修复 DrawingSpec JSON；关键工程参数缺失时必须追问用户。
- 澄清闭环使用 `NeedsClarification -> ContinueDrawingSpecAfterClarification -> CreateDrawingSpec`，服务层只保存 request id、原始用户问题、澄清问题、用户回答和 prompt version；澄清回答不得进入 `RepairDrawingSpec`。
- `enterprise-default-v1` 在 P4-01 中仍只是 `layerStandard` / profile id；企业标准配置化作为 P4/P5 接口设计项，不在本任务改动 `CadLayerStandards`、`CadTextStyleStandards`、`CadDimensionStyleStandards` 的集中入口。

插件内部接口：

- `ValidateSchema(DrawingSpec spec) -> ValidationResult`
- `ValidateBusinessRules(DrawingSpec spec, CadStandard standard) -> ValidationResult`
- `Render(DrawingSpec spec, RenderContext context) -> RenderResult`，状态为 `Success`、`Failed` 或 `Canceled`
- `ExtractGeometrySummary(Document document) -> GeometrySummary`
- `VerifyGeometry(DrawingSpec spec, GeometrySummary summary) -> VerificationResult`
- `Export(Document document, ExportOptions options) -> ExportResult`

### Data Model Implications

- DrawingSpec 需要版本号，后续协议演进必须兼容旧样例。
- 每个实体应有稳定 `id`，便于错误定位、局部修改和回归测试。
- 渲染后需要维护 `specEntityId -> cadObjectId` 映射。
- 失败路径必须使用稳定路径定位到 `$.entities[id]` 或 `$.dimensions[id]`。
- 企业标准应独立配置：图层、线型、线宽、颜色、文字样式、标注样式、图框、块库。
- 历史记录应保存输入、规格、校验结果、模型版本、插件版本和输出路径。

### Security And Policy

- 禁止执行 AI 返回的任意代码或命令。
- 禁止默认覆盖用户文件。
- 禁止把完整 DWG 或敏感图纸内容默认上传到外部模型。
- 所有外部服务调用需要超时、重试、失败提示和降级路径。
- 企业部署应支持私有模型网关或本地模型服务。

### Observability

- 每次绘图记录 request id、插件版本、模型版本、耗时、校验结果、失败原因。
- 渲染失败必须记录可定位实体 id 和错误码。
- 导出失败必须记录目标路径、导出格式、CAD 错误信息。
- 回归测试需要输出几何摘要和快照对比结果。

## NON-GOALS

- 不在 MVP 中实现任意 DWG 自动理解。
- 不在 MVP 中支持建筑平面、电气符号、管线系统图或其他非机械板件领域。
- 不在 MVP 中实现 3D 建模。
- 不在 MVP 中允许 AI 直接运行自由脚本。
- 不在 MVP 中做自动替代工程审核。
- 不在 MVP 中做无确认的批量覆盖修改。

## OPEN QUESTIONS

- 企业 CAD 标准是否已有图层、线型、标注、图框、块库文件？
- 模型服务使用云端 API、企业网关还是本地模型？
- 图纸数据是否允许离开内网？
- MVP 的验收样例数量是多少，谁负责判定图纸正确？

## HANDOFF

当前计划适合进入 P0 立项澄清和 P1 技术 POC。优先执行 `tasks/gpt-execution-tasks.md` 中的 P0 与 P1 任务，完成目标版本、SDK、样例、模板和最小插件加载验证后，再进入正式开发。
