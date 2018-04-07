using System.Collections.Immutable;

namespace VCSCompiler
{
	interface ICompiledType : IProcessedType
    {
		new IImmutableList<CompiledSubroutine> Subroutines { get; }
	}
}
