using Byron.Compiler.Lexer;

namespace Byron.Compiler.Exceptions;

public class ByronNotImplementedException(string notImplementedItemDescription, object callSite, SourceSpan span)
    : ByronException($"{notImplementedItemDescription} not implemented in {callSite.GetType().Name} at {span}")
{
    public ByronNotImplementedException(Type notImplementedType, object callSite, SourceSpan span) : this(notImplementedType.Name, callSite, span) {}
    public ByronNotImplementedException(TokenKind tokenKind, object callSite, SourceSpan span) : this(tokenKind.ToString(), callSite, span) {}
}