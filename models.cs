using System;
using System.Collections.Generic;

namespace YourApp.Models
{
    public class DeployedAppsCatalogOptions
    {
        public string[] Environments { get; set; } = Array.Empty<string>();
        public string PathTemplate { get; set; } = @"C:\inetpub\wwwroot\@entorno\@app";
        public List<DeployedAppDef> Applications { get; set; } = new();
        public List<string> GeneralAssemblies { get; set; } = new();
    }

    public class DeployedAppDef
    {
        public string Name { get; set; } = "";
        public string SpecificAssembly { get; set; } = "";
    }

    public class AssemblyInfoResult
    {
        public string AssemblyName { get; set; } = "";
        public string? FoundPath { get; set; }          // null si no encontrado
        public string? Version { get; set; }            // null si no encontrado o sin metadatos
        public DateTimeOffset? Created { get; set; }    // null si no encontrado
        public bool IsSpecific { get; set; }
    }

    public class CellData
    {
        public string Environment { get; set; } = "";
        public string AppName { get; set; } = "";
        public string AppPath { get; set; } = "";
        public AssemblyInfoResult? Specific { get; set; }
        public List<AssemblyInfoResult> Generals { get; set; } = new();
    }

    public class TableViewModel
    {
        public IReadOnlyList<string> Environments { get; init; } = Array.Empty<string>();
        public IReadOnlyList<string> Applications { get; init; } = Array.Empty<string>();

        // Mapa [AppName][Environment] -> CellData
        public Dictionary<string, Dictionary<string, CellData>> Cells { get; init; } = new();
        public IReadOnlyList<string> GeneralAssemblies { get; init; } = Array.Empty<string>();
    }
}