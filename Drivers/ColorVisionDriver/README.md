# ColorVisionDriver

An experimental x64 Windows WDM driver skeleton with Ping and version-query IOCTLs. The source is independent of the main ColorVision solutions, service host and release pipeline; it has no hardware or camera integration.

The current interface, WDK build prerequisites and installation gaps are documented in [ColorVisionDriver: experimental kernel driver](../../docs/03-architecture/components/kernel-driver.md).

Driver installation and signing require a dedicated test VM and separate validation. The INF is a package input, not a verified installation procedure. Normal ColorVision use does not require installing this skeleton.
