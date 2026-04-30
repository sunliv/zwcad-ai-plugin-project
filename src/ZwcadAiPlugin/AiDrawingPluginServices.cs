using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using ZwcadAi.AiService;
using ZwcadAi.Renderer;

namespace ZwcadAi.Plugin;

internal sealed class AiDrawingPanelServices
{
    public AiDrawingPanelServices(IAiDrawingSpecService aiService, DrawingSpecPlanRenderer renderer)
    {
        AiService = aiService ?? throw new ArgumentNullException(nameof(aiService));
        Renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
    }

    public IAiDrawingSpecService AiService { get; }

    public DrawingSpecPlanRenderer Renderer { get; }
}

internal static class AiDrawingPluginServices
{
    private const string ModelEndpointEnvironmentVariable = "ZWCAD_AI_MODEL_ENDPOINT";
    private const string ApiKeyEnvironmentNameVariable = "ZWCAD_AI_API_KEY_ENV";
    private const string DefaultApiKeyEnvironmentVariable = "ZWCAD_AI_API_KEY";

    public static AiDrawingPanelServices CreateDefault()
    {
        var endpoint = Environment.GetEnvironmentVariable(ModelEndpointEnvironmentVariable) ?? string.Empty;
        var apiKeyEnvironmentVariable =
            Environment.GetEnvironmentVariable(ApiKeyEnvironmentNameVariable)
            ?? DefaultApiKeyEnvironmentVariable;

        var aiService = new LocalAiDrawingSpecAdapter(
            new HttpAiModelClient(),
            new LocalAiServiceOptions
            {
                ServiceEndpoint = endpoint,
                ApiKeyEnvironmentVariable = apiKeyEnvironmentVariable,
                LogWriter = new RedactedAiCallLogWriter(new TraceTextWriter())
            });

        return new AiDrawingPanelServices(aiService, new DrawingSpecPlanRenderer());
    }

    private sealed class TraceTextWriter : TextWriter
    {
        public override Encoding Encoding => Encoding.UTF8;

        public override void WriteLine(string? value)
        {
            Trace.TraceInformation(value ?? string.Empty);
        }
    }
}
