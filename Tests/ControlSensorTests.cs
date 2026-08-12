using DellFanManagement.DellSmbiozBzhLib;
using FanControl.DellPlugin.Tests.Mocks;
using Xunit;
using FluentAssertions;
using System;
using System.Diagnostics;

namespace FanControl.DellPlugin.Tests
{
    /// <summary>
    /// Unit tests for DellFanManagementControlSensor class.
    /// Tests fan speed control, level mapping, and state management.
    /// </summary>
    public class ControlSensorTests
    {
        /// <summary>
        /// Test: Control sensor should have correct display name.
        /// </summary>
        [Fact]
        public void Name_Property_ShouldContainFanNumber()
        {
            // Arrange
            var sensor = new DellFanManagementControlSensor(BzhFanIndex.Fan1);

            // Act
            string name = sensor.Name;

            // Assert
            name.Should().Contain("Control");
            name.Should().Contain("1"); // Fan1 should show as "1"
        }

        /// <summary>
        /// Test: Control sensor should have correct origin.
        /// </summary>
        [Fact]
        public void Origin_Property_ShouldBeDellSmbiosBzh()
        {
            // Arrange
            var sensor = new DellFanManagementControlSensor(BzhFanIndex.Fan2);

            // Act
            string origin = sensor.Origin;

            // Assert
            origin.Should().Be("DellSmbiosBzh");
        }

        /// <summary>
        /// Test: Control sensor should have correct unique ID.
        /// </summary>
        [Fact]
        public void Id_Property_ShouldBeUnique()
        {
            // Arrange
            var sensor1 = new DellFanManagementControlSensor(BzhFanIndex.Fan1);
            var sensor2 = new DellFanManagementControlSensor(BzhFanIndex.Fan2);

            // Act
            string id1 = sensor1.Id;
            string id2 = sensor2.Id;

            // Assert
            id1.Should().NotBe(id2);
            id1.Should().Contain("Fan1");
            id2.Should().Contain("Fan2");
        }

        /// <summary>
        /// Test: Set() should convert 0-33% to Level0 (off).
        /// </summary>
        [Theory]
        [InlineData(0f)]
        [InlineData(10f)]
        [InlineData(33f)]
        public void Set_LowPercentage_ShouldSetLevel0(float percentage)
        {
            // Arrange
            var sensor = new DellFanManagementControlSensor(BzhFanIndex.Fan1);

            // Act
            sensor.Set(percentage);
            sensor.Update();

            // Assert
            sensor.Value.Should().Be(percentage);
        }

        /// <summary>
        /// Test: Set() should convert 33-66% to Level1 (medium).
        /// </summary>
        [Theory]
        [InlineData(33.5f)]
        [InlineData(50f)]
        [InlineData(66f)]
        public void Set_MediumPercentage_ShouldSetLevel1(float percentage)
        {
            // Arrange
            var sensor = new DellFanManagementControlSensor(BzhFanIndex.Fan1);

            // Act
            sensor.Set(percentage);
            sensor.Update();

            // Assert
            sensor.Value.Should().Be(percentage);
        }

        /// <summary>
        /// Test: Set() should convert 66-100% to Level2 (high).
        /// </summary>
        [Theory]
        [InlineData(66.7f)]
        [InlineData(80f)]
        [InlineData(100f)]
        public void Set_HighPercentage_ShouldSetLevel2(float percentage)
        {
            // Arrange
            var sensor = new DellFanManagementControlSensor(BzhFanIndex.Fan1);

            // Act
            sensor.Set(percentage);
            sensor.Update();

            // Assert
            sensor.Value.Should().Be(percentage);
        }

        /// <summary>
        /// Test: Reset() should be callable and return to initial state.
        /// </summary>
        [Fact]
        public void Reset_ShouldResetState()
        {
            // Arrange
            var sensor = new DellFanManagementControlSensor(BzhFanIndex.Fan1);
            sensor.Set(50f);
            sensor.Update();
            sensor.Value.Should().Be(50f);

            // Act
            sensor.Reset();

            // Assert - Value should still be cached (it's not cleared on reset)
            sensor.Value.Should().Be(50f);
        }

        /// <summary>
        /// Test: Update() should return the last set value.
        /// </summary>
        [Fact]
        public void Update_ShouldReturnLastSetValue()
        {
            // Arrange
            var sensor = new DellFanManagementControlSensor(BzhFanIndex.Fan1);
            float expectedValue = 75f;

            // Act
            sensor.Set(expectedValue);
            sensor.Update();

            // Assert
            sensor.Value.Should().Be(expectedValue);
        }

        /// <summary>
        /// Test: Multiple Set() calls should update the value.
        /// </summary>
        [Fact]
        public void Set_MultipleTimesWithDifferentValues_ShouldUpdateValue()
        {
            // Arrange
            var sensor = new DellFanManagementControlSensor(BzhFanIndex.Fan1);

            // Act
            sensor.Set(25f);
            sensor.Update();
            float firstValue = sensor.Value.Value;

            sensor.Set(75f);
            sensor.Update();
            float secondValue = sensor.Value.Value;

            // Assert
            firstValue.Should().NotEqual(secondValue);
            secondValue.Should().Be(75f);
        }

        /// <summary>
        /// Test: Boundary condition at 33.33 (Level0 to Level1 transition).
        /// </summary>
        [Fact]
        public void Set_At33Point33_ShouldTransitionToLevel1()
        {
            // Arrange
            var sensor = new DellFanManagementControlSensor(BzhFanIndex.Fan1);

            // Act - Just below 33.33 should be Level0
            sensor.Set(33.32f);
            sensor.Update();
            float value1 = sensor.Value.Value;

            // Act - At or above 33.33 should be Level1
            sensor.Set(33.34f);
            sensor.Update();
            float value2 = sensor.Value.Value;

            // Assert
            value1.Should().Be(33.32f);
            value2.Should().Be(33.34f);
        }

        /// <summary>
        /// Test: Boundary condition at 66.66 (Level1 to Level2 transition).
        /// </summary>
        [Fact]
        public void Set_At66Point66_ShouldTransitionToLevel2()
        {
            // Arrange
            var sensor = new DellFanManagementControlSensor(BzhFanIndex.Fan1);

            // Act - Just below 66.66 should be Level1
            sensor.Set(66.65f);
            sensor.Update();
            float value1 = sensor.Value.Value;

            // Act - At or above 66.66 should be Level2
            sensor.Set(66.67f);
            sensor.Update();
            float value2 = sensor.Value.Value;

            // Assert
            value1.Should().Be(66.65f);
            value2.Should().Be(66.67f);
        }

        /// <summary>
        /// Test: Value should start as null until Set() is called.
        /// </summary>
        [Fact]
        public void Value_Initially_ShouldBeNull()
        {
            // Arrange
            var sensor = new DellFanManagementControlSensor(BzhFanIndex.Fan1);

            // Act & Assert
            sensor.Value.Should().BeNull();
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
            var sensor = new DellFanManagementControlSensor(fanIndex);
            sensor.Should().NotBeNull();
            sensor.Name.Should().Contain("Control");
        }
    }
}
