"""Static route bridge for the VitePress docs site."""

from __future__ import annotations

from flask import Blueprint, redirect, send_from_directory

from services import docs_site as docs_service


docs_site = Blueprint("docs_site", __name__)
_DOCS_ROUTE_ROOT = docs_service.DOCS_BASE_PATH.rstrip("/")


@docs_site.route(docs_service.DOCS_REDIRECT_PATH)
@docs_site.route(f"{docs_service.DOCS_REDIRECT_PATH}/")
def docs_redirect():
    return redirect(docs_service.DOCS_BASE_PATH, code=302)


@docs_site.route(f"{docs_service.DOCS_REDIRECT_PATH}/<path:doc_path>")
def docs_redirect_path(doc_path: str):
    clean = doc_path.strip("/")
    suffix = clean if clean else ""
    return redirect(f"{docs_service.DOCS_BASE_PATH}{suffix}", code=302)


@docs_site.route(_DOCS_ROUTE_ROOT)
def vitepress_docs_root():
    return redirect(docs_service.DOCS_BASE_PATH, code=302)


@docs_site.route(f"{_DOCS_ROUTE_ROOT}/")
@docs_site.route(f"{_DOCS_ROUTE_ROOT}/<path:doc_path>")
def vitepress_docs(doc_path: str = ""):
    target, status_code = docs_service.resolve_docs_site_file(doc_path)
    if target is None:
        return (
            "Documentation site has not been built. Run `npm run docs:build` from the repository root.",
            status_code,
            {"Content-Type": "text/plain; charset=utf-8"},
        )

    dist = docs_service.docs_dist_dir()
    relative = target.relative_to(dist).as_posix()
    response = send_from_directory(dist, relative)
    response.status_code = status_code
    if target.suffix.lower() == ".html":
        response.cache_control.no_cache = True
    else:
        response.cache_control.public = True
        response.cache_control.max_age = 3600
    return response


def register_docs_site(app):
    app.register_blueprint(docs_site)
