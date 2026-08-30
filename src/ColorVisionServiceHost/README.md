# ColorVisionServiceHost

Windows-only local privilege broker for ColorVision (`net10.0-windows`, x64). The executable runs under SCM; the desktop owner is `ColorVision/ServiceHost/`, and the shared client/protocol is `UI/ColorVision.UI/ServiceHost/`.

Keep the complete runtime output and packaged `Tasks/` scripts together. Installation, service control, self-update, registry and directory-permission operations require explicit authorization for the target machine. `--run` starts a real broker and background scan protection; `--send` sends a real command. Neither is a documentation validation command.

Read the canonical [local broker, identity, ticket, readiness and shutdown contract](../../docs/03-architecture/components/service-host.md). That knowledge lives in the matching source checkout; this relative link does not embed it in a standalone binary package. Request timeout or disconnection does not cancel an already admitted command.
