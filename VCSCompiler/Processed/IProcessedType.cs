using Mono.Cecil;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace VCSCompiler
{
	interface IProcessedType
    {
		string Name { get; }
		string FullName { get; }
		IProcessedType BaseType { get; }
		IEnumerable<ProcessedField> Fields { get; }
		IImmutableDictionary<ProcessedField, byte> FieldOffsets { get; }
		IImmutableList<ProcessedSubroutine> Subroutines { get; }
		int TotalSize { get; }
		int ThisSize { get; }
		TypeDefinition TypeDefinition { get; }
		bool AllowedAsLValue { get; }
		bool SystemType { get; }

		IProcessedType ReplaceSubroutine(ProcessedSubroutine oldSubroutine, CompiledSubroutine newSubroutine);
	}
}
