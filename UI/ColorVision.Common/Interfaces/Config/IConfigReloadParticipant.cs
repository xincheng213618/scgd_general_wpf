namespace ColorVision.UI
{
    /// <summary>
    /// A process owner that must rebind state after the active configuration dictionary is replaced.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="BindCurrentConfig"/> runs after the new configuration dictionary is installed.
    /// Implementations that cache a configuration object must unsubscribe from the previous object,
    /// install the current object from the supplied <see cref="IConfigService"/>, and then subscribe to the
    /// new object before returning.
    /// </para>
    /// <para>
    /// Throw when the owner cannot establish that binding. The coordinator isolates the exception,
    /// continues with the remaining owners, and includes every failure in the reload result.
    /// </para>
    /// </remarks>
    public interface IConfigReloadParticipant
    {
        /// <summary>
        /// Stable diagnostic name. The coordinator falls back to the runtime type name if this
        /// getter fails or returns an empty value.
        /// </summary>
        string ConfigReloadName { get; }

        /// <summary>
        /// Lower values bind first. Equal values retain registration order. Metadata getter
        /// failures are reported without preventing this or later participants from binding.
        /// </summary>
        int ConfigReloadOrder { get; }

        /// <summary>
        /// Atomically replaces the owner's source for future work. Work that already captured a
        /// configuration snapshot remains owned by that work and is not rewritten by this call.
        /// </summary>
        void BindCurrentConfig(IConfigService currentConfig);
    }
}
