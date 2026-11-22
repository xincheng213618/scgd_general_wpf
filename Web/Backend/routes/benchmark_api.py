"""Desktop benchmark upload and privacy-minimized public comparisons."""

from __future__ import annotations

from flask import Blueprint, jsonify, request
from werkzeug.exceptions import RequestEntityTooLarge

from db.repositories.benchmarks import BenchmarkConflictError, SqliteBenchmarkRepository
from routes.request_context import current_request_context, set_authenticated_request_context
from services.api_key_service import api_key_actor_id, verify_api_key
from services.benchmark_service import (
    benchmark_source_hash, benchmark_token_hash, check_benchmark_plausibility, parse_benchmark_id, parse_benchmark_submission,
)
from services.request_context import Actor


MAX_BENCHMARK_BYTES = 16 * 1024
SUBMIT_SCOPE = "benchmark:submit"


def register_benchmark_api(app, cache):
    blueprint = Blueprint("benchmark_api", __name__)
    results = SqliteBenchmarkRepository(cache.get_db)

    @blueprint.post("/api/benchmarks/runs")
    def submit_benchmark_run():
        context = current_request_context()
        token_hash = benchmark_token_hash(context.bearer_token)
        key_id = None
        if token_hash is not None:
            retry = results.charge_anonymous_attempt(token_hash, benchmark_source_hash(context.remote_addr, app.secret_key))
            if retry:
                return jsonify({"error": "Too many benchmark submissions. Try again later.", "code": "rate_limited"}), 429, {
                    "Retry-After": str(retry),
                }
        else:
            key = verify_api_key(cache, context.bearer_token) if context.bearer_token else None
            if key is None:
                return jsonify({"error": "Bearer authentication required", "code": "authentication_required"}), 401
            scopes = frozenset(scope.strip() for scope in str(key.get("scopes") or "").split(","))
            if SUBMIT_SCOPE not in scopes:
                return jsonify({"error": "Insufficient scope", "code": "insufficient_scope", "required": [SUBMIT_SCOPE]}), 403
            key_id = key["id"]
            set_authenticated_request_context(context.with_actor(Actor(
                actor_type="api_key", actor_id=api_key_actor_id(key), auth_method="bearer",
                role="api_key", scopes=scopes, authenticated=True,
            )))
        try:
            if request.content_length is not None and request.content_length > MAX_BENCHMARK_BYTES:
                raise RequestEntityTooLarge()
            submission = parse_benchmark_submission(request.get_json(silent=True))
            if token_hash is not None or submission.publish_comparison:
                check_benchmark_plausibility(submission)
        except RequestEntityTooLarge:
            return jsonify({"error": "Benchmark payload is too large", "code": "request_too_large"}), 413
        except (ValueError, OverflowError) as exc:
            return jsonify({"error": str(exc), "code": "invalid_benchmark"}), 400
        try:
            run, created, published = results.submit(key_id, submission, token_hash=token_hash)
        except BenchmarkConflictError as exc:
            return jsonify({"error": "Benchmark identity or payload conflicts with a prior submission", "code": str(exc)}), 409
        return jsonify({
            "runId": run["id"],
            "overallScore": run["overallScore"],
            "scoringVersion": run["scoringVersion"],
            "publishedComparison": published,
        }), 201 if created else 200

    @blueprint.get("/api/benchmarks/comparison")
    def benchmark_comparison():
        exclude = request.args.get("excludeDeviceId")
        include = request.args.get("includeRunId")
        if exclude is not None:
            try:
                exclude = parse_benchmark_id(exclude, "excludeDeviceId")
            except ValueError:
                return jsonify({"error": "excludeDeviceId must be a nonzero UUID", "code": "invalid_device_id"}), 400
        if include is not None:
            try:
                include = parse_benchmark_id(include, "includeRunId")
            except ValueError:
                return jsonify({"error": "includeRunId must be a nonzero UUID", "code": "invalid_run_id"}), 400
        return jsonify(results.comparison(exclude, include)), 200, {"Cache-Control": "no-store"}

    app.register_blueprint(blueprint)
