﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

namespace CSharpTo2600.Compiler
{
    // The hierarchy only tells you if a method may call another method ever.
    // It may call a method 0 times (e.g. if(SomeConditionFalseAtRuntime){Call();} )
    // It may call a method 1 time.
    // It may call a method some arbitrary number of times (loops, gotos, etc).
    // That specific number (including 0 or 1) is not provided.
    // The absense of a method from a Calls list indicates that the method will never
    // be called from there, specifically by no invocation for that method existing
    // anywhere in the body of the method.
    /// <summary>
    /// Immutable representation of a method call hierarchy.
    /// </summary>
    public sealed class MethodCallHierarchy
    {
        private readonly ImmutableDictionary<IMethodSymbol, HierarchyNode> SymbolToNode;
        public static readonly MethodCallHierarchy Empty
            = new MethodCallHierarchy(ImmutableDictionary<IMethodSymbol, HierarchyNode>.Empty);
        //@TODO - Limit to methods with special types. No point considering user-defined methods with no callers.
        /// <summary>
        /// Returns all nodes representing methods with no callers.
        /// </summary>
        public IEnumerable<HierarchyNode> AllRoots { get { return SymbolToNode.Values.Where(n => n.Callers.Count() == 0); } }
        /// <summary>
        /// Returns the maximum possible method call depth (e.g. A() calls B() calls C() has a depth of 3).
        /// There is no guarantee that the returned depth will be reached (or if its even possible to reach)
        /// at runtime.
        /// </summary>
        public int MaxMethodDepth { get { return CalculateMaxMethodDepth(); } }

        private MethodCallHierarchy(ImmutableDictionary<IMethodSymbol, HierarchyNode> SymbolToNode)
        {
            this.SymbolToNode = SymbolToNode;
        }

        internal MethodCallHierarchy Replace(HierarchyNode Node)
        {
            if (!Contains(Node.Method))
            {
                throw new ArgumentException("Attempted to replace a node that isn't in the hierarchy.");
            }
            return new MethodCallHierarchy(SymbolToNode.SetItem(Node.Method, Node));
        }

        internal MethodCallHierarchy Add(HierarchyNode Node)
        {
            return new MethodCallHierarchy(SymbolToNode.Add(Node.Method, Node));
        }

        public HierarchyNode LookupMethod(IMethodSymbol Symbol)
        {
            return SymbolToNode[Symbol];
        }

        public bool Contains(IMethodSymbol Symbol)
        {
            return SymbolToNode.ContainsKey(Symbol);
        }

        /// <summary>
        /// Creates a string representing the hierarchies for each root method (methods with no callers).
        /// </summary>
        public string PrintHierarchyForRoots()
        {
            Action<HierarchyNode, StringBuilder, int> PrintHierarchy = null;
            PrintHierarchy =
                (Root, Builder, IndentationCount) =>
                {
                    Builder.AppendLine($"{new string(' ', IndentationCount)}{Root.Method.Name.ToString()}");
                    foreach (var Call in Root.Calls)
                    {
                        PrintHierarchy(Call, Builder, IndentationCount + 1);
                    }
                };

            var StringBuilder = new StringBuilder();
            foreach (var Root in AllRoots)
            {
                PrintHierarchy(Root, StringBuilder, 0);
            }
            return StringBuilder.ToString();
        }

        private int CalculateMaxMethodDepth()
        {
            Func<HierarchyNode, int, int> RecursiveMaxDepth = null;
            RecursiveMaxDepth =
                (Node, Depth) =>
                {
                    int MaxDepthFromHere = Depth + 1;
                    foreach (var Call in Node.Calls)
                    {
                        MaxDepthFromHere = Math.Max(MaxDepthFromHere, RecursiveMaxDepth(Call, Depth + 1));
                    }
                    return MaxDepthFromHere;
                };

            var MaxDepth = 0;
            foreach (var Root in AllRoots)
            {
                MaxDepth = Math.Max(MaxDepth, RecursiveMaxDepth(Root, 0));
            }
            return MaxDepth;
        }
    }

    /// <summary>
    /// Immutable representation of a method and both: what possibly calls it, and what it possibly calls.
    /// </summary>
    public sealed class HierarchyNode
    {
        public IMethodSymbol Method { get; }
        /// <summary>
        /// All methods that this method may call 0 or more times.
        /// Order is not significant.
        /// </summary>
        public ImmutableArray<HierarchyNode> Calls;
        /// <summary>
        /// All methods that may call this method 0 or more times.
        /// Order is not significant.
        /// </summary>
        public ImmutableArray<HierarchyNode> Callers;

        private HierarchyNode(IMethodSymbol Symbol)
        {
            Method = Symbol;
            Calls = ImmutableArray<HierarchyNode>.Empty;
            Callers = ImmutableArray<HierarchyNode>.Empty;
        }

        private HierarchyNode(HierarchyNode Base, ImmutableArray<HierarchyNode>? Calls = null,
            ImmutableArray<HierarchyNode>? Callers = null)
        {
            Method = Base.Method;
            this.Calls = Calls ?? Base.Calls;
            this.Callers = Callers ?? Base.Callers;
        }

        internal static HierarchyNode CreateEmptyNode(IMethodSymbol Symbol)
        {
            return new HierarchyNode(Symbol);
        }

        internal HierarchyNode WithCaller(HierarchyNode Caller)
        {
            return new HierarchyNode(this, Callers: Callers.Add(Caller));
        }

        internal HierarchyNode WithCall(HierarchyNode Call)
        {
            return new HierarchyNode(this, Calls: Calls.Add(Call));
        }

        public override string ToString()
        {
            return Method.Name;
        }
    }
    
    internal sealed class HierarchyBuilder : CSharpSyntaxWalker
    {
        private readonly SemanticModel Model;
        private MethodCallHierarchy Hierarchy;
        private HierarchyNode Node;

        private HierarchyBuilder(HierarchyNode Node, MethodCallHierarchy Hierarchy, SemanticModel Model)
        {
            this.Node = Node;
            this.Hierarchy = Hierarchy;
            this.Model = Model;
        }

        /// <summary>
        /// Builds a new MethodCallHierarchy by recursively exploring every method invocation encountered.
        /// </summary>
        public static MethodCallHierarchy RecursiveBuilder(IMethodSymbol Origin, MethodCallHierarchy Hierarchy,
            SemanticModel Model)
        {
            if (Hierarchy.Contains(Origin))
            {
                throw new InvalidOperationException("Attempted to begin traversal with an existing node.");
            }
            var Node = HierarchyNode.CreateEmptyNode(Origin);
            Hierarchy = Hierarchy.Add(Node);
            var Builder = new HierarchyBuilder(Node, Hierarchy, Model);
            Builder.Visit(Origin.DeclaringSyntaxReferences.Single().GetSyntax());
            return Builder.Hierarchy;
        }

        public override void VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            var InvokedMethodSymbol = (IMethodSymbol)Model.GetSymbolInfo(node).Symbol;
            if (!Hierarchy.Contains(InvokedMethodSymbol))
            {
                Hierarchy = HierarchyBuilder.RecursiveBuilder(InvokedMethodSymbol, Hierarchy, Model);
            }
            var OtherNode = Hierarchy.LookupMethod(InvokedMethodSymbol);
            Node = Node.WithCall(OtherNode);
            OtherNode = OtherNode.WithCaller(Node);
            Hierarchy = Hierarchy.Replace(OtherNode).Replace(Node);
            // Recursion breaks the hierarchy tree, and is just not supported in general.
            if (Recurses(Node, Node.Method))
            {
                throw new RecursionException(Node.Method);
            }
        }

        // Note we can't know for sure if a method is recursive (it might be in a conditional
        // that avoids recursion or something), so we err on the side of caution if a possiblity
        // exists at all.
        private bool Recurses(HierarchyNode Node, IMethodSymbol With)
        {
            foreach(var Child in Node.Calls)
            {
                if(Child.Method == With)
                {
                    return true;
                }
                else
                {
                    return Recurses(Child, With);
                }
            }
            return false;
        }
    }

}
