# Model Prompt Contract v1

Prompt version: `p4-01-model-prompt-contract-v1`

This contract defines the P4-01 boundary between natural-language input, model output, DrawingSpec validation, issue mapping, and bounded model repair. The model may only produce DrawingSpec JSON or clarification questions. It must never produce executable CAD commands, scripts, .NET code, LISP, SCR, COM calls, ZRX calls, shell commands, file operations, or arbitrary plugin instructions.

## Capability

The AI service accepts one user drawing request plus deterministic drawing context and returns either:

- a DrawingSpec v1 JSON document that can pass Schema and business validation, or
- clarification questions when critical engineering parameters are missing.

The renderer remains deterministic. AI output is data only.

## Input Boundary

Code-facing request type: `AiDrawingSpecRequest`.

```json
{
  "requestId": "optional caller id",
  "promptVersion": "p4-01-model-prompt-contract-v1",
  "userRequest": "画一个100x60矩形板，中心一个直径12孔，并标注外形尺寸",
  "units": "mm",
  "domain": "mechanical_plate",
  "drawingSpecVersion": "1.0",
  "layerStandard": "enterprise-default-v1",
  "allowedEntityTypes": ["line", "polyline", "circle", "arc", "text", "mtext", "centerMark"],
  "allowedDimensionTypes": ["linear", "aligned", "radius", "diameter", "angular"],
  "maxClarificationQuestions": 3
}
```

Rules:

- `userRequest` is natural language only. It is not a command channel.
- `domain` is locked to `mechanical_plate` for the MVP.
- `units` defaults to `mm`; if the user gives another supported unit, the model must still emit explicit `units`.
- `layerStandard` is a profile id. P4/P5 may pass different ids later, but P4-01 does not implement profile loading.
- Allowed entity and dimension types are a closed list. Unknown types must become validation issues, not renderer extensions.
- Critical dimensions, positions, units, or constraints must not be guessed. The model must ask clarifying questions.

## Output Boundary

Raw model content must be a single JSON object matching DrawingSpec v1 root shape:

```json
{
  "drawingSpecVersion": "1.0",
  "units": "mm",
  "metadata": {
    "title": "",
    "domain": "mechanical_plate",
    "createdBy": "gpt",
    "requestId": ""
  },
  "layers": [],
  "entities": [],
  "dimensions": [],
  "clarifications": []
}
```

No Markdown fences, comments, prose, or extra root envelope are allowed in the model response because `specs/drawing-spec-v1.schema.json` is the public wire contract.

The AI adapter maps the raw JSON into `AiDrawingSpecResponse`:

| Condition | `AiDrawingSpecResponse.Kind` | Required fields |
|---|---|---|
| Valid DrawingSpec with no clarification questions | `DrawingSpec` | `Spec`, `DrawingSpecJson`, `Validation` |
| Missing critical user information | `NeedsClarification` | `Clarifications`, optional invalid/partial `DrawingSpecJson`, issue code `needs_clarification` |
| Unsafe, non-JSON, schema-invalid after repair limit, or service failure | `Rejected` | `Issues`, `Validation` |

## DrawingSpec JSON Schema Boundary

Source of truth:

- Machine contract: `specs/drawing-spec-v1.schema.json`
- Human protocol: `specs/drawing-spec-v1.md`
- Internal object model: `src/ZwcadAi.Core/DrawingSpec.cs`

Schema invariants the prompt must preserve:

- Root `drawingSpecVersion` must be `"1.0"`.
- Root `layers` and `entities` are required.
- Public 2D points use array form `[x, y]`; object-shaped points are invalid on the wire.
- `dimensions` are top-level array items, never `entities[].type = "dimension"`.
- Every entity and dimension needs a stable ASCII id.
- Layer names are case-sensitive.
- Production MVP specs must declare at least `OUTLINE`, `CENTER`, and `DIM`.
- `centerMark` uses `CENTER`; dimensions use `DIM`; normal text uses `TEXT`; title block text uses `TITLE`.

## Failure Issue Mapping

All failures crossing the AI service boundary are mapped to stable issue records with:

```json
{
  "code": "missing_required",
  "path": "$.entities[0].center",
  "message": "Property 'center' is required.",
  "severity": "Error",
  "source": "SchemaValidation",
  "repairable": true
}
```

Code-facing AI boundary type: `AiModelIssue`. Existing validator and renderer failures continue to use `ValidationIssue` as the canonical low-level issue type, then map into `AiModelIssue` before they are sent into an AI repair request.

| Source | Typical codes | Path policy | Repair policy |
|---|---|---|---|
| `ModelResponse` | `model_response_not_json`, `unsafe_cad_command` | `$` or offending field if detectable | Reject unsafe command output; repair non-JSON only if raw content is clearly intended JSON |
| `UserClarification` | `needs_clarification` | `$` or `$.clarifications[index]` | Ask the user; do not run repair |
| `SchemaValidation` | `invalid_json`, `unknown_property`, `missing_required`, `invalid_type`, `invalid_value`, `invalid_point2d`, `unsupported_entity_type`, `unsupported_dimension_type` | JSONPath to the failed schema field | Repairable within attempt limit |
| `BusinessValidation` | `unsupported_layer`, `missing_required_layer`, `invalid_layer_color`, `invalid_layer_linetype`, `invalid_layer_lineweight`, `invalid_dimension_layer`, `missing_dimension_geometry`, `point_out_of_range` | Stable field path, preferably with entity or dimension index/id | Repairable only if it does not require guessing missing engineering intent |
| `Renderer` | `missing_linetype`, `entity_render_failed`, `dimension_render_failed`, `render_canceled` | Stable renderer path such as `$.entities[id]` or `$.dimensions[id]` | Not a model repair target by default; surface to user/operator |
| `Service` | timeout, rate limit, authentication, provider error | `$` | Not a DrawingSpec repair target |

## Repair Strategy

Code-facing request type: `AiDrawingSpecRepairRequest`.

```json
{
  "invalidDrawingSpecJson": "{ ... previous model response ... }",
  "issues": [
    {
      "code": "invalid_point2d",
      "path": "$.entities[0].center",
      "message": "DrawingSpec v1 point2d wire format is [x, y]."
    }
  ],
  "repairAttempt": 1,
  "maxRepairAttempts": 2,
  "repairStrategy": "RepairDrawingSpecOnly"
}
```

Repair rules:

1. Repair only the provided DrawingSpec JSON. The repair payload intentionally excludes the original user request, full DWG content, geometry summaries, screenshots, and arbitrary context.
2. Use the provided `issues[].path` and `issues[].code` as the repair target list.
3. Keep stable ids unless the issue is the id itself.
4. Do not add new geometry that was not requested.
5. Do not invent missing critical dimensions; return clarification questions instead.
6. Run Schema validation after every repair attempt.
7. Run business validation after Schema validation passes.
8. Stop after `maxRepairAttempts = 2`.
9. If repair fails twice, return `Rejected` with `repair_attempt_limit_exceeded` and preserve the last validation issues.

## Enterprise Standards Boundary

P4-01 passes `layerStandard = "enterprise-default-v1"` as a profile id. It does not implement profile loading.

The current stable in-code profile entry points remain:

- `CadLayerStandards`
- `CadTextStyleStandards`
- `CadDimensionStyleStandards`

Future P4/P5 interface work may load `config/cad/standards/enterprise-default-v1.json` and populate these concepts from data. That is intentionally outside P4-01 implementation scope.

## P3 Lightweight Regression Gate

Before changing AI integration behavior, run the P3 lightweight gate:

```powershell
dotnet .\src\ZwcadAi.Tests\bin\x64\Debug\net8.0\ZwcadAi.Tests.dll
```

P3 baseline content:

- Fixed `AIDRAW` path uses `RectangularPlateSample.Create()` and the deterministic ZWCAD writer.
- Layer/linetype/text-style/dimension-style checks remain covered by existing tests and source checks.
- The P3 closure baseline is 44 automated tests.

P6 still owns full DWG reverse extraction, batch regression, key dimension comparison, expected GeometrySummary artifacts, and export artifact verification. Those checks must not be moved back into the P3 lightweight gate.
