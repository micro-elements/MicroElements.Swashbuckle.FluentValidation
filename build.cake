///////////////////////////////////////////////////////////////////////////////
// IMPORTS
///////////////////////////////////////////////////////////////////////////////

#load tools/microelements.devops/1.8.1/scripts/imports.cake

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

Task("Test")
    .WithCriteria(()=>args.RunTests)
    .Does(() => RunTests(args));

Task("UploadTestResultsToAppVeyor")
    .WithCriteria(()=>args.RunTests)
    .Does(() => UploadTestResultsToAppVeyor(args));

Task("CopyPackagesToArtifacts")
    .IsDependentOn("Build")
    .Does(() => CopyPackagesToArtifacts(args));

Task("UploadPackages")
    .WithCriteria(()=>args.UploadPackages)
    .WithCriteria(()=>args.Version.IsRelease)
    .Does(() => UploadPackages(args));

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
    .IsDependentOn("UploadCoverageReportsToCoveralls")
    .IsDependentOn("UploadPackages")
    ;

Task("AppVeyor")
    .IsDependentOn("Build")
    //.IsDependentOn("CopyPackagesToArtifacts")
    .IsDependentOn("Test")
    .IsDependentOn("UploadTestResultsToAppVeyor")
    ;

RunTarget(args.Target);
