# Implementation Flows

## Flow 0: Pasted CadIntent JSON Drawing

1. 用户在外部 AI 软件中启用领域 skill，或人工编写 CadIntent JSON。
2. 用户运行 `AIDRAW`。
3. 插件打开 Dock 面板，默认进入“粘贴 JSON / 校验预览”路径。
4. 用户粘贴 JSON。
5. 插件识别输入类型：`TemplateIntent`、`CompositeIntent`、`SketchIntent` 或完整 `DrawingSpec`。
6. 如果输入是 CadIntent，插件先执行 intent 校验，再通过对应领域包编译为 DrawingSpec。
7. 如果输入是 DrawingSpec，插件直接进入现有 DrawingSpec 校验链路。
8. 插件执行 Schema、业务规则和 renderer plan 校验。
9. 插件显示输入类型、轮廓闭合状态、segment 数量、feature 数量、标注数量、预览摘要和可复制 issue。
10. 用户确认应用。
11. CAD-facing writer 在单个 DocumentLock + Transaction 中创建 CAD 实体和标注。
12. 插件显示写入结果；后续 P6 再做完整 DWG 几何摘要和批量回归。

该路径不得创建模型客户端、读取 API key 或联网。内部模型 API 是后续增强路径，不是 P5A 默认验收路径。

## Flow 1: Natural Language Drawing

1. 用户运行 `AIDRAW`。
2. 插件打开 Dock 面板。
3. 用户输入自然语言需求。
4. 插件收集上下文：单位、模板、企业标准、允许实体类型。
5. AI 服务返回 DrawingSpec 或澄清问题。
6. 插件校验 Schema。
7. 插件校验业务规则和几何约束。
8. 插件显示预览摘要。
9. 用户确认应用。
10. CAD-facing writer 在单个 DocumentLock + Transaction 中创建 CAD 实体和标注。
11. 插件提取几何摘要。
12. 插件复核尺寸、图层、标注和实体数量。
13. 插件显示结果报告。

该路径保留为高级增强。后续内部模型 API 默认应生成 CadIntent JSON，由本地领域包编译为 DrawingSpec；完整 DrawingSpec 生成只作为高级调试入口。

### Flow 1a: Clarification Follow-up Loop

1. 首轮 `CreateDrawingSpec` 缺少关键工程参数时，AI 服务返回 `NeedsClarification`。
2. 服务层保存最小澄清状态：`requestId`、原始用户问题、澄清问题、用户回答和 `promptVersion`。
3. 澄清状态不得携带 DWG、截图、选择集、插件上下文或 API key。
4. 用户补充回答后，服务层发起新一轮 `CreateDrawingSpec`，并把原始用户问题、澄清问题和用户回答合并为新的自然语言 `userRequest`。
5. 澄清回答不得进入 `RepairDrawingSpec`；repair loop 仍只接收上一轮无效 DrawingSpec JSON 和稳定 issue 列表。
6. 新一轮 create 返回有效 DrawingSpec 后，继续执行 Schema、业务规则和 renderer plan 校验。

## Flow 2: Parameterized Drawing

1. 用户选择模板类型，例如“矩形板件”。
2. 插件展示参数表。
3. 用户填写尺寸、孔位、倒角、圆角、材料、比例。
4. 插件直接生成 DrawingSpec，必要时 AI 只负责补全描述。
5. 插件校验参数范围。
6. CAD-facing writer 创建实体和标注，并在失败或取消时回滚事务。
7. 插件复核并导出。

P5A 后该流程并入 CadIntent：标准件走 `TemplateIntent`，模板组合件走 `CompositeIntent`，非标准二维机械新建图走 `SketchIntent`。

## Flow 3: Existing Drawing Edit

1. 用户选择现有实体或区域。
2. 插件提取选择集几何摘要。
3. 用户输入修改要求。
4. AI 服务输出 EditSpec。
5. 插件高亮即将修改的对象。
6. 用户确认。
7. 插件在事务中修改实体。
8. 插件复核修改结果。
9. 插件记录旧值、新值和 request id。

已有 DWG 修改不走 `SketchIntent`。第一阶段只支持非标准件新建绘图；已有图编辑后续单独定义 `EditSpec`、`SelectionGeometrySummary` 和本地高亮预览。

## Flow 4: Validation And Repair

1. 插件读取 DrawingSpec 和渲染后的 GeometrySummary。
2. 校验器生成错误列表。
3. 如果错误可自动修复，AI 服务只修复 DrawingSpec 的错误字段。
4. 插件重新校验。
5. 达到最大重试次数后停止，并向用户展示错误。

## Flow 5: Export And Delivery

1. 用户运行 `AIEXPORT`。
2. 插件检查图框、比例、打印样式和导出路径。
3. 插件保存 DWG 副本。
4. 插件导出 PDF。
5. 插件生成校验报告。
6. 插件记录版本、时间、用户、模型、DrawingSpec 和输出路径。
