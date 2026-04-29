# Execution Checklist

## P0 Scope And Environment

- [ ] 确认 MVP 领域。
- [ ] 确认 ZWCAD 版本。
- [ ] 确认 SDK 版本。
- [ ] 收集企业 CAD 标准。
- [ ] 收集样例需求。
- [ ] 完成环境矩阵。

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
- [ ] 接入模型服务（P4-02 已完成本地 adapter/client seam；真实 HTTP/本地模型 provider 仍未接入）。
- [x] 实现结构化输出（P4-02 本地 deterministic adapter 已将原始输出映射为 `DrawingSpec` / `NeedsClarification` / `Rejected`）。
- [ ] 实现缺参追问（P4-02 已映射 clarification 响应；UI 追问流程仍未实现）。
- [x] 实现校验失败修复循环（P4-03 已覆盖 schema/business validation 失败后的 bounded DrawingSpec-only repair；clarification/unsafe command 不进入 repair）。
- [x] 实现调用日志和脱敏（P4-06 已完成结构化日志事件、默认脱敏 writer、敏感内容 opt-in 和 API key 禁止记录测试）。

## P5 Plugin UI

- [ ] 实现 Dock 面板。
- [ ] 实现自然语言输入。
- [ ] 实现参数表。
- [ ] 实现预览摘要。
- [ ] 实现确认写入。
- [ ] 实现历史记录。

## P6 Verification

- [ ] 建立几何摘要。
- [ ] 建立回归样例。
- [ ] 建立批量渲染测试。
- [ ] 建立异常测试。
- [ ] 建立稳定性测试。

## P7 Deployment

- [ ] 制作安装包。
- [ ] 编写用户手册。
- [ ] 编写管理员手册。
- [ ] 验证新机器安装。
- [ ] 验证配置迁移。
- [ ] 建立问题样例回收流程。
