# JSON Parameter Schemas

This folder owns `schema-index.json` and its index schema. Most algorithm
schemas live beside their template under `Templates/Jsons/`, for example
`LedCheck2/FindLED.schema.json`.

The shared editor uses optional schema metadata for labels, descriptions,
enums and numeric range hints. It does not populate missing fields from
`default` or perform full JSON Schema validation. Template defaults come from
the database dictionary. Keep algorithm field names intact when improving
descriptions; provider/default-file source metadata is a maintenance snapshot,
not automatic synchronization with a DLL or database.

The authoritative [JSON template reference](../../../../../docs/04-api-reference/algorithms/templates/json-templates.md)
describes supported keywords, editing and persistence limits, schema lookup,
and maintenance examples. It also records the active HDR template's index and
embedding path mismatch: its schema is under `Services/Devices/Camera/Templates/HDR/`,
outside the normal `Templates/Jsons/` resource glob. KB uses a dedicated editor.
