﻿using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.Text;

namespace VCSCompiler
{
    internal class ProcessedType
    {
		public string Name => TypeDefinition.Name;
		public string FullName => TypeDefinition.FullName;
		public IEnumerable<ProcessedField> Fields { get; }
		public IEnumerable<ProcessedSubroutine> Subroutines { get; }
		public TypeDefinition TypeDefinition { get; }

		public ProcessedType(TypeDefinition typeDefinition, IEnumerable<ProcessedField> fields, IEnumerable<ProcessedSubroutine> subroutines)
		{
			TypeDefinition = typeDefinition;
			Fields = fields;
			Subroutines = subroutines;
		}
	}
}