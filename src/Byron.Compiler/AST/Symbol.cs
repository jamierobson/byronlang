namespace Byron.Compiler.AST;
public record Symbol(string[] Segments)
{
    protected Symbol(Symbol original)
    {
        _toString = null; 
        Segments = original.Segments;
    }
    
    public string MemberName => Segments.Length == 0 ? string.Empty : Segments[^1]; //todo: We might not need or want this. string.Empty is a reductive case of the global module symbol.  
 
    private string? _toString;
    public override string ToString()
    {
        return _toString ??= string.Join(".", Segments);
    }

    public virtual bool Equals(Symbol? other) => other is not null && Segments.SequenceEqual(other.Segments);
    
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var segment in Segments)
        {
            hash.Add(segment);
        }
        return hash.ToHashCode();
    }

    public static Symbol From(string name) => new ([..name.Split('.')]);
    public static Symbol From(string[] nameSegments) => new (nameSegments);
    public static Symbol From(IEnumerable<string> nameSegments) => new (nameSegments.ToArray());
    public static readonly Symbol Empty =  new ([]); 
    
}

public record PrimitiveTypeSymbol(string TypeName, int ByteSize, bool IsSigned) : Symbol([TypeName]);