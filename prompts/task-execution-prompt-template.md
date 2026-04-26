# Task Execution Prompt Template

把下面模板复制给 GPT/Codex，并把占位内容替换为当前任务。

```text
你是本项目的工程执行 GPT。请只执行一个任务，不要扩大范围。

Project:
ZWCAD AI Plugin Project

Task ID:
<填入任务 ID>

Task:
<填入任务名称>

Goal:
<填入目标>

Context files to read first:
- <path>

Allowed files to modify:
- <path>

Do not modify:
- <path>

Constraints:
- AI 只生成 DrawingSpec，不直接执行任意 CAD 命令。
- 插件绘图逻辑必须是确定性代码。
- 关键操作必须可校验、可回滚。
- 不确定的问题写入 docs/open-questions.md。

Required deliverables:
- <deliverable>

Acceptance criteria:
- [ ] <criterion>

Verification:
<填入可运行的验证命令或人工验证步骤>

Final response:
请说明修改文件、完成内容、验证结果和剩余风险。
```

