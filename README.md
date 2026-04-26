# ZWCAD AI Plugin Project

本项目文件夹用于规划并推进一个长期稳定的“AI 绘图插件”：在中望 CAD 内部运行插件，接收自然语言或参数化输入，生成受控的 DrawingSpec，再由确定性的 CAD Renderer 创建 DWG 实体、标注、图层、图框和导出结果。

## 核心原则

- AI 只生成结构化 DrawingSpec，不直接执行任意 CAD 命令。
- 插件内绘图内核负责把 DrawingSpec 转成 ZWCAD 实体。
- 所有输出必须经过 Schema 校验、业务规则校验和几何复核。
- 关键操作需要用户确认，所有绘图操作必须支持 Undo 或事务回滚。
- 项目先做有限领域 MVP，再扩大支持范围。

## 文件导航

| 文件 | 用途 |
|---|---|
| `docs/capability-contract.md` | 能力契约：边界、约束、接口、非目标、开放问题 |
| `docs/architecture.md` | 推荐架构、模块边界、数据流、错误处理 |
| `docs/implementation-flows.md` | 用户流程和系统流程列表 |
| `docs/open-questions.md` | 启动开发前需要确认的问题 |
| `tasks/gpt-execution-tasks.md` | 适合 GPT/Codex 分阶段执行的任务清单 |
| `tasks/task-template.md` | 单个任务交给 GPT 执行时的标准模板 |
| `checklists/feasibility-checklist.md` | 项目可行性检查清单 |
| `checklists/execution-checklist.md` | 阶段执行检查清单 |
| `specs/drawing-spec-v1.schema.json` | DrawingSpec v1 JSON Schema 初稿 |
| `examples/rectangular-plate.example.json` | 标准矩形板件样例 |
| `prompts/gpt-system-prompt.md` | 让 GPT 生成 DrawingSpec 的系统提示词 |
| `prompts/task-execution-prompt-template.md` | 交给 GPT 执行工程任务的提示词模板 |

## 建议执行顺序

1. 先阅读 `docs/capability-contract.md`，确认能力边界和非目标。
2. 使用 `checklists/feasibility-checklist.md` 完成版本、SDK、场景和数据确认。
3. 按 `tasks/gpt-execution-tasks.md` 从 P0 到 P7 依次推进。
4. 每个任务启动前复制 `tasks/task-template.md`，填入本次任务上下文。
5. 每完成一个阶段，更新 `checklists/execution-checklist.md`。

## MVP 建议范围

- 支持 2D 标准板件绘图。
- 支持矩形外形、孔、槽、圆角、倒角、中心线。
- 支持线性、对齐、半径、直径标注。
- 支持企业图层、文字样式、标注样式和图框模板。
- 支持导出 DWG/PDF。
- 支持几何校验、错误报告、用户确认和 Undo。

## 暂不纳入 MVP

- 任意复杂 DWG 自动理解。
- 三维建模。
- 从图片直接还原工程图。
- 无用户确认的批量覆盖修改。
- 让 AI 直接执行自由 LISP、脚本或 CAD 命令。

