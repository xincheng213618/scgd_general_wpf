# Getting Started

This chapter retains only the most essential entry points for first-time ColorVision users, avoiding duplication with subsequent chapters.

## Suggested Reading Order

1. [What is ColorVision](./what-is-colorvision.md)
2. [System Requirements](./prerequisites.md)
3. [Installation Guide](./installation.md)
4. [First Run](./first-steps.md)
5. [Quick Start](./quick-start.md)

## Scope

- New users who want to complete installation, launch, and basic verification
- New colleagues who want to quickly understand where the main program, devices, workflows, and plugins are located
- Developers who want to find the shortest path for building from source

## What You'll Find Here

- Product positioning and typical use cases
- Windows environment requirements and pre-installation preparation
- Basic operation paths after the first launch of the main program
- Minimal steps for running the main program from source

## Building from Source

The current repository is primarily based on Windows WPF and x64. It is recommended to restore dependencies first, then build the main program:

```powershell
dotnet restore
dotnet build -p:Platform=x64
dotnet run --project ColorVision/ColorVision.csproj
```

## Continue Reading

- For UI and daily operations: go to [User Guide](../01-user-guide/README.md)
- For system design and module boundaries: go to [Architecture Design](../03-architecture/README.md)
- For repository directory and module division: go to [Project Structure Overview](../05-resources/project-structure/README.md)
- For secondary development: go to [Developer Guide](../02-developer-guide/README.md)

## Notes

- Long-form content from the old getting-started page related to architecture, installer implementation, and release scripts has been consolidated into their respective chapters and is no longer maintained here.
- If the documentation differs from the current code behavior, the source code and actual build results shall prevail.