#!/usr/bin/env python3
"""
publish_plugin.py — Publish a plugin package (.cvxp) to the ColorVision Plugin Marketplace API.

Usage:
  python publish_plugin.py -p Spectrum -v 1.0.0.1 -f Spectrum-1.0.0.1.cvxp
  python publish_plugin.py -p Spectrum -v 1.0.0.1 -f Spectrum-1.0.0.1.cvxp --api-url http://marketplace.example.com
  python publish_plugin.py -p Spectrum -v 1.0.0.1 -f Spectrum-1.0.0.1.cvxp --icon PackageIcon.png

Options:
  -p, --plugin-id    Plugin unique ID (required)
  -v, --version      Version string (required)
  -f, --file         Path to .cvxp package file (required)
  -n, --name         Plugin display name (defaults to plugin-id)
  -d, --description  Plugin description
  -a, --author       Author name
  -c, --category     Plugin category
  -r, --requires     Minimum ColorVision engine version
  --changelog        Path to changelog file or inline text
  --icon             Path to plugin icon file
  --api-url          Marketplace API base URL (default: http://localhost:5000)

This script replaces the legacy HTTP PUT upload workflow with a structured API call
that includes full metadata, enabling the backend to track versions, categories,
and download statistics.
"""

import argparse
import os
import sys


DEFAULT_API_URL = "http://localhost:9999"
DEFAULT_CONNECT_TIMEOUT = 10
DEFAULT_READ_TIMEOUT = 120


def publish_plugin(args):
    try:
        import requests
    except ImportError:
        print("Error: publish_plugin.py requires the requests package. Please install it first.")
        sys.exit(1)

    api_url = args.api_url.rstrip("/")
    publish_url = f"{api_url}/api/packages/publish"
    auth = None

    username = (args.username or os.environ.get("COLORVISION_UPLOAD_USERNAME", "")).strip()
    password = args.password or os.environ.get("COLORVISION_UPLOAD_PASSWORD", "")
    if username or password:
        auth = (username, password)

    if not os.path.isfile(args.file):
        print(f"Error: Package file not found: {args.file}")
        sys.exit(1)

    if auth is None:
        print(
            "Error: /api/packages/publish now requires Basic Auth. "
            "Set COLORVISION_UPLOAD_USERNAME and COLORVISION_UPLOAD_PASSWORD, "
            "or pass --username/--password."
        )
        sys.exit(2)

    # Build form data
    form_data = {
        "PluginId": args.plugin_id,
        "Name": args.name or args.plugin_id,
        "Version": args.version,
    }

    if args.description:
        form_data["Description"] = args.description
    if args.author:
        form_data["Author"] = args.author
    if args.category:
        form_data["Category"] = args.category
    if args.requires:
        form_data["RequiresVersion"] = args.requires

    # Handle changelog
    if args.changelog:
        if os.path.isfile(args.changelog):
            with open(args.changelog, "r", encoding="utf-8") as f:
                form_data["ChangeLog"] = f.read()
        else:
            form_data["ChangeLog"] = args.changelog

    # Build files dict — open file handles inside try block for guaranteed cleanup
    file_size = os.path.getsize(args.file)
    file_handles = []
    try:
        pkg_fh = open(args.file, "rb")
        file_handles.append(pkg_fh)
        files = {
            "package": (os.path.basename(args.file), pkg_fh, "application/octet-stream"),
        }

        if args.icon and os.path.isfile(args.icon):
            icon_fh = open(args.icon, "rb")
            file_handles.append(icon_fh)
            files["icon"] = (os.path.basename(args.icon), icon_fh, "image/png")

        print(f"Publishing {args.plugin_id} v{args.version} to {publish_url}")
        print(f"Package: {args.file} ({file_size / 1024:.1f} KB)")

        response = requests.post(
            publish_url,
            data=form_data,
            files=files,
            auth=auth,
            timeout=(DEFAULT_CONNECT_TIMEOUT, DEFAULT_READ_TIMEOUT),
        )

        if response.status_code == 201:
            print(f"✓ Successfully published {args.plugin_id} v{args.version}")
            try:
                print(f"  Response: {response.json()}")
            except ValueError:
                print(f"  Response: {response.text}")
        elif response.status_code == 401:
            print("✗ Publish failed (HTTP 401 Unauthorized)")
            print("  Check the backend upload credentials in config.json and your environment variables.")
            sys.exit(1)
        else:
            print(f"✗ Publish failed (HTTP {response.status_code})")
            print(f"  Response: {response.text}")
            sys.exit(1)
    except requests.exceptions.ConnectionError:
        print(f"✗ Cannot connect to marketplace API at {api_url}")
        print("  Make sure the backend is running.")
        sys.exit(1)
    finally:
        for fh in file_handles:
            fh.close()


def main():
    parser = argparse.ArgumentParser(
        description="Publish a plugin to the ColorVision Plugin Marketplace"
    )
    parser.add_argument("-p", "--plugin-id", required=True, help="Plugin unique ID")
    parser.add_argument("-v", "--version", required=True, help="Version string (e.g., 1.0.0.1)")
    parser.add_argument("-f", "--file", required=True, help="Path to .cvxp package file")
    parser.add_argument("-n", "--name", help="Plugin display name (defaults to plugin-id)")
    parser.add_argument("-d", "--description", help="Plugin description")
    parser.add_argument("-a", "--author", help="Author name")
    parser.add_argument("-c", "--category", help="Plugin category")
    parser.add_argument("-r", "--requires", help="Minimum ColorVision engine version")
    parser.add_argument("--changelog", help="Path to changelog file or inline changelog text")
    parser.add_argument("--icon", help="Path to plugin icon file")
    parser.add_argument("--api-url", default=DEFAULT_API_URL, help=f"Marketplace API base URL (default: {DEFAULT_API_URL})")
    parser.add_argument("--username", help="Basic Auth username for the publish API (defaults to COLORVISION_UPLOAD_USERNAME)")
    parser.add_argument("--password", help="Basic Auth password for the publish API (defaults to COLORVISION_UPLOAD_PASSWORD)")

    args = parser.parse_args()
    publish_plugin(args)


if __name__ == "__main__":
    main()
