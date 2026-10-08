# BuildCore 1.0.0

## Release

BuildCore 1.0.0 is the first release candidate / stable release of the Windows PC optimization application.

### Included

- PC hardware and system monitoring
- Performance dashboard
- Optimization library and selected-optimization transaction flow
- Immutable optimization recovery-handler registry
- Snapshot, verification, rollback, and recovery architecture
- Benchmark history integration
- Windows/system information
- BuildCore application icon and Windows AppUserModelID integration
- Release metadata and version 1.0.0
- Self-contained unpackaged Windows App SDK publishing configuration

### Release target

- Windows x64
- .NET 8
- Windows App SDK 2.5.1
- Self-contained deployment
- Unpackaged WinUI 3 application

### Version

1.0.0

### Distribution

The release ZIP should contain the complete contents of the x64 self-contained publish directory, including `BuildCore.exe` and all runtime/native dependencies.

> Note: the local `BuildCore.ico` asset is part of the developer's working release build and should be committed to the repository before relying on a clean-clone CI release build.
