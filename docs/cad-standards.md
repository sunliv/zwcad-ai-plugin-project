# CAD Standards: Enterprise Default v1

本文件定义 P0-03 的 CAD 标准草案。当前标准服务于 `mechanical_plate` MVP：毫米制二维机械矩形板件、安装孔、槽、中心线、基础尺寸标注和交付图框。后续如果企业已有正式标准文件，应以企业文件为准，并把差异固化到配置文件，而不是写死在渲染代码中。

## Standard Profile

| Item | Value |
|---|---|
| Standard id | `enterprise-default-v1` |
| Domain | `mechanical_plate` |
| Unit | `mm` |
| Coordinate system | 2D Cartesian, model space, origin at lower-left of generated part unless the DrawingSpec explicitly states otherwise |
| Angle unit | Degrees in DrawingSpec; renderer converts to CAD runtime units if required |
| Plot target | DWG first, PDF export verification in P1-04 |
| Drawing scale | Model space geometry uses 1:1 millimeter units |

## Layer Standard

The renderer must reject DrawingSpec entities that reference layers outside this whitelist unless a future profile explicitly extends it.

| Layer | Purpose | Color Index | Line Type | Line Weight mm | Applies To |
|---|---|---:|---|---:|---|
| `OUTLINE` | Visible part outline and cut edges | 7 | `Continuous` | 0.35 | Plate outer profiles, holes, slots, visible arcs |
| `CENTER` | Centerlines and center marks | 1 | `Center` | 0.18 | Hole center marks, slot axes, plate centerlines |
| `DIM` | Dimensions and leaders | 3 | `Continuous` | 0.18 | Linear, aligned, radius, diameter dimensions |
| `TEXT` | Notes and non-title-block annotation | 2 | `Continuous` | 0.18 | General notes, material notes, process notes |
| `HIDDEN` | Hidden edges if a later sample needs them | 8 | `Hidden` | 0.18 | Hidden construction edges only when explicitly requested |
| `CONSTRUCTION` | Temporary construction geometry for preview or checks | 9 | `Continuous` | 0.09 | Non-plot helper geometry; not used in final DWG unless requested |
| `TITLE` | Title block and border entities | 4 | `Continuous` | 0.25 | Border, title block lines, title block attributes |

### Layer Rules

- `OUTLINE`, `CENTER`, and `DIM` are required for every rendered production DrawingSpec in the MVP.
- `CONSTRUCTION` must not be plotted by default.
- Entity color, line type, and line weight are inherited from the layer unless the standard profile explicitly allows an override.
- Layer names are case-sensitive in DrawingSpec validation to keep regression output deterministic.
- The renderer should create missing whitelisted layers before drawing and should not modify unrelated existing layers in the user DWG.

## Line Type Standard

| Line Type | Source | Pattern Intent | Required For POC |
|---|---|---|---|
| `Continuous` | ZWCAD built-in/default template | Solid visible geometry and dimensions | Yes |
| `Center` | ZWCAD built-in or loaded from standard linetype file | Centerline long-short pattern | Yes |
| `Hidden` | ZWCAD built-in or loaded from standard linetype file | Hidden edge pattern | No, reserved |

If a line type is missing from the active DWG, the renderer should attempt to load it from the standard template or linetype file before rejecting the render. The POC must fail with a clear validation or render error rather than silently falling back to an incorrect line type.

## Text Styles

| Text Style | Font | Height mm | Width Factor | Oblique | Applies To |
|---|---|---:|---:|---:|---|
| `AI_NOTE_3_5` | `simplex.shx` or enterprise equivalent | 3.5 | 0.8 | 0 | General notes and process notes |
| `AI_DIM_TEXT_3_5` | `simplex.shx` or enterprise equivalent | 3.5 | 0.8 | 0 | Dimension text |
| `AI_TITLE_5` | `simplex.shx` or enterprise equivalent | 5.0 | 0.8 | 0 | Title block drawing name and primary attributes |
| `AI_TITLE_3_5` | `simplex.shx` or enterprise equivalent | 3.5 | 0.8 | 0 | Title block secondary attributes |

### Text Rules

- DrawingSpec text values must be plain annotation text, not executable CAD commands or scripts.
- Text height must be explicit in DrawingSpec or resolved from the named text style.
- Production logs must not capture sensitive note text by default; logs may record text entity ids and validation status.
- If the enterprise font is unavailable, the POC may use `simplex.shx` and must record the fallback in the render log.

## Dimension Styles

| Dimension Style | Text Style | Text Height mm | Arrow Size mm | Extension Offset mm | Extension Beyond mm | Precision | Applies To |
|---|---|---:|---:|---:|---:|---:|---|
| `AI_MECH_MM` | `AI_DIM_TEXT_3_5` | 3.5 | 3.0 | 1.0 | 1.5 | 0.0 | Linear and aligned dimensions |
| `AI_MECH_DIAMETER` | `AI_DIM_TEXT_3_5` | 3.5 | 3.0 | 1.0 | 1.5 | 0.0 | Diameter dimensions |
| `AI_MECH_RADIUS` | `AI_DIM_TEXT_3_5` | 3.5 | 3.0 | 1.0 | 1.5 | 0.0 | Radius dimensions |

### Dimension Rules

- Dimension values must match the model-space geometry within the configured tolerance.
- `DIM` is the only accepted layer for production dimensions in the MVP.
- Linear dimensions should use explicit `from`, `to`, and `offset` points.
- Diameter and radius dimensions should reference `targetEntityId` when the value is derived from a circle or arc.
- The renderer must reject dimensions that cannot be tied back to stable DrawingSpec entity ids.
- Manual override text is allowed only when the numeric geometry remains recoverable for validation.

## Center Mark Standard

| Item | Value |
|---|---|
| Layer | `CENTER` |
| Color | Inherit layer color index 1 |
| Line type | `Center` |
| Line weight | 0.18 mm |
| Default mark size | 10 mm |
| Default overshoot | 3 mm past circular feature radius when centerline geometry is generated |

Center marks and centerlines must be generated from explicit DrawingSpec entities or deterministic renderer options. The renderer must not infer missing hole centers from natural language once DrawingSpec validation has started.

## Title Block Template

The MVP uses a repo-local draft path until an enterprise template is supplied.

| Asset | Draft Path | Source | Status |
|---|---|---|---|
| A3 landscape title block template | `config/cad/templates/enterprise-default-v1/A3-landscape.dwt` | To be supplied by enterprise CAD administrator; POC may use a minimal generated template placeholder | Draft path only |
| A4 landscape title block template | `config/cad/templates/enterprise-default-v1/A4-landscape.dwt` | To be supplied by enterprise CAD administrator; POC may use a minimal generated template placeholder | Draft path only |
| Plot style table | `config/cad/plot-styles/enterprise-default-v1.ctb` | To be supplied by enterprise CAD administrator | Draft path only |

P1 and P2 code must not require these files to exist unless the specific task validates title block or PDF output. P7 deployment should install or document these paths.

## Block Library

The MVP does not require a large symbol library. Blocks are reserved for title block attributes and future reusable mechanical annotations.

| Block Category | Draft Path | Source | MVP Use |
|---|---|---|---|
| Title block attribute blocks | `config/cad/blocks/enterprise-default-v1/title-blocks/` | Enterprise CAD administrator or generated POC placeholder | Optional until P7 |
| Mechanical annotation blocks | `config/cad/blocks/enterprise-default-v1/mechanical/` | Enterprise CAD administrator | Not required for P1-P3 |
| Revision table blocks | `config/cad/blocks/enterprise-default-v1/revision/` | Enterprise CAD administrator | Not required for MVP drawing generation |

If an expected block is missing, the renderer must fail the affected title-block operation clearly and must not insert an unrelated block with the same name from the active DWG.

## Configuration Draft Paths

The code-facing configuration should be introduced as data files under a future `config/` tree. These files are not created by P0-03; this task defines the expected locations and contents so later implementation tasks can add them deliberately.

| Config File | Purpose |
|---|---|
| `config/cad/standards/enterprise-default-v1.json` | Machine-readable layer, line type, text style, dimension style, center mark, title block, and block library settings |
| `config/cad/templates/enterprise-default-v1/` | DWT/DWG template files for title blocks and plotting |
| `config/cad/blocks/enterprise-default-v1/` | Enterprise block library root |
| `config/cad/plot-styles/` | CTB/STB plot style files |
| `config/cad/README.md` | Administrator-facing explanation of how to override the default standard |

Expected top-level shape for `enterprise-default-v1.json`:

```json
{
  "standardId": "enterprise-default-v1",
  "domain": "mechanical_plate",
  "units": "mm",
  "layers": [],
  "lineTypes": [],
  "textStyles": [],
  "dimensionStyles": [],
  "centerMarks": {},
  "titleBlocks": {},
  "blockLibraries": {}
}
```

## Validation Implications

P2-03 business rule validation should use this document as the initial whitelist:

- Reject layer names outside the Layer Standard table.
- Reject dimensions on non-`DIM` layers.
- Reject center marks and centerlines on non-`CENTER` layers.
- Reject text styles and dimension styles that are not present in this profile.
- Reject title block or block paths outside the configured `config/cad/` roots unless an administrator-approved absolute or network path is configured.
- Report failures with stable DrawingSpec field paths and entity ids.

## Open Standard Gaps

The following items remain administrator decisions and must be resolved before P7 packaging or enterprise deployment:

- Official enterprise DWT/DWG title block templates.
- Official CTB/STB plot style files.
- Official SHX/TTF font requirements.
- Whether multiple departments need separate standard profiles.
- Whether title block metadata is required in the MVP output.
