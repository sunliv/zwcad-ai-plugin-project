# Implementation Flows

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
10. Renderer 在事务中创建 CAD 实体。
11. 插件提取几何摘要。
12. 插件复核尺寸、图层、标注和实体数量。
13. 插件显示结果报告。

## Flow 2: Parameterized Drawing

1. 用户选择模板类型，例如“矩形板件”。
2. 插件展示参数表。
3. 用户填写尺寸、孔位、倒角、圆角、材料、比例。
4. 插件直接生成 DrawingSpec，必要时 AI 只负责补全描述。
5. 插件校验参数范围。
6. Renderer 创建实体和标注。
7. 插件复核并导出。

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

