﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using TypeInfo = System.Reflection.TypeInfo;

namespace CSharpTo2600.Compiler
{
    public sealed partial class GameCompiler
    {
        private sealed class TypeCompiler
        {
            private readonly ProcessedType ParsedType;
            private Type CLRType { get { return ParsedType.CLRType; } }
            private TypeInfo TypeInfo { get { return CLRType.GetTypeInfo(); } }

            private TypeCompiler(ProcessedType ParsedType)
            {
                this.ParsedType = ParsedType;
            }

            public static ProcessedType CompileType(ProcessedType ParsedType, CompilationInfo Info, GameCompiler GCompiler)
            {
                var Compiler = new TypeCompiler(ParsedType);
                var CompiledMethods = Compiler.CompileMethods(Info, GCompiler);
                return new ProcessedType(ParsedType.CLRType, ParsedType.Symbol, CompiledMethods, ParsedType.Globals);
            }

            private ImmutableDictionary<IMethodSymbol, Subroutine> CompileMethods(CompilationInfo CompilationInfo, GameCompiler GCompiler)
            {
                var Result = new Dictionary<IMethodSymbol, Subroutine>();
                foreach (var SymbolSubPair in ParsedType.Subroutines)
                {
                    // Pretty sure methods can only have one declaration. Even implemented partial methods
                    // only have one declaration.
                    var MethodNode = (MethodDeclarationSyntax)SymbolSubPair.Key.DeclaringSyntaxReferences.Single().GetSyntax();
                    var MethodInfo = SymbolSubPair.Value.OriginalMethod;
                    var Model = GCompiler.Compilation.GetSemanticModel(MethodNode.SyntaxTree);
                    var CompiledSubroutine = MethodCompiler.CompileMethod(MethodInfo, 
                        SymbolSubPair.Key, CompilationInfo, Model);
                    Result.Add(SymbolSubPair.Key, CompiledSubroutine);
                }
                return Result.ToImmutableDictionary();
            }
        }
    }
}
