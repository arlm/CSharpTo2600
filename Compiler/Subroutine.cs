﻿using CSharpTo2600.Framework;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace CSharpTo2600.Compiler
{
    internal class Subroutine
    {
        public readonly string Name;
        public readonly ImmutableArray<InstructionInfo> Instructions;
        public readonly MethodType Type;
        //@TODO - Handle comments.
        public int InstructionCount { get { return Instructions.Length; } }
        public int CycleCount { get { return Instructions.Sum(i => i.Cycles); } }

        public Subroutine(string Name, ImmutableArray<InstructionInfo> Instructions, MethodType Type)
        {
            this.Name = Name;
            this.Instructions = Instructions;
            this.Type = Type;
        }

        public Subroutine ReplaceInstructions(ImmutableArray<InstructionInfo> NewInstructions)
        {
            return new Subroutine(Name, NewInstructions, Type);
        }
    }
}
