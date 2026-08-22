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
        var sourceLines = File.ReadAllLines(filePath);
        var sourceText = await File.ReadAllTextAsync(filePath);

        Console.WriteLine("Parsing the following program");
        var totalLines = sourceLines.Length + 1;
        var maxDigits = totalLines.ToString().Length;
        var lineNumber = 1;
        foreach (var line in sourceLines)
        {
            Console.WriteLine($"{lineNumber.ToString().PadLeft(maxDigits, '0')}: {line}");
            lineNumber++;
        }

        var tokens = new Tokenizer(sourceText).Tokenise();
        var tokenizedFile = new TokenizedFile(filePath, tokens);
        var highLevelAst = new ByronHighLevelAstParser(tokenizedFile).Parse();

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

        var lowered = new ByronLoweringPass(semanticAnalysisResult).Lower();
        Console.WriteLine("Parsed successfully to AST");
        var generatedCode = new LlvmIrGenerator(lowered).Generate();
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
    catch (ByronHighLevelParserException e)
    {
        Console.WriteLine($"Error during token parsing: {e.Message} at line {e.Span.Line} column {e.Span.Column}");
    }
    catch (ByronSemanticAnalysisException e)
    {
        Console.WriteLine($"Error during semantic analysis: {e.Message}");
        Console.WriteLine("Compilation errors:");
        foreach (var diagnosticsDiagnosticMessage in e.Diagnostics.DiagnosticMessages)
        {
            Console.WriteLine(diagnosticsDiagnosticMessage);
        }
    }
    catch (ByronLowLevelParserException e)
    {
        Console.WriteLine($"Error during lowering: {e.Message} at {e.StackTrace}");
    }
    catch (ByronCodeGenerationException e)
    {
        Console.WriteLine($"Error during code generation: {e.Message} at {e.StackTrace}");
    }
    catch (ByronNotImplementedException e)
    {
        Console.WriteLine(e.Message);
    }
    catch(Exception e)
    {
        Console.WriteLine($"Unhandled Parser Exception: {e.GetType()}: {e.Message}");
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