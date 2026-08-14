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
                throw new ByronNotImplementedException(node.GetType(), this, node.SourceNode.Span);
        }
    }

    private void GenerateAssignStatement(AssignmentStatementNode node)
    {
        string targetPointer;
        LlvmType expectedLlvmType;
        if (node.Target is VariableExpressionNode variable)
        {
            var symbolAddress = _context.LookupVariable(variable.Name);
            targetPointer = symbolAddress.Pointer.ToString();
            expectedLlvmType = symbolAddress.LlvmType;
        }
        else if (node.Target is MemberAccessExpressionNode memberAccess)
        {
            (targetPointer, expectedLlvmType) = GenerateMemberAccessPointer(memberAccess);
        }
        else
        {
            throw new ByronNotImplementedException(node.Target.GetType(), this, node.Target.SourceNode.Span);
        }

        var (value, llvmType) = GenerateExpression(node.Value);
        _context.EmitLine($"    store {llvmType} {value}, {expectedLlvmType}* {targetPointer}");
    }
    
    private (string FieldPointerRegister, LlvmType FieldType) GenerateMemberAccessPointer(MemberAccessExpressionNode node)
    {
        string targetPointerRegister;
        LlvmType targetType;

        if (node.Target is VariableExpressionNode variable)
        {
            // Get pointer directly from symbol table, DO NOT load value!
            var symbolAddress = _context.LookupVariable(variable.Name);
            targetPointerRegister = symbolAddress.Pointer.ToString();
            targetType = symbolAddress.LlvmType;
        }
        else if (node.Target is MemberAccessExpressionNode nested)
        {
            // Chained member access: e.g. foo.bar.baz
            (targetPointerRegister, targetType) = GenerateMemberAccessPointer(nested);
        }
        else
        {
            // Fallback for expressions that evaluate to values (e.g., getPoint().x)
            var (valReg, valType) = GenerateExpression(node.Target);
            targetPointerRegister = _context.AllocateRegister();
            _context.EmitLine($"    {targetPointerRegister} = alloca {valType}");
            _context.EmitLine($"    store {valType} {valReg}, {valType}* {targetPointerRegister}");
            targetType = valType;
        }

        if (targetType is not LlvmType.Struct structType)
        {
            throw new ByronCodeGenerationException($"Cannot access member '{node.MemberName}' on non-struct type '{targetType}'.");
        }

        var layout = _context.GetStructLayout(structType.Name);
        var fieldIndex = layout.GetFieldIndex(node.MemberName);
        var fieldType = LlvmType.From(layout.GetFieldType(node.MemberName));

        var fieldPointerRegister = _context.AllocateRegister();
        _context.EmitLine($"    {fieldPointerRegister} = getelementptr {structType}, {structType}* {targetPointerRegister}, i32 0, i32 {fieldIndex}");

        return (fieldPointerRegister, fieldType);
    }

    private void GenerateWhileStatement(WhileStatement node)
    {
        var loopId = _context.AllocateLabelId();
        var conditionLabel = $"while_cond_{loopId}";
        var bodyLabel = $"while_body_{loopId}";
        var exitLabel = $"while_exit_{loopId}";

        _context.EmitLine($"    br label %{conditionLabel}");

        _context.EmitLine($"\n{conditionLabel}:");
        var (conditionValue, conditionType) = GenerateExpression(node.ContinuationCondition);
        if (conditionType is not LlvmType.Boolean)
        {
            throw new ByronCodeGenerationException($"While condition must be boolean (i1), got {conditionType}");
        }
        _context.EmitLine($"    br i1 {conditionValue}, label %{bodyLabel}, label %{exitLabel}");

        _context.EmitLine($"\n{bodyLabel}:");
    
        _context.PushLoop(continueLabel: conditionLabel, breakLabel: exitLabel);
        GenerateBlockStatement(node.Body);
        _context.PopLoop();

        if (!BlockEndsWithTerminator(node.Body))
        {
            _context.EmitLine($"    br label %{conditionLabel}");
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

        var stackPointerName = $"{node.Name}.addr";
        var stackPointerRegister = $"%{stackPointerName}";
        
        var symbolType = node.ExplicitType is not null ? LlvmType.From(node.ExplicitType) : variableType;
        var address = new SymbolAddress(new Value.Register(stackPointerName), symbolType);
        
        _context.DeclareVariable(node.Name, address);
        _context.EmitLine($"    {stackPointerRegister} = alloca {variableType}");
        _context.EmitLine($"    store {variableType} {variableValue}, {variableType}* {stackPointerRegister}");
    }
    
    private void GenerateIfStatement(IfStatementNode node)
    {
        var (conditionValue, conditionType) = GenerateExpression(node.Condition);
        if (conditionType is not LlvmType.Boolean)
        {
            throw new ByronCodeGenerationException($"If condition must be a boolean (i1), but got {conditionType}");
        }

        var branchId = _context.AllocateLabelId();
        var thenLabel = $"if_then_{branchId}";
        var elseLabel = $"if_else_{branchId}";
        var mergeLabel = $"if_merge_{branchId}";

        var falsePathLabel = node is IfElseStatementNode ? elseLabel : mergeLabel;

        _context.EmitLine($"    br i1 {conditionValue}, label %{thenLabel}, label %{falsePathLabel}");

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