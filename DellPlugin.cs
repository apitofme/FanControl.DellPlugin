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
            if (_dellInitialized)
            {
                Debug.WriteLine("[DellPlugin] Closing down plugin...");
                
                // Enable Automatic Fan Control (i.e. revert to on-board controller):
                DellSmbiosBzh.EnableAutomaticFanControl(true); // >> Alternate method
                DellSmbiosBzh.EnableAutomaticFanControl(false); // >> Default method
                Debug.WriteLine("[DellPlugin] Automatic fan control re-enabled.");
                
                // Shutdown DellSmbiosBzh interface:
                DellSmbiosBzh.Shutdown();
                Debug.WriteLine("[DellPlugin] SMBios interface shutdown.");
                
                // Clean-up File System:
                _copiedSysFile.Delete();
                // NOTE: "If the file to be deleted does not exist, no exception is thrown."
                // -> Ref: https://learn.microsoft.com/en-us/dotnet/api/system.io.file.delete?view=net-10.0
                Debug.WriteLine("[DellPlugin] Session driver file deleted.");

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
            Debug.WriteLine("[DellPlugin] Initializing plugin...");
            var copyLocation = Path.Combine(Directory.GetCurrentDirectory(), SYS_FILE);

            // Delete any residual copy of sys file (i.e. from bad shutdown):
            if (File.Exists(copyLocation))
            {
                Debug.WriteLine(
                    $"[DellPlugin] Driver file already exists at: {copyLocation}.\n"
                    + "Note: This may indicate the program previously crashed or there was an unsuccessful shutdown.\n"
                    + "- Will attempt to delete residual driver file."
                );
                File.Delete(copyLocation);
            }

            // Copy the driver file from plugin to application:
            FileInfo sysFile = new FileInfo(typeof(DellSmbiosBzh).Assembly.Location).Directory.GetFiles(SYS_FILE).FirstOrDefault();
            _copiedSysFile = sysFile.CopyTo(copyLocation, true);
            // Note: "If the file exists and overwrite is false, an IOException is thrown."
            // -> Ref: https://learn.microsoft.com/en-us/dotnet/api/system.io.fileinfo.copyto?view=net-10.0
            
            // Initialize the Dell SMBios interface:
            _dellInitialized = DellSmbiosBzh.Initialize();
            
            Debug.WriteLine("[DellPlugin] << Initialize() complete!");
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