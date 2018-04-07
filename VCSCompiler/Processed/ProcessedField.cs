using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.Text;

namespace VCSCompiler
{
    internal class ProcessedField
    {
		public string Name => FieldDefinition.Name;
		public string FullName => FieldDefinition.FullName;
		public IProcessedType FieldType { get; }
		public FieldDefinition FieldDefinition { get; }

		public ProcessedField(FieldDefinition fieldDefinition, IProcessedType fieldType)
		{
			FieldDefinition = fieldDefinition;
			FieldType = fieldType;
		}

		public override string ToString() => $"{FullName}";
	}
}
