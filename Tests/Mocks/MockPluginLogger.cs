using FanControl.Plugins;
using System;
using System.Collections.Generic;

namespace FanControl.DellPlugin.Tests.Mocks
{
    /// <summary>
    /// Mock implementation of IPluginLogger for unit testing.
    /// 
    /// This class captures all log messages so tests can verify that proper logging occurred.
    /// It allows tests to check which messages were logged and in what order.
    /// </summary>
    public class MockPluginLogger : IPluginLogger
    {
        /// <summary>
        /// Gets all messages that have been logged in order.
        /// </summary>
        public List<string> LoggedMessages { get; } = new();

        /// <summary>
        /// Gets all error messages that have been logged (messages containing "ERROR").
        /// </summary>
        public List<string> ErrorMessages => new(LoggedMessages.FindAll(m => m.Contains("ERROR")));

        /// <summary>
        /// Gets all warning messages that have been logged (messages containing "WARNING").
        /// </summary>
        public List<string> WarningMessages => new(LoggedMessages.FindAll(m => m.Contains("WARNING")));

        /// <summary>
        /// Gets all exception-related messages that have been logged (messages containing "Exception").
        /// </summary>
        public List<string> ExceptionMessages => new(LoggedMessages.FindAll(m => m.Contains("Exception")));

        /// <summary>
        /// Gets the total number of log calls made.
        /// </summary>
        public int CallCount => LoggedMessages.Count;

        /// <summary>
        /// Logs a message.
        /// </summary>
        /// <param name="message">The message to log.</param>
        public void Log(string message)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            LoggedMessages.Add(message);
        }

        /// <summary>
        /// Clears all logged messages. Useful for resetting the mock between test scenarios.
        /// </summary>
        public void Clear()
        {
            LoggedMessages.Clear();
        }

        /// <summary>
        /// Checks if a specific message was logged (exact match).
        /// </summary>
        /// <param name="message">The message to search for.</param>
        /// <returns>True if the exact message was logged; false otherwise.</returns>
        public bool ContainsMessage(string message)
        {
            return LoggedMessages.Contains(message);
        }

        /// <summary>
        /// Checks if any logged message contains the specified substring.
        /// </summary>
        /// <param name="substring">The substring to search for.</param>
        /// <returns>True if any logged message contains the substring; false otherwise.</returns>
        public bool ContainsSubstring(string substring)
        {
            return LoggedMessages.Exists(m => m.Contains(substring));
        }

        /// <summary>
        /// Gets the first logged message that contains the specified substring.
        /// </summary>
        /// <param name="substring">The substring to search for.</param>
        /// <returns>The first matching message, or null if not found.</returns>
        public string GetFirstMessageContaining(string substring)
        {
            return LoggedMessages.Find(m => m.Contains(substring));
        }

        /// <summary>
        /// Gets all logged messages that contain the specified substring.
        /// </summary>
        /// <param name="substring">The substring to search for.</param>
        /// <returns>A list of matching messages.</returns>
        public List<string> GetMessagesContaining(string substring)
        {
            return new(LoggedMessages.FindAll(m => m.Contains(substring)));
        }

        /// <summary>
        /// Verifies that a message matching the given pattern was logged.
        /// Useful for testing that specific operations were logged.
        /// </summary>
        /// <param name="pattern">A substring that should appear in a logged message.</param>
        /// <exception cref="InvalidOperationException">Thrown if no matching message was found.</exception>
        public void AssertLogged(string pattern)
        {
            if (!ContainsSubstring(pattern))
                throw new InvalidOperationException($"Expected to find logged message containing '{pattern}', but no matching message was found. Logged messages: {string.Join("; ", LoggedMessages)}");
        }

        /// <summary>
        /// Verifies that at least N messages matching the given pattern were logged.
        /// </summary>
        /// <param name="pattern">A substring that should appear in logged messages.</param>
        /// <param name="expectedCount">The minimum number of matching messages expected.</param>
        /// <exception cref="InvalidOperationException">Thrown if fewer matching messages were found than expected.</exception>
        public void AssertLoggedCount(string pattern, int expectedCount)
        {
            var matching = GetMessagesContaining(pattern);
            if (matching.Count < expectedCount)
                throw new InvalidOperationException($"Expected at least {expectedCount} messages containing '{pattern}', but found {matching.Count}. Logged messages: {string.Join("; ", LoggedMessages)}");
        }
    }
}
