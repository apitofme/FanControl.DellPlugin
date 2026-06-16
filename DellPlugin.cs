using FanControl.Plugins;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using DellFanManagement.DellSmbiozBzhLib;

namespace FanControl.DellPlugin
{
    public class DellPlugin : IPlugin, IDisposable
    {
        private const string SYS_FILE = "bzh_dell_smm_io_x64.sys";
        private bool _dellInitialized;
        private FileInfo _copiedSysFile;
        private Boolean m_DisposedValue;
        private readonly IPluginLogger _logger;

        public string Name => "Dell";

        public DellPlugin( IPluginLogger logger ) { _logger = logger; }

        public void Close()
        {
            if (_dellInitialized)
            {
                Debug.WriteLine("[DellPlugin] Closing down plugin...");

                // Enable Automatic Fan Control (i.e. revert to on-board controller):
                try
                {
                    Debug.WriteLine("[DellPlugin] Attempting to enable automatic fan controller:");
                    bool autoEnabled = DellSmbiosBzh.EnableAutomaticFanControl(false);
                    if (!autoEnabled)
                    {
                        // Default method failed > Try alternate method
                        autoEnabled = DellSmbiosBzh.EnableAutomaticFanControl(true);
                        if (!autoEnabled)
                        {
                            // Alternate method failed! > Consider disabling automatic fan control?
                            // e.g. DellSmbiosBzh.DisableAutomaticFanControl(false/true);
                            // -> Q: Would this preserve fans current speed or make them run at full-speed until next reboot?
                            Debug.WriteLine("[DellPlugin] - Failed!");
                        }
                        else
                            Debug.WriteLine("[DellPlugin] - Succeeded using alternate method.");
                    }
                    else
                        Debug.WriteLine("[DellPlugin] - Success!");
                }
                catch (Exception ex)
                {
                    _logger.Log($"[DellPlugin] Unable to restore automatic fan control during close: {ex.Message}");
                    Debug.WriteLine($"[DellPlugin] Exception while enabling automatic fan control: {ex}");
                }
                
                // Shutdown DellSmbiosBzh interface:
                try
                {
                    Debug.WriteLine("[DellPlugin] Attempting to shut down DellSmbiosBzh interface:");
                    DellSmbiosBzh.Shutdown();
                    Debug.WriteLine("[DellPlugin] - Success!");
                }
                catch (Exception ex)
                {
                    _logger.Log($"[DellPlugin] Exception during DellSmbiosBzh shutdown: {ex.GetType().Name}: {ex.Message}");
                    Debug.WriteLine($"[DellPlugin] Exception during SMBIOS driver shutdown: {ex}");
                }

                // Clear variables:
                _copiedSysFile = null;
                _dellInitialized = false;

                Debug.WriteLine("[DellPlugin] << Close() complete!");
            }

            else
                Debug.WriteLine("[DellPlugin] Close() called but plugin not initilized.");
        }

        public void Initialize()
        {
            if (!_dellInitialized)
            {
                Debug.WriteLine("\n[DellPlugin] Initializing plugin...");

                // NOTE: The DellFanManagement library [DellSmbiosBzhLib.dll] expects the driver-file [bzh_dell_smm_io_x64.sys]
                // to be in the host application's base-directory. This is a limitation of the source library (hardcoded path),
                // so we must ensure that this file is present - i.e. copy from Plugin directory (if required)!

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
                    Debug.WriteLine($"[DellPlugin] WARNING Driver file not found in expected location '{copyLocation}'!");
                    needsCopy = true;
                }
                else
                {
                    Debug.WriteLine($"[DellPlugin] Existing driver file located.");

                    // Compare timestamps to detect if source has been updated:
                    Debug.WriteLine("[DellPlugin] Verifying driver...");
                    FileInfo targetFile = new FileInfo(copyLocation);
                    if (sourceFile.LastWriteTime > targetFile.LastWriteTime)
                    {
                        Debug.WriteLine("[DellPlugin] WARNING: Driver file is out of date!");
                        needsCopy = true;
                    }
                    else
                        Debug.WriteLine("[DellPlugin] PASS: Existing file matches the plugin source.");
                    // NOTE: Not checking on file-size as if the file size differs then the LastWriteTime would too (in theory).
                    // -> I think the only way it wouldn't is if someone changed the file but force-set the LWT to the same value(?).
                }

                if (needsCopy)
                {
                    // Driver file is missing or out of date so copy source file from plugin to application directory:
                    try
                    {
                        Debug.WriteLine("[DellPlugin] Attempting to copy driver file from source:");
                        _copiedSysFile = sourceFile.CopyTo(copyLocation, true);
                        Debug.WriteLine("[DellPlugin] - Success!");
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
                    Debug.WriteLine("[DellPlugin] Attempting to initialize Dell SMBIOS interface:");
                    _dellInitialized = DellSmbiosBzh.Initialize();

                    if (_dellInitialized)
                        Debug.WriteLine("[DellPlugin] - Success!");
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
                    Debug.WriteLine($"[DellPlugin] Exception during SMBIOS driver initialize: {ex}");
                }
                catch (Exception ex)
                {
                    _logger.Log($"[DellPlugin] Exception during DellSmbiosBzh initialize: {ex.GetType().Name}: {ex.Message}");
                    Debug.WriteLine($"[DellPlugin] Exception during SMBIOS driver initialize: {ex}");
                }
                
                // Disable system fan control before application takes over (ensure no conflicting signals).
                Debug.WriteLine("[DellPlugin] Attempting to **disable** automatic fan control:");
                if (!DellSmbiosBzh.DisableAutomaticFanControl(false))
                    if (!DellSmbiosBzh.DisableAutomaticFanControl(true))
                        Debug.WriteLine("[DellPlugin] - Failed!");
                    else
                        Debug.WriteLine("[DellPlugin] - Succeeded using alternate method.");
                else
                    Debug.WriteLine("[DellPlugin] - Success!");

                Debug.WriteLine("[DellPlugin] << Initialize() complete!");
            }
            
            else
                Debug.WriteLine("[DellPlugin] Initialize() called but plugin already initialized.");
        }

        public void Load(IPluginSensorsContainer _container)
        {
            if (_dellInitialized)
            {
                Debug.WriteLine("[DellPlugin] Loading Fan Management...");
                
                try
                {
                    Debug.WriteLine("[DellPlugin] Auto-detecting available fans:");
                    var detectedFans = new List<BzhFanIndex>();

                    // Iterate through all defined fan indices:
                    foreach (BzhFanIndex fanIndex in Enum.GetValues(typeof(BzhFanIndex)))
                    {
                        try
                        {
                            float? rpm = DellSmbiosBzh.GetFanRpm(fanIndex);
                            
                            // Fan exists if we got a valid RPM (even if 0).
                            if (rpm.HasValue)
                            {
                                detectedFans.Add(fanIndex);
                                Debug.WriteLine($"[DellPlugin] - {fanIndex}: Detected (RPM: {rpm})");
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[DellPlugin] - {fanIndex}: Not available - {ex.GetType().Name}");
                        }
                    }

                    if (detectedFans.Count == 0)
                    {
                        Debug.WriteLine("[DellPlugin] WARNING: No fans detected! Falling back to defaults (i.e. Fan1 and Fan2).");
                        detectedFans.AddRange(new[] { BzhFanIndex.Fan1, BzhFanIndex.Fan2 });
                    }
                    else
                        Debug.WriteLine($"[DellPlugin] - Success! Total fans detected: {detectedFans.Count}");

                    // Register controls and sensors for all fans:
                    Debug.WriteLine("[DellPlugin] Registering fan controls:");
                    var fanControls = detectedFans
                        .Select(i => new DellFanManagementControlSensor(i))
                        .ToList();
                    Debug.WriteLine("[DellPlugin] - Success!");

                    Debug.WriteLine("[DellPlugin] Registering fan sensors:");
                    var fanSensors = detectedFans
                        .Select(i => new DellFanManagementFanSensor(i))
                        .ToList();
                    Debug.WriteLine("[DellPlugin] - Success!");

                    // Inject controls and sensors in to host application:
                    Debug.WriteLine("[DellPlugin] Injecting controls and sensors in to host:");
                    _container.ControlSensors.AddRange(fanControls);
                    _container.FanSensors.AddRange(fanSensors);

                    Debug.WriteLine($"[DellPlugin] - Success! {fanControls.Count} controls and {fanSensors.Count} fan sensors loaded.");
                }
                catch (Exception ex)
                {
                    _logger.Log($"[DellPlugin] Error during Load(): {ex.Message}");
                    Debug.WriteLine($"[DellPlugin] Load() failed with an exception: {ex}");
                    throw;
                }

                Debug.WriteLine("[DellPlugin] << Load() complete!");
            }

            else
                Debug.WriteLine("[DellPlugin] Load() called but DellPlugin not initialized!");
        }

        protected virtual void Dispose(Boolean disposing)
        {   
            if (!m_DisposedValue)
            {
                Debug.WriteLine("[DellPlugin] Running clean-up...");

                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                    Debug.WriteLine("[DellPlugin] - Disposing managed resources");
                }
                
                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                Debug.WriteLine("[DellPlugin] - Disposing unmanaged resources");
                // TODO: set large fields to null
                
                Close();
                m_DisposedValue = true;

                Debug.WriteLine("[DellPlugin] << Dispose() complete!");
            }

            else
                Debug.WriteLine("[DellPlugin] Dispose() called but plugin already disposed.");
        }

        // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        ~DellPlugin()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: false);
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}