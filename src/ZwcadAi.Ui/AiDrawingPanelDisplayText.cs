using System;
using ZwcadAi.AiService;
using ZwcadAi.Renderer;

namespace ZwcadAi.Ui;

public static class AiDrawingPanelDisplayText
{
    public static string FormatResponseKind(AiDrawingSpecResponseKind responseKind)
    {
        switch (responseKind)
        {
            case AiDrawingSpecResponseKind.DrawingSpec:
                return "已生成图纸规格";
            case AiDrawingSpecResponseKind.NeedsClarification:
                return "需要补充参数";
            case AiDrawingSpecResponseKind.Rejected:
                return "请求被拒绝";
            default:
                return "未知";
        }
    }

    public static string FormatRenderStatus(RenderStatus status)
    {
        switch (status)
        {
            case RenderStatus.Success:
                return "成功";
            case RenderStatus.Failed:
                return "失败";
            case RenderStatus.Canceled:
                return "已取消";
            default:
                return status.ToString();
        }
    }

    public static string FormatIssueSeverity(string severity)
    {
        if (string.Equals(severity, "Error", StringComparison.Ordinal))
        {
            return "错误";
        }

        if (string.Equals(severity, "Warning", StringComparison.Ordinal))
        {
            return "警告";
        }

        return severity;
    }

    public static string FormatIssueSource(string source)
    {
        switch (source)
        {
            case "ModelResponse":
                return "模型响应";
            case "SchemaValidation":
                return "Schema 校验";
            case "BusinessValidation":
                return "业务规则校验";
            case "Renderer":
                return "渲染器";
            case "UserClarification":
                return "用户澄清";
            case "Service":
                return "服务";
            case "Validation":
                return "校验";
            default:
                return source;
        }
    }

    public static string FormatExportStatus(string exportStatus)
    {
        if (string.IsNullOrWhiteSpace(exportStatus))
        {
            return "未请求";
        }

        switch (exportStatus.Trim().ToLowerInvariant())
        {
            case "not_requested":
                return "未请求";
            case "exported":
            case "success":
                return "已导出";
            case "unavailable":
                return "不可用";
            default:
                return exportStatus;
        }
    }
}
