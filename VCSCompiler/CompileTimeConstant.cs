using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Mono.Cecil;

namespace VCSCompiler
{
	internal abstract class CompileTimeConstant : IProcessedType, ICompiledType
	{
		public object Value { get; }

		public IProcessedType Type { get; }

		protected CompileTimeConstant(object value, IProcessedType type)
		{
			Value = value;
			Type = type;
		}

		public string Name => Type.Name;

		public string FullName => Type.FullName;

		public IProcessedType BaseType => Type.BaseType;

		public IEnumerable<ProcessedField> Fields => Type.Fields;

		public IImmutableDictionary<ProcessedField, byte> FieldOffsets => Type.FieldOffsets;

		public IImmutableList<ProcessedSubroutine> Subroutines => Type.Subroutines;

		IImmutableList<CompiledSubroutine> ICompiledType.Subroutines => ((ICompiledType)Type).Subroutines;

		public int TotalSize => Type.TotalSize;

		public int ThisSize => Type.ThisSize;

		public TypeDefinition TypeDefinition => Type.TypeDefinition;

		public bool AllowedAsLValue => Type.AllowedAsLValue;

		public bool SystemType => Type.SystemType;

		public IProcessedType ReplaceSubroutine(ProcessedSubroutine oldSubroutine, CompiledSubroutine newSubroutine)
		{
			throw new NotImplementedException();
		}
	}

	internal class CompileTimeConstant<T> : CompileTimeConstant
		where T : unmanaged
	{
		public new T Value { get; }

		public CompileTimeConstant(T value, IProcessedType type)
			: base(value, type)
		{
			Value = value;
		}
	}
}
