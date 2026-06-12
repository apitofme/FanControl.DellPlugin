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
                
                // Clean-up File System:
                string fileLocation = Path.Combine(Directory.GetCurrentDirectory(), SYS_FILE);
                try
                {
                    Debug.WriteLine($"[DellPlugin] Attempting to delete session driver file '{SYS_FILE}' from: {_copiedSysFile.DirectoryName}");
                    _copiedSysFile.Delete();
                    // NOTE: "If the file to be deleted does not exist, no exception is thrown."
                    // -> Ref: https://learn.microsoft.com/en-us/dotnet/api/system.io.file.delete?view=net-10.0
                    // >> That is not to say that exceptions aren't thrown from other causes!
                    Debug.WriteLine("[DellPlugin] - Success!");
                }
                catch (UnauthorizedAccessException ex)
                {
                    throw new UnauthorizedAccessException($"[DellPlugin] Access denied when deleting sys file from {fileLocation}.", ex);
                }
                catch (IOException ex)
                {
                    throw new IOException($"[DellPlugin] IO error when deleting sys file from {fileLocation}: {ex.Message}", ex);
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
                Debug.WriteLine("[DellPlugin] Initializing plugin...");

                // NOTE: The DellSmbiosBzh driver expects 'bzh_dell_smm_io_x64.sys' to be in `Directory.GetCurrentDirectory()`
                // -> i.e. In the host application's base directory
                // >> This is a limitation of the DellFanManagement library (hardcoded driver path)!
                string copyLocation = Path.Combine(Directory.GetCurrentDirectory(), SYS_FILE);

                // Delete any residual copy of sys file (i.e. from bad shutdown):
                if (File.Exists(copyLocation))
                {
                    Debug.WriteLine(
                        $"[DellPlugin] Driver file already exists at: {copyLocation}.\n"
                        + "Note: This may indicate the program previously crashed or there was an unsuccessful shutdown."
                    );
                    try
                    {
                        Debug.WriteLine($"[DellPlugin] Attempting to delete residual driver file:");
                        File.Delete(copyLocation);
                        Debug.WriteLine("[DellPlugin] - Success!");
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        throw new UnauthorizedAccessException($"[DellPlugin] Access denied when deleting file from {copyLocation}.", ex);
                    }
                    catch (IOException ex)
                    {
                        throw new IOException($"[DellPlugin] IO error when deleting file from {copyLocation}: {ex.Message}", ex);
                    }
                }

                try
                {
                    // Locate driver file in plugin directory:
                    Debug.WriteLine("[DellPlugin] Locating DellSmbiosBzh driver file from plugin directory:");
                    FileInfo sysFile = new FileInfo(typeof(DellSmbiosBzh).Assembly.Location).Directory.GetFiles(SYS_FILE).FirstOrDefault();
                    Debug.WriteLine("[DellPlugin] - Success!");
                    
                    // Copy driver file to application directory
                    Debug.WriteLine("[DellPlugin] Attempting to copy driver file to application directory:");
                    _copiedSysFile = sysFile.CopyTo(copyLocation, true);
                    // Note: "If the file exists and overwrite is false, an IOException is thrown."
                    // -> Ref: https://learn.microsoft.com/en-us/dotnet/api/system.io.fileinfo.copyto?view=net-10.0
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
                        $"[DellPlugin] Access denied when copying sys file to {copyLocation}. " +
                        "Ensure the application has write permissions to the current directory.",
                        ex);
                }
                catch (IOException ex)
                {
                    throw new IOException($"[DellPlugin] IO error when copying sys file to {copyLocation}: {ex.Message}", ex);
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
                
                // Create and register fan sensors and controls:
                IEnumerable<DellFanManagementControlSensor> fanControls = new[] {
                            BzhFanIndex.Fan1,
                            BzhFanIndex.Fan2
                        }.Select(i => new DellFanManagementControlSensor(i)).ToArray();
                
                IEnumerable<DellFanManagementFanSensor> fanSensors = new[] {
                            BzhFanIndex.Fan1,
                            BzhFanIndex.Fan2
                        }.Select(i => new DellFanManagementFanSensor(i)).ToArray();

                _container.ControlSensors.AddRange(fanControls);
                _container.FanSensors.AddRange(fanSensors);

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