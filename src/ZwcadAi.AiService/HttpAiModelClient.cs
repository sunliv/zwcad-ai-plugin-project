using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using ZwcadAi.Core;

namespace ZwcadAi.AiService;

public sealed class HttpAiModelClient : IAiModelClient
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);
    private readonly HttpClient _httpClient;

    public HttpAiModelClient()
        : this(new HttpClient())
    {
    }

    public HttpAiModelClient(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public string CreateDrawingSpec(AiDrawingSpecRequest request, AiModelCallOptions options)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        return SendJson(BuildCreatePayload(request), options);
    }

    public string RepairDrawingSpec(AiDrawingSpecRepairRequest request, AiModelCallOptions options)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        return SendJson(BuildRepairPayload(request), options);
    }

    private string SendJson(string jsonPayload, AiModelCallOptions options)
    {
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        if (string.IsNullOrWhiteSpace(options.ServiceEndpoint))
        {
            throw new InvalidOperationException("Model service endpoint is required.");
        }

        var endpoint = new Uri(options.ServiceEndpoint, UriKind.Absolute);
        var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json")
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var apiKey = ReadApiKey(options.ApiKeyEnvironmentVariable);
        if (!string.IsNullOrEmpty(apiKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(options.CancellationToken);
        timeoutSource.CancelAfter(NormalizeTimeout(options.Timeout));

        try
        {
            using var response = _httpClient
                .SendAsync(request, HttpCompletionOption.ResponseContentRead, timeoutSource.Token)
                .GetAwaiter()
                .GetResult();

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"Model service returned HTTP {(int)response.StatusCode}.");
            }

            return response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        }
        catch (OperationCanceledException exception)
        {
            if (options.CancellationToken.IsCancellationRequested)
            {
                throw;
            }

            throw new TimeoutException("Model service timed out before returning DrawingSpec JSON.", exception);
        }
        catch (HttpRequestException exception)
        {
            throw new InvalidOperationException("Model service request failed.", exception);
        }
    }

    private static TimeSpan NormalizeTimeout(TimeSpan timeout)
    {
        return timeout > TimeSpan.Zero ? timeout : DefaultTimeout;
    }

    private static string ReadApiKey(string environmentVariable)
    {
        if (string.IsNullOrWhiteSpace(environmentVariable))
        {
            return string.Empty;
        }

        var apiKey = Environment.GetEnvironmentVariable(environmentVariable);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                $"Model service API key environment variable '{environmentVariable}' is not configured.");
        }

        return apiKey;
    }

    private static string BuildCreatePayload(AiDrawingSpecRequest request)
    {
        var builder = new StringBuilder();
        builder.Append('{');
        AppendStringProperty(builder, "operation", "createDrawingSpec");
        AppendComma(builder);
        AppendStringProperty(builder, "requestId", request.RequestId);
        AppendComma(builder);
        AppendStringProperty(builder, "promptVersion", request.PromptVersion);
        AppendComma(builder);
        AppendStringProperty(builder, "userRequest", request.UserRequest);
        AppendComma(builder);
        AppendPropertyName(builder, "context");
        builder.Append('{');
        AppendStringProperty(builder, "units", request.Units);
        AppendComma(builder);
        AppendStringProperty(builder, "domain", request.Domain);
        AppendComma(builder);
        AppendStringArrayProperty(builder, "allowedEntityTypes", request.AllowedEntityTypes);
        AppendComma(builder);
        AppendStringArrayProperty(builder, "allowedDimensionTypes", request.AllowedDimensionTypes);
        AppendComma(builder);
        AppendStringProperty(builder, "layerStandard", request.LayerStandard);
        AppendComma(builder);
        AppendStringProperty(builder, "drawingSpecVersion", request.DrawingSpecVersion);
        AppendComma(builder);
        AppendIntProperty(builder, "maxClarificationQuestions", request.MaxClarificationQuestions);
        builder.Append('}');
        builder.Append('}');
        return builder.ToString();
    }

    private static string BuildRepairPayload(AiDrawingSpecRepairRequest request)
    {
        var builder = new StringBuilder();
        builder.Append('{');
        AppendStringProperty(builder, "operation", "repairDrawingSpec");
        AppendComma(builder);
        AppendStringProperty(builder, "promptVersion", ModelPromptContract.PromptVersion);
        AppendComma(builder);
        AppendStringProperty(builder, "invalidDrawingSpecJson", request.InvalidDrawingSpecJson);
        AppendComma(builder);
        AppendIssueArrayProperty(builder, "issues", request.Issues);
        AppendComma(builder);
        AppendIntProperty(builder, "repairAttempt", request.RepairAttempt);
        AppendComma(builder);
        AppendIntProperty(builder, "maxRepairAttempts", request.MaxRepairAttempts);
        AppendComma(builder);
        AppendStringProperty(builder, "repairStrategy", request.RepairStrategy.ToString());
        builder.Append('}');
        return builder.ToString();
    }

    private static void AppendIssueArrayProperty(
        StringBuilder builder,
        string propertyName,
        IReadOnlyList<AiModelIssue> issues)
    {
        AppendPropertyName(builder, propertyName);
        builder.Append('[');
        var values = issues ?? Array.Empty<AiModelIssue>();
        for (var index = 0; index < values.Count; index++)
        {
            if (index > 0)
            {
                AppendComma(builder);
            }

            var issue = values[index];
            builder.Append('{');
            AppendStringProperty(builder, "code", issue.Code);
            AppendComma(builder);
            AppendStringProperty(builder, "path", issue.Path);
            AppendComma(builder);
            AppendStringProperty(builder, "message", issue.Message);
            AppendComma(builder);
            AppendStringProperty(builder, "severity", issue.Severity.ToString());
            AppendComma(builder);
            AppendStringProperty(builder, "source", issue.Source.ToString());
            AppendComma(builder);
            AppendBooleanProperty(builder, "repairable", issue.Repairable);
            builder.Append('}');
        }

        builder.Append(']');
    }

    private static void AppendStringArrayProperty(
        StringBuilder builder,
        string propertyName,
        IReadOnlyList<string> values)
    {
        AppendPropertyName(builder, propertyName);
        builder.Append('[');
        var items = values ?? Array.Empty<string>();
        for (var index = 0; index < items.Count; index++)
        {
            if (index > 0)
            {
                AppendComma(builder);
            }

            AppendJsonString(builder, items[index]);
        }

        builder.Append(']');
    }

    private static void AppendStringProperty(StringBuilder builder, string propertyName, string value)
    {
        AppendPropertyName(builder, propertyName);
        AppendJsonString(builder, value);
    }

    private static void AppendIntProperty(StringBuilder builder, string propertyName, int value)
    {
        AppendPropertyName(builder, propertyName);
        builder.Append(value);
    }

    private static void AppendBooleanProperty(StringBuilder builder, string propertyName, bool value)
    {
        AppendPropertyName(builder, propertyName);
        builder.Append(value ? "true" : "false");
    }

    private static void AppendPropertyName(StringBuilder builder, string propertyName)
    {
        AppendJsonString(builder, propertyName);
        builder.Append(':');
    }

    private static void AppendComma(StringBuilder builder)
    {
        builder.Append(',');
    }

    private static void AppendJsonString(StringBuilder builder, string value)
    {
        builder.Append('"');
        var text = value ?? string.Empty;
        foreach (var character in text)
        {
            switch (character)
            {
                case '\\':
                    builder.Append(@"\\");
                    break;
                case '"':
                    builder.Append("\\\"");
                    break;
                case '\b':
                    builder.Append(@"\b");
                    break;
                case '\f':
                    builder.Append(@"\f");
                    break;
                case '\n':
                    builder.Append(@"\n");
                    break;
                case '\r':
                    builder.Append(@"\r");
                    break;
                case '\t':
                    builder.Append(@"\t");
                    break;
                default:
                    if (char.IsControl(character))
                    {
                        builder.Append("\\u");
                        builder.Append(((int)character).ToString("x4"));
                    }
                    else
                    {
                        builder.Append(character);
                    }

                    break;
            }
        }

        builder.Append('"');
    }
}
