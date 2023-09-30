using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ClassGenerator.CodeAnalysis
{
    public sealed class DocumentTemplate
    {
        public Project Project { get; set; }
        public string FileName { get; set; }
        public CompilationUnitSyntax Syntax { get; set; }
        public string[] Folder { get; set; }
    }
}