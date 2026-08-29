"""
Authentication routes for the React Web frontend.

The login UI is rendered by the SPA. These routes only manage Flask session
state and keep the historical /login and /logout URLs usable.
"""

from __future__ import annotations

import hmac
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Callable
from urllib.parse import unquote, urlencode

from flask import Blueprint, current_app, jsonify, redirect, request, send_from_directory, session

from db_cache import CacheManager


@dataclass(frozen=True)
class PublicPageContext:
    cache: CacheManager
    storage: Path
    config_getter: Callable[[], dict[str, Any]]
    get_upload_auth: Callable[[], tuple[str, str]]
    check_web_session_auth: Callable[[], bool]
    dist_dir: Path


@dataclass(frozen=True)
class LoginOutcome:
    payload: dict[str, Any] | None
    status_code: int
    error: str = ""
    retry_after: int = 0
    attempts_remaining: int | None = None


public_pages = Blueprint("public_pages", __name__)

_ctx: PublicPageContext | None = None


def _get_ctx() -> PublicPageContext:
    if _ctx is None:
        raise RuntimeError("Public pages not initialized")
    return _ctx


def _safe_next_url(raw: str | None) -> str:
    candidate = str(raw or "").strip()
    if not candidate.startswith("/") or candidate.startswith("//"):
        return "/admin"
    if any(ord(character) < 32 or ord(character) == 127 for character in candidate):
        return "/admin"

    normalized_path = candidate.split("#", 1)[0].split("?", 1)[0]
    for _ in range(3):
        decoded_path = unquote(normalized_path)
        if decoded_path == normalized_path:
            break
        normalized_path = decoded_path
    if normalized_path.startswith("//") or "\\" in normalized_path:
        return "/admin"
    if any(ord(character) < 32 or ord(character) == 127 for character in normalized_path):
        return "/admin"
    return candidate


def _bounded_query_int(
    name: str,
    default: int,
    *,
    minimum: int,
    maximum: int,
) -> int:
    try:
        value = int(request.args.get(name, default))
    except (TypeError, ValueError) as exc:
        raise ValueError(f"{name} must be an integer") from exc
    if value < minimum:
        raise ValueError(f"{name} must be at least {minimum}")
    if value > maximum:
        raise ValueError(f"{name} must be at most {maximum}")
    return value


def _serve_spa_index():
    dist = _get_ctx().dist_dir
    if not dist.exists():
        if current_app.config.get("TESTING"):
            return (
                '<!doctype html><html lang="zh-CN"><body><div id="root"></div></body></html>',
                200,
                {"Content-Type": "text/html; charset=utf-8"},
            )
        return (
            "Web frontend has not been built. Run `npm install` and `npm run build` in Web/Frontend.",
            503,
            {"Content-Type": "text/plain; charset=utf-8"},
        )
    return send_from_directory(dist, "index.html")


def _session_payload() -> dict[str, Any]:
    from services.csrf_protection import issue_csrf_token
    from services.account_settings import is_public_registration_enabled
    from transfer_files import get_anonymous_transfer_max_bytes, is_anonymous_transfer_upload_enabled

    from services.permission_service import (
        ALL_PERMISSION_CODES,
        get_role_permission_codes,
        role_can_access_admin,
    )

    is_admin = bool(session.get("authenticated"))
    is_user = bool(session.get("user_authenticated") or is_admin)
    must_change_password = bool(
        is_user
        and session.get("user_id") is not None
        and session.get("must_change_password")
    )
    role = str(session.get("role") or ("admin" if is_admin else ("user" if is_user else "")))
    if not is_user or must_change_password:
        permissions: list[str] = []
        can_access_admin = False
    elif is_admin:
        permissions = sorted(ALL_PERMISSION_CODES)
        can_access_admin = True
    else:
        permissions = sorted(get_role_permission_codes(_get_ctx().cache, role))
        can_access_admin = role_can_access_admin(_get_ctx().cache, role)
    config = _get_ctx().config_getter()
    return {
        "authenticated": is_user,
        "is_admin": is_admin,
        "can_access_admin": can_access_admin,
        "username": session.get("username", ""),
        "role": role,
        "must_change_password": must_change_password,
        "permissions": permissions,
        "public_registration_enabled": is_public_registration_enabled(
            config
        ),
        "anonymous_transfer_upload_enabled": is_anonymous_transfer_upload_enabled(config),
        "anonymous_transfer_max_bytes": get_anonymous_transfer_max_bytes(config),
        "csrf_token": issue_csrf_token(),
    }


def _set_login_session(user: dict[str, Any]) -> dict[str, Any]:
    role = str(user.get("role") or "user")
    is_admin = role == "admin"
    session.clear()
    session["user_authenticated"] = True
    session["username"] = str(user.get("username") or "")
    session["role"] = role
    session["must_change_password"] = bool(user.get("must_change_password"))
    if user.get("id") is not None:
        session["user_id"] = user["id"]
        session["auth_version"] = int(user.get("auth_version") or 0)
        from services.session_service import create_user_session

        session["login_session_id"] = create_user_session(
            _get_ctx().cache,
            int(user["id"]),
            auth_version=int(user.get("auth_version") or 0),
            ip_address=request.remote_addr or "",
            user_agent=request.headers.get("User-Agent", ""),
        )
    if is_admin:
        session["authenticated"] = True
    return _session_payload()


def _session_account_requires_validation(path: str) -> bool:
    return (
        path in {"/admin", "/account", "/transfer", "/browse", "/upload"}
        or path.startswith((
            "/api/",
            "/admin/",
            "/account/",
            "/transfer/",
            "/browse/",
            "/download/",
            "/upload/",
        ))
    )


def _synchronize_session_account() -> None:
    """Clear disabled database-backed sessions and refresh their role metadata."""
    if not session.get("user_authenticated") or not _session_account_requires_validation(request.path):
        return

    user_id = session.get("user_id")
    if user_id is None:
        return
    try:
        normalized_user_id = int(user_id)
    except (TypeError, ValueError):
        session.clear()
        return

    from services.auth_service import get_user_by_id

    user = get_user_by_id(_get_ctx().cache, normalized_user_id)
    if not user or not user.get("is_active"):
        session.clear()
        return

    auth_version = int(user.get("auth_version") or 0)
    session_auth_version = session.get("auth_version")
    if session_auth_version is None:
        # Preserve pre-migration sessions only while the account has never had
        # a security-sensitive change. A reset/change performed immediately
        # after deployment must still revoke an older cookie.
        if auth_version > 0:
            session.clear()
            return
        session["auth_version"] = 0
    else:
        try:
            normalized_session_version = int(session_auth_version)
        except (TypeError, ValueError):
            session.clear()
            return
        if normalized_session_version != auth_version:
            session.clear()
            return

    from services.session_service import create_user_session, validate_user_session

    login_session_id = str(session.get("login_session_id") or "")
    if login_session_id:
        valid_session = validate_user_session(
            _get_ctx().cache,
            login_session_id,
            normalized_user_id,
            auth_version=auth_version,
            ip_address=request.remote_addr or "",
            user_agent=request.headers.get("User-Agent", ""),
        )
        if not valid_session:
            session.clear()
            return
    else:
        # Upgrade an existing signed cookie without forcing the user to log in.
        login_session_id = create_user_session(
            _get_ctx().cache,
            normalized_user_id,
            auth_version=auth_version,
            ip_address=request.remote_addr or "",
            user_agent=request.headers.get("User-Agent", ""),
        )
        session["login_session_id"] = login_session_id

    username = str(user.get("username") or "")
    role = str(user.get("role") or "user")
    expected = {
        "user_authenticated": True,
        "username": username,
        "role": role,
        "user_id": normalized_user_id,
        "auth_version": auth_version,
        "must_change_password": bool(user.get("must_change_password")),
    }
    if role == "admin":
        expected["authenticated"] = True
    for key, value in expected.items():
        if session.get(key) != value:
            session[key] = value
    if role != "admin" and "authenticated" in session:
        session.pop("authenticated", None)


def _redirect_for_role(next_url: str, payload: dict[str, Any]) -> str:
    if payload.get("must_change_password"):
        return "/account?password_change=required"
    if not payload.get("can_access_admin") and next_url.startswith("/admin"):
        return "/account"
    return next_url


def _same_username(left: str, right: str) -> bool:
    if not left or not right:
        return False
    return hmac.compare_digest(
        left.casefold().encode("utf-8"),
        right.casefold().encode("utf-8"),
    )


def _same_secret(left: str, right: str) -> bool:
    return hmac.compare_digest(left.encode("utf-8"), right.encode("utf-8"))


def _login_error_response(outcome: LoginOutcome):
    payload: dict[str, Any] = {
        "error": outcome.error,
        "status": outcome.status_code,
    }
    if outcome.attempts_remaining is not None:
        payload["attempts_remaining"] = outcome.attempts_remaining
    if outcome.retry_after > 0:
        payload["retry_after"] = outcome.retry_after
    response = jsonify(payload)
    if outcome.retry_after > 0:
        response.headers["Retry-After"] = str(outcome.retry_after)
    return response, outcome.status_code


def _registration_rate_limit_response(status):
    response = jsonify({
        "error": "注册请求过于频繁，请稍后再试",
        "status": 429,
        "retry_after": status.retry_after,
    })
    response.headers["Retry-After"] = str(status.retry_after)
    return response, 429


def _finalize_registration_rate_limit(source_ip: str, reservation, *, succeeded: bool):
    from services.registration_rate_limit_service import finalize_registration_attempt

    try:
        finalized = finalize_registration_attempt(
            _get_ctx().cache,
            source_ip,
            succeeded=succeeded,
        )
    except Exception:
        current_app.logger.exception("Unable to finalize registration rate-limit reservation")
        return None
    if not (reservation.attempt_limit_reached or finalized.success_limit_reached):
        return finalized

    reasons = []
    if reservation.attempt_limit_reached:
        reasons.append("attempt_velocity")
    if finalized.success_limit_reached:
        reasons.append("success_velocity")
    _get_ctx().cache.write_audit(
        actor_type="anonymous",
        actor_id="",
        action="registration_throttled",
        target_type="registration",
        detail=(
            f"reason={'+'.join(reasons)};retry_after={finalized.retry_after};"
            f"attempts_remaining={finalized.attempts_remaining};"
            f"successes_remaining={finalized.successes_remaining}"
        ),
        ip=source_ip,
        user_agent=request.headers.get("User-Agent", "")[:200],
    )
    return finalized


def _login(username: str, password: str) -> LoginOutcome:
    from services.login_throttle_service import (
        clear_login_failures,
        get_login_throttle_status,
        record_login_failure,
    )

    ctx = _get_ctx()
    source_ip = request.remote_addr or ""
    user_agent = request.headers.get("User-Agent", "")[:200]
    throttle = get_login_throttle_status(ctx.cache, username)
    if throttle.locked:
        return LoginOutcome(
            payload=None,
            status_code=429,
            error="登录尝试过于频繁，请稍后再试",
            retry_after=throttle.retry_after,
            attempts_remaining=0,
        )

    expected_username, expected_password = ctx.get_upload_auth()
    is_config_admin = _same_username(username, expected_username)
    if is_config_admin and expected_password and _same_secret(password, expected_password):
        clear_login_failures(ctx.cache, username)
        payload = _set_login_session({"username": expected_username, "role": "admin"})
        ctx.cache.write_audit(
            actor_type="user",
            actor_id=expected_username,
            action="login_success",
            target_type="session",
            detail="role=admin;source=config",
            ip=source_ip,
            user_agent=user_agent,
        )
        return LoginOutcome(payload=payload, status_code=200)

    user: dict[str, Any] | None = None
    if not is_config_admin:
        try:
            from services.auth_service import verify_user_credentials

            user = verify_user_credentials(ctx.cache, username, password)
        except Exception:
            pass

    if user:
        clear_login_failures(ctx.cache, username)
        payload = _set_login_session(user)
        ctx.cache.write_audit(
            actor_type="user",
            actor_id=str(user.get("username") or ""),
            action="login_success",
            target_type="session",
            target_id=str(session.get("login_session_id") or ""),
            detail=f"role={user.get('role') or 'user'}",
            ip=source_ip,
            user_agent=user_agent,
        )
        return LoginOutcome(payload=payload, status_code=200)

    throttle = record_login_failure(ctx.cache, username, source_ip)
    ctx.cache.write_audit(
        actor_type="anonymous",
        actor_id=username or "",
        action="login_failed",
        target_type="session",
        detail="Invalid credentials",
        ip=source_ip,
        user_agent=user_agent,
    )
    if throttle.locked:
        ctx.cache.write_audit(
            actor_type="anonymous",
            actor_id=username or "",
            action="login_throttled",
            target_type="session",
            detail=(
                f"failed_count={throttle.failed_count} "
                f"retry_after={throttle.retry_after}"
            ),
            ip=source_ip,
            user_agent=user_agent,
        )
        return LoginOutcome(
            payload=None,
            status_code=429,
            error="登录尝试过于频繁，请稍后再试",
            retry_after=throttle.retry_after,
            attempts_remaining=0,
        )
    return LoginOutcome(
        payload=None,
        status_code=401,
        error="用户名或密码错误",
        attempts_remaining=throttle.attempts_remaining,
    )


@public_pages.route("/login", methods=["GET", "POST"])
def login_page():
    if request.method in {"GET", "HEAD"}:
        return _serve_spa_index()

    if request.is_json:
        data = request.get_json(silent=True) or {}
        username = str(data.get("username", "")).strip()
        password = str(data.get("password", ""))
        next_url = _safe_next_url(str(data.get("next", "") or request.args.get("next", "")))
        outcome = _login(username, password)
        if outcome.payload:
            outcome.payload["next"] = _redirect_for_role(next_url, outcome.payload)
            return jsonify(outcome.payload)
        return _login_error_response(outcome)

    username = request.form.get("username", "").strip()
    password = request.form.get("password", "")
    next_url = _safe_next_url(request.form.get("next") or request.args.get("next"))
    outcome = _login(username, password)
    if outcome.payload:
        return redirect(_redirect_for_role(next_url, outcome.payload))
    query = {"next": next_url}
    if outcome.retry_after > 0:
        query["retry_after"] = str(outcome.retry_after)
    return redirect(f"/login?{urlencode(query)}")


@public_pages.route("/register", methods=["GET"])
def register_page():
    return redirect("/login?mode=register")


@public_pages.route("/api/auth/session", methods=["GET"])
def api_auth_session():
    return jsonify(_session_payload())


@public_pages.route("/api/auth/login", methods=["POST"])
def api_auth_login():
    data = request.get_json(silent=True) or {}
    username = str(data.get("username", "")).strip()
    password = str(data.get("password", ""))
    next_url = _safe_next_url(str(data.get("next", "") or ""))
    outcome = _login(username, password)
    if outcome.payload:
        outcome.payload["next"] = _redirect_for_role(next_url, outcome.payload)
        return jsonify(outcome.payload)
    return _login_error_response(outcome)


@public_pages.route("/api/auth/register", methods=["POST"])
def api_auth_register():
    from services.account_settings import is_public_registration_enabled
    from services.registration_rate_limit_service import reserve_registration_attempt

    if not is_public_registration_enabled(_get_ctx().config_getter()):
        return jsonify({
            "error": "公开注册已关闭，请联系管理员创建账号",
            "status": 403,
        }), 403

    source_ip = request.remote_addr or ""
    try:
        reservation = reserve_registration_attempt(_get_ctx().cache, source_ip)
    except Exception:
        current_app.logger.exception("Unable to reserve registration rate-limit slot")
        return jsonify({"error": "注册服务暂时不可用", "status": 503}), 503
    if not reservation.allowed:
        return _registration_rate_limit_response(reservation)

    data = request.get_json(silent=True) or {}
    username = str(data.get("username", "")).strip()
    password = str(data.get("password", ""))
    display_name = str(data.get("display_name", ""))
    email = str(data.get("email", ""))
    next_url = _safe_next_url(str(data.get("next", "") or "/account"))

    expected_username, _ = _get_ctx().get_upload_auth()
    if _same_username(username, expected_username):
        _finalize_registration_rate_limit(source_ip, reservation, succeeded=False)
        return jsonify({"error": "用户名已存在", "status": 400}), 400

    from services.auth_service import create_user
    user, error = create_user(
        _get_ctx().cache,
        username,
        password,
        role="user",
        display_name=display_name,
        email=email,
        account_origin="self_registered",
    )
    if error or not user:
        _finalize_registration_rate_limit(source_ip, reservation, succeeded=False)
        return jsonify({"error": error or "注册失败", "status": 400}), 400

    _finalize_registration_rate_limit(source_ip, reservation, succeeded=True)
    payload = _set_login_session(user)
    payload["next"] = _redirect_for_role(next_url, payload)
    _get_ctx().cache.write_audit(
        actor_type="user",
        actor_id=payload.get("username", ""),
        action="user_register",
        target_type="user",
        target_id=payload.get("username", ""),
        ip=request.remote_addr or "",
        user_agent=request.headers.get("User-Agent", "")[:200],
    )
    return jsonify(payload), 201


@public_pages.route("/api/auth/password-recovery", methods=["POST"])
def api_auth_password_recovery():
    """Accept an administrator-assisted recovery request without account disclosure."""
    data = request.get_json(silent=True) or {}
    identifier = str(data.get("identifier") or "").strip()
    if not identifier:
        return jsonify({"error": "请输入用户名或邮箱", "status": 400}), 400
    if len(identifier) > 254:
        return jsonify({"error": "用户名或邮箱不能超过 254 个字符", "status": 400}), 400

    from services.password_recovery_service import (
        RECOVERY_SOURCE_ATTEMPT_WINDOW,
        reserve_password_recovery_attempt,
        submit_password_recovery_request,
    )

    source_ip = request.remote_addr or ""
    try:
        rate_status = reserve_password_recovery_attempt(_get_ctx().cache, source_ip)
    except Exception:
        current_app.logger.exception("Unable to reserve password recovery rate-limit slot")
        return jsonify({"error": "密码找回服务暂时不可用", "status": 503}), 503

    if not rate_status.allowed:
        response = jsonify({
            "error": "密码找回请求过于频繁，请稍后再试",
            "status": 429,
            "retry_after": rate_status.retry_after,
        })
        response.headers["Retry-After"] = str(rate_status.retry_after)
        return response, 429

    if rate_status.limit_reached:
        _get_ctx().cache.write_audit(
            actor_type="anonymous",
            actor_id="",
            action="password_recovery_throttled",
            target_type="password_recovery",
            detail=(
                "reason=attempt_velocity;"
                f"retry_after={int(RECOVERY_SOURCE_ATTEMPT_WINDOW.total_seconds())};"
                "attempts_remaining=0"
            ),
            ip=source_ip,
            user_agent=request.headers.get("User-Agent", "")[:200],
        )

    try:
        submission = submit_password_recovery_request(
            _get_ctx().cache,
            identifier,
            ip_address=source_ip,
        )
    except Exception:
        current_app.logger.exception("Unable to persist password recovery request")
        return jsonify({"error": "密码找回服务暂时不可用", "status": 503}), 503

    if submission.matched and submission.recorded:
        _get_ctx().cache.write_audit(
            actor_type="anonymous",
            actor_id="password_recovery",
            action="user_password_recovery_request",
            target_type="user",
            target_id=str(submission.user_id or ""),
            detail=(
                f"username={submission.username};"
                f"request_count={submission.request_count}"
            ),
            ip=source_ip,
            user_agent=request.headers.get("User-Agent", "")[:200],
        )
    return jsonify({
        "status": "accepted",
        "message": (
            "如果账号存在且可用，管理员会在用户管理中看到找回申请。"
            "请联系管理员获取临时密码。"
        ),
    }), 202


def _current_account():
    if not (session.get("user_authenticated") or session.get("authenticated")):
        return None
    user_id = session.get("user_id")
    if user_id is None:
        return None
    try:
        normalized_user_id = int(user_id)
    except (TypeError, ValueError):
        return None
    from services.auth_service import get_user_by_id

    return get_user_by_id(_get_ctx().cache, normalized_user_id)


def _account_profile_payload(account):
    from services.permission_service import describe_permissions

    session_payload = _session_payload()
    return {
        "username": session_payload["username"],
        "display_name": account.get("display_name", "") if account else "",
        "email": account.get("email", "") if account else "",
        "account_origin": account.get("account_origin", "legacy") if account else None,
        "role": session_payload["role"],
        "is_admin": session_payload["is_admin"],
        "can_access_admin": session_payload["can_access_admin"],
        "permissions": session_payload["permissions"],
        "permission_details": describe_permissions(session_payload["permissions"]),
        "created_at": account.get("created_at") if account else None,
        "updated_at": account.get("updated_at") if account else None,
        "last_login_at": account.get("last_login_at") if account else None,
        "password_changed_at": account.get("password_changed_at") if account else None,
        "can_change_password": account is not None,
        "can_edit_profile": account is not None,
        "can_manage_sessions": account is not None,
        "must_change_password": session_payload["must_change_password"],
    }


@public_pages.route("/api/account", methods=["GET", "PUT"])
def api_account_profile():
    if not (session.get("user_authenticated") or session.get("authenticated")):
        return jsonify({"error": "Authentication required", "status": 401}), 401

    account = _current_account()
    if request.method == "GET":
        return jsonify(_account_profile_payload(account))

    if account is None:
        return jsonify({
            "error": "当前管理员仍由配置文件管理，不能在个人中心修改账号资料",
            "status": 409,
        }), 409

    data = request.get_json(silent=True) or {}
    from services.auth_service import update_user_profile

    updated, error = update_user_profile(
        _get_ctx().cache,
        int(account["id"]),
        display_name=str(data.get("display_name") or ""),
        email=str(data.get("email") or ""),
    )
    if error == "user_not_found":
        return jsonify({"error": "账号不存在", "status": 404}), 404
    if error == "profile_update_failed":
        return jsonify({"error": "个人资料更新失败", "status": 500}), 500
    if error or not updated:
        return jsonify({"error": error or "个人资料更新失败", "status": 400}), 400

    _get_ctx().cache.write_audit(
        actor_type="user",
        actor_id=str(updated.get("username") or ""),
        action="user_profile_update",
        target_type="user",
        target_id=str(updated.get("id") or ""),
        detail="fields=display_name,email",
        ip=request.remote_addr or "",
        user_agent=request.headers.get("User-Agent", "")[:200],
    )
    return jsonify(_account_profile_payload(updated))


@public_pages.route("/api/account/sessions", methods=["GET"])
def api_account_sessions():
    account = _current_account()
    if account is None:
        if session.get("user_authenticated") or session.get("authenticated"):
            return jsonify({
                "error": "当前管理员仍由配置文件管理，暂无独立会话列表",
                "status": 409,
            }), 409
        return jsonify({"error": "Authentication required", "status": 401}), 401

    from services.session_service import list_user_sessions

    items = list_user_sessions(
        _get_ctx().cache,
        int(account["id"]),
        current_session_id=str(session.get("login_session_id") or ""),
    )
    return jsonify({"items": items, "total": len(items)})


@public_pages.route("/api/account/sessions/others", methods=["DELETE"])
def api_account_sessions_revoke_others():
    account = _current_account()
    if account is None:
        return jsonify({"error": "Authentication required", "status": 401}), 401

    from services.session_service import revoke_other_user_sessions

    revoked = revoke_other_user_sessions(
        _get_ctx().cache,
        int(account["id"]),
        current_session_id=str(session.get("login_session_id") or ""),
    )
    _get_ctx().cache.write_audit(
        actor_type="user",
        actor_id=str(account.get("username") or ""),
        action="user_sessions_revoke_others",
        target_type="session",
        target_id=str(account["id"]),
        detail=f"revoked={revoked}",
        ip=request.remote_addr or "",
        user_agent=request.headers.get("User-Agent", "")[:200],
    )
    return jsonify({"status": "revoked", "revoked": revoked})


@public_pages.route("/api/account/sessions/<session_id>", methods=["DELETE"])
def api_account_session_revoke(session_id: str):
    account = _current_account()
    if account is None:
        return jsonify({"error": "Authentication required", "status": 401}), 401
    if not session_id or len(session_id) > 128:
        return jsonify({"error": "登录会话不存在", "status": 404}), 404

    from services.session_service import revoke_user_session

    revoked, error = revoke_user_session(
        _get_ctx().cache,
        int(account["id"]),
        session_id,
        current_session_id=str(session.get("login_session_id") or ""),
    )
    if error == "current_session":
        return jsonify({"error": "请使用退出登录结束当前会话", "status": 409}), 409
    if error == "session_not_found" or not revoked:
        return jsonify({"error": "登录会话不存在", "status": 404}), 404

    _get_ctx().cache.write_audit(
        actor_type="user",
        actor_id=str(account.get("username") or ""),
        action="user_session_revoke",
        target_type="session",
        target_id=session_id,
        ip=request.remote_addr or "",
        user_agent=request.headers.get("User-Agent", "")[:200],
    )
    return jsonify({"status": "revoked", "id": session_id})


@public_pages.route("/api/account/activity", methods=["GET"])
def api_account_activity():
    if not (session.get("user_authenticated") or session.get("authenticated")):
        return jsonify({"error": "Authentication required", "status": 401}), 401
    try:
        limit = _bounded_query_int("limit", 8, minimum=1, maximum=50)
        offset = _bounded_query_int("offset", 0, minimum=0, maximum=100000)
    except ValueError as exc:
        return jsonify({"error": str(exc), "status": 400}), 400

    username = str(session.get("username") or "")
    account = _current_account()
    from services.account_activity_service import get_account_activity_page

    return jsonify(get_account_activity_page(
        _get_ctx().cache,
        username=username,
        user_id=int(account["id"]) if account else None,
        limit=limit,
        offset=offset,
    ))


@public_pages.route("/api/account/password", methods=["PUT"])
def api_account_password():
    account = _current_account()
    if account is None:
        if session.get("user_authenticated") or session.get("authenticated"):
            return jsonify({
                "error": "当前管理员仍由配置文件管理，请在系统配置中修改密码",
                "status": 409,
            }), 409
        return jsonify({"error": "Authentication required", "status": 401}), 401

    data = request.get_json(silent=True) or {}
    current_password = str(data.get("current_password") or "")
    new_password = str(data.get("new_password") or "")
    from services.auth_service import change_user_password

    updated, error = change_user_password(
        _get_ctx().cache,
        int(account["id"]),
        current_password=current_password,
        new_password=new_password,
    )
    if error or not updated:
        status = 404 if error == "user_not_found" else 500 if error in {
            "password_service_unavailable",
            "password_change_failed",
        } else 400
        return jsonify({"error": error or "密码修改失败", "status": status}), status

    session["auth_version"] = int(updated.get("auth_version") or 0)
    session["must_change_password"] = False
    from services.session_service import (
        create_user_session,
        revoke_all_user_sessions,
        restore_current_user_session,
    )

    login_session_id = str(session.get("login_session_id") or "")
    revoked_sessions = revoke_all_user_sessions(
        _get_ctx().cache,
        int(account["id"]),
        reason="password_changed",
    )
    restored_current_session = False
    if login_session_id:
        restored_current_session = restore_current_user_session(
            _get_ctx().cache,
            int(account["id"]),
            login_session_id,
            auth_version=int(updated.get("auth_version") or 0),
        )
    if not restored_current_session:
        session["login_session_id"] = create_user_session(
            _get_ctx().cache,
            int(account["id"]),
            auth_version=int(updated.get("auth_version") or 0),
            ip_address=request.remote_addr or "",
            user_agent=request.headers.get("User-Agent", ""),
        )
    try:
        from services.password_recovery_service import resolve_password_recovery_requests

        resolve_password_recovery_requests(
            _get_ctx().cache,
            int(account["id"]),
            resolved_by=str(updated.get("username") or ""),
            resolution="self_password_change",
        )
    except Exception:
        current_app.logger.exception("Unable to resolve password recovery after self change")
    _get_ctx().cache.write_audit(
        actor_type="user",
        actor_id=str(updated.get("username") or ""),
        action="user_password_change",
        target_type="user",
        target_id=str(updated.get("id") or ""),
        detail=(
            "sessions_invalidated=true;"
            f"revoked={max(0, revoked_sessions - (1 if restored_current_session else 0))};"
            "current_session_preserved=true"
        ),
        ip=request.remote_addr or "",
        user_agent=request.headers.get("User-Agent", "")[:200],
    )
    return jsonify({
        "status": "updated",
        "current_session_preserved": True,
        "must_change_password": False,
    })


@public_pages.route("/api/auth/logout", methods=["POST"])
def api_auth_logout():
    _clear_login_session()
    return jsonify(_session_payload())


@public_pages.route("/logout", methods=["GET", "POST"])
def logout_page():
    if request.method == "POST":
        _clear_login_session()
    return redirect("/")


def _clear_login_session() -> None:
    username = str(session.get("username") or "")
    user_id = session.get("user_id")
    login_session_id = str(session.get("login_session_id") or "")
    if user_id is not None and login_session_id:
        try:
            from services.session_service import revoke_current_user_session

            revoke_current_user_session(
                _get_ctx().cache,
                int(user_id),
                login_session_id,
            )
        except Exception:
            pass
    if username:
        _get_ctx().cache.write_audit(
            actor_type="user",
            actor_id=username,
            action="user_logout",
            target_type="session",
            target_id=login_session_id,
            ip=request.remote_addr or "",
            user_agent=request.headers.get("User-Agent", "")[:200],
        )
    session.clear()


def register_public_pages(app, ctx: PublicPageContext):
    global _ctx
    _ctx = ctx
    app.register_blueprint(public_pages)

    @app.before_request
    def _validate_session_account():
        _synchronize_session_account()
        if not session.get("must_change_password"):
            return None

        path = request.path.rstrip("/") or "/"
        allowed = (
            path in {"/account", "/api/auth/session", "/api/auth/logout", "/logout"}
            or (path == "/api/account" and request.method == "GET")
            or (path == "/api/account/password" and request.method == "PUT")
            or (path == "/api/account/sessions" and request.method == "GET")
            or (path == "/api/account/activity" and request.method == "GET")
        )
        if allowed:
            return None

        is_restricted = (
            path == "/admin"
            or path.startswith("/admin/")
            or path == "/transfer"
            or path.startswith("/api/admin/")
            or path.startswith("/api/transfer/")
            or path.startswith("/api/account")
        )
        if not is_restricted:
            return None
        if path.startswith("/api/"):
            return jsonify({
                "error": "首次登录或密码重置后必须先修改密码",
                "status": 403,
                "code": "password_change_required",
                "next": "/account?password_change=required",
            }), 403
        return redirect("/account?password_change=required")
