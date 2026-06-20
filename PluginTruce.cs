using System;

namespace HouseRegions
{
    public class PluginTrace
    {
        private readonly string _prefix;

        public PluginTrace(string prefix)
        {
            _prefix = prefix;
        }

        public void WriteLineInfo(string message)
        {
            Console.WriteLine($"{_prefix}[INFO] {message}");
        }

        public void WriteLineError(string message)
        {
            Console.WriteLine($"{_prefix}[ERROR] {message}");
        }

        public void WriteLineError(string format, params object?[] args)
        {
            Console.WriteLine($"{_prefix}[ERROR] {string.Format(format, args)}");
        }
    }
}
