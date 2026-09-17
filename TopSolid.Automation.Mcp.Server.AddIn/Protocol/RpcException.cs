using System;

namespace TopSolid.Automation.Mcp.Server.AddIn.Protocol
{
    internal sealed class RpcException : Exception
    {
        public RpcException(int code, string message) : base(message) { Code = code; }
        public int Code { get; }
    }
}
