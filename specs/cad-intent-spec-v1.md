# CadIntentSpec v1 Contract

CadIntentSpec v1 is the user-facing and external-AI-facing JSON contract for new drawing requests. It sits above DrawingSpec v1:

1. External AI tools or users produce CadIntentSpec JSON.
2. The plugin classifies the pasted JSON as `TemplateIntent`, `CompositeIntent`, `SketchIntent`, or advanced `DrawingSpec`.
3. CadIntentSpec inputs are validated before any compiler, renderer plan, or CAD writer is allowed to run.
4. Later P5A tasks compile valid CadIntentSpec inputs into DrawingSpec v1.

P5A-01 defines the contract and classifier only. It does not define the full intent-to-DrawingSpec compiler.

## Root Discriminator

CadIntentSpec v1 roots are JSON objects with:

| Field | Required | Meaning |
|---|---:|---|
| `cadIntentSpecVersion` | yes | Must be `"1.0"`. |
| `intentType` | yes | One of `TemplateIntent`, `CompositeIntent`, `SketchIntent`. |
| `domainPack` | yes | Domain pack consumed by a local compiler. |
| `units` | yes | P5A MVP accepts only `"mm"`. |

The input classifier uses this order:

1. If the root has `drawingSpecVersion`, classify as `DrawingSpec` and send it to the existing DrawingSpec validator path.
2. Otherwise, if the root has `cadIntentSpecVersion` and `intentType`, classify by `intentType`.
3. Otherwise, return `unsupported_json_contract`.

Direct DrawingSpec paste remains an advanced debugging entry. CadIntent is the default P5A production entry.

## TemplateIntent

`TemplateIntent` is for high-frequency standard templates.

Required root fields:

| Field | Value |
|---|---|
| `intentType` | `TemplateIntent` |
| `domainPack` | `mechanical_plate` |
| `template` | `rectangular_plate` in P5A-01/P5A-02 |
| `parameters` | Object containing required template parameters |

Required `rectangular_plate` parameters:

| Parameter | Type | Meaning |
|---|---|---|
| `length` | positive number | Overall X size in mm |
| `width` | positive number | Overall Y size in mm |

Example:

```json
{
  "cadIntentSpecVersion": "1.0",
  "intentType": "TemplateIntent",
  "domainPack": "mechanical_plate",
  "units": "mm",
  "template": "rectangular_plate",
  "parameters": {
    "length": 1200,
    "width": 300
  }
}
```

## CompositeIntent

`CompositeIntent` is for mechanical plate drawings built from a controlled base profile and feature list.

Required root fields:

| Field | Value |
|---|---|
| `intentType` | `CompositeIntent` |
| `domainPack` | `mechanical_plate` |
| `baseProfile.type` | `rectangle` in P5A-01/P5A-02 |
| `baseProfile.size.length` | positive number |
| `baseProfile.size.width` | positive number |

Supported first-phase feature types:

| Feature | Required fields |
|---|---|
| `hole` | `center: [x, y]`, `diameter` |
| `slot` | `center: [x, y]`, `length`, `width`, `angle` |
| `fillet` | `radius` |
| `centerMark` / `center_mark` | `center: [x, y]` |

Unknown feature types return `unsupported_feature_type`.

Example:

```json
{
  "cadIntentSpecVersion": "1.0",
  "intentType": "CompositeIntent",
  "domainPack": "mechanical_plate",
  "units": "mm",
  "baseProfile": {
    "type": "rectangle",
    "size": {
      "length": 1200,
      "width": 300
    }
  },
  "features": [
    {
      "type": "hole",
      "id": "hole-1",
      "center": [200, 150],
      "diameter": 20
    }
  ]
}
```

## SketchIntent

`SketchIntent` is for controlled non-standard 2D mechanical new drawings. It is not an existing-DWG understanding or editing format.

Required root fields:

| Field | Value |
|---|---|
| `intentType` | `SketchIntent` |
| `domainPack` | `generic_2d_mechanical` |
| `profile.closed` | `true` |
| `profile.segments` | Non-empty array of supported segments |

Supported first-phase segment types:

| Segment | Required fields |
|---|---|
| `line` | `start: [x, y]`, `end: [x, y]` |
| `arc` | `start: [x, y]`, `end: [x, y]`, `center: [x, y]`, `radius`, `startAngle`, `endAngle` |

The profile is closed only when every segment starts at the previous segment end and the final segment end returns to the first segment start. Unsupported segment types return `unsupported_segment_type`. Open or discontinuous profiles return `profile_not_closed`.

Supported first-phase feature types:

| Feature | Required fields |
|---|---|
| `hole` | `center: [x, y]`, `diameter` |
| `slot` | `center: [x, y]`, `length`, `width`, `angle` |
| `text` | `position: [x, y]`, `value` |
| `mtext` | `position: [x, y]`, `value` |

Example:

```json
{
  "cadIntentSpecVersion": "1.0",
  "intentType": "SketchIntent",
  "domainPack": "generic_2d_mechanical",
  "units": "mm",
  "profile": {
    "closed": true,
    "segments": [
      { "type": "line", "start": [0, 0], "end": [100, 0] },
      { "type": "line", "start": [100, 0], "end": [100, 50] },
      { "type": "line", "start": [100, 50], "end": [0, 50] },
      { "type": "line", "start": [0, 50], "end": [0, 0] }
    ]
  }
}
```

## Points, Units, And Angles

- Points use DrawingSpec's public wire convention: `[x, y]`.
- Units are millimeters in P5A MVP.
- Angles are degrees, with positive values counterclockwise in the XY plane.
- All key dimensions, positions, radii, and angles must be explicit.

## Stable Issue Codes

Runtime validation returns `ValidationIssue` with stable `code`, `path`, `message`, and `severity`.

| Code | Meaning |
|---|---|
| `missing_required_parameter` | A required dimension, position, radius, angle, template, profile, segment, feature parameter, or units value is missing or unusable. |
| `unsupported_domain_pack` | `domainPack` is unknown or not valid for the intent type. |
| `unsupported_template` | Template or base profile type is not supported in the current domain pack. |
| `unsupported_feature_type` | Feature `type` is not supported. |
| `unsupported_segment_type` | Sketch segment `type` is not supported. |
| `profile_not_closed` | A sketch profile is not explicitly closed or its segment endpoints do not close. |
| `unsupported_json_contract` | The JSON is not DrawingSpec v1 or CadIntentSpec v1, or violates the root contract. |

Issue paths use JSONPath-style paths such as:

- `$.parameters.length`
- `$.domainPack`
- `$.template`
- `$.features[0].type`
- `$.profile.segments[0].type`
- `$.profile`

## Clarification Rules

When a required engineering parameter is missing, the validator must return a stable issue and a clarification message. The plugin must not guess:

- critical dimensions: length, width, diameter, slot size
- positions: feature center, text position
- radius: fillet or arc radius
- angle: slot angle, arc start/end angle
- closure relation: sketch `profile.closed` and endpoint continuity

Invalid CadIntentSpec inputs must not enter DrawingSpec compilation, renderer plan creation, or CAD writer code.

## First-Phase Non-Goals

CadIntentSpec v1 P5A first phase does not support:

- arbitrary DWG automatic understanding
- existing DWG editing
- architecture, electrical, piping, or other non-mechanical-plate domains
- 3D modeling
- sheet-metal unfolding
- AI-generated CAD commands, scripts, .NET code, COM calls, LISP, or SCR
- hidden model calls, API key reads, or network calls in the pasted JSON validation path
