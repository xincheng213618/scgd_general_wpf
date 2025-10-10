# Changelog

All notable changes to the SystemMonitor plugin will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.1] - 2025-10-10

### Added
- XML documentation for all public APIs
- Minimum update speed validation (100ms) to prevent excessive CPU usage
- Thread safety for performance counter access
- Proper null checks for performance counters
- Drive ready state validation before adding to list
- Detailed error logging for cache cleanup operations

### Fixed
- **Critical**: Resource leak - Timer is now properly disposed in Dispose() method
- **Critical**: Performance counters are now properly disposed to prevent memory leaks
- **Critical**: Empty catch blocks replaced with proper error handling and logging
- Thread safety issue when accessing performance counters from timer callback
- Removed all commented dead code (CPU/RAM monitoring now fully implemented)
- UpdateSpeed now validates minimum value to prevent system overload

### Changed
- Performance counter initialization now properly initializes counters with first call
- ClearCache method now provides detailed feedback on deleted file count
- SystemMonitorSetting properties now use SetProperty helper for consistency
- Improved exception handling throughout with specific error messages
- Enhanced code documentation and inline comments

### Technical Improvements
- Added `_isDisposed` flag to prevent operations after disposal
- Added `_perfCounterLock` for thread-safe counter access
- Performance counters are now nullable to support proper cleanup
- Timer callback checks disposal state before executing
- Drive info only includes ready drives

## [1.0.0] - 2025-01-01

### Added
- Initial release
- Real-time CPU monitoring
- RAM usage tracking
- Disk space monitoring
- System time display
- Status bar integration
- Cache cleanup functionality
- Multi-language support
- Configuration panel in settings

### Features
- Configurable update interval
- Customizable time format
- Toggle time/RAM display in status bar
- Multiple disk drive support
