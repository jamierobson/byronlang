using System.Diagnostics;
using Byron.Compiler.CodeGen;
using Byron.Compiler.Driver;
using Byron.Compiler.Exceptions;
using Byron.Compiler.Lexer;
using Byron.Compiler.Parser;
using Byron.Compiler.SemanticAnalysis;

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
        var sourceText = await File.ReadAllTextAsync(filePath);
        
        Console.WriteLine("Parsing the following program");
        Console.WriteLine(sourceText);
        
        var tokens = new Tokenizer(sourceText).Tokenise();
        var highLevelAst = new ByronHighLevelAstParser(tokens).Parse();

        var semanticAnalysisResult = new SemanticAnalysisDriver(highLevelAst).Analyze();
        if (!semanticAnalysisResult.Success)
        {
            Console.WriteLine("Semantic Analysis failed:");
            foreach (var message in semanticAnalysisResult.Diagnostics.DiagnosticMessages)
            {
                Console.WriteLine(message);
            }

            return;
        }

        var lowLevelAst = new ByronLoweringPass(semanticAnalysisResult).Lower();
        Console.WriteLine("Parsed successfully to AST");
        var generatedCode = new LlvmIrGenerator().Generate(lowLevelAst);
        Console.WriteLine("Generated the following LLVM IR");
        Console.WriteLine(generatedCode);

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
        
        Console.WriteLine();
        Console.WriteLine();
        var exitState = InProcessExecutionEngine.Execute(generatedCode);
        Console.WriteLine($"Program executed: Exit state: {exitState}");
        Console.WriteLine();
    }
    catch (ByronNotImplementedException e)
    {
        Console.WriteLine(e.Message);
    }
    catch (ByronHighLevelParserException e)
    {
        Console.WriteLine($"{e.Message} at line {e.Span.Line} column {e.Span.Column}");
    }
    catch (ByronCodeGenerationException e)
    {
        Console.WriteLine($"{e.Message} at {e.StackTrace}");
    }
    catch(Exception e)
    {
        Console.WriteLine($"Unhandled Parser Exception: {e.Message}");
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