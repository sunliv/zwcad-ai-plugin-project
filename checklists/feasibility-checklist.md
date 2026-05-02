# Feasibility Checklist

Last reconciled: 2026-05-02

## Product

- [x] 已明确 MVP 业务领域（`mechanical_plate` 二维机械矩形板件/安装板）。
- [ ] 已收集至少 20 个真实绘图需求样例（当前 P0-01 文档化 12 类样例需求，尚未达到 20 个真实样例）。
- [x] 已明确用户是工程师、制图员、管理员还是二次开发人员（目标用户和插件操作者已在 capability contract 中定义）。
- [ ] 已定义成功标准：正确率、速度、稳定性、人工修正次数。
- [x] 已明确 AI 辅助绘图不替代工程审核。

## CAD Environment

- [x] 已锁定目标 ZWCAD 版本（ZWCAD 2025 x64 为当前本机基线；ZWCAD 2026 为后续兼容目标）。
- [x] 已获取匹配版本 SDK（ZWCAD 2025 managed assemblies 已确认；ZWCAD 2026 ZRX SDK 已缓存）。
- [x] 已验证插件可加载（ZWCAD 2025 `NETLOAD` + `AIDRAW` POC 基线已手工验收）。
- [x] 已验证基础实体创建能力。
- [x] 已验证标注、图层、线型、文字样式能力（P3 图层/样式基线和 ZWCAD 内视觉验收已覆盖）。
- [x] 已验证保存 DWG 和导出 PDF 能力（P1-04 `AIEXPORT` 已在 ZWCAD 内生成 DWG 副本和 PDF）。
- [x] 已验证 Undo 或事务回滚能力（P3-03 自动化事务回滚和 ZWCAD 内一次 Undo 手工验证均已覆盖）。

## Standards

- [x] 已有图层标准（`enterprise-default-v1` 图层白名单已文档化并进入校验/渲染基线）。
- [x] 已有标注样式标准（`AI_MECH_MM` / `AI_MECH_DIAMETER` / `AI_MECH_RADIUS` 草案）。
- [x] 已有文字样式标准（`AI_NOTE_3_5` / `AI_DIM_TEXT_3_5` / `AI_TITLE_*` 草案）。
- [ ] 已有图框模板。
- [ ] 已有块库。
- [x] 已确定标准配置的版本管理方式（`enterprise-default-v1` profile id 和未来 `config/cad/standards/enterprise-default-v1.json` 路径已定义）。

## AI

- [x] AI 输出限定为 DrawingSpec（P4-02 本地 adapter 执行 JSON-only 边界，clarification 映射为独立响应类型）。
- [x] 已有 JSON Schema。
- [x] 已有缺参追问策略（P4-05 服务闭环和 P5-01 面板入口已覆盖）。
- [x] 已有失败重试策略（P4-03 DrawingSpec-only bounded repair loop 和 P4-04 provider timeout/retry/cancellation 映射已覆盖）。
- [x] 已有模型调用日志策略（P4-06 默认脱敏日志、敏感内容 opt-in 和 API key 永不记录已覆盖）。
- [x] 已明确云端、私有网关或本地模型路线（P4-04 优先 HTTP/私有网关 provider 主线；本地 adapter 保留服务边界）。

## Security

- [x] 不执行 AI 返回的任意代码（P4-02 adapter 拒绝自由 CAD 命令和脚本式输出）。
- [x] API Key 不写入源码（P4-04 从配置指定环境变量读取；P4-06 日志层仍强制脱敏）。
- [ ] 文件保存路径受限制或需要用户确认。
- [x] 不默认上传完整 DWG（P4 HTTP create/repair 请求边界均不携带 DWG、截图或任意插件上下文）。
- [x] 生产日志默认脱敏。
- [x] 外部服务失败时不修改 DWG（`ZwcadAi.AiService` 只返回 rejected issue，不拥有 DWG mutation）。

## Verification

- [ ] 可以从 DWG 提取几何摘要。
- [ ] 可以比对尺寸、孔位、图层、标注。
- [ ] 有回归样例库。
- [x] 有异常输入测试（Schema/business/model non-JSON/unsafe command/HTTP timeout/cancellation 等服务与校验异常已覆盖）。
- [ ] 有连续生成稳定性测试。
