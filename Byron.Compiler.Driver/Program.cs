using Byron.Compiler.Lexer;
using Byron.Compiler.Parser;

while (true)
{
    var fileToParse = PickFile();
    await TryParseFile(fileToParse);    
}

async Task TryParseFile(string filePath)
{
    Console.WriteLine($"Parsing {filePath}...");

    try
    {
        var sourceFileLines = await File.ReadAllLinesAsync(filePath);
        var tokens = new Tokenizer(string.Concat(sourceFileLines)).Tokenise();
        var ast = new ByronAstParser(tokens).Parse();
        Console.WriteLine("Parsed successfully to AST");
        _ = ast;
    }
    catch (ByronParserException e)
    {
        Console.WriteLine($"{e.Message} at line {e.Span.Line} column {e.Span.Column}");
    }
    catch(Exception e)
    {
        Console.WriteLine($"Parser Error: {e.Message}");
    }
}

string PickFile()
{
    var sampleFiles = Directory.EnumerateFiles("./Samples").ToArray();
    
    var fileOptions = sampleFiles.Select((filePath, index) => new KeyValuePair<int, string>(index, filePath)).ToDictionary(x => x.Key, x => x.Value);

    while (true)
    {
        Console.WriteLine("Choose a sample file");
        foreach (var fileOption in fileOptions)
        {
            Console.WriteLine($"{fileOption.Key}: {Path.GetFileName(fileOption.Value)}");
        }

        var userInputIsInt = int.TryParse(Console.ReadLine(), out var parsedUserInput);
        if (!userInputIsInt)
        {
            continue;
        }

        if (fileOptions.TryGetValue(parsedUserInput, out var file))
        {
            return file;
        }
    }
    
    
}