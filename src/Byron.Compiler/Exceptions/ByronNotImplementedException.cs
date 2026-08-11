using Byron.Compiler.AST;
using Byron.Compiler.Lexer;

namespace Byron.Compiler.Exceptions;

public class ByronNotImplementedException : ByronException
{
    public ByronNotImplementedException(string notImplementedItemDescription, object callSite): base($"{notImplementedItemDescription} not implemented in {callSite.GetType().Name}"){}
    public ByronNotImplementedException(Type notImplementedType, object callSite) : this(notImplementedType.Name, callSite) {}
    public ByronNotImplementedException(string notImplementedItemDescription, object callSite, SourceSpan span): base($"{notImplementedItemDescription} not implemented in {callSite.GetType().Name} at {span}"){}
    public ByronNotImplementedException(Type notImplementedType, object callSite, SourceSpan span) : this(notImplementedType.Name, callSite, span) {}
    public ByronNotImplementedException(TokenKind tokenKind, object callSite, SourceSpan span) : this(tokenKind.ToString(), callSite, span) {}
}