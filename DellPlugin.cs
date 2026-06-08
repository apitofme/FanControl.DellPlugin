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

        public string Name => "Dell";

        public void Close()
        {
            // Conditional to avoid running code when plugin is not initialized:
            if (_dellInitialized)
            {
                Debug.WriteLine("[DellPlugin] Closing down plugin...");
                // Re-Enable Automatic Fan Control (i.e. Dell System BIOS/EC):
                // -> [bool] Uses alternate method first, then default method.
                DellSmbiosBzh.EnableAutomaticFanControl(true);
                DellSmbiosBzh.EnableAutomaticFanControl(false);
                // Q: Why use both methods (without checks for success), and why in this order?
                Debug.WriteLine("[DellPlugin] Automatic fan control re-enabled.");
                
                // Shutdown DellSmbiosBzh interface:
                DellSmbiosBzh.Shutdown();
                // Ref: https://github.com/AaronKelley/DellFanManagement/blob/develop/DellSmbiosBzhLib/DellSmbiosBzh.cs
                // Q: Should the driver be 'stopped' before the interface is shutdown?
                Debug.WriteLine("[DellPlugin] SMBios interface shutdown.");
                
                // Clean-up File System:
                _copiedSysFile.Delete();
                Debug.WriteLine("[DellPlugin] Session driver file deleted.");
                // NOTE: "If the file to be deleted does not exist, no exception is thrown."
                // Ref: https://learn.microsoft.com/en-us/dotnet/api/system.io.file.delete?view=net-10.0
                // -- So I guess no 'File.Exists' checks or 'try/catch' guards around delete are needed?!
                // Q: Why delete the driver '.sys' file each time?

                // Reset variables:
                _copiedSysFile = null;
                _dellInitialized = false;
            }

            else
                Debug.WriteLine("[DellPlugin] Close() called but plugin not initilized.");

            Debug.WriteLine("[DellPlugin] << Close() complete!");
        }

        public void Initialize()
        {
            Debug.WriteLine("[DellPlugin] Initializing plugin...");
            // NOTE: The DellSmbiosBzh driver expects the sys file to be in the parent application's base directory.
            // This is a limitation of the DellFanManagement library (i.e. hardcoded driver path).
            // So we must copy the sys file from the plugin directory to FanControl's base directory.
            var copyLocation = Path.Combine(Directory.GetCurrentDirectory(), SYS_FILE);

            // Try to delete any previous *residual* copy of sys file:
            // -> Sys file would normally be deleted during plugin 'close()' method.
            if (File.Exists(copyLocation))
            {
                Debug.WriteLine(
                    $"[DellPlugin] Driver file already exists at: {copyLocation}.\n"
                    + "Note: This may indicate the program previously crashed or there was an unsuccessful shutdown.\n"
                    + "- Will attempt to delete residual driver file."
                );
                File.Delete(copyLocation);
            }

            // Obtain a FileInfo object for the *source* sys file:
            FileInfo sysFile = new FileInfo(typeof(DellSmbiosBzh).Assembly.Location).Directory.GetFiles(SYS_FILE).FirstOrDefault();
            
            // Copy the sys file to application root [overwrite enabled], and store the file reference:
            // -> Returns a FileInfo object to file with fully qualified path.
            _copiedSysFile = sysFile.CopyTo(copyLocation, true);
            // Note: "If the file exists and overwrite is false, an IOException is thrown."
            // Ref: https://learn.microsoft.com/en-us/dotnet/api/system.io.fileinfo.copyto?view=net-10.0
            // --> Added debug notice on success, error exception on failure.
            if (_copiedSysFile.Exists)
                Debug.WriteLine($"[DellPlugin] Driver file successfully copied to: {copyLocation}.");
            else
                throw new IOException(
                    "Unable to locate driver file 'bzh_dell_smm_io_x64.sys' in expected location\n"
                    + $"- {copyLocation} does not exist after copy operation."
                );

            // Initialize the Dell SMBios interface:
            _dellInitialized = DellSmbiosBzh.Initialize();
            // --> Added debug notice on success, error exception on failure.
            if (_dellInitialized)
                Debug.WriteLine("[DellPlugin] Dell SMBios interface intialized.");
            else
                throw new InvalidOperationException(
                    "Dell SMBios interface failed to initialize."
                    + "This may indicate the system is not a supported Dell laptop or SMBios access is unavailable."
                );
            
            Debug.WriteLine("[DellPlugin] << Initialize() complete!");
        }

        /** ADDITIONAL NOTES:
            >> OBSERVATION: The sys file is copied every time the plugin is intialised (e.g. FanControl application starts),
            and deleted each time the plugin is closed (e.g. FanControl application closes).
        
            >> SUGGESTION: Wouldn't it make more sense for the file to be copied once, the first time the plugin is loaded,
            then just use the existing copy unless the source file has been updated?
            (i.e. DellFanManagement is updated -- https://github.com/AaronKelley/DellFanManagement)
        
            >> CONSIDERATION: If the plugin is deactivated or deleted the copied file would remain in the base-folder,
            unless FanControl implemented some kind of "Deactivate Plugin" clean up code (i.e. plugin registers assests).
        */

        public void Load(IPluginSensorsContainer _container)
        {
            // Conditional to prevent Load() if plugin is not initialized:
            if (_dellInitialized)
            {
                Debug.WriteLine("[DellPlugin] Loading Fan Management...");
                // Load 2x Fan Controls:
                IEnumerable<DellFanManagementControlSensor> fanControls = new[] {
                            BzhFanIndex.Fan1,
                            BzhFanIndex.Fan2
                        }.Select(i => new DellFanManagementControlSensor(i)).ToArray();
                // Load 2x Fan Sensors:
                IEnumerable<DellFanManagementFanSensor> fanSensors = new[] {
                            BzhFanIndex.Fan1,
                            BzhFanIndex.Fan2
                        }.Select(i => new DellFanManagementFanSensor(i)).ToArray();
                // Q: Why is the number of fan controllers/sensors hard coded at 2?

                // Register these with FanConrol:
                _container.ControlSensors.AddRange(fanControls);
                _container.FanSensors.AddRange(fanSensors);
            }

            else
                Debug.WriteLine("[DellPlugin] Load() called but DellPlugin not initialized!");

            Debug.WriteLine("[DellPlugin] << Load() complete!");
        }

        // Ref: https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/virtual
        protected virtual void Dispose(Boolean disposing)
        {
            Debug.WriteLine("[DellPlugin] Running clean-up...");
            // Note: This 'virtual' method appears to be called multiple times.
            // -> Once from plugin/application shutdown, then again via GC (assumed normal behaviour).
            // Q1: Does the conditional below prevent multiple calls, and should it? [Will test with Debug logging...]
            // Q2: Does the name have to be 'm_DisposedValue' or could it be something else (e.g. "_isDisposed")?
            if (!m_DisposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                    Debug.WriteLine("[DellPlugin] - Disposing managed resources");
                }
                
                Debug.WriteLine("[DellPlugin] - Disposing unmanaged resources");
                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                Close();
                // Note: This appears to be called explicitly *before* Dispose() is called during application termination.
                // Q: Would a conditional guard on '_dellInitialized' make sense here,
                // or are there reasons why Close() might need to be called here too or twice?
                m_DisposedValue = true;
            }

            else
                Debug.WriteLine("[DellPlugin] Dispose() called but plugin already disposed.");
            
            Debug.WriteLine("[DellPlugin] << Dispose() complete!");
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
        // Note: `GC.SuppressFinalize(this);` presumably prevents the GC from 'finalizing' the plugin object/instance,
        // i.e. it won't perform a clean-up on this plugin when it is closed?
        // -> I assume because the parent application (FanCtontrol) will trigger it's own GC operations?
    }
}