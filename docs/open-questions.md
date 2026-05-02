# Open Questions

## Product Scope

- 已决策：后续默认主路径调整为“外部 AI/人工生成 CadIntent JSON -> 插件粘贴 JSON -> 本地校验预览 -> 确认写入”，内部模型 API 保留为高级增强路径。
- 已决策：复杂图形第一阶段定义为二维机械新建图的模板组合和受控非标准草图，不承诺任意 DWG 自动理解、建筑、电气、管线、3D 或钣金展开。
- 已决策：DrawingSpec v1 保持为 renderer 内部稳定合同；CadIntentSpec v1 作为更适合用户和外部 AI 输入的上层合同。
- 每张图是否必须套用企业图框？
- 是否需要材料、工艺、标题栏属性联动？
- `generic_2d_mechanical` 第一批非标准件 sketch 样例应由哪些真实图纸需求构成？
- 已有 DWG 修改的 `EditSpec` 是否进入 P6/P7 前范围，还是作为后续独立阶段？

## CAD Environment

- 用户环境是单机、企业内网还是远程桌面？
- 是否允许插件联网？

## Standards

- 是否有企业正式图层、线型、线宽标准文件，需要替换 `enterprise-default-v1` 草案？
- 是否有企业正式标注样式和文字样式标准，需要替换 `enterprise-default-v1` 草案？
- 是否已有正式块库、图框模板和 CTB/STB 文件？
- 是否需要支持不同部门或项目的标准切换？

## AI And Data

- 是否允许把自然语言需求发送到云端模型？
- 是否允许上传几何摘要？
- 是否需要在策略上完全禁止 DWG 上传，而不是仅默认不上传？
- 模型调用日志的保留周期、存储位置和访问权限如何配置？
- 外部 AI Skill 模板应优先适配哪些 AI 软件：Codex、ChatGPT、Claude，还是企业内置模型平台？

## Acceptance

- MVP 样例数量是多少？
- 几何正确率目标是多少？
- 标注布局是否纳入自动化验收？
- 谁负责工程正确性最终签核？
- 出图速度和稳定性目标是多少？
- P5A 性能门槛是否锁定为 TemplateIntent < 2 秒、CompositeIntent < 3 秒、SketchIntent < 5 秒？
