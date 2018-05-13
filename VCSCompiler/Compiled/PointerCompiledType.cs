using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Mono.Cecil;

namespace VCSCompiler
{
	internal sealed class PointerCompiledType : ICompiledType
	{
		public IImmutableList<CompiledSubroutine> Subroutines => ImmutableList<CompiledSubroutine>.Empty;

		public string Name => $"{(IsZeroPagePointer ? "{zp}" : string.Empty)}{PointerType.Name}*";

		public string FullName => $"{(IsZeroPagePointer ? "{zp}" : string.Empty)}{PointerType.FullName}*";

		public IProcessedType BaseType { get; }

		public IEnumerable<ProcessedField> Fields => Enumerable.Empty<ProcessedField>();

		public IImmutableDictionary<ProcessedField, byte> FieldOffsets => ImmutableDictionary<ProcessedField, byte>.Empty;

		public int TotalSize => IsZeroPagePointer ? 1 : 2;

		public int ThisSize => IsZeroPagePointer ? 1 : 2;

		public TypeDefinition TypeDefinition => throw new InvalidOperationException("Pointer types do not have TypeDefinitions.");

		public bool AllowedAsLValue => true;

		public bool SystemType => true;

		public bool IsZeroPagePointer { get; }

		IImmutableList<ProcessedSubroutine> IProcessedType.Subroutines => ImmutableList<ProcessedSubroutine>.Empty;

		private readonly IProcessedType PointerType;

		public PointerCompiledType(IProcessedType pointerType, IProcessedType baseType, bool isZeroPagePointer)
		{
			PointerType = pointerType;
			BaseType = baseType;
			IsZeroPagePointer = isZeroPagePointer;
		}

		public IProcessedType ReplaceSubroutine(ProcessedSubroutine oldSubroutine, CompiledSubroutine newSubroutine)
		{
			throw new InvalidOperationException("Pointer types do not have subroutines.");
		}
	}
}
