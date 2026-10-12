using System;
using System.Collections;
using System.Collections.Generic;
using Xunit;

namespace SleetLib.Tests
{
    /// <summary>
    /// Tests that change process environment variables must not run in parallel.
    /// </summary>
    [CollectionDefinition(Name, DisableParallelization = true)]
    public class EnvironmentVariableCollection
    {
        public const string Name = "Environment variables";
    }

    internal sealed class EnvironmentVariableScope : IDisposable
    {
        private readonly Dictionary<string, string> _originalValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public EnvironmentVariableScope(params (string Name, string Value)[] variables)
        {
            foreach (DictionaryEntry pair in Environment.GetEnvironmentVariables())
            {
                var key = pair.Key as string;
                if (key != null
                    && (key.StartsWith("SLEET_", StringComparison.OrdinalIgnoreCase)
                        || key.StartsWith("AWS_", StringComparison.OrdinalIgnoreCase)))
                {
                    _originalValues[key] = pair.Value as string;
                    Environment.SetEnvironmentVariable(key, null);
                }
            }

            foreach (var variable in variables)
            {
                if (!_originalValues.ContainsKey(variable.Name))
                {
                    _originalValues[variable.Name] = Environment.GetEnvironmentVariable(variable.Name);
                }

                Environment.SetEnvironmentVariable(variable.Name, variable.Value);
            }
        }

        public void Dispose()
        {
            foreach (DictionaryEntry pair in Environment.GetEnvironmentVariables())
            {
                var key = pair.Key as string;
                if (key != null
                    && (key.StartsWith("SLEET_", StringComparison.OrdinalIgnoreCase)
                        || key.StartsWith("AWS_", StringComparison.OrdinalIgnoreCase))
                    && !_originalValues.ContainsKey(key))
                {
                    Environment.SetEnvironmentVariable(key, null);
                }
            }

            foreach (var pair in _originalValues)
            {
                Environment.SetEnvironmentVariable(pair.Key, pair.Value);
            }
        }
    }
}
