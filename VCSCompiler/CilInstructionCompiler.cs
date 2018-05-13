﻿using Mono.Cecil.Cil;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using System.Reflection;
using System.Linq;
using VCSFramework.Assembly;
using static VCSFramework.Assembly.AssemblyFactory;
using Mono.Cecil;
using VCSFramework;

namespace VCSCompiler
{
	/// <summary>
	/// Contains the logic for compiling individual CIL instructions to 6502 instructions.
	/// </summary>
    internal class CilInstructionCompiler
    {
		private readonly IImmutableDictionary<Code, Func<Instruction, ICompilationContext, IEnumerable<AssemblyLine>>> MethodMap;
		private readonly IImmutableDictionary<string, IProcessedType> Types;
	    private readonly MethodDefinition MethodDefinition;
	    private int CgtCount;

		public CilInstructionCompiler(MethodDefinition methodDefinition, IImmutableDictionary<string, IProcessedType> types)
		{
			MethodMap = CreateMethodMap();
			MethodDefinition = methodDefinition;
			Types = types;
		}

		public IEnumerable<AssemblyLine> CompileInstruction(Instruction instruction, ICompilationContext compilationContext)
			=> MethodMap[instruction.OpCode.Code](instruction, compilationContext);

		private IImmutableDictionary<Code, Func<Instruction, ICompilationContext, IEnumerable<AssemblyLine>>> CreateMethodMap()
		{
			var dictionary = new Dictionary<Code, Func<Instruction, ICompilationContext, IEnumerable<AssemblyLine>>>();
			var typeInfo = typeof(CilInstructionCompiler).GetTypeInfo();
			var opCodes = Enum.GetValues(typeof(Code)).Cast<Code>();
			foreach (var opCode in opCodes)
			{
				var name = Enum.GetName(typeof(Code), opCode);
				if (opCode >= Code.Ldc_I4_0 && opCode <= Code.Ldc_I4_8)
				{
					name = "Ldc_I4";
				}
				else if (opCode >= Code.Ldarg_0 && opCode <= Code.Ldarg_3)
				{
					name = "Ldarg";
				}
				else if (opCode >= Code.Ldloc_0 && opCode <= Code.Ldloc_3)
				{
					name = "Ldloc";
				}
				else if (opCode >= Code.Stloc_0 && opCode <= Code.Stloc_3)
				{
					name = "Stloc";
				}
				var method = typeInfo.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance);
				dictionary[opCode]
					= (Func<Instruction, ICompilationContext, IEnumerable<AssemblyLine>>)method?.CreateDelegate(typeof(Func<Instruction, ICompilationContext, IEnumerable<AssemblyLine>>), this)
					?? Unsupported;
			}
			return dictionary.ToImmutableDictionary();
		}

	    private IEnumerable<AssemblyLine> LoadArgument(Instruction instruction, ICompilationContext context)
	    {
		    if (instruction.Operand != null)
		    {
			    return LoadArgument(((ParameterReference) instruction.Operand).Index, context);
		    }
		    switch (instruction.OpCode.Code)
		    {
				case Code.Ldarg_0:
					return LoadArgument(0, context);
				case Code.Ldarg_1:
					return LoadArgument(1, context);
				case Code.Ldarg_2:
					return LoadArgument(2, context);
				case Code.Ldarg_3:
					return LoadArgument(3, context);
				default:
					throw new NotImplementedException();
		    }
	    }

	    private IEnumerable<AssemblyLine> LoadArgument(int index, ICompilationContext context)
	    {
		    var parameter = MethodDefinition.Parameters[index];
		    yield return LDA(LabelGenerator.GetFromParameter(parameter));
		    yield return PHA();
			context.EvaluationStack.Push(Types[parameter.ParameterType.FullName]);
	    }

		private IEnumerable<AssemblyLine> LoadConstant(Instruction instruction, ICompilationContext context)
		{
			byte value = 0;
			if (instruction.Operand != null)
			{
				try
				{
					value = Convert.ToByte(instruction.Operand);
				}
				catch (OverflowException)
				{
					throw new InvalidInstructionException(instruction, $"Constant value '{instruction.Operand}'must fit in a byte!");
				}
			}
			else
			{
				switch(instruction.OpCode.Code)
				{
					case Code.Ldc_I4_0:
						return LoadConstant(0, context);
					case Code.Ldc_I4_1:
						return LoadConstant(1, context);
					case Code.Ldc_I4_2:
						return LoadConstant(2, context);
					case Code.Ldc_I4_3:
						return LoadConstant(3, context);
					case Code.Ldc_I4_4:
						return LoadConstant(4, context);
					case Code.Ldc_I4_5:
						return LoadConstant(5, context);
					case Code.Ldc_I4_6:
						return LoadConstant(6, context);
					case Code.Ldc_I4_7:
						return LoadConstant(7, context);
					case Code.Ldc_I4_8:
						return LoadConstant(8, context);
				}
			}
			return LoadConstant(value, context);
		}

		private IEnumerable<AssemblyLine> LoadConstant(byte value, ICompilationContext context)
		{
			yield return LDA(value);
			yield return PHA();
			context.EvaluationStack.Push(Types[typeof(byte).FullName]);
		}

	    private IEnumerable<AssemblyLine> LoadLocal(Instruction instruction, ICompilationContext context)
	    {
		    if (instruction.Operand != null)
		    {
			    return LoadLocal(((VariableReference) instruction.Operand).Index, context);
		    }
		    else
		    {
			    switch (instruction.OpCode.Code)
			    {
					case Code.Ldloc_0:
						return LoadLocal(0, context);
					case Code.Ldloc_1:
						return LoadLocal(1, context);
					case Code.Ldloc_2:
						return LoadLocal(2, context);
					case Code.Ldloc_3:
						return LoadLocal(3, context);
					default:
						throw new NotImplementedException();
			    }
		    }
	    }

	    private IEnumerable<AssemblyLine> LoadLocal(int index, ICompilationContext context)
	    {
		    var local = MethodDefinition.Body.Variables[index];
		    yield return LDA(LabelGenerator.GetFromVariable(MethodDefinition, local));
		    yield return PHA();
			context.EvaluationStack.Push(Types[local.VariableType.FullName]);
	    }

	    private IEnumerable<AssemblyLine> StoreArgument(Instruction instruction, ICompilationContext context)
	    {
		    // Either Starg or Starg_S.
		    var parameter = (ParameterDefinition)instruction.Operand;
			yield return PLA();
			context.EvaluationStack.Pop();
		    yield return STA(LabelGenerator.GetFromParameter(parameter));
		}

	    private IEnumerable<AssemblyLine> StoreLocal(Instruction instruction, ICompilationContext context)
	    {
		    if (instruction.Operand != null)
		    {
			    return StoreLocal(((VariableReference) instruction.Operand).Index, context);
		    }
		    switch (instruction.OpCode.Code)
		    {
				case Code.Stloc_0:
					return StoreLocal(0, context);
				case Code.Stloc_1:
					return StoreLocal(1, context);
				case Code.Stloc_2:
					return StoreLocal(2, context);
				case Code.Stloc_3:
					return StoreLocal(3, context);
				default:
					throw new NotImplementedException();
		    }
	    }

	    private IEnumerable<AssemblyLine> StoreLocal(int index, ICompilationContext context)
	    {
		    var local = MethodDefinition.Body.Variables[index];
		    yield return PLA();
			context.EvaluationStack.Pop();
		    yield return STA(LabelGenerator.GetFromVariable(MethodDefinition, local));
	    }

		private IEnumerable<AssemblyLine> Add(Instruction instruction, ICompilationContext context)
		{
			// TODO - Should probably just allocate a couple address locations instead of trying to use the stack operations.
			yield return PLA();
			context.EvaluationStack.Pop();
			yield return STA(LabelGenerator.TemporaryRegister1);
			yield return PLA();
			context.EvaluationStack.Pop();
			yield return CLC();
			yield return ADC(LabelGenerator.TemporaryRegister1);
			yield return PHA();
			context.EvaluationStack.Push(Types[typeof(byte).FullName]);
		}

	    private IEnumerable<AssemblyLine> Br(Instruction instruction, ICompilationContext context)
	    {
			yield return JMP(LabelGenerator.GetFromInstruction((Instruction)instruction.Operand));
		}

	    private IEnumerable<AssemblyLine> Br_S(Instruction instruction, ICompilationContext context) => Br(instruction, context);

	    private IEnumerable<AssemblyLine> Brtrue(Instruction instruction, ICompilationContext context)
	    {
			yield return PLA();
			context.EvaluationStack.Pop();
		    yield return BNE(LabelGenerator.GetFromInstruction((Instruction)instruction.Operand));
		}

	    private IEnumerable<AssemblyLine> Brtrue_S(Instruction instruction, ICompilationContext context) => Brtrue(instruction, context);

		private IEnumerable<AssemblyLine> Call(Instruction instruction, ICompilationContext context)
		{
			// Could be either a MethodDefinition or MethodReference.
			MethodReference method = (MethodReference)instruction.Operand;

			var methodDeclaringType = method.DeclaringType.FullName;
			var processedSubroutine = Types[methodDeclaringType].Subroutines.Single(s => s.FullName == method.FullName);

			// Check if this method should be replaced with a direct store to a symbol (generally a TIA register).
			// Don't directly compare types since we may have received a different Framework assembly than what this library was built against.
			if (processedSubroutine.TryGetFrameworkAttribute<OverrideWithStoreToSymbolAttribute>(out var overrideStore))
			{
				if (!overrideStore.Strobe)
				{
					//TODO - We assume this is a 1-arg void method. Actually enforce this at the processing stage.
					if (method.Parameters.Count != 1)
					{
						throw new NotImplementedException($"{method.Name}, marked with {nameof(OverrideWithStoreToSymbolAttribute)}, must take 1 parameter for now.");
					}
					yield return PLA();
					context.EvaluationStack.Pop();
				}
				yield return STA(overrideStore.Symbol);
				yield break;
			}

			if (processedSubroutine.TryGetFrameworkAttribute<OverrideWithLoadToRegisterAttribute>(out var overrideRegisterLoad))
			{
				//TODO - We assume this is a 1-arg void method. Actually enforce this at the processing stage.
				if (method.Parameters.Count != 1)
				{
					throw new NotImplementedException($"{method.Name}, marked with {nameof(OverrideWithLoadToRegisterAttribute)} must take 1 parameter.");
				}
				yield return PLA();
				context.EvaluationStack.Pop();
				switch (overrideRegisterLoad.Register)
				{
					case "A":
						break;
					case "X":
						yield return TAX();
						break;
					case "Y":
						yield return TAY();
						break;
					default:
						throw new FatalCompilationException($"Attempted load to unknown register: {overrideRegisterLoad.Register}");
				}
				yield break;
			}

			if (processedSubroutine.TryGetFrameworkAttribute<OverrideWithLoadFromSymbolAttribute>(out var overrideLoad))
			{
				if (method.Parameters.Count != 0)
				{
					throw new NotImplementedException($"{method.Name}, marked with {nameof(OverrideWithLoadFromSymbolAttribute)}, must take 0 parameters.");
				}
				yield return LDA(overrideLoad.Symbol);
				yield return PHA();
				context.EvaluationStack.Push(Types[method.ReturnType.FullName]);
				yield break;
			}

			var parameters = ((MethodReference) method).Parameters.ToImmutableArray();
			if (parameters.Any())
			{
				// PLA arguments in reverse off stack and assign to parameters.
				foreach (var parameter in parameters.Reverse())
				{
					yield return PLA();
					context.EvaluationStack.Pop();
					yield return STA(LabelGenerator.GetFromParameter(parameter));
				}
			}

			if (processedSubroutine.TryGetFrameworkAttribute<AlwaysInlineAttribute>(out _))
			{
				var compiledSubroutine = Types[method.DeclaringType.FullName].Subroutines.Single(s => s.FullName == method.FullName) as CompiledSubroutine;
				if (compiledSubroutine == null)
				{
					throw new FatalCompilationException($"Attempted to inline method '{processedSubroutine.Name}' that hasn't been compiled yet. This suggests a bug in determining method compilation order.");
				}

				foreach (var assemblyLine in compiledSubroutine.Body)
				{
					//TODO - If the subroutine contains labels you can end up emitting duplicates if the inline subroutine is called more than once. Make them unique.
					//TODO - Once we have branching and multiple return statements this will explode.
					// In reality we probably want to replace RTS with JMP to a label inserted after this method body.
					if (!assemblyLine.Text.Contains("RTS"))
					{
						yield return assemblyLine;
					}
				}
				yield break;
			}

			yield return JSR(LabelGenerator.GetFromMethod(method));
		}

	    private IEnumerable<AssemblyLine> Cgt_Un(Instruction instruction, ICompilationContext context)
	    {
			// CLI says to push a 1 if true, 0 if false.
		    var endLabel = Label($"__CGT_UN_END_{CgtCount}");
		    var trueLabel = Label($"__CGT_UN_TRUE_{CgtCount}");
		    var falseLabel = Label($"__CGT_UN_FALSE_{CgtCount}");
		    CgtCount++;
			yield return PLA();
			context.EvaluationStack.Pop();
		    yield return STA(LabelGenerator.TemporaryRegister1);
		    yield return PLA();
			context.EvaluationStack.Pop();
		    yield return CMP(LabelGenerator.TemporaryRegister1);
		    yield return BEQ(falseLabel.Name);
		    yield return BCS(trueLabel.Name);

			// == and < fall to here.
		    yield return falseLabel;
		    yield return LDA(0);
		    yield return BEQ(endLabel.Name); // Always branches

			yield return trueLabel;
		    yield return LDA(1);

		    yield return endLabel;
			yield return PHA();
			context.EvaluationStack.Push(Types[typeof(byte).FullName]);
	    }

		/// <summary>
		/// Convert value on stack to int8, which it already should be.
		/// </summary>
		private IEnumerable<AssemblyLine> Conv_U1(Instruction instruction, ICompilationContext context) => Enumerable.Empty<AssemblyLine>();

		private IEnumerable<AssemblyLine> Initobj(Instruction instruction, ICompilationContext context)
		{
			var typeDefinition = (TypeDefinition)instruction.Operand;
			var processedType = Types[typeDefinition.FullName];

			yield return PLA();
			context.EvaluationStack.Pop();
			yield return TAX();

			yield return LDA(0);
			for(byte i = 0; i < processedType.TotalSize; i++)
			{
				yield return STA(i, Index.X);
			}
		}

	    private IEnumerable<AssemblyLine> Ldarg(Instruction instruction, ICompilationContext context) => LoadArgument(instruction, context);

	    private IEnumerable<AssemblyLine> Ldarg_S(Instruction instruction, ICompilationContext context) => LoadArgument(instruction, context);

	    private IEnumerable<AssemblyLine> Ldloc(Instruction instruction, ICompilationContext context) => LoadLocal(instruction, context);

	    private IEnumerable<AssemblyLine> Ldloc_S(Instruction instruction, ICompilationContext context) => LoadLocal(instruction, context);

		/// <summary>
		/// Pushes a constant uint8 onto the stack.
		/// </summary>
		/// <remarks>The spec says to push an int32, but that's impractical.</remarks>
		private IEnumerable<AssemblyLine> Ldc_I4(Instruction instruction, ICompilationContext context) => LoadConstant(instruction, context);

		/// <summary>
		/// Pushes a constant uint8 onto the stack.
		/// </summary>
		/// <remarks>The spec says to push an int32, but that's impractical.</remarks>
		private IEnumerable<AssemblyLine> Ldc_I4_S(Instruction instruction, ICompilationContext context) => Ldc_I4(instruction, context);

		private IEnumerable<AssemblyLine> Ldfld(Instruction instruction, ICompilationContext context)
		{
			var fieldDefinition = (FieldDefinition)instruction.Operand;

			var (containingType, processedField) = GetProcessedInfo(fieldDefinition);

			// Put address of instance in X.
			yield return PLA();
			context.EvaluationStack.Pop();
			yield return TAX();

			for (var i = 0; i < processedField.FieldType.TotalSize; i++)
			{
				var byteOffset = (byte)(containingType.FieldOffsets[processedField] + i);
				yield return LDA(byteOffset, Index.X);
				yield return PHA();
			}
			context.EvaluationStack.Push(processedField.FieldType);
		}

		private IEnumerable<AssemblyLine> Ldflda(Instruction instruction, ICompilationContext context)
		{
			var fieldDefinition = (FieldDefinition)instruction.Operand;

			var (containingType, processedField) = GetProcessedInfo(fieldDefinition);
			var fieldOffset = containingType.FieldOffsets[processedField];

			yield return PLA();
			context.EvaluationStack.Pop();
			if (fieldOffset != 0)
			{
				yield return CLC();
				yield return ADC(fieldOffset);
			}
			yield return PHA();
			// TODO - Push zeropage pointer.
			context.EvaluationStack.Push(Types[typeof(byte).FullName]);
		}

		private IEnumerable<AssemblyLine> Ldsfld(Instruction instruction, ICompilationContext context)
		{
			var fieldDefinition = (FieldDefinition)instruction.Operand;

			var (_, processedField) = GetProcessedInfo(fieldDefinition);

			if (processedField.FieldType.TotalSize == 1)
			{
				yield return LDA(LabelGenerator.GetFromField(fieldDefinition));
				yield return PHA();
				context.EvaluationStack.Push(processedField.FieldType);
				yield break;
			}
			
			for (var i = 0; i < processedField.FieldType.TotalSize; i++)
			{
				yield return LDA(LabelGenerator.GetFromField(fieldDefinition), i);
				yield return PHA();
				context.EvaluationStack.Push(processedField.FieldType);
			}
		}

		private IEnumerable<AssemblyLine> Ldsflda(Instruction instruction, ICompilationContext context)
		{
			var fieldDefinition = (FieldDefinition)instruction.Operand;
			yield return LDA($"#{LabelGenerator.GetFromField(fieldDefinition)}");
			yield return PHA();
			// TODO - Push zeropage pointer.
			context.EvaluationStack.Push(Types[typeof(byte).FullName]);
		}

		private IEnumerable<AssemblyLine> Nop(Instruction instruction, ICompilationContext context) => Enumerable.Empty<AssemblyLine>();

		private IEnumerable<AssemblyLine> Ret(Instruction instruction, ICompilationContext context)
		{
			// Assumes void return.
			yield return RTS();
		}

	    private IEnumerable<AssemblyLine> Starg(Instruction instruction, ICompilationContext context) => StoreArgument(instruction, context);

		private IEnumerable<AssemblyLine> Starg_S(Instruction instruction, ICompilationContext context) => StoreArgument(instruction, context);

	    private IEnumerable<AssemblyLine> Stloc(Instruction instruction, ICompilationContext context) => StoreLocal(instruction, context);

	    private IEnumerable<AssemblyLine> Stloc_S(Instruction instruction, ICompilationContext context) => StoreLocal(instruction, context);

		private IEnumerable<AssemblyLine> Stfld(Instruction instruction, ICompilationContext context)
		{
			var fieldDefinition = (FieldDefinition)instruction.Operand;

			var (containingType, processedField) = GetProcessedInfo(fieldDefinition);

			var byteOffset = containingType.FieldOffsets[processedField];
			var fieldSize = processedField.FieldType.TotalSize;

			if (fieldSize == 1)
			{
				// Put value to store in X.
				yield return PLA();
				yield return TAX();

				// Put address of containing object in Y.
				yield return PLA();
				yield return TAY();
				
				yield return STX(byteOffset, Index.Y);
			}
			else
			{
				// Unfortunately the value comes before the target address, and there isn't any stack-relative addressing on the 6502.
				// So we'll have to get to the address with some stack pointer arithmetic.
				yield return TSX();
				// Offsets are purely additive, so we'll use unchecked to give us a byte that, when added, wraps around to what we want.
				// The stack pointer points to the location where the next byte will be pushed, so we need to subtract 1 to get to the value.
				// Then we just subtract the size of the value to get to the address.
				byte offsetToAddress = unchecked((byte)-(fieldSize + 1));
				yield return LDY(offsetToAddress, Index.X);
				// TODO - Depending on size of value might be more time-efficient to just use absolute addressing with the Y register?
				yield return TYA();
				yield return TAX();

				for (var offset = fieldSize - 1; offset >= 0; offset--)
				{
					yield return PLA();
					yield return STA((byte)offset, Index.X);
				}
			}
			context.EvaluationStack.Pop();
		}

		private IEnumerable<AssemblyLine> Stsfld(Instruction instruction, ICompilationContext context)
		{
			var fieldDefinition = (FieldDefinition)instruction.Operand;

			var (_, processedField) = GetProcessedInfo(fieldDefinition);

			if (processedField.FieldType.TotalSize == 1)
			{
				yield return PLA();
				context.EvaluationStack.Pop();
				yield return STA(LabelGenerator.GetFromField(fieldDefinition));
				yield break;
			}
			
			for (var i = processedField.FieldType.TotalSize - 1; i >= 0; i--)
			{
				yield return PLA();
				context.EvaluationStack.Pop();
				yield return STA(LabelGenerator.GetFromField(fieldDefinition), i);
			}
		}

	    private IEnumerable<AssemblyLine> Sub(Instruction instruction, ICompilationContext context)
	    {
		    yield return PLA();
			context.EvaluationStack.Pop();
		    yield return STA(LabelGenerator.TemporaryRegister1);
		    yield return PLA();
			context.EvaluationStack.Pop();
		    yield return SEC();
		    yield return SBC(LabelGenerator.TemporaryRegister1);
		    yield return PHA();
			context.EvaluationStack.Push(Types[typeof(byte).FullName]);
	    }

		private IEnumerable<AssemblyLine> Unsupported(Instruction instruction, ICompilationContext compilationContext) => throw new UnsupportedOpCodeException(instruction.OpCode);

		private (IProcessedType ContainingType, ProcessedField ProcessedField) GetProcessedInfo(FieldDefinition fieldDefinition)
		{
			var containingType = fieldDefinition.DeclaringType;
			var processedType = Types[containingType.FullName];
			var processedField = processedType.Fields.Single(pf => pf.FieldDefinition == fieldDefinition);

			return (processedType, processedField);
		}
	}
}
