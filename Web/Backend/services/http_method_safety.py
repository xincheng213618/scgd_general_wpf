"""HTTP method policy for routes whose GET handlers have side effects."""

from __future__ import annotations


_UNSAFE_AUTOMATIC_HEAD_ENDPOINTS = frozenset({
    "operations_relay.create_task",
    "operations_relay.poll_tasks",
})


def disable_unsafe_automatic_head(app) -> None:
    """Return 405 for HEAD instead of executing side-effectful GET handlers."""
    matched_endpoints = set()
    for rule in app.url_map.iter_rules():
        if rule.endpoint in _UNSAFE_AUTOMATIC_HEAD_ENDPOINTS:
            rule.methods.discard("HEAD")
            matched_endpoints.add(rule.endpoint)

    missing_endpoints = _UNSAFE_AUTOMATIC_HEAD_ENDPOINTS - matched_endpoints
    if missing_endpoints:
        missing = ", ".join(sorted(missing_endpoints))
        raise RuntimeError(f"HTTP method policy references missing endpoints: {missing}")
