using DellFanManagement.DellSmbiozBzhLib;
using FanControl.Plugins;
using System;
using System.Diagnostics;

namespace FanControl.DellPlugin
{
    /// <summary>
    /// Provides fan RPM reading interface for Dell SMBIOS fans.
    /// 
    /// This class reads and reports the current speed (in RPM) of Dell SMBIOS fans.
    /// It queries the Dell SMM hardware via the DellSmbiosBzh interface to obtain
    /// real-time fan speed data.
    /// </summary>
    public class DellFanManagementFanSensor : IPluginSensor
    {
        private readonly BzhFanIndex _fanIndex;

        /// <summary>
        /// Initializes a new instance of the DellFanManagementFanSensor class.
        /// </summary>
        /// <param name="fanIndex">The fan index to monitor (Fan1-Fan7).</param>
        public DellFanManagementFanSensor(BzhFanIndex fanIndex) => _fanIndex = fanIndex;

        /// <summary>
        /// Gets the unique identifier for this sensor in FanControl's sensor registry.
        /// </summary>
        public string Identifier => $"Dell/FanSensor/{(int)_fanIndex}";

        /// <summary>
        /// Gets the current fan speed in RPM.
        /// </summary>
        public float? Value { get; private set; }

        /// <summary>
        /// Gets the display name for this sensor shown in the FanControl UI.
        /// </summary>
        public string Name => $"Dell Fan {(int)_fanIndex + 1}";

        /// <summary>
        /// Gets the origin identifier for this sensor.
        /// </summary>
        public string Origin => "DellSmbiosBzh";

        /// <summary>
        /// Gets the unique identifier for this sensor.
        /// </summary>
        public string Id => "Fan_" + _fanIndex.ToString();

        /// <summary>
        /// Updates the Value property with the current fan RPM from the Dell SMBIOS interface.
        /// 
        /// This method is called by FanControl during its update cycle. It queries the Dell SMM
        /// hardware for the current speed of this fan and stores the result in the Value property.
        /// If the query fails, Value remains null.
        /// </summary>
        public void Update()
        {
            try
            {
#if DEBUG
                Debug.WriteLine($"[DellFanManagementFanSensor] Update() reading RPM for {_fanIndex}");
#endif
                Value = DellSmbiosBzh.GetFanRpm(_fanIndex);
#if DEBUG
                if (Value.HasValue)
                    Debug.WriteLine($"[DellFanManagementFanSensor] {_fanIndex} RPM: {Value}");
                else
                    Debug.WriteLine($"[DellFanManagementFanSensor] {_fanIndex} RPM: null");
#endif
            }
            catch (Exception ex)
            {
#if DEBUG
                Debug.WriteLine($"[DellFanManagementFanSensor] Exception in Update() for {_fanIndex}: {ex}");
#endif
                // Do not throw - let Update() fail silently so one bad sensor doesn't crash the host application.
                // The Value will remain null, which signals to FanControl that the reading is unavailable.
                Value = null;
            }
        }
    }
}
