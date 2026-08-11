from pathlib import Path

try:
    from . import build, build_update
except ImportError:
    import build
    import build_update


def run_release(args) -> int:
    repository_root = Path(__file__).resolve().parent.parent
    projects = build.build_projects(repository_root)
    if args.project not in projects:
        print(f"Unknown project: {args.project}")
        print(f"Available projects: {', '.join(sorted(projects))}")
        return 2

    remote_settings = build.build_remote_settings(args)
    if not remote_settings.username or not remote_settings.password:
        print(
            "Remote upload requires Basic Auth credentials. "
            "Set COLORVISION_UPLOAD_USERNAME and COLORVISION_UPLOAD_PASSWORD."
        )
        return 2
    if not build.preflight_remote_upload(remote_settings):
        print("Remote upload preflight failed; aborting before build/upload.")
        return 2

    primary = build.prepare_primary_release(projects[args.project])
    if primary is None:
        return 1
    update = build_update.prepare_update_release(expected_version=primary.version)
    if update is None:
        return 1
    if update.version != primary.version:
        print(
            f"Prepared release version mismatch: installer {primary.version}, "
            f"update {update.version}."
        )
        return 1

    if not build.publish_primary_release(
        primary.version,
        primary.installer,
        primary.changelog,
        remote_settings,
        incremental_file=update.incremental_package,
        required_local_artifacts=(update.full_package,),
    ):
        return 1
    return 0


def main() -> int:
    return run_release(build.parse_args())


if __name__ == "__main__":
    raise SystemExit(main())
