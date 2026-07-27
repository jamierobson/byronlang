using Byron.Compiler.AST;

namespace Byron.Compiler.Exceptions;

public class ByronNotImplementedException(string notImplementedItemDescription, object callSite)
    : ByronException($"{notImplementedItemDescription} not implemented in {callSite.GetType().Name}")
{
    public ByronNotImplementedException(Type notImplementedType, object callSite) : this(notImplementedType.Name, callSite)
    {
    }
}