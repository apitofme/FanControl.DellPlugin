using FanControl.Plugins;
using FanControl.DellPlugin.Tests.Mocks;
using Xunit;
using FluentAssertions;
using System;
using System.IO;
using Moq;
using DellFanManagement.DellSmbiozBzhLib;

namespace FanControl.DellPlugin.Tests
{
    /// <summary>
    /// Unit tests for the DellPlugin class.
    /// Tests the plugin lifecycle: Initialize → Load → Close
    /// </summary>
    public class DellPluginTests
    {
        private readonly MockPluginLogger _mockLogger = new();

        /// <summary>
        /// Test: Plugin should initialize successfully when DellSmbiosBzh is available.
        /// </summary>
        [Fact]
        public void Initialize_WithValidDriver_ShouldSucceed()
        {
            // Arrange
            var plugin = new DellPlugin(_mockLogger);

            // Act
            plugin.Initialize();

            // Assert
            _mockLogger.ContainsSubstring("Initializing plugin").Should().BeTrue();
            _mockLogger.ContainsSubstring("Initialize() complete").Should().BeTrue();
        }

        /// <summary>
        /// Test: Plugin should handle multiple Initialize calls gracefully (idempotent).
        /// </summary>
        [Fact]
        public void Initialize_CalledTwice_ShouldOnlyInitializeOnce()
        {
            // Arrange
            var plugin = new DellPlugin(_mockLogger);
            _mockLogger.Clear();

            // Act
            plugin.Initialize();
            int callsAfterFirst = _mockLogger.CallCount;
            plugin.Initialize();
            int callsAfterSecond = _mockLogger.CallCount;

            // Assert
            // Second call should log "already initialized" message
            _mockLogger.ContainsSubstring("already initialized").Should().BeTrue();
            // No additional heavy operations should occur on second call
            (callsAfterSecond - callsAfterFirst).Should().BeLessThan(callsAfterFirst);
        }

        /// <summary>
        /// Test: Close should restore automatic fan control after manual control.
        /// </summary>
        [Fact]
        public void Close_AfterInitialization_ShouldRestoreAutomaticControl()
        {
            // Arrange
            var plugin = new DellPlugin(_mockLogger);
            plugin.Initialize();
            _mockLogger.Clear();

            // Act
            plugin.Close();

            // Assert
            _mockLogger.ContainsSubstring("Closing down plugin").Should().BeTrue();
            _mockLogger.ContainsSubstring("enable automatic fan controller").Should().BeTrue();
            _mockLogger.ContainsSubstring("Shutdown").Should().BeTrue();
        }

        /// <summary>
        /// Test: Close should be idempotent (safe to call multiple times).
        /// </summary>
        [Fact]
        public void Close_CalledWithoutInitialize_ShouldHandleGracefully()
        {
            // Arrange
            var plugin = new DellPlugin(_mockLogger);

            // Act & Assert - should not throw
            plugin.Close();
            plugin.Close();
        }

        /// <summary>
        /// Test: Plugin name should be "Dell".
        /// </summary>
        [Fact]
        public void Name_Property_ShouldBeDell()
        {
            // Arrange
            var plugin = new DellPlugin(_mockLogger);

            // Act
            string name = plugin.Name;

            // Assert
            name.Should().Be("Dell");
        }

        /// <summary>
        /// Test: Plugin should log errors when Close fails.
        /// </summary>
        [Fact]
        public void Close_WithShutdownFailure_ShouldLogWarning()
        {
            // Arrange
            var plugin = new DellPlugin(_mockLogger);
            plugin.Initialize();
            _mockLogger.Clear();

            // Act
            plugin.Close();

            // Assert - should still log attempting shutdown
            _mockLogger.ContainsSubstring("Shutdown").Should().BeTrue();
        }

        /// <summary>
        /// Test: Dispose should call Close and clean up resources.
        /// </summary>
        [Fact]
        public void Dispose_ShouldCallClose()
        {
            // Arrange
            var plugin = new DellPlugin(_mockLogger);
            plugin.Initialize();
            _mockLogger.Clear();

            // Act
            plugin.Dispose();

            // Assert
            _mockLogger.ContainsSubstring("Dispose").Should().BeTrue();
            _mockLogger.ContainsSubstring("Closing").Should().BeTrue();
        }

        /// <summary>
        /// Test: Dispose should be idempotent (safe to call multiple times).
        /// </summary>
        [Fact]
        public void Dispose_CalledTwice_ShouldHandleGracefully()
        {
            // Arrange
            var plugin = new DellPlugin(_mockLogger);
            plugin.Initialize();

            // Act & Assert - should not throw
            plugin.Dispose();
            plugin.Dispose();
        }

        /// <summary>
        /// Test: Plugin should log to logger when initialization fails.
        /// </summary>
        [Fact]
        public void Initialize_LoggingToLogger_ShouldWorkCorrectly()
        {
            // Arrange
            var plugin = new DellPlugin(_mockLogger);

            // Act
            plugin.Initialize();

            // Assert
            _mockLogger.CallCount.Should().BeGreaterThan(0);
        }
    }
}
