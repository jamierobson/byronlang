using System.Diagnostics;
using Byron.Compiler.CodeGen;
using Byron.Compiler.Lexer;
using Byron.Compiler.Parser;

while (true)
{
    var fileToParse = PickFile();
    await TryParseFile(fileToParse);    
}

async Task TryParseFile(string filePath)
{
    var moduleName = Path.GetFileNameWithoutExtension(filePath);
    Console.WriteLine($"Parsing {filePath}...");

    try
    {
        var sourceFileLines = await File.ReadAllLinesAsync(filePath);
        var tokens = new Tokenizer(string.Concat(sourceFileLines)).Tokenise();
        var ast = new ByronAstParser(tokens).Parse();
        Console.WriteLine("Parsed successfully to AST");
        var generatedCode = new LlvmIrGenerator().Generate(ast);
        Console.WriteLine($"Generated the following LLVM IR: {generatedCode}");

        var outputIrPath = Path.Combine("./Out", $"{moduleName}.ll");
        var outputExePath = Path.ChangeExtension(outputIrPath, ".exe");
        await File.WriteAllTextAsync(outputIrPath, generatedCode);
        var clangProcess = new ProcessStartInfo
        {
            FileName = "clang",
            ArgumentList = { outputIrPath, "-o", outputExePath },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        using var clang = Process.Start(clangProcess);
        if (clang is null)
        {
            Console.Error.WriteLine("Could not find clang process.");
            return;
        }

        var stdout = await clang.StandardOutput.ReadToEndAsync();
        var stderr = await clang.StandardError.ReadToEndAsync();
        await clang.WaitForExitAsync();

        if (clang.ExitCode != 0)
        {
            Console.Error.WriteLine($"clang failed for {moduleName}:\n{stderr}");
        }
        else
        {
            Console.WriteLine($"Compiled successfully. Executable output to ${outputExePath}");
        }

        if (!string.IsNullOrWhiteSpace(stdout))
        {
            Console.WriteLine(stdout);
        }

    }
    catch (NotImplementedException e)
    {
        Console.WriteLine($"Not implemented: {e.Message}");
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