using FanControl.Plugins;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using DellFanManagement.DellSmbiozBzhLib;

namespace FanControl.DellPlugin
{
    /// <summary>
    /// Dell laptop fan control plugin for FanControl application.
    /// 
    /// Provides dynamic detection and management of Dell SMBIOS fans using the DellFanManagement library.
    /// Supports automatic fan discovery (Fan1-Fan7) and exposes both fan sensors (RPM monitoring) and
    /// fan controls (speed adjustment) to the FanControl host application.
    /// 
    /// The plugin follows the IPlugin lifecycle: Initialize() → Load() → Close()
    /// These methods can be called multiple times without side effects or resource leaks.
    /// </summary>
    public class DellPlugin : IPlugin, IDisposable
    {
        private const string SYS_FILE = "bzh_dell_smm_io_x64.sys";
        private bool _dellInitialized;
        private FileInfo _copiedSysFile;
        private Boolean m_DisposedValue;
        private readonly IPluginLogger _logger;

        /// <summary>
        /// Gets the display name of the plugin shown in the FanControl UI.
        /// </summary>
        public string Name => "Dell";

        /// <summary>
        /// Initializes a new instance of the DellPlugin class.
        /// </summary>
        /// <param name="logger">Logger instance for recording plugin events and errors.</param>
        public DellPlugin(IPluginLogger logger) { _logger = logger; }

        /// <summary>
        /// Closes and cleans up the plugin resources.
        /// 
        /// Restores automatic BIOS fan control and shuts down the SMBIOS driver interface.
        /// Can be called multiple times safely; subsequent calls after the first close are no-ops.
        /// </summary>
        public void Close()
        {
            if (_dellInitialized)
            {
#if DEBUG
                Debug.WriteLine("[DellPlugin] Closing down plugin...");
#endif

                // Enable Automatic Fan Control (i.e. revert to on-board controller):
                try
                {
#if DEBUG
                    Debug.WriteLine("[DellPlugin] Attempting to enable automatic fan controller:");
#endif
                    bool autoEnabled = DellSmbiosBzh.EnableAutomaticFanControl(false);
                    if (!autoEnabled)
                    {
                        // Default method failed > Try alternate method
                        autoEnabled = DellSmbiosBzh.EnableAutomaticFanControl(true);
                        if (!autoEnabled)
                        {
                            // Alternate method failed! Log warning but continue cleanup.
                            // Note: The underlying Dell SMM hardware uses Fan1 as the master control for all fans.
                            // Failure to restore automatic control may leave fans in manual mode, but the
                            // system will likely recover on next restart.
                            _logger.Log("[DellPlugin] WARNING: Failed to restore automatic fan control. Fans may remain in manual mode until reboot.");
#if DEBUG
                            Debug.WriteLine("[DellPlugin] - Failed!");
#endif
                        }
                        else
                        {
#if DEBUG
                            Debug.WriteLine("[DellPlugin] - Succeeded using alternate method.");
#endif
                        }
                    }
                    else
                    {
#if DEBUG
                        Debug.WriteLine("[DellPlugin] - Success!");
#endif
                    }
                }
                catch (Exception ex)
                {
                    _logger.Log($"[DellPlugin] Exception while enabling automatic fan control during close: {ex.GetType().Name}: {ex.Message}");
#if DEBUG
                    Debug.WriteLine($"[DellPlugin] Exception while enabling automatic fan control: {ex}");
#endif
                }
                
                // Shutdown DellSmbiosBzh interface:
                try
                {
#if DEBUG
                    Debug.WriteLine("[DellPlugin] Attempting to shut down DellSmbiosBzh interface:");
#endif
                    DellSmbiosBzh.Shutdown();
#if DEBUG
                    Debug.WriteLine("[DellPlugin] - Success!");
#endif
                }
                catch (Exception ex)
                {
                    _logger.Log($"[DellPlugin] Exception during DellSmbiosBzh shutdown: {ex.GetType().Name}: {ex.Message}");
#if DEBUG
                    Debug.WriteLine($"[DellPlugin] Exception during SMBIOS driver shutdown: {ex}");
#endif
                }

                // Clear variables:
                _copiedSysFile = null;
                _dellInitialized = false;

#if DEBUG
                Debug.WriteLine("[DellPlugin] << Close() complete!");
#endif
            }
            else
            {
#if DEBUG
                Debug.WriteLine("[DellPlugin] Close() called but plugin not initialized.");
#endif
            }
        }

        /// <summary>
        /// Initializes the plugin and sets up the Dell SMBIOS driver interface.
        /// 
        /// This method:
        /// 1. Locates and verifies the system driver file (bzh_dell_smm_io_x64.sys)
        /// 2. Copies the driver to the application directory if missing or outdated
        /// 3. Initializes the DellSmbiosBzh interface to communicate with the SMBIOS
        /// 4. Disables automatic BIOS fan control to prepare for manual control
        /// 
        /// Can be called multiple times; subsequent calls after the first successful init are no-ops.
        /// </summary>
        public void Initialize()
        {
            if (!_dellInitialized)
            {
#if DEBUG
                Debug.WriteLine("\n[DellPlugin] Initializing plugin...");
#endif

                // NOTE: The DellFanManagement library [DellSmbiosBzhLib.dll] expects the driver-file [bzh_dell_smm_io_x64.sys]
                // to be in the host application's base-directory. This is a limitation of the source library (hardcoded path),
                // so we must ensure that this file is present - i.e. copy from Plugin directory (if required)!
                // 
                // PERSISTENCE STRATEGY: We intentionally persist the driver file across restarts rather than deleting it
                // on Close(). This avoids file system race conditions that can occur when FanControl rapidly re-initializes
                // the plugin. If the file is deleted during Close(), the next Initialize() call might fail if the file
                // handle hasn't fully released or if another process momentarily locks the file. By keeping the file in place
                // and only re-copying if it's missing or outdated (via timestamp check), we maintain reliability.

                // Locate driver source file in plugin directory:
                FileInfo sourceFile = new FileInfo(typeof(DellSmbiosBzh).Assembly.Location).Directory.GetFiles(SYS_FILE).FirstOrDefault() ?? throw new FileNotFoundException(
                    $"[DellPlugin] Could not find {SYS_FILE} in DellFanManagement assembly directory. "
                    + "Ensure DellPlugin package is properly installed.");
                
                // Configure destination for copy operation:
                string copyLocation = Path.Combine(Directory.GetCurrentDirectory(), SYS_FILE);

                // Verify if the driver file exists/needs copying or needs updating (overwrite existing copy):
                bool needsCopy = false;
                if (!File.Exists(copyLocation))
                {
#if DEBUG
                    Debug.WriteLine($"[DellPlugin] WARNING Driver file not found in expected location '{copyLocation}'!");
#endif
                    _logger.Log($"[DellPlugin] Driver file missing at {copyLocation}. Will attempt to copy from plugin directory.");
                    needsCopy = true;
                }
                else
                {
#if DEBUG
                    Debug.WriteLine($"[DellPlugin] Existing driver file located.");
#endif

                    // Compare timestamps to detect if source has been updated:
#if DEBUG
                    Debug.WriteLine("[DellPlugin] Verifying driver...");
#endif
                    FileInfo targetFile = new FileInfo(copyLocation);
                    if (sourceFile.LastWriteTime > targetFile.LastWriteTime)
                    {
#if DEBUG
                        Debug.WriteLine("[DellPlugin] WARNING: Driver file is out of date!");
#endif
                        _logger.Log($"[DellPlugin] Driver file is outdated. Source modification time ({sourceFile.LastWriteTime}) is newer than current copy ({targetFile.LastWriteTime}). Updating...");
                        needsCopy = true;
                    }
                    else
                    {
#if DEBUG
                        Debug.WriteLine("[DellPlugin] PASS: Existing file matches the plugin source.");
#endif
                    }
                    // NOTE: Not checking file-size as if the file size differs then the LastWriteTime would too (in theory).
                    // The only scenario where this wouldn't apply is if someone manually changed the file but force-set 
                    // the LastWriteTime to the same value, which is an edge case we don't need to handle.
                }

                if (needsCopy)
                {
                    // Driver file is missing or out of date so copy source file from plugin to application directory:
                    try
                    {
#if DEBUG
                        Debug.WriteLine("[DellPlugin] Attempting to copy driver file from source:");
#endif
                        _copiedSysFile = sourceFile.CopyTo(copyLocation, true);
#if DEBUG
                        Debug.WriteLine("[DellPlugin] - Success!");
#endif
                    }
                    catch (FileNotFoundException ex)
                    {
                        throw new FileNotFoundException(
                                $"[DellPlugin] Could not find '{SYS_FILE}' in plugin source directory. "
                                + "Ensure plugin is properly installed and directory is accessible.",
                                ex);
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        throw new UnauthorizedAccessException(
                            $"[DellPlugin] Access denied when copying driver file to {copyLocation}. "
                            + "Ensure the application has write permissions to the current directory.",
                            ex);
                    }
                    catch (IOException ex)
                    {
                        throw new IOException($"[DellPlugin] IO error when copying driver file to {copyLocation}: {ex.Message}", ex);
                    }
                }
                else
                {
                    // Driver file already exists and is up to date so simply store the reference to existing file:
                    _copiedSysFile = new FileInfo(copyLocation);
                }

                // Initialize the Dell SMBIOS interface:
                try
                {
#if DEBUG
                    Debug.WriteLine("[DellPlugin] Attempting to initialize Dell SMBIOS interface:");
#endif
                    _dellInitialized = DellSmbiosBzh.Initialize();

                    if (_dellInitialized)
                    {
#if DEBUG
                        Debug.WriteLine("[DellPlugin] - Success!");
#endif
                    }
                    else
                    {
                        throw new InvalidOperationException(
                            "DellSmbiosBzh.Initialize() returned false. " +
                            "This may indicate the system is not a supported Dell laptop or SMBIOS access is unavailable.");
                    }
                }
                catch (InvalidOperationException ex)
                {
                    _logger.Log($"[DellPlugin] Failed to initialize SMBIOS driver interface: {ex.Message}");
#if DEBUG
                    Debug.WriteLine($"[DellPlugin] Exception during SMBIOS driver initialize: {ex}");
#endif
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.Log($"[DellPlugin] Unexpected exception during DellSmbiosBzh initialization: {ex.GetType().Name}: {ex.Message}");
#if DEBUG
                    Debug.WriteLine($"[DellPlugin] Exception during SMBIOS driver initialize: {ex}");
#endif
                    throw;
                }
                
                // Disable system fan control before application takes over (ensure no conflicting signals).
                // Note: Fan1 acts as the master control for all fans on the Dell SMM hardware.
#if DEBUG
                Debug.WriteLine("[DellPlugin] Attempting to disable automatic fan control:");
#endif
                if (!DellSmbiosBzh.DisableAutomaticFanControl(false))
                {
                    if (!DellSmbiosBzh.DisableAutomaticFanControl(true))
                    {
#if DEBUG
                        Debug.WriteLine("[DellPlugin] - Failed!");
#endif
                        _logger.Log("[DellPlugin] WARNING: Failed to disable automatic fan control. Fans may not respond to manual control commands.");
                    }
                    else
                    {
#if DEBUG
                        Debug.WriteLine("[DellPlugin] - Succeeded using alternate method.");
#endif
                    }
                }
                else
                {
#if DEBUG
                    Debug.WriteLine("[DellPlugin] - Success!");
#endif
                }

#if DEBUG
                Debug.WriteLine("[DellPlugin] << Initialize() complete!");
#endif
            }
            else
            {
#if DEBUG
                Debug.WriteLine("[DellPlugin] Initialize() called but plugin already initialized.");
#endif
            }
        }

        /// <summary>
        /// Loads fan sensors and controls into the FanControl host application.
        /// 
        /// This method performs automatic fan detection by querying the SMBIOS for all possible
        /// fan indices (Fan1-Fan7). For each fan that responds with a valid RPM reading, a corresponding
        /// sensor (for RPM monitoring) and control (for speed adjustment) are created and registered.
        /// 
        /// If no fans are detected, falls back to Fan1 and Fan2 as defaults. This provides a safety net
        /// in case the detection logic encounters unexpected behavior.
        /// </summary>
        /// <param name="_container">The container provided by FanControl to inject sensors and controls.</param>
        public void Load(IPluginSensorsContainer _container)
        {
            if (_dellInitialized)
            {
#if DEBUG
                Debug.WriteLine("[DellPlugin] Loading Fan Management...");
#endif
                
                try
                {
#if DEBUG
                    Debug.WriteLine("[DellPlugin] Auto-detecting available fans:");
#endif
                    var detectedFans = new List<BzhFanIndex>();

                    // Iterate through ALL defined fan indices (Fan1-Fan7).
                    // Note: We iterate through all possible fans because fans relate to specific hardware components
                    // that may or may not be present. The absence of one fan does not imply the absence of subsequent fans.
                    // For example, a laptop may have Fan1 (CPU) and Fan3 (GPU) but not Fan2.
                    foreach (BzhFanIndex fanIndex in Enum.GetValues(typeof(BzhFanIndex)))
                    {
                        try
                        {
                            float? rpm = DellSmbiosBzh.GetFanRpm(fanIndex);
                            
                            // Fan exists if we got a valid RPM reading (even if 0, which means fan is off).
                            if (rpm.HasValue)
                            {
                                detectedFans.Add(fanIndex);
#if DEBUG
                                Debug.WriteLine($"[DellPlugin] - {fanIndex}: Detected (RPM: {rpm})");
#endif
                                _logger.Log($"[DellPlugin] Fan detected: {fanIndex} (current RPM: {rpm})");
                            }
                        }
                        catch (Exception ex)
                        {
#if DEBUG
                            Debug.WriteLine($"[DellPlugin] - {fanIndex}: Not available - {ex.GetType().Name}");
#endif
                        }
                    }

                    if (detectedFans.Count == 0)
                    {
#if DEBUG
                        Debug.WriteLine("[DellPlugin] WARNING: No fans detected! Falling back to defaults (i.e. Fan1 and Fan2).");
#endif
                        _logger.Log("[DellPlugin] WARNING: No fans detected via auto-detection. Falling back to Fan1 and Fan2 as defaults.");
                        detectedFans.AddRange(new[] { BzhFanIndex.Fan1, BzhFanIndex.Fan2 });
                    }
                    else
                    {
#if DEBUG
                        Debug.WriteLine($"[DellPlugin] - Success! Total fans detected: {detectedFans.Count}");
#endif
                        _logger.Log($"[DellPlugin] Fan auto-detection completed successfully. Found {detectedFans.Count} fan(s): {string.Join(", ", detectedFans)}");
                    }

                    // Register controls and sensors for all fans:
#if DEBUG
                    Debug.WriteLine("[DellPlugin] Registering fan controls:");
#endif
                    var fanControls = detectedFans
                        .Select(i => new DellFanManagementControlSensor(i))
                        .ToList();
#if DEBUG
                    Debug.WriteLine("[DellPlugin] - Success!");
#endif

#if DEBUG
                    Debug.WriteLine("[DellPlugin] Registering fan sensors:");
#endif
                    var fanSensors = detectedFans
                        .Select(i => new DellFanManagementFanSensor(i))
                        .ToList();
#if DEBUG
                    Debug.WriteLine("[DellPlugin] - Success!");
#endif

                    // Inject controls and sensors into host application:
#if DEBUG
                    Debug.WriteLine("[DellPlugin] Injecting controls and sensors into host:");
#endif
                    _container.ControlSensors.AddRange(fanControls);
                    _container.FanSensors.AddRange(fanSensors);

#if DEBUG
                    Debug.WriteLine($"[DellPlugin] - Success! {fanControls.Count} controls and {fanSensors.Count} fan sensors loaded.");
#endif
                }
                catch (Exception ex)
                {
                    _logger.Log($"[DellPlugin] Error during Load(): {ex.GetType().Name}: {ex.Message}");
#if DEBUG
                    Debug.WriteLine($"[DellPlugin] Load() failed with an exception: {ex}");
#endif
                    throw;
                }

#if DEBUG
                Debug.WriteLine("[DellPlugin] << Load() complete!");
#endif
            }
            else
            {
#if DEBUG
                Debug.WriteLine("[DellPlugin] Load() called but DellPlugin not initialized!");
#endif
                _logger.Log("[DellPlugin] ERROR: Load() called before plugin initialization completed.");
            }
        }

        /// <summary>
        /// Disposes managed and unmanaged resources.
        /// </summary>
        /// <param name="disposing">True if called from Dispose(); false if called from finalizer.</param>
        protected virtual void Dispose(Boolean disposing)
        {   
            if (!m_DisposedValue)
            {
#if DEBUG
                Debug.WriteLine("[DellPlugin] Running clean-up...");
#endif

                if (disposing)
                {
#if DEBUG
                    Debug.WriteLine("[DellPlugin] - Disposing managed resources");
#endif
                    // TODO: dispose managed state (managed objects)
                }
                
#if DEBUG
                Debug.WriteLine("[DellPlugin] - Disposing unmanaged resources");
#endif
                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                
                Close();
                m_DisposedValue = true;

#if DEBUG
                Debug.WriteLine("[DellPlugin] << Dispose() complete!");
#endif
            }
            else
            {
#if DEBUG
                Debug.WriteLine("[DellPlugin] Dispose() called but plugin already disposed.");
#endif
            }
        }

        /// <summary>
        /// Finalizer to ensure cleanup of unmanaged resources.
        /// </summary>
        ~DellPlugin()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: false);
        }

        /// <summary>
        /// Disposes the plugin and suppresses the finalizer.
        /// </summary>
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
