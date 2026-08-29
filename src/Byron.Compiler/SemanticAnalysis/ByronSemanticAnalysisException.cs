using Byron.Compiler.Exceptions;

namespace Byron.Compiler.SemanticAnalysis;

public class ByronSemanticAnalysisException(string message, Diagnostics diagnostics) : ByronException(message)
{
    public Diagnostics Diagnostics = diagnostics;
}