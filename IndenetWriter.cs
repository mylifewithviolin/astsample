using System;
using System.Text;

namespace ReMindBackend
{
    public class IndentWriter
    {
        private readonly StringBuilder _sb = new();
        private int _indent = 0;
        private const string IndentString = "    ";

        public void Indent() => _indent++;
        public void Unindent() => _indent--;

        public void WriteLine(string text = "")
        {
            for (int i = 0; i < _indent; i++)
                _sb.Append(IndentString);

            _sb.AppendLine(text);
        }

        public override string ToString() => _sb.ToString();
    }
}






