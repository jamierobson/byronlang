using Byron.Compiler.AST.LowLevel;
using Byron.Compiler.Exceptions;

namespace Byron.Compiler.CodeGen;

public partial class LlvmIrGenerator
{
    private void GenerateStatement(StatementNode node)
    {
        switch (node)
        {
            case IfStatementNode @if:
                GenerateIfStatement(@if);
                break;
            case WhileStatement @while:
                GenerateWhileStatement(@while);
                break;
            case BreakStatement:
                GenerateBreakStatement();
                break;
            case ContinueStatement:
                GenerateContinueStatement();
                break;
            case ReturnStatementNode statement:
                GenerateReturnStatement(statement);
                break;
            case VariableDeclarationNode declaration:
                GenerateVariableDeclarationStatement(declaration);
                break;
            case AssignmentStatementNode assign:
                GenerateAssignStatement(assign);
                break;
            default:
                throw new ByronNotImplementedException(node.GetType(), this);
        }
    }

    private void GenerateAssignStatement(AssignmentStatementNode node)
    {
        string stackPointer;
        if (node.Target is VariableExpressionNode variable)
        {
            stackPointer = _context.LookupVariable(variable.Name);
        }
        else
        {
            throw new ByronNotImplementedException(node.Target.GetType(), this);
        }

        var (value, llvmType) = GenerateExpression(node.Value);
        _context.EmitLine($"    store {llvmType} {value}, {llvmType}* {stackPointer}");
    }

    private void GenerateWhileStatement(WhileStatement node)
    {
        var loopId = _context.AllocateLabelId();
        var condLabel = $"while_cond_{loopId}";
        var bodyLabel = $"while_body_{loopId}";
        var exitLabel = $"while_exit_{loopId}";

        _context.EmitLine($"    br label %{condLabel}");

        _context.EmitLine($"\n{condLabel}:");
        var (condValue, condType) = GenerateExpression(node.ContinuationCondition);
        if (condType != "i1")
        {
            throw new ByronCodeGenerationException($"While condition must be boolean (i1), got {condType}");
        }
        _context.EmitLine($"    br i1 {condValue}, label %{bodyLabel}, label %{exitLabel}");

        _context.EmitLine($"\n{bodyLabel}:");
    
        _context.PushLoop(continueLabel: condLabel, breakLabel: exitLabel);
        GenerateBlockStatement(node.Body);
        _context.PopLoop();

        if (!BlockEndsWithTerminator(node.Body))
        {
            _context.EmitLine($"    br label %{condLabel}");
        }

        _context.EmitLine($"\n{exitLabel}:");
    }
    
    private void GenerateBreakStatement()
    {
        var (_, breakLabel) = _context.CurrentLoop;
        _context.EmitLine($"    br label %{breakLabel}");
    }

    private void GenerateContinueStatement()
    {
        var (continueLabel, _) = _context.CurrentLoop;
        _context.EmitLine($"    br label %{continueLabel}");
    }

    private void GenerateBlockStatement(BlockStatementNode node)
    {
        foreach (var statement in node.Statements)
        {
            GenerateStatement(statement);
        }
    }

    private void GenerateReturnStatement(ReturnStatementNode node)
    {
        if (node.Expression == null)
        {
            _context.EmitLine("    ret void");
            return;
        }

        var (returnValue, returnType) = GenerateExpression(node.Expression);
        _context.EmitLine($"    ret {returnType} {returnValue}");
    }
    
    private void GenerateVariableDeclarationStatement(VariableDeclarationNode node)
    {
        var (variableValue, variableType) = GenerateExpression(node.Initializer);

        var stackPointer = $"%{node.Name}.addr";
        _context.DeclareVariable(node.Name, stackPointer);
        _context.EmitLine($"    {stackPointer} = alloca {variableType}");
        _context.EmitLine($"    store {variableType} {variableValue}, {variableType}* {stackPointer}");
    }
    
    private void GenerateIfStatement(IfStatementNode node)
    {
        var (condValue, condType) = GenerateExpression(node.Condition);
        if (condType != "i1")
        {
            throw new ByronCodeGenerationException($"If condition must be a boolean (i1), but got {condType}");
        }

        var branchId = _context.AllocateLabelId(); // Assuming your context has a counter helper
        var thenLabel = $"if_then_{branchId}";
        var elseLabel = $"if_else_{branchId}";
        var mergeLabel = $"if_merge_{branchId}";

        var falsePathLabel = node is IfElseStatementNode ? elseLabel : mergeLabel;

        _context.EmitLine($"    br i1 {condValue}, label %{thenLabel}, label %{falsePathLabel}");

        _context.EmitLine($"\n{thenLabel}:");
        GenerateBlockStatement(node.ThenBranch);
        
        var thenTerminates = BlockEndsWithTerminator(node.ThenBranch);
        
        if (!thenTerminates)
        {
            _context.EmitLine($"    br label %{mergeLabel}");
        }

        bool elseTerminates;
        if (node is IfElseStatementNode ifElseStatementNode)
        {
            _context.EmitLine($"\n{elseLabel}:");
            GenerateBlockStatement(ifElseStatementNode.ElseBranch);

            elseTerminates = BlockEndsWithTerminator(ifElseStatementNode.ElseBranch); 
            if (!elseTerminates)
            {
                _context.EmitLine($"    br label %{mergeLabel}");
            }
        }
        else
        {
            elseTerminates = false;
        }

        if (!thenTerminates || !elseTerminates)
        {
            _context.EmitLine($"\n{mergeLabel}:");
        }
    }

    private static bool BlockEndsWithTerminator(BlockStatementNode block)
    {
        if (block.Statements.Count == 0) return false;
        var last = block.Statements[^1];
    
        return last is ReturnStatementNode or BreakStatement or ContinueStatement;
    }
}