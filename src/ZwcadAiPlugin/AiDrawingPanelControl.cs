using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using ZwcadAi.AiService;
using ZwcadAi.Renderer;
using ZwcadAi.Ui;

namespace ZwcadAi.Plugin;

internal sealed class AiDrawingPanelControl : UserControl
{
    private readonly AiDrawingPanelServices _services;
    private readonly TextBox _requestText = CreateMultilineTextBox();
    private readonly TextBox _clarificationText = CreateMultilineTextBox();
    private readonly TextBox _statusText = CreateReadOnlyTextBox();
    private readonly TextBox _issuesText = CreateReadOnlyTextBox();
    private readonly TextBox _previewText = CreateReadOnlyTextBox();
    private readonly DataGridView _parameterGrid = CreateParameterGrid();
    private readonly Button _createButton = new Button { Text = "生成", Dock = DockStyle.Fill };
    private readonly Button _continueButton = new Button { Text = "继续生成", Dock = DockStyle.Fill, Enabled = false };
    private readonly Button _confirmButton = new Button { Text = "确认写入", Dock = DockStyle.Fill, Enabled = false };

    private AiDrawingSpecResponse? _currentResponse;
    private RenderResult? _currentPreview;
    private DrawingRenderPlan? _currentPreviewPlan;
    private AiDrawingPanelState _currentState = new AiDrawingPanelState();
    private string _requestId = string.Empty;

    public AiDrawingPanelControl(AiDrawingPanelServices services)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        Font = SystemFonts.MessageBoxFont;
        BuildLayout();
        BindEvents();
        ApplyState(new AiDrawingPanelState(), "就绪。");
    }

    private void BindEvents()
    {
        _createButton.Click += CreateButton_Click;
        _continueButton.Click += ContinueButton_Click;
        _confirmButton.Click += ConfirmButton_Click;
    }

    private void BuildLayout()
    {
        Dock = DockStyle.Fill;
        Padding = new Padding(8);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 10
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 22));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 14));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 16));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 24));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));

        layout.Controls.Add(CreateLabel("绘图需求"), 0, 0);
        layout.Controls.Add(_requestText, 0, 1);
        layout.Controls.Add(_createButton, 0, 2);
        layout.Controls.Add(CreateLabel("补充说明"), 0, 3);
        layout.Controls.Add(_clarificationText, 0, 4);
        layout.Controls.Add(_continueButton, 0, 5);
        layout.Controls.Add(CreateLabel("状态"), 0, 6);

        var statusTabs = new TabControl { Dock = DockStyle.Fill };
        statusTabs.TabPages.Add(CreateTab("摘要", _statusText));
        statusTabs.TabPages.Add(CreateTab("问题", _issuesText));
        layout.Controls.Add(statusTabs, 0, 7);

        var previewTabs = new TabControl { Dock = DockStyle.Fill };
        previewTabs.TabPages.Add(CreateTab("预览", _previewText));
        previewTabs.TabPages.Add(CreateTab("参数", _parameterGrid));
        layout.Controls.Add(previewTabs, 0, 8);

        layout.Controls.Add(_confirmButton, 0, 9);
        Controls.Add(layout);
    }

    private void CreateButton_Click(object? sender, EventArgs e)
    {
        var userRequest = _requestText.Text.Trim();
        if (string.IsNullOrWhiteSpace(userRequest))
        {
            ApplyState(new AiDrawingPanelState(), "请输入绘图需求。");
            return;
        }

        ExecutePanelAction(() =>
        {
            _requestId = CreateRequestId();
            var response = _services.AiService.CreateDrawingSpec(new AiDrawingSpecRequest
            {
                RequestId = _requestId,
                UserRequest = userRequest
            });

            ProcessAiResponse(response);
        });
    }

    private void ContinueButton_Click(object? sender, EventArgs e)
    {
        var clarificationState = _currentResponse?.ClarificationState;
        var answers = SplitAnswers(_clarificationText.Text).ToArray();

        if (clarificationState == null)
        {
            ApplyState(_currentState, "当前没有可继续的澄清状态。");
            return;
        }

        if (answers.Length == 0)
        {
            ApplyState(_currentState, "请输入补充说明。");
            return;
        }

        ExecutePanelAction(() =>
        {
            var response = _services.AiService.ContinueDrawingSpecAfterClarification(new AiClarificationFollowUpRequest
            {
                ClarificationState = clarificationState,
                UserAnswers = answers
            });

            ProcessAiResponse(response);
        });
    }

    private void ConfirmButton_Click(object? sender, EventArgs e)
    {
        if (_currentResponse?.Spec == null || !_currentState.ConfirmEnabled)
        {
            ApplyState(_currentState, "预览通过后才能确认写入。");
            return;
        }

        ExecutePanelAction(() =>
        {
            var plan = _currentPreviewPlan ?? _services.Renderer.CreatePlan(_currentResponse.Spec);
            if (!plan.Validation.IsValid)
            {
                var failedPreview = new RenderResult(
                    RenderStatus.Failed,
                    Array.Empty<RenderedEntity>(),
                    plan.Validation,
                    GeometrySummary.FromPlan(
                        plan,
                        RenderStatus.Failed,
                        Array.Empty<RenderedEntity>(),
                        plan.Validation));
                _currentPreview = failedPreview;
                _currentState = AiDrawingPanelStateMapper.FromResponse(_currentResponse, failedPreview);
                ApplyState(_currentState, "渲染计划校验失败。");
                return;
            }

            var renderResult = new ZwcadDrawingWriter().Render(plan);
            _currentPreview = renderResult;
            _currentState = AiDrawingPanelStateMapper.FromResponse(_currentResponse, renderResult);
            ApplyState(
                _currentState,
                renderResult.Success
                    ? "已写入图纸。"
                    : renderResult.Canceled
                        ? "写入前已取消。"
                        : "写入失败，已回滚。");
        });
    }

    private void ProcessAiResponse(AiDrawingSpecResponse response)
    {
        _currentResponse = response ?? throw new ArgumentNullException(nameof(response));
        _currentPreview = null;
        _currentPreviewPlan = null;

        if (response.Kind == AiDrawingSpecResponseKind.DrawingSpec && response.Spec != null)
        {
            _currentPreviewPlan = _services.Renderer.CreatePlan(response.Spec);
            _currentPreview = CreatePreviewRenderResult(_currentPreviewPlan);
        }

        _currentState = AiDrawingPanelStateMapper.FromResponse(response, _currentPreview);
        ApplyState(_currentState, CreateStatusMessage(_currentState));
    }

    private void ApplyState(AiDrawingPanelState state, string status)
    {
        _currentState = state;
        _statusText.Text = FormatStatus(status, state.ClarificationQuestions);
        _issuesText.Text = FormatIssues(state.Issues);
        _previewText.Text = FormatPreview(state.Preview);
        PopulateParameterGrid(state);
        _continueButton.Enabled = state.ResponseKind == AiDrawingSpecResponseKind.NeedsClarification
            && _currentResponse?.ClarificationState != null;
        _confirmButton.Enabled = state.ConfirmEnabled;
    }

    private void ExecutePanelAction(Action action)
    {
        try
        {
            Cursor.Current = Cursors.WaitCursor;
            action();
        }
        catch (Exception exception)
        {
            CadLog.WriteInfo($"AI drawing panel failed: {exception.GetType().Name}");
            ApplyState(new AiDrawingPanelState(), "AI 绘图面板执行失败。");
        }
        finally
        {
            Cursor.Current = Cursors.Default;
        }
    }

    private static string CreateRequestId()
    {
        return $"p5-{Guid.NewGuid():N}";
    }

    private static RenderResult CreatePreviewRenderResult(DrawingRenderPlan plan)
    {
        if (!plan.Validation.IsValid)
        {
            return new RenderResult(
                RenderStatus.Failed,
                Array.Empty<RenderedEntity>(),
                plan.Validation,
                GeometrySummary.FromPlan(
                    plan,
                    RenderStatus.Failed,
                    Array.Empty<RenderedEntity>(),
                    plan.Validation));
        }

        var renderedEntities = plan.Entities
            .Select(entity => new RenderedEntity(entity.SpecEntityId, $"planned:{entity.SpecEntityId}"))
            .Concat(plan.Dimensions.Select(dimension => new RenderedEntity(dimension.SpecDimensionId, $"planned:{dimension.SpecDimensionId}")))
            .ToArray();

        return new RenderResult(
            RenderStatus.Success,
            renderedEntities,
            plan.Validation,
            GeometrySummary.FromPlan(plan, RenderStatus.Success, renderedEntities, plan.Validation));
    }

    private static string CreateStatusMessage(AiDrawingPanelState state)
    {
        switch (state.ResponseKind)
        {
            case AiDrawingSpecResponseKind.DrawingSpec:
                return state.ConfirmEnabled
                    ? "预览已就绪，请确认后写入。"
                    : "已生成图纸规格，但预览尚未就绪。";
            case AiDrawingSpecResponseKind.NeedsClarification:
                return "需要补充参数。";
            case AiDrawingSpecResponseKind.Rejected:
                return "请求被拒绝。";
            default:
                return "未知响应。";
        }
    }

    private static string FormatStatus(string status, IReadOnlyList<string> clarificationQuestions)
    {
        if (clarificationQuestions == null || clarificationQuestions.Count == 0)
        {
            return status;
        }

        var builder = new StringBuilder();
        builder.AppendLine(status);
        builder.AppendLine();
        builder.AppendLine("需补充的问题：");
        for (var index = 0; index < clarificationQuestions.Count; index++)
        {
            builder.Append(index + 1);
            builder.Append(". ");
            builder.AppendLine(clarificationQuestions[index]);
        }

        return builder.ToString().TrimEnd();
    }

    private void PopulateParameterGrid(AiDrawingPanelState state)
    {
        _parameterGrid.Rows.Clear();
        AddParameter("响应类型", AiDrawingPanelDisplayText.FormatResponseKind(state.ResponseKind));
        AddParameter("允许确认", state.ConfirmEnabled ? "是" : "否");
        AddParameter("澄清问题数", state.ClarificationQuestions.Count.ToString());
        AddParameter("问题数", state.Issues.Count.ToString());

        var preview = state.Preview;
        if (preview == null)
        {
            return;
        }

        AddParameter("渲染状态", AiDrawingPanelDisplayText.FormatRenderStatus(preview.Status));
        AddParameter("实体数", preview.EntityCount.ToString());
        AddParameter("标注数", preview.DimensionCount.ToString());
        AddParameter("CAD 对象数", preview.CadObjectCount.ToString());
        AddParameter("图层数", preview.LayerCounts.Count.ToString());
        AddParameter("规格 ID 映射数", preview.SpecIdMappings.Count.ToString());

        if (preview.Bounds != null)
        {
            AddParameter("范围最小值", $"{preview.Bounds.MinX}, {preview.Bounds.MinY}");
            AddParameter("范围最大值", $"{preview.Bounds.MaxX}, {preview.Bounds.MaxY}");
            AddParameter("范围尺寸", $"{preview.Bounds.Width} x {preview.Bounds.Height}");
        }
    }

    private void AddParameter(string name, string value)
    {
        _parameterGrid.Rows.Add(name, value);
    }

    private static string FormatIssues(IReadOnlyList<AiDrawingPanelIssue> issues)
    {
        if (issues == null || issues.Count == 0)
        {
            return "暂无问题。";
        }

        var builder = new StringBuilder();
        foreach (var issue in issues)
        {
            builder.Append(AiDrawingPanelDisplayText.FormatIssueSeverity(issue.Severity));
            builder.Append(" ");
            builder.Append(issue.Code);
            builder.Append(" [");
            builder.Append(AiDrawingPanelDisplayText.FormatIssueSource(issue.Source));
            builder.Append("] ");
            builder.Append(issue.Path);
            if (!string.IsNullOrWhiteSpace(issue.Message))
            {
                builder.Append(": ");
                builder.Append(issue.Message);
            }

            builder.AppendLine();
        }

        return builder.ToString().TrimEnd();
    }

    private static string FormatPreview(AiDrawingPreviewSummary? preview)
    {
        if (preview == null)
        {
            return "暂无预览。";
        }

        var builder = new StringBuilder();
        builder.AppendLine($"状态：{AiDrawingPanelDisplayText.FormatRenderStatus(preview.Status)}");
        builder.AppendLine($"实体：{preview.EntityCount}");
        builder.AppendLine($"标注：{preview.DimensionCount}");
        builder.AppendLine($"CAD 对象：{preview.CadObjectCount}");
        builder.AppendLine("图层：");
        foreach (var layer in preview.LayerCounts.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            builder.AppendLine($"  {layer.Key}: {layer.Value}");
        }

        builder.AppendLine("类型：");
        foreach (var type in preview.TypeCounts.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            builder.AppendLine($"  {type.Key}: {type.Value}");
        }

        if (preview.Bounds != null)
        {
            builder.AppendLine(
                $"范围：({preview.Bounds.MinX}, {preview.Bounds.MinY}) - ({preview.Bounds.MaxX}, {preview.Bounds.MaxY})");
            builder.AppendLine($"尺寸：{preview.Bounds.Width} x {preview.Bounds.Height}");
        }

        builder.AppendLine($"规格 ID 映射：{preview.SpecIdMappings.Count}");
        builder.AppendLine($"导出：{AiDrawingPanelDisplayText.FormatExportStatus(preview.ExportStatus)}");
        return builder.ToString().TrimEnd();
    }

    private static IEnumerable<string> SplitAnswers(string text)
    {
        return (text ?? string.Empty)
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line));
    }

    private static Label CreateLabel(string text)
    {
        return new Label
        {
            Text = text,
            Dock = DockStyle.Fill,
            AutoSize = true,
            Font = new Font(SystemFonts.MessageBoxFont, FontStyle.Bold),
            Padding = new Padding(0, 6, 0, 2)
        };
    }

    private static TextBox CreateMultilineTextBox()
    {
        var textBox = new TextBox
        {
            Dock = DockStyle.Fill,
            ImeMode = ImeMode.On,
            Multiline = true,
            ShortcutsEnabled = true,
            ScrollBars = ScrollBars.Vertical
        };
        BindTextInputFocus(textBox);
        return textBox;
    }

    private static void BindTextInputFocus(TextBox textBox)
    {
        textBox.Enter += (_, _) => textBox.Focus();
        textBox.MouseDown += (_, _) => textBox.Focus();
    }

    private static TextBox CreateReadOnlyTextBox()
    {
        return new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical
        };
    }

    private static DataGridView CreateParameterGrid()
    {
        var grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            BackgroundColor = SystemColors.Window,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
            ReadOnly = true,
            RowHeadersVisible = false
        };
        grid.Columns.Add("Name", "项目");
        grid.Columns.Add("Value", "值");
        return grid;
    }

    private static TabPage CreateTab(string title, Control content)
    {
        var tab = new TabPage(title);
        tab.Controls.Add(content);
        return tab;
    }
}
