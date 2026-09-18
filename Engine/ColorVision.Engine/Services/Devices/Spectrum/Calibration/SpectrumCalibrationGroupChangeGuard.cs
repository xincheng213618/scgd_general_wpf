using System;

namespace ColorVision.Engine.Services.Devices.Spectrum.Calibration
{
    internal sealed class SpectrumCalibrationGroupChangeGuard
    {
        private readonly object _sync = new object();
        private int _applyDepth;
        private bool _userSwitchPending;

        public bool TryBeginUserSwitch()
        {
            lock (_sync)
            {
                if (_applyDepth > 0 || _userSwitchPending)
                    return false;

                _userSwitchPending = true;
                return true;
            }
        }

        public void CompleteUserSwitch()
        {
            lock (_sync)
                _userSwitchPending = false;
        }

        public IDisposable EnterApply()
        {
            lock (_sync)
                _applyDepth++;

            return new ApplyScope(this);
        }

        private void ExitApply()
        {
            lock (_sync)
                _applyDepth--;
        }

        private sealed class ApplyScope : IDisposable
        {
            private SpectrumCalibrationGroupChangeGuard? _owner;

            public ApplyScope(SpectrumCalibrationGroupChangeGuard owner)
            {
                _owner = owner;
            }

            public void Dispose()
            {
                SpectrumCalibrationGroupChangeGuard? owner = _owner;
                if (owner == null)
                    return;

                _owner = null;
                owner.ExitApply();
            }
        }
    }
}
