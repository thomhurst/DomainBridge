using System.Text;

namespace DomainBridge.SourceGenerators.Services
{
    internal class CodeBuilder
    {
        private readonly StringBuilder _sb = new StringBuilder();
        private int _indentLevel = 0;
        private const int IndentSize = 4;

        public void AppendLine(string? text = null)
        {
            if (string.IsNullOrEmpty(text))
            {
                _sb.AppendLine();
                return;
            }

            var indent = new string(' ', _indentLevel * IndentSize);
            _sb.AppendLine($"{indent}{text}");
        }

        public void OpenBlock(string text)
        {
            AppendLine(text);
            AppendLine("{");
            _indentLevel++;
        }

        public void CloseBlock()
        {
            _indentLevel--;
            AppendLine("}");
        }

        public void IncreaseIndent() => _indentLevel++;
        public void DecreaseIndent() => _indentLevel--;

        public override string ToString() => _sb.ToString();
    }
}