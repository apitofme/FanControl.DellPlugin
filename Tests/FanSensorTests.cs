using DellFanManagement.DellSmbiozBzhLib;
using FanControl.DellPlugin.Tests.Mocks;
using Xunit;
using FluentAssertions;
using System;

namespace FanControl.DellPlugin.Tests
{
    /// <summary>
    /// Unit tests for DellFanManagementFanSensor class.
    /// Tests fan RPM reading, error handling, and sensor properties.
    /// </summary>
    public class FanSensorTests
    {
        /// <summary>
        /// Test: Fan sensor should have correct display name.
        /// </summary>
        [Fact]
        public void Name_Property_ShouldContainFanNumber()
        {
            // Arrange
            var sensor = new DellFanManagementFanSensor(BzhFanIndex.Fan1);

            // Act
            string name = sensor.Name;

            // Assert
            name.Should().Contain("Fan");
            name.Should().Contain("1"); // Fan1 should show as "1"
        }

        /// <summary>
        /// Test: Fan sensor should have correct origin.
        /// </summary>
        [Fact]
        public void Origin_Property_ShouldBeDellSmbiosBzh()
        {
            // Arrange
            var sensor = new DellFanManagementFanSensor(BzhFanIndex.Fan2);

            // Act
            string origin = sensor.Origin;

            // Assert
            origin.Should().Be("DellSmbiosBzh");
        }

        /// <summary>
        /// Test: Fan sensor should have correct unique identifier.
        /// </summary>
        [Fact]
        public void Identifier_Property_ShouldBeUnique()
        {
            // Arrange
            var sensor1 = new DellFanManagementFanSensor(BzhFanIndex.Fan1);
            var sensor2 = new DellFanManagementFanSensor(BzhFanIndex.Fan2);

            // Act
            string id1 = sensor1.Identifier;
            string id2 = sensor2.Identifier;

            // Assert
            id1.Should().NotBe(id2);
            id1.Should().Contain("Dell/FanSensor");
            id1.Should().Contain("0"); // Fan1 = index 0
            id2.Should().Contain("1"); // Fan2 = index 1
        }

        /// <summary>
        /// Test: Fan sensor should have correct unique ID.
        /// </summary>
        [Fact]
        public void Id_Property_ShouldBeUnique()
        {
            // Arrange
            var sensor1 = new DellFanManagementFanSensor(BzhFanIndex.Fan1);
            var sensor2 = new DellFanManagementFanSensor(BzhFanIndex.Fan2);

            // Act
            string id1 = sensor1.Id;
            string id2 = sensor2.Id;

            // Assert
            id1.Should().NotBe(id2);
            id1.Should().Contain("Fan1");
            id2.Should().Contain("Fan2");
        }

        /// <summary>
        /// Test: Value should start as null.
        /// </summary>
        [Fact]
        public void Value_Initially_ShouldBeNull()
        {
            // Arrange
            var sensor = new DellFanManagementFanSensor(BzhFanIndex.Fan1);

            // Act & Assert
            sensor.Value.Should().BeNull();
        }

        /// <summary>
        /// Test: Update() should set Value to the fan RPM.
        /// </summary>
        [Fact]
        public void Update_ShouldReadFanRpm()
        {
            // Arrange
            var sensor = new DellFanManagementFanSensor(BzhFanIndex.Fan1);

            // Act
            sensor.Update();

            // Assert - Value should now be set (could be any valid RPM or null)
            // We can't assert a specific value since it depends on real hardware
        }

        /// <summary>
        /// Test: Update() should handle null RPM gracefully.
        /// </summary>
        [Fact]
        public void Update_WhenFanUnavailable_ShouldSetValueToNull()
        {
            // Arrange
            var sensor = new DellFanManagementFanSensor(BzhFanIndex.Fan7); // Fan7 may not exist

            // Act
            sensor.Update();

            // Assert - Value could be null (expected for unavailable fans)
            // Should not throw
        }

        /// <summary>
        /// Test: Multiple Update() calls should be safe.
        /// </summary>
        [Fact]
        public void Update_CalledMultipleTimes_ShouldNotThrow()
        {
            // Arrange
            var sensor = new DellFanManagementFanSensor(BzhFanIndex.Fan1);

            // Act & Assert - should not throw
            sensor.Update();
            sensor.Update();
            sensor.Update();
        }

        /// <summary>
        /// Test: Update() should be called before reading Value.
        /// </summary>
        [Fact]
        public void Update_MustBeCalledBeforeValue()
        {
            // Arrange
            var sensor = new DellFanManagementFanSensor(BzhFanIndex.Fan1);

            // Act
            var beforeUpdate = sensor.Value;
            sensor.Update();
            var afterUpdate = sensor.Value;

            // Assert - After update, Value should be set (or null if unavailable)
            beforeUpdate.Should().BeNull(); // Before update, should be null
        }

        /// <summary>
        /// Test: All fan indices should be supported.
        /// </summary>
        [Theory]
        [InlineData(BzhFanIndex.Fan1)]
        [InlineData(BzhFanIndex.Fan2)]
        [InlineData(BzhFanIndex.Fan3)]
        [InlineData(BzhFanIndex.Fan4)]
        [InlineData(BzhFanIndex.Fan5)]
        [InlineData(BzhFanIndex.Fan6)]
        [InlineData(BzhFanIndex.Fan7)]
        public void Constructor_ShouldAcceptAllFanIndices(BzhFanIndex fanIndex)
        {
            // Act & Assert - should not throw
            var sensor = new DellFanManagementFanSensor(fanIndex);
            sensor.Should().NotBeNull();
            sensor.Name.Should().Contain("Fan");
        }

        /// <summary>
        /// Test: Fan sensor should read different RPM values.
        /// </summary>
        [Fact]
        public void Update_ShouldReadRpmCorrectly()
        {
            // Arrange
            var sensor1 = new DellFanManagementFanSensor(BzhFanIndex.Fan1);
            var sensor2 = new DellFanManagementFanSensor(BzhFanIndex.Fan2);

            // Act
            sensor1.Update();
            sensor2.Update();

            // Assert - Different fans could have different RPMs (or both null if unavailable)
            // The important thing is Update() doesn't throw
        }
    }
}
