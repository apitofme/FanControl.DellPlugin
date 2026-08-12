using DellFanManagement.DellSmbiozBzhLib;
using FanControl.Plugins;
using System;
using System.Diagnostics;

namespace FanControl.DellPlugin
{
    /// <summary>
    /// Provides fan control interface for Dell SMBIOS fans.
    /// 
    /// This class allows setting fan speeds via the Dell SMBIOS interface. It maps FanControl's
    /// 0-100% range to the three discrete fan levels supported by Dell hardware:
    /// - Level 0 (off):        0-33% (fan disabled)
    /// - Level 1 (medium):     33-66% (medium speed)
    /// - Level 2 (high):       66-100% (high speed)
    /// 
    /// The class also manages the automatic vs manual BIOS fan control mode, ensuring the system
    /// BIOS doesn't conflict with manual control commands. Note: Fan1 acts as the master control
    /// for all fans on the Dell SMM hardware.
    /// </summary>
    public class DellFanManagementControlSensor : IPluginControlSensor
    {
        private readonly BzhFanIndex _fanIndex;
        private bool _isSet = false;
        private float? _val;

        /// <summary>
        /// Initializes a new instance of the DellFanManagementControlSensor class.
        /// </summary>
        /// <param name="fanIndex">The fan index to control (Fan1-Fan7).</param>
        public DellFanManagementControlSensor(BzhFanIndex fanIndex) => _fanIndex = fanIndex;

        /// <summary>
        /// Gets the current control value last set by the host application (0-100%).
        /// </summary>
        public float? Value { get; private set; }

        /// <summary>
        /// Gets the display name for this control shown in the FanControl UI.
        /// </summary>
        public string Name => $"Dell Control {(int)_fanIndex + 1}";

        /// <summary>
        /// Gets the origin identifier for this control.
        /// </summary>
        public string Origin => "DellSmbiosBzh";

        /// <summary>
        /// Gets the unique identifier for this control.
        /// </summary>
        public string Id => "Control_" + _fanIndex.ToString();

        /// <summary>
        /// Resets the control to automatic BIOS fan control mode.
        /// 
        /// This method is called when the user disables manual fan control in FanControl.
        /// It restores automatic BIOS fan control so the system's embedded controller
        /// manages the fan speed based on temperature sensors.
        /// </summary>
        public void Reset()
        {
            try
            {
#if DEBUG
                Debug.WriteLine($"[DellFanManagementControlSensor] Reset() called for {_fanIndex}");
#endif
                // Note: The boolean parameter relates to which BIOS control method to use.
                // Fan1 is the master control for all fans.
                DellSmbiosBzh.EnableAutomaticFanControl(_fanIndex == BzhFanIndex.Fan1 ? false : true);
                _isSet = false;
            }
            catch (Exception ex)
            {
#if DEBUG
                Debug.WriteLine($"[DellFanManagementControlSensor] Exception in Reset() for {_fanIndex}: {ex}");
#endif
                throw;
            }
        }

        /// <summary>
        /// Sets the fan speed to a specific level (0-100%).
        /// 
        /// This method is called by FanControl when the user enables manual fan control.
        /// The speed value is converted to one of three discrete fan levels:
        /// - 0-33%:    Level 0 (fan off/minimum)
        /// - 33-66%:   Level 1 (medium speed)
        /// - 66-100%:  Level 2 (maximum speed)
        /// 
        /// On first call, this method disables automatic BIOS fan control to prevent the system
        /// from overriding manual commands. Subsequent calls only update the fan speed level.
        /// </summary>
        /// <param name="val">The desired fan speed as a percentage (0-100).</param>
        public void Set(float val)
        {
            try
            {
#if DEBUG
                Debug.WriteLine($"[DellFanManagementControlSensor] Set({val}) called for {_fanIndex}");
#endif
                if (!_isSet)
                {
                    // First time setting fan speed: disable automatic BIOS fan control.
                    // Note: The boolean parameter relates to which BIOS control method to use.
                    // Fan1 is the master control for all fans.
                    DellSmbiosBzh.DisableAutomaticFanControl(_fanIndex == BzhFanIndex.Fan1 ? false : true);
                    _isSet = true;
                }

                // Cache the value for Update() to report back to FanControl.
                _val = val;
                
                // Convert 0-100 percentage to discrete fan level.
                BzhFanLevel fanLevel = GetFanLevel(val);
#if DEBUG
                Debug.WriteLine($"[DellFanManagementControlSensor] Setting {_fanIndex} to {fanLevel} (input: {val}%)");
#endif
                DellSmbiosBzh.SetFanLevel(_fanIndex, fanLevel);
            }
            catch (Exception ex)
            {
#if DEBUG
                Debug.WriteLine($"[DellFanManagementControlSensor] Exception in Set({val}) for {_fanIndex}: {ex}");
#endif
                throw;
            }
        }

        /// <summary>
        /// Updates the Value property with the last speed that was set.
        /// 
        /// This method is called by FanControl during its update cycle to retrieve the current
        /// control value. It returns the most recently set speed value, which represents what
        /// the user last commanded, not necessarily the actual hardware state.
        /// </summary>
        public void Update() => Value = _val;

        /// <summary>
        /// Converts a percentage value (0-100) to a discrete Dell fan level.
        /// 
        /// The Dell SMM hardware only supports three fan levels, so we map the continuous
        /// 0-100% range to these three states with equal thirds:
        /// - 0-33%:    Level 0 (fan off or minimum)
        /// - 33-66%:   Level 1 (medium speed)
        /// - 66-100%:  Level 2 (maximum speed)
        /// </summary>
        /// <param name="val">The desired fan speed as a percentage (0-100).</param>
        /// <returns>The corresponding BzhFanLevel.</returns>
        private BzhFanLevel GetFanLevel(float val)
        {
            if (val < 33.33f)
                return BzhFanLevel.Level0;
            else if (val < 66.66f)
                return BzhFanLevel.Level1;
            else
                return BzhFanLevel.Level2;
        }
    }
}
