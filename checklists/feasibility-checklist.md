# Feasibility Checklist

## Product

- [ ] 已明确 MVP 业务领域。
- [ ] 已收集至少 20 个真实绘图需求样例。
- [ ] 已明确用户是工程师、制图员、管理员还是二次开发人员。
- [ ] 已定义成功标准：正确率、速度、稳定性、人工修正次数。
- [ ] 已明确 AI 辅助绘图不替代工程审核。

## CAD Environment

- [ ] 已锁定目标 ZWCAD 版本。
- [ ] 已获取匹配版本 SDK。
- [ ] 已验证插件可加载。
- [ ] 已验证基础实体创建能力。
- [ ] 已验证标注、图层、线型、文字样式能力。
- [ ] 已验证保存 DWG 和导出 PDF 能力。
- [x] 已验证 Undo 或事务回滚能力（P3-03 自动化事务回滚和 ZWCAD 内一次 Undo 手工验证均已覆盖）。

## Standards

- [ ] 已有图层标准。
- [ ] 已有标注样式标准。
- [ ] 已有文字样式标准。
- [ ] 已有图框模板。
- [ ] 已有块库。
- [ ] 已确定标准配置的版本管理方式。

## AI

- [x] AI 输出限定为 DrawingSpec（P4-02 本地 adapter 执行 JSON-only 边界，clarification 映射为独立响应类型）。
- [x] 已有 JSON Schema。
- [x] 已有缺参追问策略（P4-01 合同和 P4-02 adapter 映射已覆盖；UI 追问流程未完成）。
- [x] 已有失败重试策略（P4-03 已实现 DrawingSpec-only bounded repair loop；真实 provider timeout/retry/cancellation hardening 仍放到 P4-04 或独立 provider 接入任务）。
- [ ] 已有模型调用日志策略。
- [x] 已明确云端、私有网关或本地模型路线（P4-02 优先本地 adapter/client seam，后续可挂 HTTP、本地模型或私有网关 provider）。

## Security

- [x] 不执行 AI 返回的任意代码（P4-02 adapter 拒绝自由 CAD 命令和脚本式输出）。
- [x] API Key 不写入源码（P4-02 只保留外部配置/env var seam，未硬编码密钥）。
- [ ] 文件保存路径受限制或需要用户确认。
- [x] 不默认上传完整 DWG（P4-02 adapter 请求边界不接收 DWG 内容）。
- [ ] 生产日志默认脱敏。
- [x] 外部服务失败时不修改 DWG（`ZwcadAi.AiService` 只返回 rejected issue，不拥有 DWG mutation）。

## Verification

- [ ] 可以从 DWG 提取几何摘要。
- [ ] 可以比对尺寸、孔位、图层、标注。
- [ ] 有回归样例库。
- [ ] 有异常输入测试。
- [ ] 有连续生成稳定性测试。
