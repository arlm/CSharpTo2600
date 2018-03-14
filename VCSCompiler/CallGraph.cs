using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace VCSCompiler
{
	internal sealed class NeoCallGraph : Graph<MethodDefinition>
	{
		// TODO - Inject edge to static constructor.
		public static NeoCallGraph CreateFromEntryMethod(ProcessedSubroutine entryPoint)
		{
			var graph = new NeoCallGraph();
			graph.AddRootNode(entryPoint.MethodDefinition);
			AddCalleesToGraph(entryPoint.MethodDefinition);
			return graph;

			void AddCalleesToGraph(MethodDefinition method)
			{
				var callees = method.Body.Instructions
					.Where(i => i.OpCode == OpCodes.Call)
					.Select(i => i.Operand)
					.OfType<MethodDefinition>();

				foreach (var callee in callees)
				{
					graph.AddEdge(method, callee);
					AddCalleesToGraph(callee);
				}
			}
		}

		public string Print()
		{
			var stringBuilder = new StringBuilder();
			PrintInternal(Root, stringBuilder, 0);
			return stringBuilder.ToString();

			void PrintInternal(Node<MethodDefinition> node, StringBuilder builder, int indentLevel)
			{
				builder.AppendLine($"{new string('-', indentLevel)}{node.Value.Name}");
				foreach (var child in node.Neighbors)
				{
					PrintInternal(child, builder, indentLevel + 1);
				}
			}
		}
	}

	[Obsolete]
	internal sealed class CallGraph
	{
		public Node Root { get; }

		private CallGraph(Node root)
		{
			Root = root;
		}

		public static CallGraph CreateFromEntryMethod(ProcessedSubroutine entryPoint)
		{
			return new CallGraph(BuildNode(entryPoint.MethodDefinition));
		}

		public IEnumerable<Node> AllNodes()
		{
			ISet<Node> nodes = new HashSet<Node>();
			nodes.Add(Root);
			AddChildren(Root, nodes);
			return nodes.OrderBy(n => n.MethodDefinition.DeclaringType.Name).ThenBy(n => n.MethodDefinition.Name);

			void AddChildren(Node root, ISet<Node> nodeSet)
			{
				foreach (var child in root.Children)
				{
					nodeSet.Add(child);
					AddChildren(child, nodeSet);
				}
			}
		}

		public string Print()
		{
			var stringBuilder = new StringBuilder();
			PrintInternal(Root, stringBuilder, 0);
			return stringBuilder.ToString();

			void PrintInternal(Node node, StringBuilder builder, int indentLevel)
			{
				builder.AppendLine($"{new string('-', indentLevel)}{node.MethodDefinition.Name}");
				foreach (var child in node.Children)
				{
					PrintInternal(child, builder, indentLevel+1);
				}
			}
		}

		private static Node BuildNode(MethodDefinition methodDefinition)
		{
			var calls = methodDefinition.Body.Instructions.Where(i => i.OpCode.Code == Code.Call).Where(i => i.Operand is MethodDefinition);
			var children = calls.Select(call => BuildNode((MethodDefinition) call.Operand)).ToList();
			return new Node(methodDefinition, children);
		}

		public class Node
		{
			public MethodDefinition MethodDefinition { get; }
			public IEnumerable<Node> Children { get; }

			public Node(MethodDefinition methodDefinition, IEnumerable<Node> children)
			{
				MethodDefinition = methodDefinition;
				Children = children;
			}
		}
	}
}