using System.Collections.Generic;

namespace ReMindBackend
{
    public class MappingTable
    {
        public Dictionary<string, string> ConsoleWriteLine { get; } = new();
        public Dictionary<string, string> VariableDeclaration { get; } = new();
        public Dictionary<string, string> Assignment { get; } = new();
        public Dictionary<string, string> If { get; } = new();
        public Dictionary<string, string> ElseIf { get; } = new();
        public Dictionary<string, string> Else { get; } = new();
        public Dictionary<string, string> While { get; } = new();
    }
}
