using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using ZwcadAi.AiService;
using ZwcadAi.Core;
using ZwcadAi.Renderer;

namespace ZwcadAi.Tests;

public static class Program
{
    public static int Main()
    {
        var tests = new List<(string Name, Action Execute)>
        {
            ("Core uses the locked MVP domain", CoreUsesLockedMvpDomain),
            ("AI request defaults to P4 model prompt contract", AiRequestDefaultsToP4ModelPromptContract),
            ("AI repair request excludes original request payload", AiRepairRequestExcludesOriginalRequestPayload),
            ("AI clarification state excludes drawing context", AiClarificationStateExcludesDrawingContext),
            ("Redacted AI call log writer records safe fields only by default", RedactedAiCallLogWriterRecordsSafeFieldsOnlyByDefault),
            ("Redacted AI call log writer always redacts API keys from sensitive content", RedactedAiCallLogWriterAlwaysRedactsApiKeysFromSensitiveContent),
            ("Local AI adapter writes redacted create and clarification logs", LocalAiAdapterWritesRedactedCreateAndClarificationLogs),
            ("Sensitive AI call logging opt-in never records API key", SensitiveAiCallLoggingOptInNeverRecordsApiKey),
            ("Local AI adapter accepts JSON-only DrawingSpec responses", LocalAiAdapterAcceptsJsonOnlyDrawingSpecResponses),
            ("Local AI adapter rejects non-JSON model responses", LocalAiAdapterRejectsNonJsonModelResponses),
            ("Local AI adapter rejects Markdown fenced model responses", LocalAiAdapterRejectsMarkdownFencedModelResponses),
            ("Local AI adapter rejects unsafe CAD command responses", LocalAiAdapterRejectsUnsafeCadCommandResponses),
            ("Local AI adapter allows command-like words inside JSON text", LocalAiAdapterAllowsCommandLikeWordsInsideJsonText),
            ("Local AI adapter maps clarification responses", LocalAiAdapterMapsClarificationResponses),
            ("Local AI adapter maps schema validation issues as repairable", LocalAiAdapterMapsSchemaValidationIssuesAsRepairable),
            ("Local AI adapter maps business validation issues as repairable", LocalAiAdapterMapsBusinessValidationIssuesAsRepairable),
            ("Local AI adapter maps timeout failures as non-repairable", LocalAiAdapterMapsTimeoutFailuresAsNonRepairable),
            ("Local AI adapter maps service failures as non-repairable", LocalAiAdapterMapsServiceFailuresAsNonRepairable),
            ("Local AI adapter maps cancellation failures as non-repairable", LocalAiAdapterMapsCancellationFailuresAsNonRepairable),
            ("HTTP AI model client posts create requests with env API key", HttpAiModelClientPostsCreateRequestsWithEnvApiKey),
            ("HTTP AI model client posts repair requests without original context", HttpAiModelClientPostsRepairRequestsWithoutOriginalContext),
            ("Clarification follow-up service flow uses HTTP create and valid renderer plan", ClarificationFollowUpServiceFlowUsesHttpCreateAndValidRendererPlan),
            ("HTTP AI model client timeout failures retry through adapter", HttpAiModelClientTimeoutFailuresRetryThroughAdapter),
            ("HTTP AI model client cancellation failures do not retry", HttpAiModelClientCancellationFailuresDoNotRetry),
            ("HTTP AI model client non-success failures stay redacted", HttpAiModelClientNonSuccessFailuresStayRedacted),
            ("HTTP AI model client rejects missing API key environment variable", HttpAiModelClientRejectsMissingApiKeyEnvironmentVariable),
            ("Local AI adapter enforces repair attempt limit", LocalAiAdapterEnforcesRepairAttemptLimit),
            ("Local AI adapter rejects invalid repair attempt numbers", LocalAiAdapterRejectsInvalidRepairAttemptNumbers),
            ("Local AI adapter repairs first schema invalid response", LocalAiAdapterRepairsFirstSchemaInvalidResponse),
            ("Local AI adapter repairs first business invalid response", LocalAiAdapterRepairsFirstBusinessInvalidResponse),
            ("Local AI adapter rejects after bounded repair failures", LocalAiAdapterRejectsAfterBoundedRepairFailures),
            ("Local AI adapter continues explicit repair within attempt limit", LocalAiAdapterContinuesExplicitRepairWithinAttemptLimit),
            ("Local AI adapter does not repair clarification responses", LocalAiAdapterDoesNotRepairClarificationResponses),
            ("Local AI adapter starts fresh create after clarification follow-up", LocalAiAdapterStartsFreshCreateAfterClarificationFollowUp),
            ("Service flow returns render result summary after clarification", ServiceFlowReturnsRenderResultSummaryAfterClarification),
            ("Local AI adapter never repairs unsafe command responses", LocalAiAdapterNeverRepairsUnsafeCommandResponses),
            ("Fixed rectangular plate sample matches P1-03 geometry", FixedRectangularPlateSampleMatchesP103Geometry),
            ("DrawingSpec schema accepts valid example files", DrawingSpecSchemaAcceptsValidExampleFiles),
            ("Basic entities combo example validates and plans P3-01 entities", BasicEntitiesComboExampleValidatesAndPlansP301Entities),
            ("DrawingSpec schema rejects invalid example files", DrawingSpecSchemaRejectsInvalidExampleFiles),
            ("DrawingSpec schema rejects unsupported entity type", DrawingSpecSchemaRejectsUnsupportedEntityType),
            ("DrawingSpec schema rejects missing required fields", DrawingSpecSchemaRejectsMissingRequiredFields),
            ("DrawingSpec schema issue paths locate the failed field", DrawingSpecSchemaIssuePathsLocateFailedField),
            ("DrawingSpec schema rejects object model layer color zero", DrawingSpecSchemaRejectsObjectModelLayerColorZero),
            ("DrawingSpec business rules accept the fixed P1-03 sample", DrawingSpecBusinessRulesAcceptFixedP103Sample),
            ("DrawingSpec business rules reject unsupported layers", DrawingSpecBusinessRulesRejectUnsupportedLayers),
            ("CAD layer standards expose P3 enterprise defaults", CadLayerStandardsExposeP3EnterpriseDefaults),
            ("DrawingSpec business rules reject layer style drift", DrawingSpecBusinessRulesRejectLayerStyleDrift),
            ("DrawingSpec business rules reject oversized coordinates", DrawingSpecBusinessRulesRejectOversizedCoordinates),
            ("DrawingSpec business rules reject entity count over limit", DrawingSpecBusinessRulesRejectEntityCountOverLimit),
            ("DrawingSpec business rules reject incomplete angular dimensions", DrawingSpecBusinessRulesRejectIncompleteAngularDimensions),
            ("Renderer plans P1-03 plate entities on standard layers", RendererPlansP103PlateEntitiesOnStandardLayers),
            ("Renderer resolves enterprise text and dimension styles", RendererResolvesEnterpriseTextAndDimensionStyles),
            ("Renderer maps every P3-01 basic entity id", RendererMapsEveryP301BasicEntityId),
            ("Renderer plans P3-02 aligned and angular dimensions", RendererPlansP302AlignedAndAngularDimensions),
            ("Renderer plans P3-02 hole array centerlines", RendererPlansP302HoleArrayCenterlines),
            ("Renderer geometry summary captures counts layers bounds and mapping", RendererGeometrySummaryCapturesCountsLayersBoundsAndMapping),
            ("Renderer dimension failures locate stable dimension ids", RendererDimensionFailuresLocateStableDimensionIds),
            ("Renderer failures locate stable entity ids", RendererFailuresLocateStableEntityIds),
            ("Renderer rejects specs missing production layers", RendererRejectsSpecsMissingProductionLayers),
            ("Renderer result preserves spec-to-CAD mapping", RendererResultPreservesMapping),
            ("Writer transaction boundary commits successful writes once", WriterTransactionBoundaryCommitsSuccessfulWritesOnce),
            ("Writer transaction boundary rolls back injected entity failures", WriterTransactionBoundaryRollsBackInjectedEntityFailures),
            ("Writer transaction boundary rolls back injected dimension failures", WriterTransactionBoundaryRollsBackInjectedDimensionFailures),
            ("Writer transaction boundary treats cancellation as non-committed render", WriterTransactionBoundaryTreatsCancellationAsNonCommittedRender),
            ("Project references follow architecture boundaries", ProjectReferencesFollowArchitectureBoundaries),
            ("Core project has no ZWCAD runtime references", CoreProjectHasNoZwcadRuntimeReferences),
            ("Plugin references ZWCAD 2025 managed assemblies", PluginReferencesZwcad2025ManagedAssemblies),
            ("Plugin registers AIDRAW command", PluginRegistersAiDrawCommand),
            ("Plugin AIDRAW uses fixed POC sample and transaction writer", PluginAiDrawUsesFixedPocSampleAndTransactionWriter),
            ("Plugin registers AIEXPORT command", PluginRegistersAiExportCommand),
            ("AIEXPORT saves a DWG copy without saving the active drawing", PluginAiExportSavesDwgCopyWithoutSavingActiveDrawing),
            ("AIEXPORT covers the PDF plot-to-file path", PluginAiExportCoversPdfPlotToFilePath),
            ("Plugin writer uses shared transaction boundary", PluginWriterUsesSharedTransactionBoundary),
            ("Plugin writer failures locate stable entity and dimension ids", PluginWriterFailuresLocateStableEntityAndDimensionIds),
            ("Plugin writer supports P3-01 basic entity dispatch", PluginWriterSupportsP301BasicEntityDispatch),
            ("Plugin writer supports P3-02 dimensions and center marks", PluginWriterSupportsP302DimensionsAndCenterMarks),
            ("Plugin writer applies enterprise layer and style standards", PluginWriterAppliesEnterpriseLayerAndStyleStandards)
        };

        foreach (var test in tests)
        {
            test.Execute();
            Console.WriteLine($"PASS {test.Name}");
        }

        Console.WriteLine($"{tests.Count} tests passed.");
        return 0;
    }

    private static void CoreUsesLockedMvpDomain()
    {
        AssertEqual("mechanical_plate", DrawingDomain.MechanicalPlate);
    }

    private static void AiRequestDefaultsToP4ModelPromptContract()
    {
        var request = new AiDrawingSpecRequest();

        AssertEqual(ModelPromptContract.PromptVersion, request.PromptVersion);
        AssertEqual(DrawingDomain.MechanicalPlate, request.Domain);
        AssertEqual("mm", request.Units);
        AssertEqual(DrawingSpecWireFormat.Version, request.DrawingSpecVersion);
        AssertEqual(ModelPromptContract.LayerStandard, request.LayerStandard);
        AssertEqual(ModelPromptContract.MaxClarificationQuestions, request.MaxClarificationQuestions);
        AssertSequenceEqual(ModelPromptContract.AllowedEntityTypes, request.AllowedEntityTypes);
        AssertSequenceEqual(ModelPromptContract.AllowedDimensionTypes, request.AllowedDimensionTypes);

        var validationIssue = new ValidationIssue(
            "missing_required",
            "$.entities[0].center",
            "Property 'center' is required.",
            ValidationSeverity.Error);
        var modelIssue = AiModelIssue.FromValidationIssue(
            validationIssue,
            AiModelIssueSource.SchemaValidation,
            ModelPromptContract.IsRepairableIssueCode(validationIssue.Code));

        AssertEqual(validationIssue.Code, modelIssue.Code);
        AssertEqual(validationIssue.Path, modelIssue.Path);
        AssertEqual(validationIssue.Message, modelIssue.Message);
        AssertEqual(AiModelIssueSource.SchemaValidation, modelIssue.Source);
        Assert(modelIssue.Repairable, "Schema validation issue should be eligible for bounded model repair.");

        var repairRequest = new AiDrawingSpecRepairRequest
        {
            InvalidDrawingSpecJson = "{\"entities\":[]}",
            Issues = new[] { modelIssue }
        };

        AssertEqual(AiRepairStrategy.RepairDrawingSpecOnly, repairRequest.RepairStrategy);
        AssertEqual(1, repairRequest.RepairAttempt);
        AssertEqual(ModelPromptContract.MaxRepairAttempts, repairRequest.MaxRepairAttempts);
        AssertEqual(1, repairRequest.Issues.Count);
    }

    private static void AiRepairRequestExcludesOriginalRequestPayload()
    {
        var propertyNames = typeof(AiDrawingSpecRepairRequest)
            .GetProperties()
            .Select(property => property.Name)
            .ToArray();

        Assert(!propertyNames.Contains("OriginalRequest"), "Repair requests must not carry the original user request or future drawing context payload.");
        Assert(propertyNames.Contains("InvalidDrawingSpecJson"), "Repair requests must carry the previous invalid DrawingSpec JSON.");
        Assert(propertyNames.Contains("Issues"), "Repair requests must carry mapped validation issues.");
        Assert(propertyNames.Contains("RepairAttempt"), "Repair requests must carry the bounded attempt number.");
        Assert(propertyNames.Contains("MaxRepairAttempts"), "Repair requests must carry the configured attempt limit.");
        Assert(propertyNames.Contains("RepairStrategy"), "Repair requests must carry the DrawingSpec-only repair strategy.");
    }

    private static void AiClarificationStateExcludesDrawingContext()
    {
        var statePropertyNames = typeof(AiDrawingSpecClarificationState)
            .GetProperties()
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        var followUpPropertyNames = typeof(AiClarificationFollowUpRequest)
            .GetProperties()
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        AssertSequenceEqual(
            new[]
            {
                "ClarificationQuestions",
                "OriginalUserRequest",
                "PromptVersion",
                "RequestId",
                "UserAnswers"
            },
            statePropertyNames);
        AssertSequenceEqual(
            new[] { "ClarificationState", "UserAnswers" },
            followUpPropertyNames);
        Assert(
            statePropertyNames.All(name => name.IndexOf("dwg", StringComparison.OrdinalIgnoreCase) < 0),
            "Clarification state must not carry DWG content.");
        Assert(
            statePropertyNames.All(name => name.IndexOf("screenshot", StringComparison.OrdinalIgnoreCase) < 0),
            "Clarification state must not carry screenshots.");
        Assert(
            statePropertyNames.All(name => name.IndexOf("plugin", StringComparison.OrdinalIgnoreCase) < 0),
            "Clarification state must not carry plugin context.");
    }

    private static void RedactedAiCallLogWriterRecordsSafeFieldsOnlyByDefault()
    {
        var output = new StringWriter();
        var logWriter = new RedactedAiCallLogWriter(output);

        logWriter.Write(new AiCallLogEvent
        {
            RequestId = "p4-06-safe",
            PromptVersion = ModelPromptContract.PromptVersion,
            ResponseKind = AiDrawingSpecResponseKind.Rejected,
            Issues = new[]
            {
                new AiCallLogIssue
                {
                    Code = "missing_required",
                    Path = "$.entities[0].layer",
                    Source = AiModelIssueSource.SchemaValidation
                }
            },
            Elapsed = TimeSpan.FromMilliseconds(12),
            AttemptCount = 2,
            ClarificationQuestionCount = 3,
            ClarificationAnswerCount = 1
        });

        var logLine = output.ToString();
        Assert(!logLine.Contains("userRequest", StringComparison.OrdinalIgnoreCase), "Default AI logs must not include a userRequest field.");
        Assert(!logLine.Contains("drawingSpecJson", StringComparison.OrdinalIgnoreCase), "Default AI logs must not include full DrawingSpec JSON.");
        Assert(!logLine.Contains("invalidDrawingSpecJson", StringComparison.OrdinalIgnoreCase), "Default AI logs must not include repair DrawingSpec JSON.");
        Assert(!logLine.Contains("message", StringComparison.OrdinalIgnoreCase), "Default AI logs must not include provider or validation detail messages.");
        Assert(!logLine.Contains("dwg", StringComparison.OrdinalIgnoreCase), "Default AI logs must not include DWG content.");
        Assert(!logLine.Contains("screenshot", StringComparison.OrdinalIgnoreCase), "Default AI logs must not include screenshots.");
        Assert(!logLine.Contains("plugin", StringComparison.OrdinalIgnoreCase), "Default AI logs must not include plugin context.");
        Assert(!logLine.Contains("api", StringComparison.OrdinalIgnoreCase), "Default AI logs must not include API key fields.");

        using var document = JsonDocument.Parse(logLine);
        var root = document.RootElement;
        AssertSequenceEqual(
            new[]
            {
                "attemptCount",
                "clarificationAnswerCount",
                "clarificationQuestionCount",
                "elapsedMilliseconds",
                "issues",
                "promptVersion",
                "requestId",
                "responseKind"
            },
            root.EnumerateObject().Select(property => property.Name).OrderBy(name => name, StringComparer.Ordinal).ToArray());
        AssertEqual("p4-06-safe", root.GetProperty("requestId").GetString());
        AssertEqual(ModelPromptContract.PromptVersion, root.GetProperty("promptVersion").GetString());
        AssertEqual("Rejected", root.GetProperty("responseKind").GetString());
        AssertEqual(2, root.GetProperty("attemptCount").GetInt32());
        Assert(root.GetProperty("elapsedMilliseconds").GetDouble() >= 0, "Elapsed time must be logged as a non-negative duration.");
        AssertEqual(3, root.GetProperty("clarificationQuestionCount").GetInt32());
        AssertEqual(1, root.GetProperty("clarificationAnswerCount").GetInt32());

        var issue = root.GetProperty("issues")[0];
        AssertSequenceEqual(
            new[] { "code", "path", "source" },
            issue.EnumerateObject().Select(property => property.Name).OrderBy(name => name, StringComparer.Ordinal).ToArray());
        AssertEqual("missing_required", issue.GetProperty("code").GetString());
        AssertEqual("$.entities[0].layer", issue.GetProperty("path").GetString());
        AssertEqual("SchemaValidation", issue.GetProperty("source").GetString());
    }

    private static void RedactedAiCallLogWriterAlwaysRedactsApiKeysFromSensitiveContent()
    {
        const string configuredApiKey = "test-secret-token";
        const string openAiStyleApiKey = "sk-proj-direct-writer-key-1234567890abcdef";
        var output = new StringWriter();
        var logWriter = new RedactedAiCallLogWriter(output);

        logWriter.Write(new AiCallLogEvent
        {
            RequestId = "p4-06-writer-redaction",
            PromptVersion = ModelPromptContract.PromptVersion,
            ResponseKind = AiDrawingSpecResponseKind.DrawingSpec,
            AttemptCount = 1,
            ApiKeyRedactionValues = new[] { configuredApiKey },
            SensitiveContent = new AiCallSensitiveContent
            {
                UserRequest = $"Use configured key {configuredApiKey} and generated key {openAiStyleApiKey}.",
                DrawingSpecJson = $"{{\"metadata\":{{\"title\":\"{configuredApiKey}\"}}}}",
                InvalidDrawingSpecJson = $"{{\"debug\":\"{openAiStyleApiKey}\"}}"
            }
        });

        var logLine = output.ToString();
        Assert(!logLine.Contains(configuredApiKey, StringComparison.Ordinal), "Writer-level redaction must remove configured API key values from sensitive content.");
        Assert(!logLine.Contains(openAiStyleApiKey, StringComparison.Ordinal), "Writer-level redaction must remove OpenAI-style API key values from sensitive content.");
        Assert(logLine.Contains("[redacted-api-key]", StringComparison.Ordinal), "Writer-level redaction must use a stable API key redaction marker.");

        using var document = JsonDocument.Parse(logLine);
        var properties = document.RootElement.EnumerateObject().Select(property => property.Name).ToArray();
        Assert(!properties.Contains("apiKeyRedactionValues", StringComparer.Ordinal), "API key redaction values must never be serialized.");
    }

    private static void LocalAiAdapterWritesRedactedCreateAndClarificationLogs()
    {
        var clarificationQuestion = "Please provide the confidential plate width.";
        var clarificationJson = $$"""
            {
              "drawingSpecVersion": "1.0",
              "units": "mm",
              "metadata": {
                "title": "clarification log test",
                "domain": "mechanical_plate",
                "createdBy": "test",
                "requestId": "p4-06-clarification"
              },
              "layers": [
                { "name": "OUTLINE", "color": 7, "lineType": "Continuous", "lineWeight": 0.35 }
              ],
              "entities": [],
              "dimensions": [],
              "clarifications": [
                "{{clarificationQuestion}}"
              ]
            }
            """;
        var completedSpecJson = ReadExampleJson("rectangular-plate.example.json");
        var modelClient = new SequenceAiModelClient(
            new[] { clarificationJson, completedSpecJson },
            Array.Empty<string>());
        var output = new StringWriter();
        var adapter = new LocalAiDrawingSpecAdapter(
            modelClient,
            new LocalAiServiceOptions
            {
                LogWriter = new RedactedAiCallLogWriter(output)
            });

        var firstResponse = adapter.CreateDrawingSpec(new AiDrawingSpecRequest
        {
            RequestId = "p4-06-clarification",
            UserRequest = "SECRET-USER-REQUEST: draw a private part."
        });
        var secondResponse = adapter.ContinueDrawingSpecAfterClarification(new AiClarificationFollowUpRequest
        {
            ClarificationState = firstResponse.ClarificationState!,
            UserAnswers = new[] { "SECRET-ANSWER: width 100 mm and height 60 mm." }
        });

        AssertEqual(AiDrawingSpecResponseKind.NeedsClarification, firstResponse.Kind);
        AssertEqual(AiDrawingSpecResponseKind.DrawingSpec, secondResponse.Kind);
        var logText = output.ToString();
        Assert(!logText.Contains("SECRET-USER-REQUEST", StringComparison.Ordinal), "Default logs must redact the original user request.");
        Assert(!logText.Contains("SECRET-ANSWER", StringComparison.Ordinal), "Default logs must redact clarification answers.");
        Assert(!logText.Contains(clarificationQuestion, StringComparison.Ordinal), "Default logs must record clarification counts, not question text.");
        Assert(!logText.Contains("outer-profile", StringComparison.Ordinal), "Default logs must not include full DrawingSpec entity ids.");
        Assert(!logText.Contains(completedSpecJson.Trim(), StringComparison.Ordinal), "Default logs must not include full DrawingSpec JSON.");

        var logLines = logText.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
        AssertEqual(2, logLines.Length);

        using (var firstLog = JsonDocument.Parse(logLines[0]))
        {
            var root = firstLog.RootElement;
            AssertEqual("p4-06-clarification", root.GetProperty("requestId").GetString());
            AssertEqual("NeedsClarification", root.GetProperty("responseKind").GetString());
            AssertEqual(1, root.GetProperty("attemptCount").GetInt32());
            AssertEqual(1, root.GetProperty("clarificationQuestionCount").GetInt32());
            AssertEqual(0, root.GetProperty("clarificationAnswerCount").GetInt32());
            AssertEqual(AiIssueCodes.NeedsClarification, root.GetProperty("issues")[0].GetProperty("code").GetString());
            AssertEqual("$.clarifications", root.GetProperty("issues")[0].GetProperty("path").GetString());
            AssertEqual("UserClarification", root.GetProperty("issues")[0].GetProperty("source").GetString());
        }

        using (var secondLog = JsonDocument.Parse(logLines[1]))
        {
            var root = secondLog.RootElement;
            AssertEqual("p4-06-clarification", root.GetProperty("requestId").GetString());
            AssertEqual("DrawingSpec", root.GetProperty("responseKind").GetString());
            AssertEqual(1, root.GetProperty("attemptCount").GetInt32());
            AssertEqual(1, root.GetProperty("clarificationQuestionCount").GetInt32());
            AssertEqual(1, root.GetProperty("clarificationAnswerCount").GetInt32());
            AssertEqual(0, root.GetProperty("issues").GetArrayLength());
        }
    }

    private static void SensitiveAiCallLoggingOptInNeverRecordsApiKey()
    {
        const string apiKeyEnv = "ZWCAD_AI_LOGGING_TEST_API_KEY";
        const string apiKey = "test-secret-token";
        var previousApiKey = Environment.GetEnvironmentVariable(apiKeyEnv);
        var responseJson = MinimalSpecJson("""
            {
              "id": "sensitive-log-line",
              "type": "line",
              "layer": "OUTLINE",
              "start": [0, 0],
              "end": [100, 0]
            }
            """).Replace("schema validation test", apiKey);
        var output = new StringWriter();

        try
        {
            Environment.SetEnvironmentVariable(apiKeyEnv, apiKey);
            var adapter = new LocalAiDrawingSpecAdapter(
                new StaticAiModelClient(responseJson),
                new LocalAiServiceOptions
                {
                    ApiKeyEnvironmentVariable = apiKeyEnv,
                    LogSensitiveDrawingContent = true,
                    LogWriter = new RedactedAiCallLogWriter(output)
                });

            var response = adapter.CreateDrawingSpec(new AiDrawingSpecRequest
            {
                RequestId = "p4-06-sensitive",
                UserRequest = $"Draw a plate. The API key is {apiKey}."
            });

            AssertEqual(AiDrawingSpecResponseKind.DrawingSpec, response.Kind);
            var logLine = output.ToString();
            Assert(!logLine.Contains(apiKey, StringComparison.Ordinal), "Sensitive logging opt-in must still redact the configured API key value.");
            Assert(logLine.Contains("[redacted-api-key]", StringComparison.Ordinal), "Sensitive logging must replace API key values with a stable redaction marker.");

            using var document = JsonDocument.Parse(logLine);
            var sensitiveContent = document.RootElement.GetProperty("sensitiveContent");
            Assert(
                sensitiveContent.GetProperty("userRequest").GetString()!.Contains("[redacted-api-key]", StringComparison.Ordinal),
                "Opt-in sensitive logging may include the user request only after API key redaction.");
            Assert(
                !sensitiveContent.GetProperty("drawingSpecJson").GetString()!.Contains(apiKey, StringComparison.Ordinal),
                "Opt-in sensitive logging must redact API key values from DrawingSpec JSON.");
        }
        finally
        {
            Environment.SetEnvironmentVariable(apiKeyEnv, previousApiKey);
        }
    }

    private static void LocalAiAdapterAcceptsJsonOnlyDrawingSpecResponses()
    {
        var json = MinimalSpecJson("""
            {
              "id": "adapter-line",
              "type": "line",
              "layer": "OUTLINE",
              "start": [0, 0],
              "end": [100, 0]
            }
            """);
        var adapter = new LocalAiDrawingSpecAdapter(new StaticAiModelClient(json));

        var response = adapter.CreateDrawingSpec(new AiDrawingSpecRequest { RequestId = "adapter-json-only" });

        AssertEqual(AiDrawingSpecResponseKind.DrawingSpec, response.Kind);
        Assert(response.Spec != null, "Valid JSON-only DrawingSpec response must include a parsed spec.");
        AssertEqual(json, response.DrawingSpecJson);
        Assert(response.Validation.IsValid, $"Valid JSON-only DrawingSpec response should pass validation: {FormatIssues(response.Validation.Issues)}");
        AssertEqual("adapter-line", response.Spec!.Entities.Single().Id);
    }

    private static void LocalAiAdapterRejectsNonJsonModelResponses()
    {
        var adapter = new LocalAiDrawingSpecAdapter(new StaticAiModelClient("Here is the drawing specification."));

        var response = adapter.CreateDrawingSpec(new AiDrawingSpecRequest { RequestId = "adapter-non-json" });

        AssertEqual(AiDrawingSpecResponseKind.Rejected, response.Kind);
        Assert(
            response.Issues.Any(issue => issue.Code == AiIssueCodes.ModelResponseNotJson
                && issue.Source == AiModelIssueSource.ModelResponse
                && !issue.Repairable),
            $"Non-JSON model response must be rejected as non-repairable model output: {FormatModelIssues(response.Issues)}");
    }

    private static void LocalAiAdapterRejectsMarkdownFencedModelResponses()
    {
        var json = MinimalSpecJson("""
            {
              "id": "fenced-line",
              "type": "line",
              "layer": "OUTLINE",
              "start": [0, 0],
              "end": [25, 0]
            }
            """);
        var adapter = new LocalAiDrawingSpecAdapter(new StaticAiModelClient($"```json{Environment.NewLine}{json}{Environment.NewLine}```"));

        var response = adapter.CreateDrawingSpec(new AiDrawingSpecRequest { RequestId = "adapter-fenced" });

        AssertEqual(AiDrawingSpecResponseKind.Rejected, response.Kind);
        Assert(
            response.Issues.Any(issue => issue.Code == AiIssueCodes.ModelResponseNotJson),
            $"Markdown fenced output must fail the JSON-only adapter boundary: {FormatModelIssues(response.Issues)}");
    }

    private static void LocalAiAdapterRejectsUnsafeCadCommandResponses()
    {
        var adapter = new LocalAiDrawingSpecAdapter(new StaticAiModelClient("(command \"LINE\" \"0,0\" \"100,0\" \"\")"));

        var response = adapter.CreateDrawingSpec(new AiDrawingSpecRequest { RequestId = "adapter-command" });

        AssertEqual(AiDrawingSpecResponseKind.Rejected, response.Kind);
        Assert(
            response.Issues.Any(issue => issue.Code == AiIssueCodes.UnsafeCadCommand
                && issue.Source == AiModelIssueSource.ModelResponse
                && !issue.Repairable),
            $"Free CAD commands must be rejected before any DrawingSpec parsing: {FormatModelIssues(response.Issues)}");
    }

    private static void LocalAiAdapterAllowsCommandLikeWordsInsideJsonText()
    {
        var json = MinimalSpecJson("""
            {
              "id": "safe-text-line",
              "type": "line",
              "layer": "OUTLINE",
              "start": [0, 0],
              "end": [100, 0]
            }
            """).Replace(
                "\"schema validation test\"",
                "\"PowerShell shell LISP reference note\"");
        var adapter = new LocalAiDrawingSpecAdapter(new StaticAiModelClient(json));

        var response = adapter.CreateDrawingSpec(new AiDrawingSpecRequest { RequestId = "adapter-safe-json-text" });

        AssertEqual(AiDrawingSpecResponseKind.DrawingSpec, response.Kind);
        Assert(response.Validation.IsValid, $"Command-like words inside JSON text should not be treated as executable output: {FormatIssues(response.Validation.Issues)}");
    }

    private static void LocalAiAdapterMapsClarificationResponses()
    {
        var adapter = new LocalAiDrawingSpecAdapter(new StaticAiModelClient("""
            {
              "drawingSpecVersion": "1.0",
              "units": "mm",
              "metadata": {
                "title": "clarification test",
                "domain": "mechanical_plate",
                "createdBy": "test",
                "requestId": "adapter-clarification"
              },
              "layers": [
                { "name": "OUTLINE", "color": 7, "lineType": "Continuous", "lineWeight": 0.35 }
              ],
              "entities": [],
              "dimensions": [],
              "clarifications": [
                "Please provide the rectangular plate width.",
                "Please provide the rectangular plate height."
              ]
            }
            """));

        var response = adapter.CreateDrawingSpec(new AiDrawingSpecRequest { RequestId = "adapter-clarification" });

        AssertEqual(AiDrawingSpecResponseKind.NeedsClarification, response.Kind);
        AssertEqual(2, response.Clarifications.Count);
        AssertEqual("Please provide the rectangular plate width.", response.Clarifications[0]);
        Assert(
            response.Issues.Any(issue => issue.Code == AiIssueCodes.NeedsClarification
                && issue.Source == AiModelIssueSource.UserClarification
                && !issue.Repairable),
            $"Clarification response must include a stable non-repair issue: {FormatModelIssues(response.Issues)}");
    }

    private static void LocalAiAdapterMapsSchemaValidationIssuesAsRepairable()
    {
        var json = MinimalSpecJson("""
            {
              "id": "missing-layer",
              "type": "line",
              "start": [0, 0],
              "end": [100, 0]
            }
            """);
        var adapter = new LocalAiDrawingSpecAdapter(new StaticAiModelClient(json));

        var response = adapter.CreateDrawingSpec(new AiDrawingSpecRequest { RequestId = "adapter-schema-invalid" });

        AssertEqual(AiDrawingSpecResponseKind.Rejected, response.Kind);
        Assert(
            response.Issues.Any(issue => issue.Code == "missing_required"
                && issue.Path == "$.entities[0].layer"
                && issue.Source == AiModelIssueSource.SchemaValidation
                && issue.Repairable),
            $"Repairable schema issues must be mapped into stable AI model issues: {FormatModelIssues(response.Issues)}");
    }

    private static void LocalAiAdapterMapsBusinessValidationIssuesAsRepairable()
    {
        var adapter = new LocalAiDrawingSpecAdapter(new StaticAiModelClient("""
            {
              "drawingSpecVersion": "1.0",
              "units": "mm",
              "metadata": {
                "title": "business validation test",
                "domain": "mechanical_plate",
                "createdBy": "test",
                "requestId": "adapter-business-invalid"
              },
              "layers": [
                { "name": "OUTLINE", "color": 7, "lineType": "Continuous", "lineWeight": 0.35 }
              ],
              "entities": [
                {
                  "id": "business-line",
                  "type": "line",
                  "layer": "OUTLINE",
                  "start": [0, 0],
                  "end": [100, 0]
                }
              ],
              "dimensions": [],
              "clarifications": []
            }
            """));

        var response = adapter.CreateDrawingSpec(new AiDrawingSpecRequest { RequestId = "adapter-business-invalid" });

        AssertEqual(AiDrawingSpecResponseKind.Rejected, response.Kind);
        Assert(
            response.Issues.Any(issue => issue.Code == "missing_required_layer"
                && issue.Path == "$.layers[CENTER]"
                && issue.Source == AiModelIssueSource.BusinessValidation
                && issue.Repairable),
            $"Repairable business issues must be mapped into stable AI model issues: {FormatModelIssues(response.Issues)}");
    }

    private static void LocalAiAdapterMapsTimeoutFailuresAsNonRepairable()
    {
        var modelClient = new ThrowingAiModelClient(new TimeoutException("slow model"));
        var adapter = new LocalAiDrawingSpecAdapter(
            modelClient,
            new LocalAiServiceOptions { MaxRetries = 2 });

        var response = adapter.CreateDrawingSpec(new AiDrawingSpecRequest { RequestId = "adapter-timeout" });

        AssertEqual(AiDrawingSpecResponseKind.Rejected, response.Kind);
        AssertEqual(3, modelClient.DrawingSpecCalls);
        Assert(
            response.Issues.Any(issue => issue.Code == AiIssueCodes.ModelServiceTimeout
                && issue.Source == AiModelIssueSource.Service
                && !issue.Repairable),
            $"Service timeout must be a non-repairable service issue after bounded retry: {FormatModelIssues(response.Issues)}");
    }

    private static void LocalAiAdapterMapsServiceFailuresAsNonRepairable()
    {
        var modelClient = new ThrowingAiModelClient(new InvalidOperationException("bad endpoint"));
        var adapter = new LocalAiDrawingSpecAdapter(
            modelClient,
            new LocalAiServiceOptions { MaxRetries = 2 });

        var response = adapter.CreateDrawingSpec(new AiDrawingSpecRequest { RequestId = "adapter-service-failure" });

        AssertEqual(AiDrawingSpecResponseKind.Rejected, response.Kind);
        AssertEqual(1, modelClient.DrawingSpecCalls);
        Assert(
            response.Issues.Any(issue => issue.Code == AiIssueCodes.ModelServiceFailed
                && issue.Source == AiModelIssueSource.Service
                && !issue.Repairable),
            $"Service failure must be a non-repairable service issue: {FormatModelIssues(response.Issues)}");
    }

    private static void LocalAiAdapterMapsCancellationFailuresAsNonRepairable()
    {
        var modelClient = new ThrowingAiModelClient(new OperationCanceledException("user canceled"));
        var adapter = new LocalAiDrawingSpecAdapter(
            modelClient,
            new LocalAiServiceOptions { MaxRetries = 2 });

        var response = adapter.CreateDrawingSpec(new AiDrawingSpecRequest { RequestId = "adapter-canceled" });

        AssertEqual(AiDrawingSpecResponseKind.Rejected, response.Kind);
        AssertEqual(1, modelClient.DrawingSpecCalls);
        Assert(
            response.Issues.Any(issue => issue.Code == AiIssueCodes.ModelServiceCanceled
                && issue.Source == AiModelIssueSource.Service
                && !issue.Repairable),
            $"Service cancellation must be a non-repairable service issue without retry: {FormatModelIssues(response.Issues)}");
    }

    private static void HttpAiModelClientPostsCreateRequestsWithEnvApiKey()
    {
        const string apiKeyEnv = "ZWCAD_AI_TEST_API_KEY";
        const string apiKey = "test-secret-token";
        var responseJson = MinimalSpecJson("""
            {
              "id": "http-create-line",
              "type": "line",
              "layer": "OUTLINE",
              "start": [0, 0],
              "end": [100, 0]
            }
            """);
        string? capturedBody = null;
        HttpRequestMessage? capturedRequest = null;
        var previousApiKey = Environment.GetEnvironmentVariable(apiKeyEnv);

        try
        {
            Environment.SetEnvironmentVariable(apiKeyEnv, apiKey);
            var client = new HttpAiModelClient(new HttpClient(new CapturingHttpMessageHandler(request =>
            {
                capturedRequest = request;
                capturedBody = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(responseJson)
                };
            })));

            var raw = client.CreateDrawingSpec(
                new AiDrawingSpecRequest
                {
                    RequestId = "http-create",
                    UserRequest = "画一个100x60矩形板"
                },
                new AiModelCallOptions
                {
                    ServiceEndpoint = "https://model-gateway.example.test/drawing-spec",
                    ApiKeyEnvironmentVariable = apiKeyEnv,
                    Timeout = TimeSpan.FromSeconds(5)
                });

            AssertEqual(responseJson, raw);
            Assert(capturedRequest != null, "HTTP provider must send a request.");
            AssertEqual(HttpMethod.Post, capturedRequest!.Method);
            AssertEqual("https://model-gateway.example.test/drawing-spec", capturedRequest.RequestUri!.ToString());
            AssertEqual("Bearer", capturedRequest.Headers.Authorization!.Scheme);
            AssertEqual(apiKey, capturedRequest.Headers.Authorization.Parameter);
            Assert(capturedBody != null, "HTTP provider must serialize a JSON body.");
            Assert(!capturedBody!.Contains(apiKey), "Serialized model requests must not include the API key value in the body.");

            using var body = JsonDocument.Parse(capturedBody);
            var root = body.RootElement;
            AssertEqual("createDrawingSpec", root.GetProperty("operation").GetString());
            AssertEqual("http-create", root.GetProperty("requestId").GetString());
            AssertEqual(ModelPromptContract.PromptVersion, root.GetProperty("promptVersion").GetString());
            AssertEqual("画一个100x60矩形板", root.GetProperty("userRequest").GetString());
            AssertEqual("mm", root.GetProperty("context").GetProperty("units").GetString());
            AssertEqual(DrawingDomain.MechanicalPlate, root.GetProperty("context").GetProperty("domain").GetString());
            AssertEqual(DrawingSpecWireFormat.Version, root.GetProperty("context").GetProperty("drawingSpecVersion").GetString());
        }
        finally
        {
            Environment.SetEnvironmentVariable(apiKeyEnv, previousApiKey);
        }
    }

    private static void HttpAiModelClientPostsRepairRequestsWithoutOriginalContext()
    {
        var invalidJson = MinimalSpecJson("""
            {
              "id": "http-repair-missing-layer",
              "type": "line",
              "start": [0, 0],
              "end": [100, 0]
            }
            """);
        var repairedJson = MinimalSpecJson("""
            {
              "id": "http-repaired-line",
              "type": "line",
              "layer": "OUTLINE",
              "start": [0, 0],
              "end": [100, 0]
            }
            """);
        string? capturedBody = null;
        var client = new HttpAiModelClient(new HttpClient(new CapturingHttpMessageHandler(request =>
        {
            capturedBody = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(repairedJson)
            };
        })));

        var raw = client.RepairDrawingSpec(
            new AiDrawingSpecRepairRequest
            {
                InvalidDrawingSpecJson = invalidJson,
                RepairAttempt = 2,
                MaxRepairAttempts = 2,
                Issues = new[]
                {
                    new AiModelIssue(
                        "missing_required",
                        "$.entities[0].layer",
                        "Property 'layer' is required.",
                        ValidationSeverity.Error,
                        AiModelIssueSource.SchemaValidation,
                        repairable: true)
                }
            },
            new AiModelCallOptions
            {
                ServiceEndpoint = "http://localhost:3210/repair",
                Timeout = TimeSpan.FromSeconds(5)
            });

        AssertEqual(repairedJson, raw);
        Assert(capturedBody != null, "HTTP repair provider must serialize a JSON body.");
        Assert(!capturedBody!.Contains("userRequest"), "Repair requests must not include the original natural language request.");
        Assert(!capturedBody.Contains("originalRequest"), "Repair requests must not include original request aliases.");
        Assert(!capturedBody.Contains("dwg", StringComparison.OrdinalIgnoreCase), "Repair requests must not include DWG context.");
        Assert(!capturedBody.Contains("screenshot", StringComparison.OrdinalIgnoreCase), "Repair requests must not include screenshots.");
        Assert(!capturedBody.Contains("pluginContext", StringComparison.Ordinal), "Repair requests must not include plugin context.");

        using var body = JsonDocument.Parse(capturedBody);
        var root = body.RootElement;
        AssertSequenceEqual(
            new[]
            {
                "invalidDrawingSpecJson",
                "issues",
                "maxRepairAttempts",
                "operation",
                "promptVersion",
                "repairAttempt",
                "repairStrategy"
            },
            root.EnumerateObject().Select(property => property.Name).OrderBy(name => name, StringComparer.Ordinal).ToArray());
        AssertEqual("repairDrawingSpec", root.GetProperty("operation").GetString());
        AssertEqual(ModelPromptContract.PromptVersion, root.GetProperty("promptVersion").GetString());
        AssertEqual(invalidJson, root.GetProperty("invalidDrawingSpecJson").GetString());
        AssertEqual(2, root.GetProperty("repairAttempt").GetInt32());
        AssertEqual(2, root.GetProperty("maxRepairAttempts").GetInt32());
        var issue = root.GetProperty("issues")[0];
        AssertSequenceEqual(
            new[] { "code", "message", "path", "repairable", "severity", "source" },
            issue.EnumerateObject().Select(property => property.Name).OrderBy(name => name, StringComparer.Ordinal).ToArray());
        AssertEqual("missing_required", issue.GetProperty("code").GetString());
        AssertEqual("$.entities[0].layer", issue.GetProperty("path").GetString());
        AssertEqual("SchemaValidation", issue.GetProperty("source").GetString());
    }

    private static void ClarificationFollowUpServiceFlowUsesHttpCreateAndValidRendererPlan()
    {
        var clarificationJson = """
            {
              "drawingSpecVersion": "1.0",
              "units": "mm",
              "metadata": {
                "title": "http clarification test",
                "domain": "mechanical_plate",
                "createdBy": "test",
                "requestId": "p4-05-http"
              },
              "layers": [
                { "name": "OUTLINE", "color": 7, "lineType": "Continuous", "lineWeight": 0.35 }
              ],
              "entities": [],
              "dimensions": [],
              "clarifications": [
                "Please provide the rectangular plate width and height."
              ]
            }
            """;
        var completedSpecJson = ReadExampleJson("rectangular-plate.example.json");
        var capturedBodies = new List<string>();
        var handler = new CapturingHttpMessageHandler(request =>
        {
            var body = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            capturedBodies.Add(body);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(capturedBodies.Count == 1 ? clarificationJson : completedSpecJson)
            };
        });
        var adapter = new LocalAiDrawingSpecAdapter(
            new HttpAiModelClient(new HttpClient(handler)),
            new LocalAiServiceOptions
            {
                ServiceEndpoint = "https://model-gateway.example.test/drawing-spec",
                Timeout = TimeSpan.FromSeconds(5)
            });

        var firstResponse = adapter.CreateDrawingSpec(new AiDrawingSpecRequest
        {
            RequestId = "p4-05-http",
            UserRequest = "Draw a rectangular plate. Dimensions are not specified."
        });

        AssertEqual(AiDrawingSpecResponseKind.NeedsClarification, firstResponse.Kind);
        Assert(firstResponse.ClarificationState != null, "HTTP clarification response must include follow-up state.");

        var secondResponse = adapter.ContinueDrawingSpecAfterClarification(new AiClarificationFollowUpRequest
        {
            ClarificationState = firstResponse.ClarificationState!,
            UserAnswers = new[] { "Use width 100 mm and height 60 mm; keep the centered diameter 12 hole." }
        });

        AssertEqual(AiDrawingSpecResponseKind.DrawingSpec, secondResponse.Kind);
        AssertEqual(2, handler.Calls);
        AssertEqual(2, capturedBodies.Count);
        foreach (var body in capturedBodies)
        {
            using var document = JsonDocument.Parse(body);
            AssertEqual("createDrawingSpec", document.RootElement.GetProperty("operation").GetString());
            Assert(!body.Contains("repairDrawingSpec", StringComparison.Ordinal), "Clarification follow-up must not use the repair operation.");
            Assert(!body.Contains("invalidDrawingSpecJson", StringComparison.Ordinal), "Clarification follow-up create must not carry repair JSON.");
            Assert(!body.Contains("dwg", StringComparison.OrdinalIgnoreCase), "Clarification follow-up create must not carry DWG content.");
            Assert(!body.Contains("screenshot", StringComparison.OrdinalIgnoreCase), "Clarification follow-up create must not carry screenshots.");
            Assert(!body.Contains("pluginContext", StringComparison.Ordinal), "Clarification follow-up create must not carry plugin context.");
        }

        using (var secondBody = JsonDocument.Parse(capturedBodies[1]))
        {
            var root = secondBody.RootElement;
            AssertEqual("p4-05-http", root.GetProperty("requestId").GetString());
            AssertEqual(ModelPromptContract.PromptVersion, root.GetProperty("promptVersion").GetString());
            var userRequest = root.GetProperty("userRequest").GetString() ?? string.Empty;
            Assert(
                userRequest.IndexOf("Draw a rectangular plate.", StringComparison.Ordinal) >= 0,
                "Follow-up create must retain the original user request.");
            Assert(
                userRequest.IndexOf("Please provide the rectangular plate width and height.", StringComparison.Ordinal) >= 0,
                "Follow-up create must include the clarification question.");
            Assert(
                userRequest.IndexOf("Use width 100 mm and height 60 mm", StringComparison.Ordinal) >= 0,
                "Follow-up create must include the user's clarification answer.");
            AssertEqual("mm", root.GetProperty("context").GetProperty("units").GetString());
            AssertEqual(DrawingDomain.MechanicalPlate, root.GetProperty("context").GetProperty("domain").GetString());
        }

        Assert(secondResponse.Spec != null, "HTTP follow-up must return a parsed DrawingSpec.");
        var businessValidation = DrawingSpecValidator.ValidateBusinessRules(secondResponse.Spec!);
        Assert(businessValidation.IsValid, $"HTTP follow-up DrawingSpec must pass business validation: {FormatIssues(businessValidation.Issues)}");

        var plan = new DrawingSpecPlanRenderer().CreatePlan(secondResponse.Spec!);
        Assert(plan.Validation.IsValid, $"HTTP follow-up DrawingSpec must produce a valid renderer plan: {FormatIssues(plan.Validation.Issues)}");
        AssertEqual(3, plan.Dimensions.Count);
        Assert(plan.Dimensions.Any(dimension => dimension.SpecDimensionId == "dim-width"), "Renderer plan must include the clarified width dimension.");
        Assert(plan.Dimensions.Any(dimension => dimension.SpecDimensionId == "dim-height"), "Renderer plan must include the clarified height dimension.");
    }

    private static void HttpAiModelClientTimeoutFailuresRetryThroughAdapter()
    {
        var handler = new CapturingHttpMessageHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}")
            };
        });
        var adapter = new LocalAiDrawingSpecAdapter(
            new HttpAiModelClient(new HttpClient(handler)),
            new LocalAiServiceOptions
            {
                ServiceEndpoint = "http://localhost:3210/timeout",
                Timeout = TimeSpan.FromMilliseconds(10),
                MaxRetries = 2
            });

        var response = adapter.CreateDrawingSpec(new AiDrawingSpecRequest { RequestId = "http-timeout" });

        AssertEqual(AiDrawingSpecResponseKind.Rejected, response.Kind);
        AssertEqual(3, handler.Calls);
        Assert(
            response.Issues.Any(issue => issue.Code == AiIssueCodes.ModelServiceTimeout
                && issue.Source == AiModelIssueSource.Service
                && !issue.Repairable),
            $"HTTP timeouts must retry and end as a stable service issue: {FormatModelIssues(response.Issues)}");
    }

    private static void HttpAiModelClientCancellationFailuresDoNotRetry()
    {
        using var cancellation = new CancellationTokenSource();
        var handler = new CapturingHttpMessageHandler(async (_, cancellationToken) =>
        {
            cancellation.Cancel();
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}")
            };
        });
        var adapter = new LocalAiDrawingSpecAdapter(
            new HttpAiModelClient(new HttpClient(handler)),
            new LocalAiServiceOptions
            {
                ServiceEndpoint = "http://localhost:3210/cancel",
                Timeout = TimeSpan.FromSeconds(5),
                MaxRetries = 2,
                CancellationToken = cancellation.Token
            });

        var response = adapter.CreateDrawingSpec(new AiDrawingSpecRequest { RequestId = "http-canceled" });

        AssertEqual(AiDrawingSpecResponseKind.Rejected, response.Kind);
        AssertEqual(1, handler.Calls);
        Assert(
            response.Issues.Any(issue => issue.Code == AiIssueCodes.ModelServiceCanceled
                && issue.Source == AiModelIssueSource.Service
                && !issue.Repairable),
            $"HTTP cancellation must not retry and must end as a stable service issue: {FormatModelIssues(response.Issues)}");
    }

    private static void HttpAiModelClientNonSuccessFailuresStayRedacted()
    {
        var providerControlledReason = "leaked userRequest=draw-secret-plate token=abc123";
        var handler = new CapturingHttpMessageHandler(_ =>
        {
            return new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                ReasonPhrase = providerControlledReason,
                Content = new StringContent("provider body should not be surfaced")
            };
        });
        var adapter = new LocalAiDrawingSpecAdapter(
            new HttpAiModelClient(new HttpClient(handler)),
            new LocalAiServiceOptions
            {
                ServiceEndpoint = "http://localhost:3210/fail",
                Timeout = TimeSpan.FromSeconds(5),
                MaxRetries = 2
            });

        var response = adapter.CreateDrawingSpec(new AiDrawingSpecRequest { RequestId = "http-500" });

        AssertEqual(AiDrawingSpecResponseKind.Rejected, response.Kind);
        AssertEqual(1, handler.Calls);
        var serviceIssue = response.Issues.Single(issue => issue.Code == AiIssueCodes.ModelServiceFailed);
        AssertEqual(AiModelIssueSource.Service, serviceIssue.Source);
        Assert(!serviceIssue.Repairable, "HTTP non-success failure must not enter the DrawingSpec repair loop.");
        AssertEqual("Model service failed before returning DrawingSpec JSON.", serviceIssue.Message);
        Assert(
            serviceIssue.Message.IndexOf(providerControlledReason, StringComparison.Ordinal) < 0,
            "Service issue messages must not surface provider-controlled HTTP reason phrases.");
    }

    private static void HttpAiModelClientRejectsMissingApiKeyEnvironmentVariable()
    {
        const string apiKeyEnv = "ZWCAD_AI_TEST_MISSING_API_KEY";
        var previousApiKey = Environment.GetEnvironmentVariable(apiKeyEnv);

        try
        {
            Environment.SetEnvironmentVariable(apiKeyEnv, null);
            var handler = new CapturingHttpMessageHandler(_ =>
            {
                throw new InvalidOperationException("HTTP should not be called when the API key is missing.");
            });
            var adapter = new LocalAiDrawingSpecAdapter(
                new HttpAiModelClient(new HttpClient(handler)),
                new LocalAiServiceOptions
                {
                    ServiceEndpoint = "http://localhost:3210/missing-key",
                    ApiKeyEnvironmentVariable = apiKeyEnv,
                    Timeout = TimeSpan.FromSeconds(5),
                    MaxRetries = 2
                });

            var response = adapter.CreateDrawingSpec(new AiDrawingSpecRequest { RequestId = "http-missing-key" });

            AssertEqual(AiDrawingSpecResponseKind.Rejected, response.Kind);
            AssertEqual(0, handler.Calls);
            var serviceIssue = response.Issues.Single(issue => issue.Code == AiIssueCodes.ModelServiceFailed);
            AssertEqual(AiModelIssueSource.Service, serviceIssue.Source);
            Assert(!serviceIssue.Repairable, "Missing API key configuration must not enter the DrawingSpec repair loop.");
            AssertEqual("Model service failed before returning DrawingSpec JSON.", serviceIssue.Message);
            Assert(
                serviceIssue.Message.IndexOf(apiKeyEnv, StringComparison.Ordinal) < 0,
                "Service issue messages must not surface environment variable names or secret configuration detail.");
        }
        finally
        {
            Environment.SetEnvironmentVariable(apiKeyEnv, previousApiKey);
        }
    }

    private static void LocalAiAdapterEnforcesRepairAttemptLimit()
    {
        var modelClient = new StaticAiModelClient(MinimalSpecJson("""
            {
              "id": "should-not-run",
              "type": "line",
              "layer": "OUTLINE",
              "start": [0, 0],
              "end": [100, 0]
            }
            """));
        var adapter = new LocalAiDrawingSpecAdapter(modelClient);
        var request = new AiDrawingSpecRepairRequest
        {
            InvalidDrawingSpecJson = "{\"entities\":[]}",
            RepairAttempt = ModelPromptContract.MaxRepairAttempts + 1,
            MaxRepairAttempts = ModelPromptContract.MaxRepairAttempts
        };

        var response = adapter.RepairDrawingSpec(request);

        AssertEqual(AiDrawingSpecResponseKind.Rejected, response.Kind);
        AssertEqual(0, modelClient.DrawingSpecCalls);
        AssertEqual(0, modelClient.RepairCalls);
        Assert(
            response.Issues.Any(issue => issue.Code == AiIssueCodes.RepairAttemptLimitExceeded
                && issue.Source == AiModelIssueSource.Service
                && !issue.Repairable),
            $"Repair attempt limit must return a stable service issue without calling the model: {FormatModelIssues(response.Issues)}");
    }

    private static void LocalAiAdapterRejectsInvalidRepairAttemptNumbers()
    {
        var modelClient = new StaticAiModelClient(MinimalSpecJson("""
            {
              "id": "should-not-run-invalid-attempt",
              "type": "line",
              "layer": "OUTLINE",
              "start": [0, 0],
              "end": [100, 0]
            }
            """));
        var adapter = new LocalAiDrawingSpecAdapter(modelClient);
        var request = new AiDrawingSpecRepairRequest
        {
            InvalidDrawingSpecJson = "{\"entities\":[]}",
            RepairAttempt = 0,
            MaxRepairAttempts = ModelPromptContract.MaxRepairAttempts
        };

        var response = adapter.RepairDrawingSpec(request);

        AssertEqual(AiDrawingSpecResponseKind.Rejected, response.Kind);
        AssertEqual(0, modelClient.DrawingSpecCalls);
        AssertEqual(0, modelClient.RepairCalls);
        Assert(
            response.Issues.Any(issue => issue.Code == AiIssueCodes.InvalidRepairAttempt
                && issue.Source == AiModelIssueSource.Service
                && !issue.Repairable),
            $"Invalid repair attempt numbers must return a stable service issue without calling the model: {FormatModelIssues(response.Issues)}");
    }

    private static void LocalAiAdapterRepairsFirstSchemaInvalidResponse()
    {
        var invalidJson = MinimalSpecJson("""
            {
              "id": "schema-repair-target",
              "type": "line",
              "start": [0, 0],
              "end": [100, 0]
            }
            """);
        var repairedJson = MinimalSpecJson("""
            {
              "id": "schema-repaired-line",
              "type": "line",
              "layer": "OUTLINE",
              "start": [0, 0],
              "end": [100, 0]
            }
            """);
        var modelClient = new SequenceAiModelClient(
            new[] { invalidJson },
            new[] { repairedJson });
        var adapter = new LocalAiDrawingSpecAdapter(modelClient);

        var response = adapter.CreateDrawingSpec(new AiDrawingSpecRequest { RequestId = "adapter-schema-repair" });

        AssertEqual(AiDrawingSpecResponseKind.DrawingSpec, response.Kind);
        AssertEqual(1, modelClient.DrawingSpecCalls);
        AssertEqual(1, modelClient.RepairCalls);
        AssertEqual(1, modelClient.RepairRequests.Count);
        AssertEqual(1, modelClient.RepairRequests[0].RepairAttempt);
        AssertEqual(ModelPromptContract.MaxRepairAttempts, modelClient.RepairRequests[0].MaxRepairAttempts);
        AssertEqual(AiRepairStrategy.RepairDrawingSpecOnly, modelClient.RepairRequests[0].RepairStrategy);
        AssertEqual(invalidJson, modelClient.RepairRequests[0].InvalidDrawingSpecJson);
        Assert(
            modelClient.RepairRequests[0].Issues.Any(issue => issue.Code == "missing_required"
                && issue.Path == "$.entities[0].layer"
                && issue.Source == AiModelIssueSource.SchemaValidation
                && issue.Repairable),
            $"Schema repair request must carry mapped issue paths: {FormatModelIssues(modelClient.RepairRequests[0].Issues)}");
        AssertEqual(repairedJson, response.DrawingSpecJson);
        AssertEqual("schema-repaired-line", response.Spec!.Entities.Single().Id);
    }

    private static void LocalAiAdapterRepairsFirstBusinessInvalidResponse()
    {
        var invalidJson = BusinessInvalidSpecJson("business-repair-target");
        var repairedJson = MinimalSpecJson("""
            {
              "id": "business-repaired-line",
              "type": "line",
              "layer": "OUTLINE",
              "start": [0, 0],
              "end": [100, 0]
            }
            """);
        var modelClient = new SequenceAiModelClient(
            new[] { invalidJson },
            new[] { repairedJson });
        var adapter = new LocalAiDrawingSpecAdapter(modelClient);

        var response = adapter.CreateDrawingSpec(new AiDrawingSpecRequest { RequestId = "adapter-business-repair" });

        AssertEqual(AiDrawingSpecResponseKind.DrawingSpec, response.Kind);
        AssertEqual(1, modelClient.DrawingSpecCalls);
        AssertEqual(1, modelClient.RepairCalls);
        AssertEqual(1, modelClient.RepairRequests.Count);
        AssertEqual(1, modelClient.RepairRequests[0].RepairAttempt);
        AssertEqual(ModelPromptContract.MaxRepairAttempts, modelClient.RepairRequests[0].MaxRepairAttempts);
        AssertEqual(AiRepairStrategy.RepairDrawingSpecOnly, modelClient.RepairRequests[0].RepairStrategy);
        AssertEqual(invalidJson, modelClient.RepairRequests[0].InvalidDrawingSpecJson);
        Assert(
            modelClient.RepairRequests[0].Issues.Any(issue => issue.Code == "missing_required_layer"
                && issue.Path == "$.layers[CENTER]"
                && issue.Source == AiModelIssueSource.BusinessValidation
                && issue.Repairable),
            $"Business repair request must carry mapped issue paths: {FormatModelIssues(modelClient.RepairRequests[0].Issues)}");
        AssertEqual(repairedJson, response.DrawingSpecJson);
        AssertEqual("business-repaired-line", response.Spec!.Entities.Single().Id);
    }

    private static void LocalAiAdapterRejectsAfterBoundedRepairFailures()
    {
        var initialInvalidJson = MinimalSpecJson("""
            {
              "id": "initial-invalid-line",
              "type": "line",
              "start": [0, 0],
              "end": [100, 0]
            }
            """);
        var firstRepairInvalidJson = MinimalSpecJson("""
            {
              "id": "first-repair-invalid-line",
              "type": "line",
              "start": [0, 0],
              "end": [100, 0]
            }
            """);
        var secondRepairInvalidJson = MinimalSpecJson("""
            {
              "id": "second-repair-invalid-line",
              "type": "line",
              "start": [0, 0],
              "end": [100, 0]
            }
            """);
        var modelClient = new SequenceAiModelClient(
            new[] { initialInvalidJson },
            new[] { firstRepairInvalidJson, secondRepairInvalidJson });
        var adapter = new LocalAiDrawingSpecAdapter(modelClient);

        var response = adapter.CreateDrawingSpec(new AiDrawingSpecRequest { RequestId = "adapter-repair-limit" });

        AssertEqual(AiDrawingSpecResponseKind.Rejected, response.Kind);
        AssertEqual(1, modelClient.DrawingSpecCalls);
        AssertEqual(ModelPromptContract.MaxRepairAttempts, modelClient.RepairCalls);
        AssertEqual(ModelPromptContract.MaxRepairAttempts, modelClient.RepairRequests.Count);
        AssertEqual(1, modelClient.RepairRequests[0].RepairAttempt);
        AssertEqual(2, modelClient.RepairRequests[1].RepairAttempt);
        AssertEqual(secondRepairInvalidJson, response.DrawingSpecJson);
        Assert(
            response.Issues.Any(issue => issue.Code == AiIssueCodes.RepairAttemptLimitExceeded
                && issue.Source == AiModelIssueSource.Service
                && !issue.Repairable),
            $"Bounded repair failures must end with repair_attempt_limit_exceeded: {FormatModelIssues(response.Issues)}");
        Assert(
            response.Issues.Any(issue => issue.Code == "missing_required"
                && issue.Path == "$.entities[0].layer"
                && issue.Source == AiModelIssueSource.SchemaValidation),
            $"Final rejection should preserve the last mapped validation issue for user explanation: {FormatModelIssues(response.Issues)}");
    }

    private static void LocalAiAdapterContinuesExplicitRepairWithinAttemptLimit()
    {
        var firstRepairInvalidJson = MinimalSpecJson("""
            {
              "id": "explicit-first-invalid-line",
              "type": "line",
              "start": [0, 0],
              "end": [100, 0]
            }
            """);
        var secondRepairInvalidJson = MinimalSpecJson("""
            {
              "id": "explicit-second-invalid-line",
              "type": "line",
              "start": [0, 0],
              "end": [100, 0]
            }
            """);
        var startingIssue = new AiModelIssue(
            "missing_required",
            "$.entities[0].layer",
            "Property 'layer' is required.",
            ValidationSeverity.Error,
            AiModelIssueSource.SchemaValidation,
            repairable: true);
        var modelClient = new SequenceAiModelClient(
            Array.Empty<string>(),
            new[] { firstRepairInvalidJson, secondRepairInvalidJson });
        var adapter = new LocalAiDrawingSpecAdapter(modelClient);

        var response = adapter.RepairDrawingSpec(new AiDrawingSpecRepairRequest
        {
            InvalidDrawingSpecJson = "{\"entities\":[]}",
            Issues = new[] { startingIssue },
            RepairAttempt = 1,
            MaxRepairAttempts = 2
        });

        AssertEqual(AiDrawingSpecResponseKind.Rejected, response.Kind);
        AssertEqual(0, modelClient.DrawingSpecCalls);
        AssertEqual(2, modelClient.RepairCalls);
        AssertEqual(2, modelClient.RepairRequests.Count);
        AssertEqual(1, modelClient.RepairRequests[0].RepairAttempt);
        AssertEqual(2, modelClient.RepairRequests[1].RepairAttempt);
        AssertEqual(firstRepairInvalidJson, modelClient.RepairRequests[1].InvalidDrawingSpecJson);
        Assert(
            response.Issues.Any(issue => issue.Code == AiIssueCodes.RepairAttemptLimitExceeded
                && issue.Source == AiModelIssueSource.Service
                && !issue.Repairable),
            $"Explicit repair continuation must stop at the configured limit: {FormatModelIssues(response.Issues)}");
    }

    private static void LocalAiAdapterDoesNotRepairClarificationResponses()
    {
        var clarificationJson = """
            {
              "drawingSpecVersion": "1.0",
              "units": "mm",
              "metadata": {
                "title": "clarification repair gate test",
                "domain": "mechanical_plate",
                "createdBy": "test",
                "requestId": "adapter-clarification-no-repair"
              },
              "layers": [
                { "name": "OUTLINE", "color": 7, "lineType": "Continuous", "lineWeight": 0.35 }
              ],
              "entities": [],
              "dimensions": [],
              "clarifications": [
                "Please provide the plate width."
              ]
            }
            """;
        var modelClient = new SequenceAiModelClient(
            new[] { clarificationJson },
            new[] { MinimalSpecJson("""
                {
                  "id": "should-not-repair-clarification",
                  "type": "line",
                  "layer": "OUTLINE",
                  "start": [0, 0],
                  "end": [100, 0]
                }
                """) });
        var adapter = new LocalAiDrawingSpecAdapter(modelClient);

        var response = adapter.CreateDrawingSpec(new AiDrawingSpecRequest { RequestId = "adapter-clarification-no-repair" });

        AssertEqual(AiDrawingSpecResponseKind.NeedsClarification, response.Kind);
        AssertEqual(1, modelClient.DrawingSpecCalls);
        AssertEqual(0, modelClient.RepairCalls);
        AssertEqual(1, response.Clarifications.Count);
    }

    private static void LocalAiAdapterStartsFreshCreateAfterClarificationFollowUp()
    {
        var clarificationJson = """
            {
              "drawingSpecVersion": "1.0",
              "units": "mm",
              "metadata": {
                "title": "clarification follow-up test",
                "domain": "mechanical_plate",
                "createdBy": "test",
                "requestId": "p4-05-initial"
              },
              "layers": [
                { "name": "OUTLINE", "color": 7, "lineType": "Continuous", "lineWeight": 0.35 }
              ],
              "entities": [],
              "dimensions": [],
              "clarifications": [
                "Please provide the rectangular plate width.",
                "Please provide the rectangular plate height."
              ]
            }
            """;
        var completedSpecJson = ReadExampleJson("rectangular-plate.example.json");
        var modelClient = new SequenceAiModelClient(
            new[] { clarificationJson, completedSpecJson },
            Array.Empty<string>());
        var adapter = new LocalAiDrawingSpecAdapter(modelClient);

        var firstResponse = adapter.CreateDrawingSpec(new AiDrawingSpecRequest
        {
            RequestId = "p4-05-initial",
            UserRequest = "Create a rectangular plate with one hole, dimensions not yet specified."
        });

        AssertEqual(AiDrawingSpecResponseKind.NeedsClarification, firstResponse.Kind);
        AssertEqual(1, modelClient.DrawingSpecCalls);
        AssertEqual(0, modelClient.RepairCalls);
        AssertEqual(2, firstResponse.Clarifications.Count);
        Assert(firstResponse.ClarificationState != null, "Clarification response must include follow-up state.");
        AssertEqual("p4-05-initial", firstResponse.ClarificationState!.RequestId);
        AssertEqual(
            "Create a rectangular plate with one hole, dimensions not yet specified.",
            firstResponse.ClarificationState.OriginalUserRequest);
        AssertEqual(ModelPromptContract.PromptVersion, firstResponse.ClarificationState.PromptVersion);
        AssertEqual(2, firstResponse.ClarificationState.ClarificationQuestions.Count);

        var secondResponse = adapter.ContinueDrawingSpecAfterClarification(new AiClarificationFollowUpRequest
        {
            ClarificationState = firstResponse.ClarificationState,
            UserAnswers = new[]
            {
                "Width 100 mm, height 60 mm, hole diameter 12 mm centered at 30,30."
            }
        });

        AssertEqual(AiDrawingSpecResponseKind.DrawingSpec, secondResponse.Kind);
        AssertEqual(2, modelClient.DrawingSpecCalls);
        AssertEqual(0, modelClient.RepairCalls);
        AssertEqual(2, modelClient.DrawingSpecRequests.Count);
        AssertEqual("p4-05-initial", modelClient.DrawingSpecRequests[1].RequestId);
        Assert(
            modelClient.DrawingSpecRequests[1].UserRequest.IndexOf("Width 100 mm", StringComparison.OrdinalIgnoreCase) >= 0,
            "Clarification answer must be sent through a fresh CreateDrawingSpec request.");
        Assert(
            modelClient.DrawingSpecRequests[1].UserRequest.IndexOf("Please provide the rectangular plate width.", StringComparison.Ordinal) >= 0,
            "Fresh create request must include the original clarification question.");
        AssertEqual(ModelPromptContract.PromptVersion, modelClient.DrawingSpecRequests[1].PromptVersion);
        Assert(secondResponse.ClarificationState != null, "Follow-up DrawingSpec response should preserve answered clarification state.");
        AssertEqual(1, secondResponse.ClarificationState!.UserAnswers.Count);
        Assert(secondResponse.Spec != null, "Clarification follow-up must return a parsed DrawingSpec.");
        Assert(secondResponse.Validation.IsValid, $"Follow-up DrawingSpec must pass adapter validation: {FormatIssues(secondResponse.Validation.Issues)}");

        var businessValidation = DrawingSpecValidator.ValidateBusinessRules(secondResponse.Spec!);
        Assert(businessValidation.IsValid, $"Follow-up DrawingSpec must pass business validation: {FormatIssues(businessValidation.Issues)}");

        var plan = new DrawingSpecPlanRenderer().CreatePlan(secondResponse.Spec!);
        Assert(plan.Validation.IsValid, $"Follow-up DrawingSpec must produce a valid renderer plan: {FormatIssues(plan.Validation.Issues)}");
        AssertEqual(3, plan.Dimensions.Count);
        Assert(plan.Dimensions.Any(dimension => dimension.SpecDimensionId == "dim-width"), "Renderer plan must include the clarified plate width dimension.");
        Assert(plan.Dimensions.Any(dimension => dimension.SpecDimensionId == "dim-height"), "Renderer plan must include the clarified plate height dimension.");
    }

    private static void ServiceFlowReturnsRenderResultSummaryAfterClarification()
    {
        var clarificationJson = """
            {
              "drawingSpecVersion": "1.0",
              "units": "mm",
              "metadata": {
                "title": "P5 UI service contract clarification test",
                "domain": "mechanical_plate",
                "createdBy": "test",
                "requestId": "p5-ui-contract"
              },
              "layers": [
                { "name": "OUTLINE", "color": 7, "lineType": "Continuous", "lineWeight": 0.35 }
              ],
              "entities": [],
              "dimensions": [],
              "clarifications": [
                "Please provide the rectangular plate width and height."
              ]
            }
            """;
        var completedSpecJson = ReadExampleJson("rectangular-plate.example.json");
        var modelClient = new SequenceAiModelClient(
            new[] { clarificationJson, completedSpecJson },
            Array.Empty<string>());
        var adapter = new LocalAiDrawingSpecAdapter(modelClient);
        var renderer = new DrawingSpecPlanRenderer();

        var firstResponse = adapter.CreateDrawingSpec(new AiDrawingSpecRequest
        {
            RequestId = "p5-ui-contract",
            UserRequest = "Create a rectangular plate with one centered hole."
        });

        AssertEqual(AiDrawingSpecResponseKind.NeedsClarification, firstResponse.Kind);
        AssertEqual(1, firstResponse.Clarifications.Count);
        Assert(firstResponse.ClarificationState != null, "P5 UI contract requires a clarification state to continue the service flow.");

        var secondResponse = adapter.ContinueDrawingSpecAfterClarification(new AiClarificationFollowUpRequest
        {
            ClarificationState = firstResponse.ClarificationState!,
            UserAnswers = new[] { "Use width 100 mm, height 60 mm, and a 12 mm centered hole." }
        });

        AssertEqual(AiDrawingSpecResponseKind.DrawingSpec, secondResponse.Kind);
        Assert(secondResponse.Spec != null, "P5 UI contract requires a parsed DrawingSpec after clarification.");

        var plan = renderer.CreatePlan(secondResponse.Spec!);
        Assert(plan.Validation.IsValid, $"P5 UI contract requires a valid renderer plan: {FormatIssues(plan.Validation.Issues)}");
        AssertEqual(3, plan.Dimensions.Count);

        var renderResult = renderer.Render(
            secondResponse.Spec!,
            new RenderContext("p5-ui-contract", ModelPromptContract.LayerStandard));
        var summary = renderResult.Summary;

        Assert(renderResult.Success, $"P5 UI contract requires successful render result: {FormatIssues(renderResult.Validation.Issues)}");
        Assert(summary.Success, "P5 UI contract requires RenderResult.Summary to report success.");
        AssertEqual(RenderStatus.Success, summary.Status);
        AssertEqual(plan.Entities.Count, summary.EntityCount);
        AssertEqual(plan.Dimensions.Count, summary.DimensionCount);
        AssertEqual(renderResult.Entities.Count, summary.CadObjectCount);
        Assert(summary.Bounds != null, "P5 UI contract requires non-empty geometry bounds for preview summary.");
        Assert(summary.ObjectIdBySpecId.ContainsKey("outer-profile"), "P5 UI contract requires spec id to rendered object id mapping.");
        Assert(summary.LayerCounts.ContainsKey(CadLayerNames.Outline), "P5 UI contract requires layer counts for UI summary.");
        AssertEqual("not_requested", summary.ExportStatus);
    }

    private static void LocalAiAdapterNeverRepairsUnsafeCommandResponses()
    {
        var modelClient = new SequenceAiModelClient(
            new[] { "(command \"LINE\" \"0,0\" \"100,0\" \"\")" },
            new[] { MinimalSpecJson("""
                {
                  "id": "should-not-repair-unsafe",
                  "type": "line",
                  "layer": "OUTLINE",
                  "start": [0, 0],
                  "end": [100, 0]
                }
                """) });
        var adapter = new LocalAiDrawingSpecAdapter(modelClient);

        var response = adapter.CreateDrawingSpec(new AiDrawingSpecRequest { RequestId = "adapter-unsafe-no-repair" });

        AssertEqual(AiDrawingSpecResponseKind.Rejected, response.Kind);
        AssertEqual(1, modelClient.DrawingSpecCalls);
        AssertEqual(0, modelClient.RepairCalls);
        Assert(
            response.Issues.Any(issue => issue.Code == AiIssueCodes.UnsafeCadCommand
                && issue.Source == AiModelIssueSource.ModelResponse
                && !issue.Repairable),
            $"Unsafe command output must remain outside the repair loop: {FormatModelIssues(response.Issues)}");
    }

    private static void FixedRectangularPlateSampleMatchesP103Geometry()
    {
        var spec = RectangularPlateSample.Create();

        AssertEqual("1.0", spec.DrawingSpecVersion);
        AssertEqual("mm", spec.Units);
        AssertSequenceEqual(new[] { "OUTLINE", "CENTER", "DIM" }, spec.Layers.Select(layer => layer.Name).ToArray());

        var outline = spec.Entities.Single(entity => entity.Id == "outer-profile");
        AssertEqual(EntityTypes.Polyline, outline.Type);
        AssertEqual("OUTLINE", outline.Layer);
        Assert(outline.Closed, "Outer profile must be closed.");
        AssertEqual(4, outline.Points.Count);
        AssertPoint(0, 0, outline.Points[0]);
        AssertPoint(100, 0, outline.Points[1]);
        AssertPoint(100, 60, outline.Points[2]);
        AssertPoint(0, 60, outline.Points[3]);

        var hole = spec.Entities.Single(entity => entity.Id == "hole-1");
        AssertEqual(EntityTypes.Circle, hole.Type);
        AssertEqual("OUTLINE", hole.Layer);
        AssertPoint(30, 30, hole.Center);
        AssertEqual(6d, hole.Radius);

        var centerMark = spec.Entities.Single(entity => entity.Id == "hole-1-center");
        AssertEqual(EntityTypes.CenterMark, centerMark.Type);
        AssertEqual("CENTER", centerMark.Layer);
        AssertPoint(30, 30, centerMark.Center);
        AssertEqual(10d, centerMark.Size);
    }

    private static void DrawingSpecSchemaAcceptsValidExampleFiles()
    {
        var examplesDirectory = Path.Combine(FindRepositoryRoot(), "examples");
        var allExamplePaths = Directory
            .GetFiles(examplesDirectory, "*.example.json")
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        var validExamplePaths = Directory
            .GetFiles(examplesDirectory, "*.example.json")
            .Where(path => Path.GetFileName(path).IndexOf(".invalid.", StringComparison.OrdinalIgnoreCase) < 0)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        Assert(allExamplePaths.Length >= 6, "P2 must keep the existing example and add at least five protocol examples.");

        foreach (var examplePath in validExamplePaths)
        {
            var result = DrawingSpecValidator.ValidateSchemaJson(File.ReadAllText(examplePath));

            Assert(
                result.IsValid,
                $"Example '{Path.GetFileName(examplePath)}' must pass schema validation: {FormatIssues(result.Issues)}");
        }
    }

    private static void BasicEntitiesComboExampleValidatesAndPlansP301Entities()
    {
        var json = ReadExampleJson("basic-entities-combo.example.json");
        var schemaResult = DrawingSpecValidator.ValidateSchemaJson(json);
        Assert(
            schemaResult.IsValid,
            $"basic-entities-combo.example.json must pass schema validation: {FormatIssues(schemaResult.Issues)}");

        var spec = ReadExampleSpec("basic-entities-combo.example.json");
        var businessResult = DrawingSpecValidator.ValidateBusinessRules(spec);
        Assert(
            businessResult.IsValid,
            $"basic-entities-combo.example.json must pass business validation: {FormatIssues(businessResult.Issues)}");

        var plan = new DrawingSpecPlanRenderer().CreatePlan(spec);
        Assert(
            plan.Validation.IsValid,
            $"basic-entities-combo.example.json must produce a valid render plan: {FormatIssues(plan.Validation.Issues)}");

        var textLayer = plan.Layers.Single(layer => layer.Name == "TEXT");
        AssertEqual(2, textLayer.Color);
        AssertEqual("Continuous", textLayer.LineType);
        AssertEqual(0.18d, textLayer.LineWeight);

        foreach (var expected in P301BasicEntityKinds())
        {
            var planned = plan.Entities.Single(entity => entity.SpecEntityId == expected.Key);
            AssertEqual(expected.Value, planned.Kind);
            AssertEqual(expected.Key, planned.SourceEntityId);
        }

        var line = plan.Entities.Single(entity => entity.SpecEntityId == "baseline");
        AssertPoint(0, 0, line.Start);
        AssertPoint(90, 0, line.End);

        var polyline = plan.Entities.Single(entity => entity.SpecEntityId == "open-profile");
        Assert(!polyline.Closed, "Open profile must remain open.");
        AssertEqual(4, polyline.Points.Count);
        AssertPoint(20, 25, polyline.Points[1]);

        var circle = plan.Entities.Single(entity => entity.SpecEntityId == "reference-circle");
        AssertPoint(45, 12, circle.Center);
        AssertEqual(8d, circle.Radius);

        var arc = plan.Entities.Single(entity => entity.SpecEntityId == "relief-arc");
        AssertPoint(45, 25, arc.Center);
        AssertEqual(14d, arc.Radius);
        AssertEqual(180d, arc.StartAngle);
        AssertEqual(360d, arc.EndAngle);

        var text = plan.Entities.Single(entity => entity.SpecEntityId == "note-1");
        AssertPoint(0, 38, text.Position);
        AssertEqual("BASIC ENTITY SAMPLE", text.Value);
        AssertEqual(3.5d, text.Height);
        AssertEqual(0d, text.Rotation);

        var mtext = plan.Entities.Single(entity => entity.SpecEntityId == "note-mtext-1");
        AssertPoint(0, 44, mtext.Position);
        AssertEqual("Line, polyline, circle, arc, text, and mtext", mtext.Value);
        AssertEqual(2.5d, mtext.Height);

        AssertEqual(2, plan.Dimensions.Count);
    }

    private static void DrawingSpecSchemaRejectsInvalidExampleFiles()
    {
        var examplesDirectory = Path.Combine(FindRepositoryRoot(), "examples");
        var invalidExamplePaths = Directory
            .GetFiles(examplesDirectory, "*.invalid.example.json")
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        Assert(invalidExamplePaths.Length >= 1, "P2 must include at least one invalid DrawingSpec example.");

        foreach (var examplePath in invalidExamplePaths)
        {
            var result = DrawingSpecValidator.ValidateSchemaJson(File.ReadAllText(examplePath));

            Assert(
                !result.IsValid,
                $"Invalid example '{Path.GetFileName(examplePath)}' must fail schema validation.");
        }
    }

    private static void DrawingSpecSchemaRejectsUnsupportedEntityType()
    {
        var json = MinimalSpecJson("""
          {
            "id": "bad-entity",
            "type": "spline",
            "layer": "OUTLINE",
            "start": [0, 0],
            "end": [10, 0]
          }
        """);

        var result = DrawingSpecValidator.ValidateSchemaJson(json);

        Assert(!result.IsValid, "Unsupported entity type must fail schema validation.");
        Assert(
            result.Issues.Any(issue => issue.Code == "unsupported_entity_type" && issue.Path == "$.entities[0].type"),
            $"Unsupported entity type must report $.entities[0].type, got: {FormatIssues(result.Issues)}");
    }

    private static void DrawingSpecSchemaRejectsMissingRequiredFields()
    {
        var json = MinimalSpecJson("""
          {
            "id": "missing-layer-line",
            "type": "line",
            "start": [0, 0],
            "end": [10, 0]
          }
        """);

        var result = DrawingSpecValidator.ValidateSchemaJson(json);

        Assert(!result.IsValid, "Missing entity layer must fail schema validation.");
        Assert(
            result.Issues.Any(issue => issue.Code == "missing_required" && issue.Path == "$.entities[0].layer"),
            $"Missing layer must report $.entities[0].layer, got: {FormatIssues(result.Issues)}");
    }

    private static void DrawingSpecSchemaIssuePathsLocateFailedField()
    {
        var json = MinimalSpecJson("""
          {
            "id": "bad-point",
            "type": "circle",
            "layer": "OUTLINE",
            "center": { "x": 5, "y": 5 },
            "radius": 2
          }
        """);

        var result = DrawingSpecValidator.ValidateSchemaJson(json);

        Assert(!result.IsValid, "Object-shaped point must fail because point2d wire format is [x, y].");
        Assert(
            result.Issues.Any(issue => issue.Path == "$.entities[0].center"),
            $"Point wire-format failure must report $.entities[0].center, got: {FormatIssues(result.Issues)}");
    }

    private static void DrawingSpecSchemaRejectsObjectModelLayerColorZero()
    {
        var spec = RectangularPlateSample.Create();
        spec.Layers = new[]
        {
            new LayerSpec
            {
                Name = CadLayerNames.Outline,
                Color = 0,
                LineType = "Continuous",
                LineWeight = 0.35
            }
        };

        var result = DrawingSpecValidator.ValidateSchema(spec);

        Assert(!result.IsValid, "Layer color 0 must fail object-model schema validation to match the JSON schema.");
        Assert(
            result.Issues.Any(issue => issue.Code == "invalid_value" && issue.Path == "$.layers[0].color"),
            $"Layer color 0 must report $.layers[0].color, got: {FormatIssues(result.Issues)}");
    }

    private static void DrawingSpecBusinessRulesAcceptFixedP103Sample()
    {
        var result = DrawingSpecValidator.ValidateBusinessRules(RectangularPlateSample.Create());

        Assert(result.IsValid, $"Fixed P1-03 sample must satisfy P2 business rules: {FormatIssues(result.Issues)}");
    }

    private static void DrawingSpecBusinessRulesRejectUnsupportedLayers()
    {
        var spec = RectangularPlateSample.Create();
        spec.Layers = spec.Layers.Concat(new[]
        {
            new LayerSpec
            {
                Name = "BAD",
                Color = 6,
                LineType = "Continuous",
                LineWeight = 0.18
            }
        }).ToArray();

        var result = DrawingSpecValidator.ValidateBusinessRules(spec);

        Assert(!result.IsValid, "Layer names outside enterprise-default-v1 must fail business validation.");
        Assert(
            result.Issues.Any(issue => issue.Code == "unsupported_layer" && issue.Path == "$.layers[BAD].name"),
            $"Unsupported layer must report $.layers[BAD].name, got: {FormatIssues(result.Issues)}");
    }

    private static void CadLayerStandardsExposeP3EnterpriseDefaults()
    {
        AssertSequenceEqual(
            new[]
            {
                CadLayerNames.Outline,
                CadLayerNames.Center,
                CadLayerNames.Dimension,
                CadLayerNames.Text,
                CadLayerNames.Hidden,
                CadLayerNames.Construction,
                CadLayerNames.Title
            },
            CadLayerStandards.All.Select(layer => layer.Name).ToArray());

        AssertLayerStandard(CadLayerNames.Outline, 7, "Continuous", 0.35);
        AssertLayerStandard(CadLayerNames.Center, 1, "Center", 0.18);
        AssertLayerStandard(CadLayerNames.Dimension, 3, "Continuous", 0.18);
        AssertLayerStandard(CadLayerNames.Text, 2, "Continuous", 0.18);
        AssertLayerStandard(CadLayerNames.Hidden, 8, "Hidden", 0.18);
        AssertLayerStandard(CadLayerNames.Construction, 9, "Continuous", 0.09);
        AssertLayerStandard(CadLayerNames.Title, 4, "Continuous", 0.25);
    }

    private static void DrawingSpecBusinessRulesRejectLayerStyleDrift()
    {
        var spec = RectangularPlateSample.Create();
        var centerLayer = spec.Layers.Single(layer => layer.Name == CadLayerNames.Center);
        centerLayer.Color = 6;
        centerLayer.LineType = "Continuous";
        centerLayer.LineWeight = 0.25;

        var result = DrawingSpecValidator.ValidateBusinessRules(spec);

        Assert(!result.IsValid, "Layer style values that drift from enterprise-default-v1 must fail business validation.");
        Assert(
            result.Issues.Any(issue => issue.Code == "invalid_layer_color" && issue.Path == "$.layers[CENTER].color"),
            $"Layer color drift must report $.layers[CENTER].color, got: {FormatIssues(result.Issues)}");
        Assert(
            result.Issues.Any(issue => issue.Code == "invalid_layer_linetype" && issue.Path == "$.layers[CENTER].lineType"),
            $"Layer linetype drift must report $.layers[CENTER].lineType, got: {FormatIssues(result.Issues)}");
        Assert(
            result.Issues.Any(issue => issue.Code == "invalid_layer_lineweight" && issue.Path == "$.layers[CENTER].lineWeight"),
            $"Layer lineweight drift must report $.layers[CENTER].lineWeight, got: {FormatIssues(result.Issues)}");
    }

    private static void DrawingSpecBusinessRulesRejectOversizedCoordinates()
    {
        var spec = RectangularPlateSample.Create();
        spec.Entities = spec.Entities.Concat(new[]
        {
            new EntitySpec
            {
                Id = "unsafe-line",
                Type = EntityTypes.Line,
                Layer = CadLayerNames.Outline,
                Start = new DrawingPoint(0, 0),
                End = new DrawingPoint(100001, 0)
            }
        }).ToArray();

        var result = DrawingSpecValidator.ValidateBusinessRules(spec);

        Assert(!result.IsValid, "Coordinates outside the configured P2 boundary must fail business validation.");
        Assert(
            result.Issues.Any(issue => issue.Code == "coordinate_out_of_range" && issue.Path == "$.entities[unsafe-line].end"),
            $"Oversized coordinate must report $.entities[unsafe-line].end, got: {FormatIssues(result.Issues)}");
    }

    private static void DrawingSpecBusinessRulesRejectEntityCountOverLimit()
    {
        var spec = RectangularPlateSample.Create();
        spec.Entities = Enumerable
            .Range(0, DrawingSpecBusinessRuleLimits.DefaultMaxEntities + 1)
            .Select(index => new EntitySpec
            {
                Id = $"line-{index}",
                Type = EntityTypes.Line,
                Layer = CadLayerNames.Outline,
                Start = new DrawingPoint(index, 0),
                End = new DrawingPoint(index, 10)
            })
            .ToArray();

        var result = DrawingSpecValidator.ValidateBusinessRules(spec);

        Assert(!result.IsValid, "Entity count over the configured P2 limit must fail business validation.");
        Assert(
            result.Issues.Any(issue => issue.Code == "entity_count_exceeded" && issue.Path == "$.entities"),
            $"Entity count failures must report $.entities, got: {FormatIssues(result.Issues)}");
    }

    private static void DrawingSpecBusinessRulesRejectIncompleteAngularDimensions()
    {
        var spec = RectangularPlateSample.Create();
        spec.Dimensions = spec.Dimensions.Concat(new[]
        {
            new DimensionSpec
            {
                Id = "dim-angle-missing-geometry",
                Type = DimensionTypes.Angular,
                Layer = CadLayerNames.Dimension
            }
        }).ToArray();

        var result = DrawingSpecValidator.ValidateBusinessRules(spec);

        Assert(!result.IsValid, "Angular dimensions without center/from/to/offset must fail business validation.");
        Assert(
            result.Issues.Any(issue => issue.Code == "missing_dimension_geometry" && issue.Path == "$.dimensions[dim-angle-missing-geometry]"),
            $"Incomplete angular dimension must report $.dimensions[dim-angle-missing-geometry], got: {FormatIssues(result.Issues)}");
    }

    private static void RendererPlansP103PlateEntitiesOnStandardLayers()
    {
        var spec = RectangularPlateSample.Create();
        var renderer = new DrawingSpecPlanRenderer();

        var plan = renderer.CreatePlan(spec);

        Assert(plan.Validation.IsValid, "P1-03 sample should be valid for render planning.");
        AssertEqual(3, plan.Layers.Count);
        AssertEqual(1, plan.Entities.Count(entity => entity.Kind == PlannedEntityKind.Polyline));
        AssertEqual(1, plan.Entities.Count(entity => entity.Kind == PlannedEntityKind.Circle));
        AssertEqual(2, plan.Entities.Count(entity => entity.Kind == PlannedEntityKind.CenterLine));
        AssertEqual(3, plan.Dimensions.Count);

        var circle = plan.Entities.Single(entity => entity.SpecEntityId == "hole-1");
        AssertEqual("OUTLINE", circle.Layer);
        AssertPoint(30, 30, circle.Center);
        AssertEqual(6d, circle.Radius);

        var centerLines = plan.Entities.Where(entity => entity.SourceEntityId == "hole-1-center").ToArray();
        Assert(centerLines.All(entity => entity.Layer == "CENTER"), "Center mark lines must be on CENTER layer.");
        Assert(centerLines.Any(entity => PointsEqual(new DrawingPoint(20, 30), entity.Start) && PointsEqual(new DrawingPoint(40, 30), entity.End)),
            "Center mark must include the horizontal centerline.");
        Assert(centerLines.Any(entity => PointsEqual(new DrawingPoint(30, 20), entity.Start) && PointsEqual(new DrawingPoint(30, 40), entity.End)),
            "Center mark must include the vertical centerline.");

        Assert(plan.Dimensions.All(dimension => dimension.Layer == "DIM"), "All dimensions must be on DIM layer.");
        AssertEqual("dim-hole-dia", plan.Dimensions.Single(dimension => dimension.Type == DimensionTypes.Diameter).SpecDimensionId);
    }

    private static void RendererResolvesEnterpriseTextAndDimensionStyles()
    {
        var spec = RectangularPlateSample.Create();
        spec.Layers = StandardLayerSpecs(
            CadLayerNames.Outline,
            CadLayerNames.Center,
            CadLayerNames.Dimension,
            CadLayerNames.Text,
            CadLayerNames.Title);
        spec.Entities = spec.Entities.Concat(new[]
        {
            new EntitySpec
            {
                Id = "note-default",
                Type = EntityTypes.Text,
                Layer = CadLayerNames.Text,
                Position = new DrawingPoint(0, 72),
                Value = "GENERAL NOTE",
                Height = 3.5,
                Rotation = 15
            },
            new EntitySpec
            {
                Id = "title-main",
                Type = EntityTypes.MText,
                Layer = CadLayerNames.Title,
                Position = new DrawingPoint(0, 84),
                Value = "TITLE",
                Height = 5.0,
                Rotation = 0
            }
        }).ToArray();

        var plan = new DrawingSpecPlanRenderer().CreatePlan(spec);

        Assert(plan.Validation.IsValid, $"Styled text sample should produce a valid render plan: {FormatIssues(plan.Validation.Issues)}");

        var note = plan.Entities.Single(entity => entity.SpecEntityId == "note-default");
        AssertEqual(CadTextStyleNames.Note, note.TextStyleName);
        AssertEqual(15d, note.Rotation);
        AssertEqual(3.5d, note.Height);

        var title = plan.Entities.Single(entity => entity.SpecEntityId == "title-main");
        AssertEqual(CadTextStyleNames.TitlePrimary, title.TextStyleName);
        AssertEqual(5.0d, title.Height);

        AssertEqual(
            CadDimensionStyleNames.Mechanical,
            plan.Dimensions.Single(dimension => dimension.SpecDimensionId == "dim-width").DimensionStyleName);
        AssertEqual(
            CadDimensionStyleNames.Diameter,
            plan.Dimensions.Single(dimension => dimension.SpecDimensionId == "dim-hole-dia").DimensionStyleName);
    }

    private static void RendererMapsEveryP301BasicEntityId()
    {
        var spec = ReadExampleSpec("basic-entities-combo.example.json");
        var renderer = new DrawingSpecPlanRenderer();

        var result = renderer.Render(spec, new RenderContext(spec.Metadata.RequestId, "enterprise-default-v1"));

        Assert(result.Success, "P3-01 basic entity combo should render through the deterministic renderer stub.");
        Assert(result.Validation.IsValid, $"Render validation should be valid: {FormatIssues(result.Validation.Issues)}");

        foreach (var specEntityId in P301BasicEntityKinds().Keys)
        {
            var rendered = result.Entities.Single(entity => entity.SpecEntityId == specEntityId);
            AssertEqual($"planned:{specEntityId}", rendered.CadObjectId);
        }
    }

    private static void RendererPlansP302AlignedAndAngularDimensions()
    {
        var json = ReadExampleJson("annotation-angular-aligned.example.json");
        var schemaResult = DrawingSpecValidator.ValidateSchemaJson(json);
        Assert(
            schemaResult.IsValid,
            $"annotation-angular-aligned.example.json must pass schema validation: {FormatIssues(schemaResult.Issues)}");

        var spec = ReadExampleSpec("annotation-angular-aligned.example.json");
        var businessResult = DrawingSpecValidator.ValidateBusinessRules(spec);
        Assert(
            businessResult.IsValid,
            $"annotation-angular-aligned.example.json must pass business validation: {FormatIssues(businessResult.Issues)}");

        var plan = new DrawingSpecPlanRenderer().CreatePlan(spec);
        Assert(
            plan.Validation.IsValid,
            $"annotation-angular-aligned.example.json must produce a valid render plan: {FormatIssues(plan.Validation.Issues)}");

        AssertEqual(2, plan.Dimensions.Count);

        var aligned = plan.Dimensions.Single(dimension => dimension.SpecDimensionId == "dim-angled-edge");
        AssertEqual(DimensionTypes.Aligned, aligned.Type);
        AssertEqual(CadLayerNames.Dimension, aligned.Layer);
        AssertPoint(0, 0, aligned.From);
        AssertPoint(40, 30, aligned.To);
        AssertPoint(8, 8, aligned.Offset);
        AssertEqual("50", aligned.Text);

        var angular = plan.Dimensions.Single(dimension => dimension.SpecDimensionId == "dim-angle-between-edges");
        AssertEqual(DimensionTypes.Angular, angular.Type);
        AssertEqual(CadLayerNames.Dimension, angular.Layer);
        AssertPoint(0, 0, angular.Center);
        AssertPoint(50, 0, angular.From);
        AssertPoint(40, 30, angular.To);
        AssertPoint(18, 14, angular.Offset);
        AssertEqual("36.9%%d", angular.Text);

        var result = new DrawingSpecPlanRenderer().Render(spec, new RenderContext(spec.Metadata.RequestId, "enterprise-default-v1"));
        Assert(result.Success, "P3-02 annotation example should render through the deterministic renderer stub.");
        Assert(
            result.Entities.Any(entity => entity.SpecEntityId == "dim-angled-edge")
                && result.Entities.Any(entity => entity.SpecEntityId == "dim-angle-between-edges"),
            "P3-02 renderer result must preserve aligned and angular dimension ids.");
    }

    private static void RendererPlansP302HoleArrayCenterlines()
    {
        var spec = ReadExampleSpec("hole-array-centerlines.example.json");
        var plan = new DrawingSpecPlanRenderer().CreatePlan(spec);

        Assert(
            plan.Validation.IsValid,
            $"hole-array-centerlines.example.json must produce a valid render plan: {FormatIssues(plan.Validation.Issues)}");

        AssertEqual(3, plan.Entities.Count(entity => entity.Kind == PlannedEntityKind.Circle));
        AssertEqual(7, plan.Entities.Count(entity => entity.Kind == PlannedEntityKind.CenterLine));

        var explicitArrayCenterline = plan.Entities.Single(entity => entity.SpecEntityId == "array-centerline");
        AssertEqual(PlannedEntityKind.CenterLine, explicitArrayCenterline.Kind);
        AssertEqual("array-centerline", explicitArrayCenterline.SourceEntityId);
        AssertEqual(CadLayerNames.Center, explicitArrayCenterline.Layer);
        AssertPoint(20, 35, explicitArrayCenterline.Start);
        AssertPoint(130, 35, explicitArrayCenterline.End);
        AssertEqual(110d, CenterLineLength(explicitArrayCenterline));

        foreach (var holeCenterId in new[] { "hole-1-center", "hole-2-center", "hole-3-center" })
        {
            var centerLines = plan.Entities
                .Where(entity => entity.SourceEntityId == holeCenterId)
                .OrderBy(entity => entity.SpecEntityId, StringComparer.Ordinal)
                .ToArray();

            AssertEqual(2, centerLines.Length);
            Assert(centerLines.All(entity => entity.Layer == CadLayerNames.Center), $"{holeCenterId} centerlines must stay on CENTER.");
            Assert(centerLines.Any(entity => entity.SpecEntityId == $"{holeCenterId}-horizontal"), $"{holeCenterId} must map a horizontal derived id.");
            Assert(centerLines.Any(entity => entity.SpecEntityId == $"{holeCenterId}-vertical"), $"{holeCenterId} must map a vertical derived id.");
            Assert(
                centerLines.All(entity => CenterLineLength(entity) == 22d),
                $"{holeCenterId} centerlines must expand size 11 to total length 22.");
        }

        AssertEqual(3, plan.Dimensions.Count);

        var pitch1 = plan.Dimensions.Single(dimension => dimension.SpecDimensionId == "dim-array-pitch-1");
        AssertEqual(DimensionTypes.Linear, pitch1.Type);
        AssertPoint(35, 35, pitch1.From);
        AssertPoint(75, 35, pitch1.To);
        AssertPoint(0, -18, pitch1.Offset);
        AssertEqual("40", pitch1.Text);

        var pitch2 = plan.Dimensions.Single(dimension => dimension.SpecDimensionId == "dim-array-pitch-2");
        AssertEqual(DimensionTypes.Linear, pitch2.Type);
        AssertPoint(75, 35, pitch2.From);
        AssertPoint(115, 35, pitch2.To);
        AssertPoint(0, -18, pitch2.Offset);
        AssertEqual("40", pitch2.Text);

        var diameter = plan.Dimensions.Single(dimension => dimension.Type == DimensionTypes.Diameter);
        AssertEqual("dim-hole-dia", diameter.SpecDimensionId);
        AssertEqual("hole-1", diameter.TargetEntityId);
        AssertEqual("3X %%c12", diameter.Text);

        var result = new DrawingSpecPlanRenderer().Render(spec, new RenderContext(spec.Metadata.RequestId, "enterprise-default-v1"));
        Assert(result.Success, "P3-02 hole array example should render through the deterministic renderer stub.");
        Assert(
            result.Entities.Any(entity => entity.SpecEntityId == "hole-1-center-horizontal")
                && result.Entities.Any(entity => entity.SpecEntityId == "hole-3-center-vertical")
                && result.Entities.Any(entity => entity.SpecEntityId == "array-centerline")
                && result.Entities.Any(entity => entity.SpecEntityId == "dim-hole-dia"),
            "P3-02 renderer result must preserve centerline and dimension ids.");
    }

    private static void RendererGeometrySummaryCapturesCountsLayersBoundsAndMapping()
    {
        var spec = RectangularPlateSample.Create();
        var result = new DrawingSpecPlanRenderer().Render(
            spec,
            new RenderContext(spec.Metadata.RequestId, "enterprise-default-v1"));

        Assert(result.Success, $"P1-03 sample should render for summary verification: {FormatIssues(result.Validation.Issues)}");

        var summary = result.Summary;
        AssertEqual(RenderStatus.Success, summary.Status);
        Assert(summary.Success, "Successful render summary must report Success.");
        AssertEqual(4, summary.EntityCount);
        AssertEqual(3, summary.DimensionCount);
        AssertEqual(7, summary.CadObjectCount);
        AssertEqual(1, summary.TypeCounts[PlannedEntityKind.Polyline.ToString()]);
        AssertEqual(1, summary.TypeCounts[PlannedEntityKind.Circle.ToString()]);
        AssertEqual(2, summary.TypeCounts[PlannedEntityKind.CenterLine.ToString()]);
        AssertEqual(2, summary.TypeCounts[DimensionTypes.Linear]);
        AssertEqual(1, summary.TypeCounts[DimensionTypes.Diameter]);
        AssertEqual(2, summary.LayerCounts[CadLayerNames.Outline]);
        AssertEqual(2, summary.LayerCounts[CadLayerNames.Center]);
        AssertEqual(3, summary.LayerCounts[CadLayerNames.Dimension]);
        AssertEqual("planned:outer-profile", summary.ObjectIdBySpecId["outer-profile"]);
        AssertEqual("planned:dim-hole-dia", summary.ObjectIdBySpecId["dim-hole-dia"]);
        AssertEqual("not_requested", summary.ExportStatus);
        AssertEqual(string.Empty, summary.OutputPath);
        Assert(summary.Bounds != null, "Successful summary must include a bounding box.");
        AssertEqual(0d, summary.Bounds!.MinX);
        AssertEqual(-12d, summary.Bounds.MinY);
        AssertEqual(112d, summary.Bounds.MaxX);
        AssertEqual(60d, summary.Bounds.MaxY);
    }

    private static void RendererDimensionFailuresLocateStableDimensionIds()
    {
        var spec = ReadExampleSpec("annotation-angular-aligned.example.json");
        spec.Dimensions.Single(dimension => dimension.Id == "dim-angle-between-edges").Offset = null;
        var renderer = new DrawingSpecPlanRenderer();

        var plan = renderer.CreatePlan(spec);

        Assert(!plan.Validation.IsValid, "Invalid angular dimension geometry must fail render planning.");
        Assert(
            plan.Validation.Issues.Any(issue =>
                issue.Code == "missing_dimension_geometry" && issue.Path == "$.dimensions[dim-angle-between-edges]"),
            $"Angular dimension failure must locate dim-angle-between-edges, got: {FormatIssues(plan.Validation.Issues)}");
    }

    private static void RendererFailuresLocateStableEntityIds()
    {
        var spec = ReadExampleSpec("basic-entities-combo.example.json");
        spec.Entities.Single(entity => entity.Id == "relief-arc").Radius = 0;
        var renderer = new DrawingSpecPlanRenderer();

        var plan = renderer.CreatePlan(spec);

        Assert(!plan.Validation.IsValid, "Invalid arc geometry must fail render planning.");
        Assert(
            plan.Validation.Issues.Any(issue =>
                issue.Code == "invalid_arc_geometry" && issue.Path == "$.entities[relief-arc]"),
            $"Arc render planning failure must locate relief-arc, got: {FormatIssues(plan.Validation.Issues)}");
    }

    private static void RendererRejectsSpecsMissingProductionLayers()
    {
        var spec = new DrawingSpec
        {
            DrawingSpecVersion = "1.0",
            Units = "mm",
            Metadata = new DrawingMetadata
            {
                Domain = DrawingDomain.MechanicalPlate,
                CreatedBy = "test",
                RequestId = "renderer-missing-production-layers"
            },
            Layers = new[]
            {
                new LayerSpec
                {
                    Name = CadLayerNames.Outline,
                    Color = 7,
                    LineType = "Continuous",
                    LineWeight = 0.35
                }
            },
            Entities = new[]
            {
                new EntitySpec
                {
                    Id = "line-1",
                    Type = EntityTypes.Line,
                    Layer = CadLayerNames.Outline,
                    Start = new DrawingPoint(0, 0),
                    End = new DrawingPoint(10, 0)
                }
            },
            Dimensions = Array.Empty<DimensionSpec>(),
            Clarifications = Array.Empty<string>()
        };

        var renderer = new DrawingSpecPlanRenderer();
        var plan = renderer.CreatePlan(spec);

        Assert(!plan.Validation.IsValid, "Renderer must enforce production DrawingSpec business validation before planning.");
        Assert(
            plan.Validation.Issues.Any(issue => issue.Code == "missing_required_layer" && issue.Path == "$.layers[CENTER]"),
            $"Renderer should surface missing CENTER layer from business validation, got: {FormatIssues(plan.Validation.Issues)}");
    }

    private static void RendererResultPreservesMapping()
    {
        var result = new RenderResult(
            success: true,
            entities: new[] { new RenderedEntity("hole-1", "cad-object-1") },
            validation: ValidationResult.Success());

        Assert(result.Success, "Render result should be successful.");
        Assert(result.Validation.IsValid, "Validation should be valid.");
        AssertEqual("hole-1", result.Entities.Single().SpecEntityId);
        AssertEqual("cad-object-1", result.Entities.Single().CadObjectId);
        AssertEqual(RenderStatus.Success, result.Summary.Status);
        AssertEqual(1, result.Summary.CadObjectCount);
        AssertEqual("cad-object-1", result.Summary.ObjectIdBySpecId["hole-1"]);
    }

    private static void WriterTransactionBoundaryCommitsSuccessfulWritesOnce()
    {
        var plan = new DrawingSpecPlanRenderer().CreatePlan(RectangularPlateSample.Create());
        FakeWriterTransactionScope? scope = null;
        var boundary = new WriterTransactionBoundary(() =>
        {
            scope = new FakeWriterTransactionScope();
            return scope;
        });

        var result = boundary.Execute(plan, WriterRenderOptions.Default, SimulatePlanWrites);

        Assert(result.Success, $"Successful fake writer pass should commit: {FormatIssues(result.Validation.Issues)}");
        Assert(scope != null, "Transaction scope must be created for a valid plan.");
        Assert(scope!.CommitCalled, "Successful writer pass must call Commit exactly once.");
        Assert(scope.Disposed, "Transaction scope must be disposed after render.");
        AssertEqual(plan.Entities.Count + plan.Dimensions.Count, result.Entities.Count);
        AssertEqual(result.Entities.Count, scope.CommittedEntities.Count);
        AssertEqual(RenderStatus.Success, result.Summary.Status);
        AssertEqual(plan.Entities.Count, result.Summary.EntityCount);
        AssertEqual(plan.Dimensions.Count, result.Summary.DimensionCount);
        AssertEqual(result.Entities.Count, result.Summary.CadObjectCount);
    }

    private static void WriterTransactionBoundaryRollsBackInjectedEntityFailures()
    {
        var plan = new DrawingSpecPlanRenderer().CreatePlan(RectangularPlateSample.Create());
        FakeWriterTransactionScope? scope = null;
        var boundary = new WriterTransactionBoundary(() =>
        {
            scope = new FakeWriterTransactionScope();
            return scope;
        });

        var result = boundary.Execute(
            plan,
            new WriterRenderOptions(failureInjector: WriterFailureInjection.AfterEntity("hole-1")),
            SimulatePlanWrites);

        Assert(!result.Success, "Injected entity failure must fail the render result.");
        Assert(!result.Canceled, "Injected entity failure is an error, not a cancellation.");
        Assert(scope != null, "Transaction scope must be created before mid-render entity failure.");
        Assert(!scope!.CommitCalled, "Injected entity failure must not commit the transaction.");
        Assert(scope.Disposed, "Failed transaction scope must still be disposed.");
        AssertEqual(0, scope.CommittedEntities.Count);
        AssertEqual(0, result.Entities.Count);
        AssertEqual(RenderStatus.Failed, result.Summary.Status);
        AssertEqual(0, result.Summary.EntityCount);
        AssertEqual(0, result.Summary.DimensionCount);
        AssertEqual(0, result.Summary.ObjectIdBySpecId.Count);
        Assert(
            result.Validation.Issues.Any(issue =>
                issue.Code == "injected_writer_failure" && issue.Path == "$.entities[hole-1]"),
            $"Injected entity failure must report $.entities[hole-1], got: {FormatIssues(result.Validation.Issues)}");
    }

    private static void WriterTransactionBoundaryRollsBackInjectedDimensionFailures()
    {
        var plan = new DrawingSpecPlanRenderer().CreatePlan(RectangularPlateSample.Create());
        FakeWriterTransactionScope? scope = null;
        var boundary = new WriterTransactionBoundary(() =>
        {
            scope = new FakeWriterTransactionScope();
            return scope;
        });

        var result = boundary.Execute(
            plan,
            new WriterRenderOptions(failureInjector: WriterFailureInjection.AfterDimension("dim-hole-dia")),
            SimulatePlanWrites);

        Assert(!result.Success, "Injected dimension failure must fail the render result.");
        Assert(scope != null, "Transaction scope must be created before mid-render dimension failure.");
        Assert(!scope!.CommitCalled, "Injected dimension failure must not commit the transaction.");
        Assert(scope.Disposed, "Failed transaction scope must still be disposed.");
        AssertEqual(0, scope.CommittedEntities.Count);
        AssertEqual(0, result.Entities.Count);
        AssertEqual(RenderStatus.Failed, result.Summary.Status);
        AssertEqual(0, result.Summary.EntityCount);
        AssertEqual(0, result.Summary.DimensionCount);
        AssertEqual(0, result.Summary.ObjectIdBySpecId.Count);
        Assert(
            result.Validation.Issues.Any(issue =>
                issue.Code == "injected_writer_failure" && issue.Path == "$.dimensions[dim-hole-dia]"),
            $"Injected dimension failure must report $.dimensions[dim-hole-dia], got: {FormatIssues(result.Validation.Issues)}");
    }

    private static void WriterTransactionBoundaryTreatsCancellationAsNonCommittedRender()
    {
        var plan = new DrawingSpecPlanRenderer().CreatePlan(RectangularPlateSample.Create());
        using var cancellation = new CancellationTokenSource();
        FakeWriterTransactionScope? scope = null;
        var boundary = new WriterTransactionBoundary(() =>
        {
            scope = new FakeWriterTransactionScope();
            return scope;
        });

        var result = boundary.Execute(
            plan,
            new WriterRenderOptions(cancellation.Token),
            (transaction, context) =>
            {
                var fakeTransaction = (FakeWriterTransactionScope)transaction;
                var renderedEntity = new RenderedEntity(plan.Entities[0].SpecEntityId, $"fake:{plan.Entities[0].SpecEntityId}");
                fakeTransaction.Append(renderedEntity);
                cancellation.Cancel();
                context.AfterEntityAppended(plan.Entities[0], renderedEntity);
                return new[] { renderedEntity };
            });

        Assert(result.Canceled, "Cancellation must produce a canceled render result.");
        Assert(!result.Success, "Cancellation must not be reported as success.");
        Assert(scope != null, "Transaction scope must be created before mid-render cancellation.");
        Assert(!scope!.CommitCalled, "Cancellation must not commit the transaction.");
        Assert(scope.Disposed, "Canceled transaction scope must still be disposed.");
        AssertEqual(0, scope.CommittedEntities.Count);
        AssertEqual(0, result.Entities.Count);
        AssertEqual(RenderStatus.Canceled, result.Summary.Status);
        Assert(result.Summary.Canceled, "Canceled summary must report Canceled.");
        AssertEqual(0, result.Summary.EntityCount);
        AssertEqual(0, result.Summary.DimensionCount);
        AssertEqual(0, result.Summary.ObjectIdBySpecId.Count);
        Assert(
            result.Validation.Issues.Any(issue =>
                issue.Code == "render_canceled" && issue.Path == "$"),
            $"Cancellation must report render_canceled at $, got: {FormatIssues(result.Validation.Issues)}");
    }

    private static void ProjectReferencesFollowArchitectureBoundaries()
    {
        var root = FindRepositoryRoot();

        var coreRefs = ReadProjectReferences(Path.Combine(root, "src", "ZwcadAi.Core", "ZwcadAi.Core.csproj"));
        var rendererRefs = ReadProjectReferences(Path.Combine(root, "src", "ZwcadAi.Renderer", "ZwcadAi.Renderer.csproj"));
        var aiRefs = ReadProjectReferences(Path.Combine(root, "src", "ZwcadAi.AiService", "ZwcadAi.AiService.csproj"));
        var pluginRefs = ReadProjectReferences(Path.Combine(root, "src", "ZwcadAiPlugin", "ZwcadAiPlugin.csproj"));

        AssertEqual(0, coreRefs.Count);
        AssertSequenceEqual(new[] { "ZwcadAi.Core.csproj" }, rendererRefs);
        AssertSequenceEqual(new[] { "ZwcadAi.Core.csproj" }, aiRefs);
        AssertSequenceEqual(
            new[] { "ZwcadAi.Core.csproj", "ZwcadAi.Renderer.csproj", "ZwcadAi.AiService.csproj" },
            pluginRefs);
    }

    private static void CoreProjectHasNoZwcadRuntimeReferences()
    {
        var root = FindRepositoryRoot();
        var projectPath = Path.Combine(root, "src", "ZwcadAi.Core", "ZwcadAi.Core.csproj");
        var document = XDocument.Load(projectPath);

        var references = document
            .Descendants("Reference")
            .Select(reference => (string?)reference.Attribute("Include") ?? string.Empty)
            .ToArray();

        var forbiddenReference = references.FirstOrDefault(IsZwcadRuntimeReference);
        Assert(
            forbiddenReference == null,
            $"Core project must not reference ZWCAD runtime assemblies, found '{forbiddenReference}'.");
    }

    private static void PluginReferencesZwcad2025ManagedAssemblies()
    {
        var root = FindRepositoryRoot();
        var projectPath = Path.Combine(root, "src", "ZwcadAiPlugin", "ZwcadAiPlugin.csproj");
        var document = XDocument.Load(projectPath);

        var references = document
            .Descendants("Reference")
            .ToDictionary(
                reference => ((string?)reference.Attribute("Include") ?? string.Empty).Split(',')[0],
                StringComparer.OrdinalIgnoreCase);

        Assert(references.ContainsKey("ZwManaged"), "Plugin must reference ZwManaged.dll.");
        Assert(references.ContainsKey("ZwDatabaseMgd"), "Plugin must reference ZwDatabaseMgd.dll.");

        AssertZwcadReference(references["ZwManaged"], "ZwManaged.dll");
        AssertZwcadReference(references["ZwDatabaseMgd"], "ZwDatabaseMgd.dll");
    }

    private static void PluginRegistersAiDrawCommand()
    {
        var source = ReadPluginSource();

        Assert(
            source.Contains("IExtensionApplication", StringComparison.Ordinal),
            "Plugin must expose a ZWCAD extension application entry point.");
        Assert(
            source.Contains("CommandMethod(PluginCommandCatalog.AiDraw", StringComparison.Ordinal),
            "Plugin must register AIDRAW through CommandMethod.");
        Assert(
            source.Contains("readyForCadLoad: true", StringComparison.Ordinal),
            "Plugin runtime status must report CAD load readiness.");
    }

    private static void PluginAiDrawUsesFixedPocSampleAndTransactionWriter()
    {
        var source = ReadPluginSource();

        Assert(
            source.Contains("RectangularPlateSample.Create()", StringComparison.Ordinal),
            "AIDRAW must load the fixed P1-03 rectangular plate sample before AI integration exists.");
        Assert(
            source.Contains("ZwcadDrawingWriter", StringComparison.Ordinal),
            "AIDRAW must use the ZWCAD transaction writer for the POC render.");
        Assert(
            source.Contains("StartTransaction()", StringComparison.Ordinal),
            "P1-03 CAD writes must be wrapped in a transaction for rollback on failure.");
    }

    private static void PluginRegistersAiExportCommand()
    {
        var source = ReadPluginSource();

        Assert(
            source.Contains("CommandMethod(PluginCommandCatalog.AiExport", StringComparison.Ordinal),
            "Plugin must register AIEXPORT through CommandMethod.");
        Assert(
            source.Contains("ExportActiveDocument()", StringComparison.Ordinal),
            "AIEXPORT must call the export service entry point.");
    }

    private static void PluginAiExportSavesDwgCopyWithoutSavingActiveDrawing()
    {
        var source = ReadPluginSource();

        Assert(
            source.Contains("SaveDwgCopy(", StringComparison.Ordinal),
            "AIEXPORT must have an explicit DWG copy save step.");
        Assert(
            source.Contains("database.Wblock()", StringComparison.Ordinal),
            "AIEXPORT must create an independent DWG database copy before saving.");
        Assert(
            source.Contains("copy.SaveAs(dwgPath, DwgVersion.Current)", StringComparison.Ordinal),
            "AIEXPORT must save the copied database to a DWG output path using Database.SaveAs.");
        Assert(
            !source.Contains(".Save()", StringComparison.Ordinal),
            "AIEXPORT must not call Database.Save because the POC must not save over the active drawing.");
        Assert(
            source.Contains("AIEXPORT DWG copy:", StringComparison.Ordinal),
            "AIEXPORT must log the DWG copy output path.");
    }

    private static void PluginAiExportCoversPdfPlotToFilePath()
    {
        var source = ReadPluginSource();

        Assert(
            source.Contains("ZwSoft.ZwCAD.PlottingServices", StringComparison.Ordinal),
            "AIEXPORT must use the ZWCAD plotting services for the PDF export path.");
        Assert(
            source.Contains("PlotFactory.CreatePublishEngine()", StringComparison.Ordinal),
            "AIEXPORT must create a publish plot engine for PDF output.");
        Assert(
            source.Contains("BeginDocument(plotInfo, document.Name, null, 1, true, pdfPath)", StringComparison.Ordinal),
            "AIEXPORT must plot to a PDF file path instead of only plotting to a device.");
        Assert(
            source.Contains("AIEXPORT PDF export unavailable", StringComparison.Ordinal),
            "AIEXPORT must clearly log when the current ZWCAD environment cannot export PDF.");
    }

    private static void PluginWriterUsesSharedTransactionBoundary()
    {
        var source = ReadPluginSource();

        Assert(
            source.Contains("WriterTransactionBoundary", StringComparison.Ordinal),
            "ZWCAD writer must delegate transaction commit/rollback policy to the shared writer transaction boundary.");
        Assert(
            source.Contains("ZwcadWriterTransactionScope", StringComparison.Ordinal),
            "ZWCAD writer must isolate DocumentLock and Transaction acquisition in an explicit transaction scope.");
        Assert(
            source.Contains("document.LockDocument()", StringComparison.Ordinal)
                && source.Contains("Database.TransactionManager.StartTransaction()", StringComparison.Ordinal)
                && source.Contains("Transaction.Commit()", StringComparison.Ordinal),
            "ZWCAD transaction scope must hold DocumentLock + Transaction and commit only through the scope.");
        Assert(
            source.Contains("AppendPlanToModelSpace", StringComparison.Ordinal)
                && source.Contains("context.AfterEntityAppended", StringComparison.Ordinal)
                && source.Contains("context.AfterDimensionAppended", StringComparison.Ordinal),
            "ZWCAD writer must keep all entity and dimension appends inside the shared transaction context.");
    }

    private static void PluginWriterFailuresLocateStableEntityAndDimensionIds()
    {
        var source = ReadPluginSource();

        Assert(
            source.Contains("CreateEntityFailure", StringComparison.Ordinal)
                && source.Contains("$.entities[", StringComparison.Ordinal)
                && source.Contains("plannedEntity.SpecEntityId", StringComparison.Ordinal),
            "Entity writer failures must locate stable DrawingSpec entity ids.");
        Assert(
            source.Contains("CreateDimensionFailure", StringComparison.Ordinal)
                && source.Contains("$.dimensions[", StringComparison.Ordinal)
                && source.Contains("plannedDimension.SpecDimensionId", StringComparison.Ordinal),
            "Dimension writer failures must locate stable DrawingSpec dimension ids.");
        Assert(
            source.Contains("render failed and was rolled back", StringComparison.Ordinal),
            "AIDRAW must report writer failures as rolled-back render results.");
    }

    private static void PluginWriterSupportsP301BasicEntityDispatch()
    {
        var source = ReadPluginSource();

        Assert(
            source.Contains("EntityFactories", StringComparison.Ordinal),
            "ZWCAD writer should use an explicit entity dispatch table instead of keeping P1 switch growth.");
        Assert(
            source.Contains("[PlannedEntityKind.Arc] = CreateArc", StringComparison.Ordinal),
            "ZWCAD writer must dispatch P3-01 arc entities.");
        Assert(
            source.Contains("[PlannedEntityKind.Text] = CreateText", StringComparison.Ordinal),
            "ZWCAD writer must dispatch P3-01 text entities.");
        Assert(
            source.Contains("[PlannedEntityKind.MText] = CreateMText", StringComparison.Ordinal),
            "ZWCAD writer must dispatch P3-01 mtext entities.");
        Assert(
            source.Contains("new Arc(", StringComparison.Ordinal)
                && source.Contains("ToRadians(plannedEntity.StartAngle)", StringComparison.Ordinal)
                && source.Contains("ToRadians(plannedEntity.EndAngle)", StringComparison.Ordinal),
            "Arc writer must create a CAD Arc and convert DrawingSpec degrees to CAD radians.");
        Assert(
            source.Contains("new DBText", StringComparison.Ordinal)
                && source.Contains("TextString = plannedEntity.Value", StringComparison.Ordinal),
            "Text writer must create a DBText from the planned text value.");
        Assert(
            source.Contains("new MText", StringComparison.Ordinal)
                && source.Contains("Contents = plannedEntity.Value", StringComparison.Ordinal),
            "MText writer must create an MText from the planned text value.");
        Assert(
            source.Contains("new RenderedEntity(plannedEntity.SpecEntityId, objectId.ToString())", StringComparison.Ordinal),
            "ZWCAD writer must preserve spec entity id to CAD object id mapping.");
        Assert(
            source.Contains("case DimensionTypes.Radius", StringComparison.Ordinal)
                && source.Contains("CreateRadiusDimension", StringComparison.Ordinal),
            "ZWCAD writer must support the radius dimension already present in basic-entities-combo.example.json.");
    }

    private static void PluginWriterSupportsP302DimensionsAndCenterMarks()
    {
        var source = ReadPluginSource();

        Assert(
            source.Contains("case DimensionTypes.Aligned", StringComparison.Ordinal)
                && source.Contains("CreateAlignedDimension", StringComparison.Ordinal),
            "ZWCAD writer must dispatch P3-02 aligned dimensions.");
        Assert(
            source.Contains("case DimensionTypes.Angular", StringComparison.Ordinal)
                && source.Contains("CreateAngularDimension", StringComparison.Ordinal),
            "ZWCAD writer must dispatch P3-02 angular dimensions.");
        Assert(
            source.Contains("new AlignedDimension(", StringComparison.Ordinal),
            "Aligned dimension writer must create a CAD AlignedDimension.");
        Assert(
            source.Contains("new Point3AngularDimension(", StringComparison.Ordinal),
            "Angular dimension writer must create a CAD Point3AngularDimension.");
        Assert(
            source.Contains("CreateDimensionFailure", StringComparison.Ordinal)
                && source.Contains("$.dimensions[", StringComparison.Ordinal)
                && source.Contains("plannedDimension.SpecDimensionId", StringComparison.Ordinal),
            "Dimension writer failures must locate stable DrawingSpec dimension ids.");
        Assert(
            source.Contains("[PlannedEntityKind.CenterLine] = CreateLine", StringComparison.Ordinal),
            "ZWCAD writer must render center mark expansions and explicit centerlines through line entities.");
    }

    private static void PluginWriterAppliesEnterpriseLayerAndStyleStandards()
    {
        var source = ReadPluginSource();

        Assert(
            source.Contains("ApplyLayerStandard", StringComparison.Ordinal)
                && source.Contains("OpenMode.ForWrite", StringComparison.Ordinal),
            "ZWCAD writer must reuse existing managed layers and update them to the enterprise standard.");
        var loadLinetypeIndex = source.IndexOf(
            "TryLoadStandardLinetype(database, linetypeTable, layer.LineType)",
            StringComparison.Ordinal);
        var missingLinetypeIndex = source.IndexOf("\"missing_linetype\"", StringComparison.Ordinal);
        Assert(
            loadLinetypeIndex >= 0
                && source.Contains("LoadLineTypeFile", StringComparison.Ordinal)
                && source.Contains("zwcad.lin", StringComparison.Ordinal)
                && source.Contains("zwcadiso.lin", StringComparison.Ordinal)
                && missingLinetypeIndex > loadLinetypeIndex
                && source.Contains("$.layers[", StringComparison.Ordinal)
                && source.Contains(".lineType", StringComparison.Ordinal),
            "ZWCAD writer must load standard linetypes before reporting missing_linetype for unavailable custom linetypes.");
        Assert(
            source.Contains("IsPlottable = false", StringComparison.Ordinal)
                || source.Contains("IsPlottable = !string.Equals(layer.Name, CadLayerNames.Construction", StringComparison.Ordinal),
            "CONSTRUCTION layer must be marked non-plot by default.");
        Assert(
            source.Contains("TextStyleTable", StringComparison.Ordinal)
                && source.Contains("CadTextStyleStandards.Definitions", StringComparison.Ordinal)
                && source.Contains("TextStyleId", StringComparison.Ordinal),
            "DBText and MText must use deterministic enterprise text styles.");
        Assert(
            source.Contains("DimStyleTable", StringComparison.Ordinal)
                && source.Contains("CadDimensionStyleStandards.Definitions", StringComparison.Ordinal)
                && source.Contains("DimensionStyleName", StringComparison.Ordinal),
            "Dimensions must use deterministic enterprise dimension styles with a configurable helper path.");
    }

    private static IReadOnlyList<RenderedEntity> SimulatePlanWrites(
        IWriterTransactionScope transaction,
        WriterTransactionContext context)
    {
        var fakeTransaction = (FakeWriterTransactionScope)transaction;
        var renderedEntities = new List<RenderedEntity>();

        foreach (var plannedEntity in context.Plan.Entities)
        {
            var renderedEntity = new RenderedEntity(
                plannedEntity.SpecEntityId,
                $"fake:{plannedEntity.SpecEntityId}");
            fakeTransaction.Append(renderedEntity);
            renderedEntities.Add(renderedEntity);
            context.AfterEntityAppended(plannedEntity, renderedEntity);
        }

        foreach (var plannedDimension in context.Plan.Dimensions)
        {
            var renderedEntity = new RenderedEntity(
                plannedDimension.SpecDimensionId,
                $"fake:{plannedDimension.SpecDimensionId}");
            fakeTransaction.Append(renderedEntity);
            renderedEntities.Add(renderedEntity);
            context.AfterDimensionAppended(plannedDimension, renderedEntity);
        }

        return renderedEntities;
    }

    private sealed class FakeWriterTransactionScope : IWriterTransactionScope
    {
        private readonly List<RenderedEntity> _pendingEntities = new List<RenderedEntity>();
        private readonly List<RenderedEntity> _committedEntities = new List<RenderedEntity>();

        public bool CommitCalled { get; private set; }

        public bool Disposed { get; private set; }

        public IReadOnlyList<RenderedEntity> CommittedEntities => _committedEntities;

        public void Append(RenderedEntity renderedEntity)
        {
            Assert(!Disposed, "Fake transaction must not accept appends after disposal.");
            _pendingEntities.Add(renderedEntity);
        }

        public void Commit()
        {
            Assert(!Disposed, "Fake transaction must not commit after disposal.");
            CommitCalled = true;
            _committedEntities.AddRange(_pendingEntities);
            _pendingEntities.Clear();
        }

        public void Dispose()
        {
            if (!CommitCalled)
            {
                _pendingEntities.Clear();
            }

            Disposed = true;
        }
    }

    private static IReadOnlyDictionary<string, PlannedEntityKind> P301BasicEntityKinds()
    {
        return new Dictionary<string, PlannedEntityKind>(StringComparer.Ordinal)
        {
            ["baseline"] = PlannedEntityKind.Line,
            ["open-profile"] = PlannedEntityKind.Polyline,
            ["reference-circle"] = PlannedEntityKind.Circle,
            ["relief-arc"] = PlannedEntityKind.Arc,
            ["note-1"] = PlannedEntityKind.Text,
            ["note-mtext-1"] = PlannedEntityKind.MText
        };
    }

    private static IReadOnlyList<LayerSpec> StandardLayerSpecs(params string[] layerNames)
    {
        return layerNames
            .Select(name => CadLayerStandards.Definitions[name].ToLayerSpec())
            .ToArray();
    }

    private static void AssertLayerStandard(string name, int color, string lineType, double lineWeight)
    {
        Assert(CadLayerStandards.TryGet(name, out var standard), $"Layer standard '{name}' must exist.");
        AssertEqual(color, standard.Color);
        AssertEqual(lineType, standard.LineType);
        AssertEqual(lineWeight, standard.LineWeight);
    }

    private static string ReadExampleJson(string fileName)
    {
        return File.ReadAllText(Path.Combine(FindRepositoryRoot(), "examples", fileName));
    }

    private static DrawingSpec ReadExampleSpec(string fileName)
    {
        using var document = JsonDocument.Parse(ReadExampleJson(fileName));
        var root = document.RootElement;

        return new DrawingSpec
        {
            DrawingSpecVersion = ReadString(root, "drawingSpecVersion"),
            Units = ReadString(root, "units"),
            Metadata = root.TryGetProperty("metadata", out var metadata)
                ? ReadMetadata(metadata)
                : new DrawingMetadata(),
            Layers = ReadLayers(root),
            Entities = ReadEntities(root),
            Dimensions = ReadDimensions(root),
            Clarifications = ReadStringArray(root, "clarifications")
        };
    }

    private static DrawingMetadata ReadMetadata(JsonElement metadata)
    {
        return new DrawingMetadata
        {
            Title = ReadString(metadata, "title"),
            Domain = ReadString(metadata, "domain"),
            Author = ReadString(metadata, "author"),
            CreatedBy = ReadString(metadata, "createdBy"),
            RequestId = ReadString(metadata, "requestId")
        };
    }

    private static IReadOnlyList<LayerSpec> ReadLayers(JsonElement root)
    {
        if (!root.TryGetProperty("layers", out var layers))
        {
            return Array.Empty<LayerSpec>();
        }

        return layers.EnumerateArray()
            .Select(layer => new LayerSpec
            {
                Name = ReadString(layer, "name"),
                Color = ReadInt(layer, "color"),
                LineType = ReadString(layer, "lineType"),
                LineWeight = ReadDouble(layer, "lineWeight")
            })
            .ToArray();
    }

    private static IReadOnlyList<EntitySpec> ReadEntities(JsonElement root)
    {
        if (!root.TryGetProperty("entities", out var entities))
        {
            return Array.Empty<EntitySpec>();
        }

        return entities.EnumerateArray()
            .Select(entity => new EntitySpec
            {
                Id = ReadString(entity, "id"),
                Type = ReadString(entity, "type"),
                Layer = ReadString(entity, "layer"),
                Closed = ReadBoolean(entity, "closed"),
                Points = ReadPointArray(entity, "points"),
                Start = ReadPoint(entity, "start"),
                End = ReadPoint(entity, "end"),
                Center = ReadPoint(entity, "center"),
                Position = ReadPoint(entity, "position"),
                Radius = ReadDouble(entity, "radius"),
                Size = ReadDouble(entity, "size"),
                StartAngle = ReadDouble(entity, "startAngle"),
                EndAngle = ReadDouble(entity, "endAngle"),
                Value = ReadString(entity, "value"),
                Height = ReadDouble(entity, "height"),
                Rotation = ReadDouble(entity, "rotation")
            })
            .ToArray();
    }

    private static IReadOnlyList<DimensionSpec> ReadDimensions(JsonElement root)
    {
        if (!root.TryGetProperty("dimensions", out var dimensions))
        {
            return Array.Empty<DimensionSpec>();
        }

        return dimensions.EnumerateArray()
            .Select(dimension => new DimensionSpec
            {
                Id = ReadString(dimension, "id"),
                Type = ReadString(dimension, "type"),
                Layer = ReadString(dimension, "layer"),
                From = ReadPoint(dimension, "from"),
                To = ReadPoint(dimension, "to"),
                Center = ReadPoint(dimension, "center"),
                TargetEntityId = ReadString(dimension, "targetEntityId"),
                Offset = ReadPoint(dimension, "offset"),
                Text = ReadString(dimension, "text")
            })
            .ToArray();
    }

    private static IReadOnlyList<string> ReadStringArray(JsonElement obj, string propertyName)
    {
        if (!obj.TryGetProperty(propertyName, out var values))
        {
            return Array.Empty<string>();
        }

        return values.EnumerateArray()
            .Select(value => value.GetString() ?? string.Empty)
            .ToArray();
    }

    private static IReadOnlyList<DrawingPoint> ReadPointArray(JsonElement obj, string propertyName)
    {
        if (!obj.TryGetProperty(propertyName, out var values))
        {
            return Array.Empty<DrawingPoint>();
        }

        return values.EnumerateArray()
            .Select(ReadPoint)
            .ToArray();
    }

    private static DrawingPoint? ReadPoint(JsonElement obj, string propertyName)
    {
        return obj.TryGetProperty(propertyName, out var point) ? ReadPoint(point) : null;
    }

    private static DrawingPoint ReadPoint(JsonElement point)
    {
        var coordinates = point.EnumerateArray()
            .Select(coordinate => coordinate.GetDouble())
            .ToArray();

        if (coordinates.Length != 2)
        {
            throw new InvalidOperationException("DrawingSpec point arrays must contain exactly two coordinates.");
        }

        return new DrawingPoint(coordinates[0], coordinates[1]);
    }

    private static string ReadString(JsonElement obj, string propertyName)
    {
        return obj.TryGetProperty(propertyName, out var value) ? value.GetString() ?? string.Empty : string.Empty;
    }

    private static int ReadInt(JsonElement obj, string propertyName)
    {
        return obj.TryGetProperty(propertyName, out var value) ? value.GetInt32() : 0;
    }

    private static double ReadDouble(JsonElement obj, string propertyName)
    {
        return obj.TryGetProperty(propertyName, out var value) ? value.GetDouble() : 0d;
    }

    private static bool ReadBoolean(JsonElement obj, string propertyName)
    {
        return obj.TryGetProperty(propertyName, out var value) && value.GetBoolean();
    }

    private static string ReadPluginSource()
    {
        var root = FindRepositoryRoot();
        var pluginDirectory = Path.Combine(root, "src", "ZwcadAiPlugin");

        return string.Join(
            Environment.NewLine,
            Directory.GetFiles(pluginDirectory, "*.cs")
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(File.ReadAllText));
    }

    private static IReadOnlyList<string> ReadProjectReferences(string projectPath)
    {
        var document = XDocument.Load(projectPath);

        return document
            .Descendants("ProjectReference")
            .Select(reference => (string?)reference.Attribute("Include") ?? string.Empty)
            .Select(path => Path.GetFileName(path) ?? string.Empty)
            .Where(fileName => !string.IsNullOrWhiteSpace(fileName))
            .ToArray();
    }

    private static bool IsZwcadRuntimeReference(string reference)
    {
        return reference.StartsWith("Zw", StringComparison.OrdinalIgnoreCase)
            || reference.StartsWith("Zcad", StringComparison.OrdinalIgnoreCase)
            || reference.IndexOf("ZWCAD", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static void AssertZwcadReference(XElement reference, string assemblyFileName)
    {
        var hintPath = reference.Element("HintPath")?.Value ?? string.Empty;
        var copyLocal = reference.Element("Private")?.Value ?? string.Empty;

        Assert(
            hintPath.EndsWith(assemblyFileName, StringComparison.OrdinalIgnoreCase),
            $"{assemblyFileName} reference must use a HintPath ending in {assemblyFileName}.");
        Assert(
            hintPath.IndexOf(@"C:\Program Files", StringComparison.OrdinalIgnoreCase) < 0,
            $"{assemblyFileName} reference must not hardcode a local absolute install path.");
        AssertEqual("false", copyLocal.ToLowerInvariant());
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ZwcadAi.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root from test output directory.");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void AssertEqual<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"Expected '{expected}', got '{actual}'.");
        }
    }

    private static void AssertPoint(double expectedX, double expectedY, DrawingPoint? actual)
    {
        if (actual == null)
        {
            throw new InvalidOperationException($"Expected point ({expectedX}, {expectedY}) but got null.");
        }

        AssertEqual(expectedX, actual.X);
        AssertEqual(expectedY, actual.Y);
    }

    private static bool PointsEqual(DrawingPoint? expected, DrawingPoint? actual)
    {
        return expected != null
            && actual != null
            && Math.Abs(expected.X - actual.X) < 0.000001
            && Math.Abs(expected.Y - actual.Y) < 0.000001;
    }

    private static double CenterLineLength(PlannedEntity entity)
    {
        if (entity.Start == null || entity.End == null)
        {
            throw new InvalidOperationException($"Centerline '{entity.SpecEntityId}' must include start and end points.");
        }

        var dx = entity.End.X - entity.Start.X;
        var dy = entity.End.Y - entity.Start.Y;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }

    private static string MinimalSpecJson(string entityJson)
    {
        return $$"""
        {
          "drawingSpecVersion": "1.0",
          "units": "mm",
          "metadata": {
            "title": "schema validation test",
            "domain": "mechanical_plate",
            "createdBy": "test",
            "requestId": "schema-validation-test"
          },
          "layers": [
            { "name": "OUTLINE", "color": 7, "lineType": "Continuous", "lineWeight": 0.35 },
            { "name": "CENTER", "color": 1, "lineType": "Center", "lineWeight": 0.18 },
            { "name": "DIM", "color": 3, "lineType": "Continuous", "lineWeight": 0.18 }
          ],
          "entities": [
            {{entityJson}}
          ],
          "dimensions": [],
          "clarifications": []
        }
        """;
    }

    private static string BusinessInvalidSpecJson(string entityId)
    {
        return $$"""
        {
          "drawingSpecVersion": "1.0",
          "units": "mm",
          "metadata": {
            "title": "business repair test",
            "domain": "mechanical_plate",
            "createdBy": "test",
            "requestId": "business-repair-test"
          },
          "layers": [
            { "name": "OUTLINE", "color": 7, "lineType": "Continuous", "lineWeight": 0.35 }
          ],
          "entities": [
            {
              "id": "{{entityId}}",
              "type": "line",
              "layer": "OUTLINE",
              "start": [0, 0],
              "end": [100, 0]
            }
          ],
          "dimensions": [],
          "clarifications": []
        }
        """;
    }

    private static string FormatIssues(IEnumerable<ValidationIssue> issues)
    {
        return string.Join(
            "; ",
            issues.Select(issue => $"{issue.Code} at {issue.Path}: {issue.Message}"));
    }

    private static string FormatModelIssues(IEnumerable<AiModelIssue> issues)
    {
        return string.Join(
            "; ",
            issues.Select(issue => $"{issue.Code} from {issue.Source} at {issue.Path}: {issue.Message}"));
    }

    private static void AssertSequenceEqual<T>(IReadOnlyList<T> expected, IReadOnlyList<T> actual)
    {
        if (!expected.SequenceEqual(actual))
        {
            throw new InvalidOperationException(
                $"Expected sequence [{string.Join(", ", expected)}], got [{string.Join(", ", actual)}].");
        }
    }

    private sealed class StaticAiModelClient : IAiModelClient
    {
        private readonly string _rawResponse;

        public StaticAiModelClient(string rawResponse)
        {
            _rawResponse = rawResponse;
        }

        public int DrawingSpecCalls { get; private set; }

        public int RepairCalls { get; private set; }

        public string CreateDrawingSpec(AiDrawingSpecRequest request, AiModelCallOptions options)
        {
            DrawingSpecCalls++;
            return _rawResponse;
        }

        public string RepairDrawingSpec(AiDrawingSpecRepairRequest request, AiModelCallOptions options)
        {
            RepairCalls++;
            return _rawResponse;
        }
    }

    private sealed class SequenceAiModelClient : IAiModelClient
    {
        private readonly Queue<string> _drawingSpecResponses;
        private readonly Queue<string> _repairResponses;
        private readonly List<AiDrawingSpecRequest> _drawingSpecRequests = new List<AiDrawingSpecRequest>();
        private readonly List<AiDrawingSpecRepairRequest> _repairRequests = new List<AiDrawingSpecRepairRequest>();

        public SequenceAiModelClient(IEnumerable<string> drawingSpecResponses, IEnumerable<string> repairResponses)
        {
            _drawingSpecResponses = new Queue<string>(drawingSpecResponses);
            _repairResponses = new Queue<string>(repairResponses);
        }

        public int DrawingSpecCalls { get; private set; }

        public int RepairCalls { get; private set; }

        public IReadOnlyList<AiDrawingSpecRequest> DrawingSpecRequests => _drawingSpecRequests;

        public IReadOnlyList<AiDrawingSpecRepairRequest> RepairRequests => _repairRequests;

        public string CreateDrawingSpec(AiDrawingSpecRequest request, AiModelCallOptions options)
        {
            DrawingSpecCalls++;
            _drawingSpecRequests.Add(request);
            if (_drawingSpecResponses.Count == 0)
            {
                throw new InvalidOperationException("No drawing spec response configured for sequence model client.");
            }

            return _drawingSpecResponses.Dequeue();
        }

        public string RepairDrawingSpec(AiDrawingSpecRepairRequest request, AiModelCallOptions options)
        {
            RepairCalls++;
            _repairRequests.Add(request);
            if (_repairResponses.Count == 0)
            {
                throw new InvalidOperationException("No repair response configured for sequence model client.");
            }

            return _repairResponses.Dequeue();
        }
    }

    private sealed class ThrowingAiModelClient : IAiModelClient
    {
        private readonly Exception _exception;

        public ThrowingAiModelClient(Exception exception)
        {
            _exception = exception;
        }

        public int DrawingSpecCalls { get; private set; }

        public int RepairCalls { get; private set; }

        public string CreateDrawingSpec(AiDrawingSpecRequest request, AiModelCallOptions options)
        {
            DrawingSpecCalls++;
            throw _exception;
        }

        public string RepairDrawingSpec(AiDrawingSpecRepairRequest request, AiModelCallOptions options)
        {
            RepairCalls++;
            throw _exception;
        }
    }

    private sealed class CapturingHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _respond;

        public CapturingHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
            : this((request, _) => Task.FromResult(respond(request)))
        {
        }

        public CapturingHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond)
        {
            _respond = respond;
        }

        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return _respond(request, cancellationToken);
        }
    }
}
