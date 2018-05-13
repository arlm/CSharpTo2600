using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;
using Mono.Cecil.Cil;
using Mono.Cecil;
using VCSFramework.Assembly;
using System.Collections.Immutable;
using System.Reflection;

namespace VCSCompiler
{
    internal class CilCompiler
    {
		public static IEnumerable<AssemblyLine> CompileMethod(MethodDefinition definition, ControlFlowGraph controlFlowGraph, IImmutableDictionary<string, ProcessedType> types, Assembly frameworkAssembly)
		{
			var instructionCompiler = new CilInstructionCompiler(definition, types);
			var instructions = definition.Body.Instructions;
			var compilationActions = ProcessInstructions(instructions, types, frameworkAssembly).ToArray();
			var instructionsToLabel = GetInstructionsToEmitLabelsFor(instructions).ToArray();
			var compiledBody = new List<AssemblyLine>();
			var evaluationStacks = new Dictionary<BasicBlock, Stack<ProcessedType>>();
			foreach (var block in controlFlowGraph.Graph.Nodes)
			{
				evaluationStacks[block] = new Stack<ProcessedType>();
			}

			BasicBlock previousBasicBlock = null;
			foreach (var action in compilationActions)
			{
				var needLabel = action.ConsumedInstructions.Where(i => instructionsToLabel.Contains(i)).ToArray();
				foreach (var toLabel in needLabel)
				{
					compiledBody.Add(AssemblyFactory.Label(LabelGenerator.GetFromInstruction(toLabel)));
				}

				Stack<ProcessedType> evaluationStack = null;
				if (action is CompileCompilationAction)
				{
					// TODO - Move deep nesting elsewhere.
					var basicBlock = controlFlowGraph.BlockContainingInstruction(action.ConsumedInstructions.Single());
					evaluationStack = evaluationStacks[basicBlock];
					if (basicBlock != previousBasicBlock)
					{
						Console.WriteLine($"Picked evaluation stack for block: {basicBlock}");
						if (previousBasicBlock != null)
						{
							var previousEvaluationStack = evaluationStacks[previousBasicBlock];
							// If we're moving onto a new basic block and the previous one still has values in its evaluation stack,
							// then chances are the next attempt to pop() is going to throw.
							// For the specific case of a basic block that has only one edge going to it (from the previous basic block),
							// we will transfer the remaining values in its evaluation stack to the current one.
							if (previousEvaluationStack?.Count > 0)
							{
								Console.WriteLine($" Previous evaluation stack still has {previousEvaluationStack.Count} type[s] in it!");
								var neighborsTo = controlFlowGraph.Graph.GetNeighborsTo(basicBlock);
								Console.WriteLine($" {neighborsTo.Count()} blocks have an edge to the new basic block");
								if (neighborsTo.Count() == 1)
								{
									Console.WriteLine(" Pushing previous evaluation stack's remaining values onto the current evaluation stack");
									foreach (var value in previousEvaluationStack.Reverse())
									{
										evaluationStack.Push(value);
									}
									previousEvaluationStack.Clear();
								}
							}
						}
					}
					previousBasicBlock = basicBlock;
				}
				var compilationContext = new CompilationContext(instructionCompiler, evaluationStack);
				compiledBody.AddRange(action.Execute(compilationContext));
			}

			compiledBody = OptimizeMethod(compiledBody).ToList();
			return compiledBody;
		}

		private static IEnumerable<ICompilationAction> ProcessInstructions(IEnumerable<Instruction> instructions, IImmutableDictionary<string, IProcessedType> types, Assembly frameworkAssembly)
		{
			var actions = new List<ICompilationAction>();

			// We iterate over the instructions backwards since compile time executable methods have constants loaded
			// prior to the actual call. We don't know they're meant for such a method until we hit the call instruction.
			// Whereas when going backwards we hit the call first, and then can work out which constant loads to 
			// avoid processing later on.
			foreach(var instruction in instructions.Reverse())
			{
				if (actions.Any(a => a.ConsumedInstructions.Contains(instruction)))
				{
					continue;
				}

				if (instruction.OpCode == OpCodes.Call && IsCompileTimeExecutable((MethodReference)instruction.Operand))
				{
					actions.Add(CreateExecuteCommand(instruction, types, frameworkAssembly));
				}
				else
				{
					actions.Add(new CompileCompilationAction(instruction));
				}
			}

			return ((IEnumerable<ICompilationAction>)actions).Reverse();

			bool IsCompileTimeExecutable(MethodReference methodDefinition)
			{
				if (types.TryGetValue(methodDefinition.DeclaringType.FullName, out var processedType))
				{
					var processedSubroutine = processedType.Subroutines.SingleOrDefault(s => s.MethodDefinition.FullName == methodDefinition.FullName);
					return processedSubroutine?.TryGetFrameworkAttribute<VCSFramework.CompileTimeExecutedMethodAttribute>(out _) == true;
				}
				return false;
			}
		}

		private static ICompilationAction CreateExecuteCommand(Instruction instruction, IImmutableDictionary<string, IProcessedType> types, Assembly frameworkAssembly)
		{
			var nextInstruction = instruction.Next;
			var methodDefinition = (MethodReference)instruction.Operand;
			var processedType = types[methodDefinition.DeclaringType.FullName];
			var processedSubroutine = processedType.Subroutines.Single(s => s.MethodDefinition.FullName == methodDefinition.FullName);

			return new ExecuteCompilationAction(instruction, processedSubroutine, frameworkAssembly);
		}

	    private static IEnumerable<AssemblyLine> OptimizeMethod(IEnumerable<AssemblyLine> body)
	    {
		    var mutableBody = body.ToList();

			// Remove redundant PHA/PLA pairs.
			var pairs = mutableBody.OfType<AssemblyInstruction>().Zip(mutableBody.OfType<AssemblyInstruction>().Skip(1), Tuple.Create);
			var phaPlaPairs = pairs.Where(p => p.Item1.OpCode == "PHA" && p.Item2.OpCode == "PLA").ToArray();

			foreach(var pair in phaPlaPairs)
			{
				mutableBody.RemoveAll(line => ReferenceEquals(line, pair.Item1));
				mutableBody.RemoveAll(line => ReferenceEquals(line, pair.Item2));
			}
			
			Console.WriteLine($"Eliminated {phaPlaPairs.Length} redundant PHA/PLA pairs.");

			// Remove LDAs following a STA to the same argument.
			pairs = mutableBody.OfType<AssemblyInstruction>().Zip(mutableBody.OfType<AssemblyInstruction>().Skip(1), Tuple.Create);
			var staLdaPairs = pairs
				.Where(p => p.Item1.OpCode == "STA" && p.Item2.OpCode == "LDA")
				.Where(p => p.Item1.Argument == p.Item2.Argument)
				.ToArray();

			foreach(var pair in staLdaPairs)
			{
				mutableBody.RemoveAll(line => ReferenceEquals(line, pair.Item2));
			}
			
			Console.WriteLine($"Eliminated {staLdaPairs.Length} redundant LDAs.");
			return mutableBody;
	    }
		
		/// <summary>
		/// Gets instructions that need to be labeled, generally for branching instructions.
		/// </summary>
		private static IEnumerable<Instruction> GetInstructionsToEmitLabelsFor(IEnumerable<Instruction> instructions)
		{
			foreach(var instruction in instructions)
			{
				// Branch opcodes have an Instruction as their operand.
				var targetInstruction = instruction.Operand as Instruction;
				if (targetInstruction != null)
				{
					Console.WriteLine($"{instruction} references {targetInstruction}, marking to emit label.");
					yield return targetInstruction;
				}
			}
		}
    }
}
