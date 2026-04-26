# GPT Task Template

复制本模板，填入一个任务 ID 后交给 GPT/Codex 执行。每次只派发一个任务，避免模型跨阶段发散。

## Task

Task ID:

Task Name:

## Goal

用 1 到 3 句话说明本任务完成后系统新增什么能力。

## Context Files

- `path/to/file`

## Allowed Files To Modify

- `path/to/file`

## Do Not Modify

- `path/to/file`

## Constraints

- AI 不能直接生成或执行任意 CAD 命令。
- 所有输出必须可校验。
- 不确定的问题写入 `docs/open-questions.md`。
- 不要扩大任务范围。

## Required Deliverables

- 

## Acceptance Criteria

- [ ] 

## Verification

说明如何验证任务完成，例如：

```powershell
dotnet test
```

## Final Response Requirements

完成后请说明：

- 修改了哪些文件。
- 实现了什么。
- 如何验证。
- 仍有哪些风险或未决问题。

