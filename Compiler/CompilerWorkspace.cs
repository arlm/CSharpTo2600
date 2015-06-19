﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace CSharpTo2600.Compiler
{
    internal sealed class CompilerWorkspace
    {
        private readonly AdhocWorkspace Workspace;
        private readonly Project UserProject;
        private static readonly MetadataReference FrameworkReference = MetadataReference.CreateFromFile(typeof(CSharpTo2600.Framework.TIARegisters).Assembly.Location);
        internal static readonly MetadataReference MSCorLibReference = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);
        private static readonly CompilationOptions Options =
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary);
        
        public CSharpCompilation Compilation { get; }

        private struct FileInfo
        {
            public readonly string Name;
            public readonly string Text;

            public FileInfo(string Name, string Text)
            {
                this.Name = Name;
                this.Text = Text;
            }
        }

        private CompilerWorkspace(ProjectInfo UserProjectInfo)
        {
            Workspace = new AdhocWorkspace();
            if (!Workspace.Services.IsSupported(UserProjectInfo.Language))
            {
                throw new InvalidOperationException($"Not supported: {UserProjectInfo.Language}. Make sure to include Microsoft.CodeAnalysis.CSharp.Workspaces or else no languages are supported.");
            }
            UserProject = Workspace.AddProject(UserProjectInfo);
            Compilation = Compile();
        }

        public static CompilerWorkspace FromFilePaths(IEnumerable<string> FilePaths)
        {
            var UserProject = CreateUserProjectInfo(ReadFiles(FilePaths));
            return new CompilerWorkspace(UserProject);
        }

        public static CompilerWorkspace FromSourceTexts(IEnumerable<string> SourceTexts)
        {
            var Sources = new List<FileInfo>();
            foreach (var Source in SourceTexts)
            {
                Sources.Add(new FileInfo($"NoFile_{Sources.Count}", Source));
            }
            var UserProject = CreateUserProjectInfo(Sources);
            return new CompilerWorkspace(UserProject);
        }

        private static IEnumerable<FileInfo> ReadFiles(IEnumerable<string> FilePaths)
        {
            foreach (var FilePath in FilePaths)
            {
                var Text = File.ReadAllText(FilePath);
                var Name = Path.GetFileNameWithoutExtension(FilePath);
                yield return new FileInfo(Name, Text);
            }
        }

        private static ProjectInfo CreateUserProjectInfo(IEnumerable<FileInfo> Files)
        {
            var ProjectID = ProjectId.CreateNewId();
            var Documents = new List<DocumentInfo>();
            foreach (var File in Files)
            {
                var Source = TextAndVersion.Create(SourceText.From(File.Text), VersionStamp.Default);
                Documents.Add(
                    DocumentInfo.Create(DocumentId.CreateNewId(ProjectID), File.Name,
                    loader: TextLoader.From(Source)));
            }

            var Info = ProjectInfo.Create(ProjectID, VersionStamp.Create(), "User", "User", "C#",
                metadataReferences: new[] { MSCorLibReference, FrameworkReference },
                compilationOptions: Options,
                documents: Documents);
            // projectReferences when we get multiple projects

            return Info;
        }

        private TextLoader GetTextLoaderFromReader(StreamReader Reader, string Path)
        {
            var SourceContainer = SourceText.From(Reader.BaseStream).Container;
            var Version = VersionStamp.Create();
            return TextLoader.From(SourceContainer, Version, Path);
        }

        private CSharpCompilation Compile()
        {
            var Compilation = (CSharpCompilation)UserProject.GetCompilationAsync().Result;
            var Errors = Compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error);
            if (Errors.Any())
            {
                Console.WriteLine("!Roslyn compilation failed! Messages:");
                foreach (var Error in Errors)
                {
                    Console.WriteLine(Error.ToString());
                }
                throw new FatalCompilationException("File must compile with Roslyn to be compiled to 6502.");
            }
            return Compilation;
        }
    }
}
