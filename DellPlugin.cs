using FanControl.Plugins;
using System;
using System.Collections.Generic;
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
                // Re-Enable Automatic Fan Control (i.e. Dell System BIOS/EC):
                // -> [bool] Uses alternate method first, then default method.
                DellSmbiosBzh.EnableAutomaticFanControl(true);
                DellSmbiosBzh.EnableAutomaticFanControl(false);
                
                // Shutdown DellSmbiosBzh interface:
                DellSmbiosBzh.Shutdown();

                // Clean-up File System:
                _copiedSysFile.Delete();
                // NOTE: "If the file to be deleted does not exist, no exception is thrown."
                // Ref: https://learn.microsoft.com/en-us/dotnet/api/system.io.file.delete?view=net-10.0
                // -- So I guess no 'File.Exists' checks or 'try/catch' guards around delete are needed?!

                // Reset variables:
                _copiedSysFile = null;
                _dellInitialized = false;
            }
        }

        public void Initialize()
        {
            // NOTE: The DellSmbiosBzh driver expects the sys file to be in the parent application's base directory.
            // This is a limitation of the DellFanManagement library (i.e. hardcoded driver path).
            // So we must copy the sys file from the plugin directory to FanControl's base directory.
            var copyLocation = Path.Combine(Directory.GetCurrentDirectory(), SYS_FILE);

            // Try to delete any previous *residual* copy of sys file:
            // -> Sys file would normally be deleted during plugin 'close()' method.
            if (File.Exists(copyLocation))
                File.Delete(copyLocation);

            // Obtain a FileInfo object for the *source* sys file:
            FileInfo sysFile = new FileInfo(typeof(DellSmbiosBzh).Assembly.Location).Directory.GetFiles(SYS_FILE).FirstOrDefault();
            
            // Copy the sys file to application root [overwrite enabled], and store the file reference:
            // -> Returns a FileInfo object to file with fully qualified path.
            _copiedSysFile = sysFile.CopyTo(copyLocation, true);
            // Note: "If the file exists and overwrite is false, an IOException is thrown."
            // Ref: https://learn.microsoft.com/en-us/dotnet/api/system.io.fileinfo.copyto?view=net-10.0

            // Initialize the Dell SMBios interface:
            _dellInitialized = DellSmbiosBzh.Initialize();
            
            // -- Added a quick sanity check (unsure if this is really necessary).
            if (!_dellInitialized)
                throw new InvalidOperationException(
                        "DellSmbiosBzh.Initialize() returned false. " +
                        "This may indicate the system is not a supported Dell laptop or SMBios access is unavailable.");
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
                // Register these with FanConrol:
                _container.ControlSensors.AddRange(fanControls);
                _container.FanSensors.AddRange(fanSensors);
            }
        }

        // Ref: https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/virtual
        protected virtual void Dispose(Boolean disposing)
        {
            // Note: This 'virtual' method appears to be called multiple times.
            // -> Once from plugin/application shutdown, then again via GC (assumed normal behaviour).
            // Q: Does the conditional below prevent multiple calls, and should it? [Will test with Debug logging...]
            System.Diagnostics.Debug.WriteLine($"[DellPlugin] Virtual Dispose called: 'disposing={disposing}'...");
            if (!m_DisposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                    System.Diagnostics.Debug.WriteLine("[DellPlugin] - Disposing managed resources");
                }
                
                System.Diagnostics.Debug.WriteLine("[DellPlugin] - Disposing unmanaged resources");
                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                Close();
                m_DisposedValue = true;
            }
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