///////////////////////////////////////////////////////////////////////////////
// IMPORTS
///////////////////////////////////////////////////////////////////////////////

#load tools/microelements.devops/1.9.1/scripts/imports.cake

///////////////////////////////////////////////////////////////////////////////
// SCRIPT ARGS AND CONVENTIONS
///////////////////////////////////////////////////////////////////////////////

ScriptArgs args = new ScriptArgs(Context)
    .PrintHeader(new []{"MicroElements", "FluentValidation"})
    .UseDefaultConventions()
    .UseCoverlet()
    .Build();

///////////////////////////////////////////////////////////////////////////////
// TASKS
///////////////////////////////////////////////////////////////////////////////

Task("Build")
    .Does(() => args.Build().BuildSamples());

var isWindows = Environment.Platform.Family == PlatformFamily.Windows;
Task("Tests")
    .Does(() =>
    {
        var solution = "MicroElements.Swashbuckle.FluentValidation.sln";

        if (isWindows)
        {
            Information("Executing tests net481, net8.0, net9.0 on Windows");

            DotNetTest(solution, new DotNetTestSettings { Framework = "net481" });
            DotNetTest(solution, new DotNetTestSettings { Framework = "net8.0" });
            DotNetTest(solution, new DotNetTestSettings { Framework = "net9.0" });
        }
        else
        {
            Information("Executing tests net8.0 y net9.0 on Linux/macOS");

            DotNetTest(solution, new DotNetTestSettings { Framework = "net8.0" });
            DotNetTest(solution, new DotNetTestSettings { Framework = "net9.0" });
        }
    });

Task("UploadTestResultsToAppVeyor")
    .WithCriteria(()=>args.RunTests)
    .Does(() => UploadTestResultsToAppVeyor(args));

Task("CopyPackagesToArtifacts")
    .IsDependentOn("Build")
    .Does(() => CopyPackagesToArtifacts(args));

// Task("UploadPackages")
//     .Does(() => UploadPackagesIfNeeded(args));

Task("DoVersioning")
    .Does(() => DoVersioning(args));

Task("CodeCoverage")
    .Does(() => RunCoverage(args));

Task("UploadCoverageReportsToCoveralls")
    .Does(() => UploadCoverageReportsToCoveralls(args));

Task("Default")
    .IsDependentOn("Build")
    .IsDependentOn("CopyPackagesToArtifacts")
    .IsDependentOn("Test")
    ;

Task("Travis")
    .IsDependentOn("DoVersioning")
    .IsDependentOn("Build")
    .IsDependentOn("CopyPackagesToArtifacts")
    .IsDependentOn("Test")
    .IsDependentOn("CodeCoverage")
    // .IsDependentOn("UploadCoverageReportsToCoveralls")
    // .IsDependentOn("UploadPackages")
    ;

Task("AppVeyor")
    .IsDependentOn("Build")
    //.IsDependentOn("CopyPackagesToArtifacts")
    .IsDependentOn("Test")
    .IsDependentOn("UploadTestResultsToAppVeyor")
    ;

RunTarget(args.Target);