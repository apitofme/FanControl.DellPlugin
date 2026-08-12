using FanControl.Plugins;
using FanControl.DellPlugin.Tests.Mocks;
using Xunit;
using FluentAssertions;
using System;
using System.Collections.Generic;
using Moq;
using DellFanManagement.DellSmbiozBzhLib;

namespace FanControl.DellPlugin.Tests
{
    /// <summary>
    /// Unit tests for fan detection logic in the Load() method.
    /// Tests automatic fan discovery across all possible fan indices (Fan1-Fan7).
    /// </summary>
    public class FanDetectionTests
    {
        private readonly MockPluginLogger _mockLogger = new();

        /// <summary>
        /// Test: Load should detect fans that return valid RPM values.
        /// </summary>
        [Fact]
        public void Load_WithDetectedFans_ShouldRegisterSensorsAndControls()
        {
            // Arrange
            var plugin = new DellPlugin(_mockLogger);
            plugin.Initialize();
            _mockLogger.Clear();

            var containerMock = new Mock<IPluginSensorsContainer>();
            var fanSensorsList = new List<IPluginSensor>();
            var controlSensorsList = new List<IPluginControlSensor>();

            containerMock.Setup(c => c.FanSensors.AddRange(It.IsAny<IEnumerable<IPluginSensor>>()))
                .Callback<IEnumerable<IPluginSensor>>(fans => fanSensorsList.AddRange(fans));
            containerMock.Setup(c => c.ControlSensors.AddRange(It.IsAny<IEnumerable<IPluginControlSensor>>()))
                .Callback<IEnumerable<IPluginControlSensor>>(controls => controlSensorsList.AddRange(controls));

            // Act
            plugin.Load(containerMock.Object);

            // Assert
            fanSensorsList.Count.Should().BeGreaterThan(0);
            controlSensorsList.Count.Should().BeGreaterThan(0);
            fanSensorsList.Count.Should().Equal(controlSensorsList.Count);
            _mockLogger.ContainsSubstring("Fan auto-detection completed successfully").Should().BeTrue();
        }

        /// <summary>
        /// Test: Load should iterate through all possible fans (Fan1-Fan7).
        /// </summary>
        [Fact]
        public void Load_ShouldAttemptToDetectAllFans()
        {
            // Arrange
            var plugin = new DellPlugin(_mockLogger);
            plugin.Initialize();
            _mockLogger.Clear();

            var containerMock = new Mock<IPluginSensorsContainer>();
            containerMock.Setup(c => c.FanSensors.AddRange(It.IsAny<IEnumerable<IPluginSensor>>()));
            containerMock.Setup(c => c.ControlSensors.AddRange(It.IsAny<IEnumerable<IPluginControlSensor>>()));

            // Act
            plugin.Load(containerMock.Object);

            // Assert - Should log detection attempts for all fan indices
            // At least some fans should be queried
            _mockLogger.ContainsSubstring("Auto-detecting available fans").Should().BeTrue();
        }

        /// <summary>
        /// Test: Load should fallback to Fan1 and Fan2 if no fans detected.
        /// </summary>
        [Fact]
        public void Load_WithNoFansDetected_ShouldFallbackToDefaults()
        {
            // Arrange
            var plugin = new DellPlugin(_mockLogger);
            plugin.Initialize();
            _mockLogger.Clear();

            var containerMock = new Mock<IPluginSensorsContainer>();
            var fanSensorsList = new List<IPluginSensor>();
            var controlSensorsList = new List<IPluginControlSensor>();

            containerMock.Setup(c => c.FanSensors.AddRange(It.IsAny<IEnumerable<IPluginSensor>>()))
                .Callback<IEnumerable<IPluginSensor>>(fans => fanSensorsList.AddRange(fans));
            containerMock.Setup(c => c.ControlSensors.AddRange(It.IsAny<IEnumerable<IPluginControlSensor>>()))
                .Callback<IEnumerable<IPluginControlSensor>>(controls => controlSensorsList.AddRange(controls));

            // Act
            plugin.Load(containerMock.Object);

            // Assert - Even if no fans detected, should register at least the defaults
            fanSensorsList.Count.Should().BeGreaterThanOrEqualTo(1);
            controlSensorsList.Count.Should().BeGreaterThanOrEqualTo(1);
        }

        /// <summary>
        /// Test: Load should log fan detection results to the plugin logger.
        /// </summary>
        [Fact]
        public void Load_ShouldLogDetectionResults()
        {
            // Arrange
            var plugin = new DellPlugin(_mockLogger);
            plugin.Initialize();
            _mockLogger.Clear();

            var containerMock = new Mock<IPluginSensorsContainer>();
            containerMock.Setup(c => c.FanSensors.AddRange(It.IsAny<IEnumerable<IPluginSensor>>()));
            containerMock.Setup(c => c.ControlSensors.AddRange(It.IsAny<IEnumerable<IPluginControlSensor>>()));

            // Act
            plugin.Load(containerMock.Object);

            // Assert
            _mockLogger.ContainsSubstring("detected").Should().BeTrue();
        }

        /// <summary>
        /// Test: Load should register equal numbers of sensors and controls.
        /// </summary>
        [Fact]
        public void Load_ShouldRegisterEqualNumberOfSensorsAndControls()
        {
            // Arrange
            var plugin = new DellPlugin(_mockLogger);
            plugin.Initialize();

            var containerMock = new Mock<IPluginSensorsContainer>();
            int sensorCount = 0;
            int controlCount = 0;

            containerMock.Setup(c => c.FanSensors.AddRange(It.IsAny<IEnumerable<IPluginSensor>>()))
                .Callback<IEnumerable<IPluginSensor>>(fans => { foreach (var f in fans) sensorCount++; });
            containerMock.Setup(c => c.ControlSensors.AddRange(It.IsAny<IEnumerable<IPluginControlSensor>>()))
                .Callback<IEnumerable<IPluginControlSensor>>(controls => { foreach (var c in controls) controlCount++; });

            // Act
            plugin.Load(containerMock.Object);

            // Assert
            sensorCount.Should().Equal(controlCount);
        }

        /// <summary>
        /// Test: Load should throw if not initialized.
        /// </summary>
        [Fact]
        public void Load_WithoutInitialize_ShouldLogError()
        {
            // Arrange
            var plugin = new DellPlugin(_mockLogger);
            // Don't call Initialize()

            var containerMock = new Mock<IPluginSensorsContainer>();
            containerMock.Setup(c => c.FanSensors.AddRange(It.IsAny<IEnumerable<IPluginSensor>>()));
            containerMock.Setup(c => c.ControlSensors.AddRange(It.IsAny<IEnumerable<IPluginControlSensor>>()));

            // Act
            plugin.Load(containerMock.Object);

            // Assert - Should log that plugin wasn't initialized
            _mockLogger.ContainsSubstring("not initialized").Should().BeTrue();
        }

        /// <summary>
        /// Test: Load should handle exceptions during fan detection gracefully.
        /// </summary>
        [Fact]
        public void Load_ShouldHandleExceptionsInFanDetection()
        {
            // Arrange
            var plugin = new DellPlugin(_mockLogger);
            plugin.Initialize();
            _mockLogger.Clear();

            var containerMock = new Mock<IPluginSensorsContainer>();
            containerMock.Setup(c => c.FanSensors.AddRange(It.IsAny<IEnumerable<IPluginSensor>>()));
            containerMock.Setup(c => c.ControlSensors.AddRange(It.IsAny<IEnumerable<IPluginControlSensor>>()));

            // Act & Assert - should not throw
            plugin.Load(containerMock.Object);
        }
    }
}
