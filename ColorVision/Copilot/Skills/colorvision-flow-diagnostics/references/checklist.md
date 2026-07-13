# Cross-layer diagnostic checklist

- Flow: running/failed/ignored state, current node, elapsed time, timeout, `ContinueOnFail`.
- Device: configured instance, connection state, last heartbeat, command acknowledgement.
- Data: input artifact exists, output schema matches, parser returned a usable result.
- Downstream: failure was propagated, ignored deliberately, or converted into an empty/default result.
- Evidence: include timestamps and stable identifiers when logs provide them; redact credentials and tokens.
