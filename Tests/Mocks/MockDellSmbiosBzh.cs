using DellFanManagement.DellSmbiozBzhLib;
using System;
using System.Collections.Generic;

namespace FanControl.DellPlugin.Tests.Mocks
{
    /// <summary>
    /// Mock implementation of DellSmbiosBzh for unit testing.
    /// 
    /// This class simulates the behavior of the actual Dell SMBIOS interface without requiring
    /// Dell hardware or system drivers. It allows tests to control fan detection, RPM readings,
    /// and command success/failure scenarios.
    /// 
    /// Usage: Set up the mock's state before calling plugin methods, then verify the calls made.
    /// </summary>
    public class MockDellSmbiosBzh
    {
        /// <summary>
        /// Gets or sets the initialization state. When false, Initialize() will fail.
        /// </summary>
        public bool InitializeReturnValue { get; set; } = true;

        /// <summary>
        /// Gets or sets whether the mock should throw an exception during Initialize().
        /// </summary>
        public bool InitializeThrowsException { get; set; } = false;

        /// <summary>
        /// Gets or sets the exception to throw during Initialize() if InitializeThrowsException is true.
        /// </summary>
        public Exception InitializeException { get; set; } = new InvalidOperationException("Mock initialization failed");

        /// <summary>
        /// Gets or sets the RPM values for each fan. Key is BzhFanIndex, Value is RPM or null if unavailable.
        /// </summary>
        public Dictionary<BzhFanIndex, float?> FanRpmMap { get; set; } = new()
        {
            { BzhFanIndex.Fan1, 3000f },
            { BzhFanIndex.Fan2, 2500f },
            { BzhFanIndex.Fan3, null },
            { BzhFanIndex.Fan4, null },
            { BzhFanIndex.Fan5, null },
            { BzhFanIndex.Fan6, null },
            { BzhFanIndex.Fan7, null }
        };

        /// <summary>
        /// Gets or sets whether GetFanRpm should throw an exception.
        /// </summary>
        public bool GetFanRpmThrowsException { get; set; } = false;

        /// <summary>
        /// Gets or sets the exception to throw during GetFanRpm() if GetFanRpmThrowsException is true.
        /// </summary>
        public Exception GetFanRpmException { get; set; } = new InvalidOperationException("Mock GetFanRpm failed");

        /// <summary>
        /// Gets or sets whether DisableAutomaticFanControl should succeed.
        /// </summary>
        public bool DisableAutomaticFanControlReturnValue { get; set; } = true;

        /// <summary>
        /// Gets or sets whether EnableAutomaticFanControl should succeed.
        /// </summary>
        public bool EnableAutomaticFanControlReturnValue { get; set; } = true;

        /// <summary>
        /// Gets or sets whether SetFanLevel should succeed.
        /// </summary>
        public bool SetFanLevelReturnValue { get; set; } = true;

        /// <summary>
        /// Gets or sets whether SetFanLevel should throw an exception.
        /// </summary>
        public bool SetFanLevelThrowsException { get; set; } = false;

        /// <summary>
        /// Gets or sets the exception to throw during SetFanLevel() if SetFanLevelThrowsException is true.
        /// </summary>
        public Exception SetFanLevelException { get; set; } = new InvalidOperationException("Mock SetFanLevel failed");

        /// <summary>
        /// Gets or sets whether Shutdown should throw an exception.
        /// </summary>
        public bool ShutdownThrowsException { get; set; } = false;

        /// <summary>
        /// Gets or sets the exception to throw during Shutdown() if ShutdownThrowsException is true.
        /// </summary>
        public Exception ShutdownException { get; set; } = new InvalidOperationException("Mock Shutdown failed");

        /// <summary>
        /// Tracks all calls to DisableAutomaticFanControl for verification.
        /// </summary>
        public List<(bool alternate)> DisableAutomaticFanControlCalls { get; } = new();

        /// <summary>
        /// Tracks all calls to EnableAutomaticFanControl for verification.
        /// </summary>
        public List<(bool alternate)> EnableAutomaticFanControlCalls { get; } = new();

        /// <summary>
        /// Tracks all calls to SetFanLevel for verification.
        /// </summary>
        public List<(BzhFanIndex fanIndex, BzhFanLevel fanLevel)> SetFanLevelCalls { get; } = new();

        /// <summary>
        /// Tracks all calls to GetFanRpm for verification.
        /// </summary>
        public List<BzhFanIndex> GetFanRpmCalls { get; } = new();

        /// <summary>
        /// Tracks all calls to Initialize for verification.
        /// </summary>
        public int InitializeCalls { get; private set; } = 0;

        /// <summary>
        /// Tracks all calls to Shutdown for verification.
        /// </summary>
        public int ShutdownCalls { get; private set; } = 0;

        /// <summary>
        /// Simulates DellSmbiosBzh.Initialize().
        /// </summary>
        public bool Initialize()
        {
            InitializeCalls++;

            if (InitializeThrowsException)
                throw InitializeException;

            return InitializeReturnValue;
        }

        /// <summary>
        /// Simulates DellSmbiosBzh.Shutdown().
        /// </summary>
        public void Shutdown()
        {
            ShutdownCalls++;

            if (ShutdownThrowsException)
                throw ShutdownException;
        }

        /// <summary>
        /// Simulates DellSmbiosBzh.GetFanRpm().
        /// </summary>
        public float? GetFanRpm(BzhFanIndex fanIndex)
        {
            GetFanRpmCalls.Add(fanIndex);

            if (GetFanRpmThrowsException)
                throw GetFanRpmException;

            if (FanRpmMap.TryGetValue(fanIndex, out var rpm))
                return rpm;

            return null;
        }

        /// <summary>
        /// Simulates DellSmbiosBzh.DisableAutomaticFanControl().
        /// </summary>
        public bool DisableAutomaticFanControl(bool alternate = false)
        {
            DisableAutomaticFanControlCalls.Add((alternate));
            return DisableAutomaticFanControlReturnValue;
        }

        /// <summary>
        /// Simulates DellSmbiosBzh.EnableAutomaticFanControl().
        /// </summary>
        public bool EnableAutomaticFanControl(bool alternate = false)
        {
            EnableAutomaticFanControlCalls.Add((alternate));
            return EnableAutomaticFanControlReturnValue;
        }

        /// <summary>
        /// Simulates DellSmbiosBzh.SetFanLevel().
        /// </summary>
        public bool SetFanLevel(BzhFanIndex fanIndex, BzhFanLevel fanLevel)
        {
            SetFanLevelCalls.Add((fanIndex, fanLevel));

            if (SetFanLevelThrowsException)
                throw SetFanLevelException;

            return SetFanLevelReturnValue;
        }

        /// <summary>
        /// Resets all call tracking lists. Useful for re-using the mock in multiple test scenarios.
        /// </summary>
        public void ResetCallTracking()
        {
            InitializeCalls = 0;
            ShutdownCalls = 0;
            DisableAutomaticFanControlCalls.Clear();
            EnableAutomaticFanControlCalls.Clear();
            SetFanLevelCalls.Clear();
            GetFanRpmCalls.Clear();
        }
    }
}
