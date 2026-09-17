using System;
using TopSolid.Kernel.Automating;

namespace TopSolid.Automation.Mcp.Server.AddIn.Automation
{
    // Testable transaction boundary. It never attempts to end a modification it did not start.
    internal static class ModificationScope
    {
        public static T Run<T>(string label, Func<string, bool> start, Action<bool, bool> end, Func<T> action)
        {
            if (!start(label)) throw new InvalidOperationException("TopSolid is busy. Finish its current command and request a new preview.");
            try
            {
                var result = action();
                end(true, true);
                return result;
            }
            catch (Exception failure)
            {
                try { end(false, false); }
                catch (Exception rollback) { throw new AggregateException("Modification failed and rollback could not be confirmed. Inspect TopSolid before retrying.", failure, rollback); }
                throw;
            }
        }
    }
}
