# DrawingSpec v1 Protocol

DrawingSpec v1 is the public JSON wire contract between the AI service, validators, deterministic renderer, and regression suite. The C# model may use object types such as `DrawingPoint`, but JSON exchanged across process or file boundaries must follow this document and `drawing-spec-v1.schema.json`.

## Version And Root Shape

Every DrawingSpec v1 document is a JSON object with:

- `drawingSpecVersion`: required string, exactly `"1.0"`.
- `units`: required string. The schema permits `mm`, `cm`, `m`, and `inch`; the MVP business profile accepts only `mm`.
- `metadata`: optional object for traceability.
- `layers`: required array of layer declarations.
- `entities`: required array of geometric and annotation entities.
- `dimensions`: optional array of dimensions.
- `clarifications`: optional array of questions when the request is under-specified.

Minimal legal DrawingSpec:

```json
{
  "drawingSpecVersion": "1.0",
  "units": "mm",
  "layers": [
    { "name": "OUTLINE", "color": 7, "lineType": "Continuous", "lineWeight": 0.35 },
    { "name": "CENTER", "color": 1, "lineType": "Center", "lineWeight": 0.18 },
    { "name": "DIM", "color": 3, "lineType": "Continuous", "lineWeight": 0.18 }
  ],
  "entities": []
}
```

## Wire Format

All public JSON points use array form:

```json
[x, y]
```

Object-shaped points such as `{ "x": 10, "y": 20 }` or `{ "X": 10, "Y": 20 }` are invalid on the wire. `DrawingPoint { X, Y }` exists only as the internal C# representation used after validation or controlled construction.

All point values are finite JSON numbers. Coordinates are expressed in `units`.

## Units And Coordinates

The MVP production profile is `mechanical_plate` in millimeters.

- Model space uses 1:1 units.
- The coordinate system is 2D Cartesian XY.
- Unless a future field explicitly says otherwise, the generated part origin is the lower-left of the part envelope.
- Positive X points right; positive Y points up.
- Z coordinates are not part of DrawingSpec v1.
- Business validation rejects coordinates outside the configured range. The initial range is `-100000` to `100000` drawing units per coordinate.

## Angles

Angles in DrawingSpec v1 are degrees.

- `0` degrees points along positive X.
- Positive angles rotate counterclockwise in the XY plane.
- Arc entities use `startAngle` and `endAngle` with this convention.
- Renderers may convert to CAD runtime radians or native angle types internally, but that conversion is not visible in the JSON contract.

## Layers

The `enterprise-default-v1` business profile recognizes these layer names exactly:

| Layer | Purpose |
|---|---|
| `OUTLINE` | Visible part outlines, holes, slots, and visible arcs |
| `CENTER` | Center marks, centerlines, and axes |
| `DIM` | Dimensions and leaders |
| `TEXT` | General notes and non-title-block annotation |
| `HIDDEN` | Explicit hidden construction or hidden edges |
| `CONSTRUCTION` | Non-plot helper geometry when explicitly requested |
| `TITLE` | Title block and border entities |

Layer names are case-sensitive. Production MVP specs must declare at least `OUTLINE`, `CENTER`, and `DIM`.

Business rules:

- Entities may only reference declared layers.
- Layer names outside the whitelist are rejected.
- Center marks and centerlines must be on `CENTER`.
- Dimensions must be on `DIM`.
- Text entities must be on `TEXT` or `TITLE`.
- Entity color, line type, and line weight are inherited from the layer unless a later protocol version explicitly allows overrides.

## Entity Ids

Every entity and dimension must have a stable `id`.

Ids are used for validation paths, targeted repair, renderer mapping, and regression comparison. They must not depend on list ordering, random values, timestamps, localized display text, or model prose.

Allowed id characters:

- ASCII letters `A-Z` and `a-z`
- digits `0-9`
- period `.`
- underscore `_`
- hyphen `-`

Examples:

- `outer-profile`
- `hole-1`
- `hole-1-center`
- `dim-hole-dia`

## Entities

Supported entity types:

| Type | Required fields | Notes |
|---|---|---|
| `line` | `id`, `type`, `layer`, `start`, `end` | `start` and `end` are point arrays |
| `polyline` | `id`, `type`, `layer`, `points` | `points` has at least 2 point arrays; `closed` is optional |
| `circle` | `id`, `type`, `layer`, `center`, `radius` | `radius` must be greater than 0 |
| `arc` | `id`, `type`, `layer`, `center`, `radius`, `startAngle`, `endAngle` | angles are degrees, counterclockwise-positive |
| `text` | `id`, `type`, `layer`, `position`, `value`, `height` | `value` is plain annotation text; `rotation` is optional degrees |
| `mtext` | `id`, `type`, `layer`, `position`, `value`, `height` | multiline annotation; no executable CAD commands |
| `centerMark` | `id`, `type`, `layer`, `center`, `size` | must be on `CENTER` by business rules |

The schema rejects unknown entity types. P3 renderer support may lag the full schema during implementation, but schema acceptance remains the v1 protocol boundary.

## Dimensions

Dimensions are top-level `dimensions[]` items, not `entities[]`.

Supported dimension types:

| Type | Expected fields |
|---|---|
| `linear` | `from`, `to`, `offset` |
| `aligned` | `from`, `to`, `offset` |
| `radius` | `targetEntityId`; may include `text` |
| `diameter` | `targetEntityId`; may include `text` |
| `angular` | `center`, `from`, `to`, `offset` or a later profile-specific equivalent |

Business rules:

- Dimensions must be on `DIM`.
- `targetEntityId` must reference an existing stable entity id when present.
- Diameter and radius dimensions should reference the circle or arc whose value they describe.
- Linear and aligned dimensions should use explicit `from`, `to`, and `offset` points.
- Manual `text` is allowed only when geometry remains recoverable for validation.

## Clarifications

When critical information is missing, the AI service should return a DrawingSpec with `clarifications` explaining the missing items. It may leave `entities` empty or include only safe, fully specified parts.

Validators still enforce root shape and schema rules. Missing engineering dimensions must not be guessed.

## Error Codes

Core validators return `ValidationIssue` with `code`, `path`, `message`, and `severity`.

Schema-level codes:

| Code | Meaning |
|---|---|
| `invalid_json` | JSON could not be parsed |
| `invalid_type` | Value type is wrong |
| `missing_required` | Required property is absent |
| `unknown_property` | Property is not allowed in this location |
| `invalid_value` | Value violates enum, const, or numeric constraint |
| `unsupported_entity_type` | Entity type is not in DrawingSpec v1 |
| `invalid_point2d` | Point is not encoded as `[x, y]` |
| `array_too_small` | Array has fewer items than required |
| `string_too_short` | String does not meet minimum length |

Business-rule codes:

| Code | Meaning |
|---|---|
| `unsupported_version` | Business profile does not accept this protocol version |
| `unsupported_units` | Business profile does not accept this unit |
| `unsupported_domain` | Business profile does not accept this metadata domain |
| `unsupported_layer` | Layer name is outside the standard whitelist |
| `missing_required_layer` | Production-required layer is absent |
| `duplicate_layer` | Layer name is declared more than once |
| `missing_entity_id` | Entity id is missing |
| `duplicate_entity_id` | Entity id is not unique |
| `invalid_entity_id` | Entity id is not stable ASCII id format |
| `missing_entity_layer` | Entity references an undeclared layer |
| `invalid_center_mark_layer` | Center mark is not on `CENTER` |
| `invalid_text_layer` | Text is not on `TEXT` or `TITLE` |
| `invalid_line_geometry` | Line lacks required geometry |
| `invalid_polyline_points` | Polyline lacks enough points |
| `invalid_circle_geometry` | Circle lacks center or positive radius |
| `invalid_arc_geometry` | Arc lacks required geometry |
| `invalid_center_mark_geometry` | Center mark lacks center or positive size |
| `invalid_text_geometry` | Text lacks position or positive height |
| `coordinate_out_of_range` | Point exceeds configured coordinate range |
| `radius_out_of_range` | Radius is not positive or exceeds configured limit |
| `size_out_of_range` | Size is not positive or exceeds configured limit |
| `text_height_out_of_range` | Text height is not positive or exceeds configured limit |
| `entity_count_exceeded` | Entity count exceeds configured limit |
| `dimension_count_exceeded` | Dimension count exceeds configured limit |
| `missing_dimension_id` | Dimension id is missing |
| `duplicate_dimension_id` | Dimension id is not unique |
| `invalid_dimension_id` | Dimension id is not stable ASCII id format |
| `unsupported_dimension_type` | Dimension type is not in DrawingSpec v1 |
| `missing_dimension_layer` | Dimension references an undeclared layer |
| `invalid_dimension_layer` | Dimension is not on `DIM` |
| `missing_dimension_geometry` | Dimension lacks required points |
| `missing_dimension_target` | Dimension target entity is missing |

## Field Paths

Schema validation paths use JSONPath-like indexes:

- `$.entities[0].type`
- `$.entities[2].center`
- `$.dimensions[0].targetEntityId`

Business validation paths prefer stable ids:

- `$.entities[hole-1].layer`
- `$.entities[outer-profile].points[3]`
- `$.dimensions[dim-hole-dia].targetEntityId`

This distinction keeps schema errors close to raw JSON structure while keeping business errors stable across harmless array reordering.

## Prompt Contract Notes

P4-01 fixes the model prompt contract at `prompts/model-prompt-contract-v1.md` with prompt version `p4-01-model-prompt-contract-v1`. AI prompts must instruct models to:

- Output JSON only.
- Use point arrays `[x, y]` on the wire.
- Use only supported entity and dimension types.
- Declare all referenced layers.
- Use stable ids for entities and dimensions.
- Put dimensions in top-level `dimensions[]`, never as entity type `"dimension"`.
- Ask clarifying questions instead of guessing missing engineering values.
- Repair loops may only repair DrawingSpec JSON fields identified by stable validation issue paths.
