# Execution Checklist

Last reconciled: 2026-05-02

## Active Development Flow

- [x] P5A-01 定义 `CadIntentSpec v1` 合同、输入类型识别和稳定 issue code。
- [ ] P5A-02 实现 `mechanical_plate` 领域包，把标准件/组合特征 intent 本地编译为 DrawingSpec。
- [ ] P5A-03 实现 `generic_2d_mechanical` SketchIntent 领域包，支持非标准二维机械新建图。
- [ ] P5A-04 将插件默认主路径调整为“粘贴 JSON / 校验预览 / 确认写入”。
- [ ] P5A-05 形成外部 AI Skill 模板，让 AI 默认输出 CadIntent JSON。
- [ ] P5A-06 建立 CadIntent 样例、编译回归和性能门槛。
- [ ] 完成 P5A 后再推进 P6 DWG 反向几何摘要、批量回归、稳定性和部署文档。

## P0 Scope And Environment

- [x] 确认 MVP 领域（`mechanical_plate` 已在 `docs/capability-contract.md` 锁定）。
- [x] 确认 ZWCAD 版本（P1 本机基线为 ZWCAD 2025 x64；ZWCAD 2026 保留为后续兼容目标）。
- [x] 确认 SDK 版本（ZWCAD 2025 managed assemblies 已用于本机基线；ZWCAD 2026 ZRX SDK 已缓存但未作为当前验收前提）。
- [x] 收集企业 CAD 标准（P0-03 已形成 `enterprise-default-v1` 草案；正式企业模板、CTB/STB、块库仍待管理员提供）。
- [x] 收集样例需求（P0-01 已列出 12 类 MVP 样例需求）。
- [x] 完成环境矩阵（`docs/environment-matrix.md` 已记录本机基线、兼容目标和未验证环境）。

## P1 Technical POC

- [x] 创建解决方案骨架。
- [x] 实现最小插件命令。
- [x] 绘制固定矩形板件。
- [x] 验证事务回滚。
- [x] 验证 DWG 保存（`AIEXPORT` 已在 ZWCAD 内生成 DWG 副本）。
- [x] 验证 PDF 导出（`AIEXPORT` 已在 ZWCAD 内生成 PDF）。

## P2 DrawingSpec Protocol

- [x] 完成 DrawingSpec v1 Schema。
- [x] 完成协议说明文档。
- [x] 完成 5 个以上样例。
- [x] 实现 Schema 校验。
- [x] 实现业务规则校验。

## P3 Renderer

- [x] 渲染基础实体。
- [x] 渲染标注。
- [x] 渲染中心线。
- [x] 应用图层和样式。
- [x] 输出几何摘要。
- [x] 支持错误回滚（自动化失败注入和取消回滚已覆盖）。
- [x] P3-03 渲染中途异常不提交半成品。
- [x] P3-03 用户取消不提交正式实体。
- [x] P3-03 在 ZWCAD 内手工验证一次 Undo 回退自动绘制内容。

## P4 AI Service

- [x] 完成 Prompt 合同。
- [x] 接入模型服务（P4-04 已完成 HTTP/私有网关 `IAiModelClient` 主线；API key 只从配置指定环境变量读取）。
- [x] 实现结构化输出（P4-02 本地 deterministic adapter 已将原始输出映射为 `DrawingSpec` / `NeedsClarification` / `Rejected`）。
- [x] 实现缺参追问（P4-05 已完成服务层 `NeedsClarification -> ContinueDrawingSpecAfterClarification -> CreateDrawingSpec` 闭环；P5-01 面板已按该服务入口继续生成）。
- [x] 实现校验失败修复循环（P4-03 已覆盖 schema/business validation 失败后的 bounded DrawingSpec-only repair；clarification/unsafe command 不进入 repair）。
- [x] 实现调用日志和脱敏（P4-06 已完成结构化日志事件、默认脱敏 writer、敏感内容 opt-in 和 API key 禁止记录测试）。

## P5 Plugin UI

- [x] 实现 Dock 面板（P5-01 代码层接入 ZWCAD `PaletteSet`；仍需 ZWCAD 内手工视觉验收）。
- [x] 实现自然语言输入。
- [x] 实现参数表（P5-01 最小 summary/parameter table）。
- [x] 实现预览摘要。
- [x] 实现确认写入。
- [ ] 完成粘贴 JSON 到预览/确认写入的 ZWCAD 内手工验收（P5A 默认主路径，当前尚未实现）。
- [ ] 完成内部模型 API 到预览/确认写入的 ZWCAD 内手工验收（高级增强路径，当前成功端到端验收未关闭）。
- [ ] 实现历史记录（P5-02 尚无历史记录存储、历史面板或 `AIHISTORY` 命令实现）。

## P5A Production JSON Input And CadIntent

- [x] `CadIntentSpec v1` 支持 `TemplateIntent`、`CompositeIntent`、`SketchIntent` 和直接 `DrawingSpec` 识别。
- [x] intent 校验覆盖缺少关键尺寸、未知领域、未知模板、未知 feature、未知 segment、未闭合 profile。
- [ ] `mechanical_plate` 领域包可编译 `1200x300 rectangular_plate` 并自动补齐图层、稳定 id、基础标注。
- [ ] `mechanical_plate` 组合特征支持 hole、slot、fillet、overall dimensions、feature dimensions 和 center marks。
- [ ] `generic_2d_mechanical` SketchIntent 支持 line/arc 混合闭合轮廓、hole/slot、text/mtext 和基础 dimension intent。
- [ ] 非标准件预览显示轮廓闭合状态、segment 数量、feature 数量、标注数量和未解析问题。
- [ ] 插件面板新增“粘贴 JSON / 校验预览”路径，且该路径不创建模型客户端、不读取 API key、不联网。
- [ ] 粘贴 JSON 失败时显示可复制 `code/path/message`。
- [ ] 外部 AI Skill 模板默认输出 CadIntent JSON，完整 DrawingSpec 仅作为高级调试输出。
- [ ] 回归样例不少于 10 个 `mechanical_plate` intent 和 5 个 `generic_2d_mechanical` sketch intent。
- [ ] 性能门槛：TemplateIntent < 2 秒，CompositeIntent < 3 秒，SketchIntent < 5 秒；简单矩形模型调用次数为 0。

## P6 Verification

- [ ] 建立 DWG 反向几何摘要。
- [ ] 建立 DrawingSpec 与 CadIntent 联合回归样例。
- [ ] 建立批量编译/批量渲染测试。
- [ ] 建立异常输入测试（非法 JSON、缺参 intent、未闭合 sketch、网络失败、导出失败）。
- [ ] 建立稳定性测试。
- [ ] 建立关键尺寸比对和误差容差配置。

## P7 Deployment

- [ ] 制作安装包。
- [ ] 编写用户手册（默认说明外部 AI Skill 生成 CadIntent、插件粘贴 JSON 校验预览）。
- [ ] 编写管理员手册。
- [ ] 验证新机器安装。
- [ ] 验证配置迁移。
- [ ] 建立问题样例回收流程。
