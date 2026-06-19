# JSON Parameter Schemas

This folder contains the schema index and maintenance notes for JSON Schema
files used by ColorVision JSON template editors.

The actual algorithm schema files live beside their corresponding template
folders under `Engine/ColorVision.Engine/Templates/Jsons/`. For example,
`FindLED` is maintained at `LedCheck2/FindLED.schema.json`, and
`LEDStripDetection` is maintained at
`LEDStripDetectionV2/LEDStripDetection.schema.json`.

These files are optional UI metadata. They do not change the C++ JSON string
interface. If a provider maintains a schema, the runtime can use it for better
labels, grouping, defaults, validation hints, units, and descriptions while
still passing JSON to the algorithm unchanged.

Generation notes:

- `KB` is intentionally excluded because it already has a dedicated editor.
- Template folders under `Deprecated` are intentionally excluded.
- `schema-index.json` lists every generated schema and its template folder.
- If a provider test-case `defaultparam.txt` exists, `default` and
  `properties` are sourced from that provider-maintained file.
- Otherwise, `default` values mirror the database `cfg_json` at generation
  time.
- `additionalProperties` is kept as `true` so newer DLLs can add JSON fields.
- Providers can edit `title`, `description`, `minimum`, `maximum`, `enum`,
  `unit`, and `x-ui` metadata without changing the algorithm interface.
- Unknown JSON fields should still be preserved by the editor.

Suggested provider-owned fields:

```json
{
  "title": "Min Area",
  "description": "Minimum connected component area.",
  "type": "integer",
  "minimum": 0,
  "unit": "px",
  "x-ui": {
    "order": 10,
    "group": "Blob",
    "advanced": false
  }
}
```

Recommended AI prompt for providers:

```text
You are maintaining a ColorVision algorithm JSON Schema.
The C++ interface receives the JSON string directly, so do not rename JSON
property keys, and do not set additionalProperties to false.

Please enrich this schema only by adding or improving:
- title
- description
- unit
- minimum / maximum
- enum and enum descriptions
- examples
- x-ui grouping/order/advanced hints

When the provider-maintained defaultparam.txt changes, update default,
properties, and type together so the schema remains aligned with the DLL.
```

Provider and AI guidance fields:

- `description`: user-facing explanation that can be shown in UI.
- `$comment`: maintainer-only note; validators and UI can ignore it.
- `x-provider`: ColorVision/provider extension for JSON path and AI hints.
- `x-colorvision.providerSchema`: root-level maintenance rules for providers.
