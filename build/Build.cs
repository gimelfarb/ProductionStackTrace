using System;
using System.Collections.Generic;
using System.Linq;
using Nuke.Common;
using Nuke.Common.CI;
using Nuke.Common.CI.GitHubActions;
using Nuke.Common.Execution;
using Nuke.Common.Git;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.Git;
using Nuke.Common.Tools.GitVersion;
using Nuke.Common.Tools.MSBuild;

using Nuke.Common.Utilities.Collections;
using static Nuke.Common.EnvironmentInfo;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.IO.PathConstruction;

using static Nuke.Common.Tools.MSBuild.MSBuildTasks;
using static Nuke.Common.Tools.DotNet.DotNetTasks;
using Nuke.Common.Tools.DotNet;

[GitHubActions(
	"continuous",
	GitHubActionsImage.WindowsLatest,
	//ConfigOptions.Debug, ConfigOptions.Release,
	OnPushBranchesIgnore = new[] { "trash" },
	//OnPushBranchesIgnore = new[] { MasterBranch, ReleaseBranchPrefix + "/*" },
	Submodules = GitHubActionsSubmodules.Recursive,
	
	PublishArtifacts = true,
	InvokedTargets = new[] { nameof(Pack) }
)]
class Build : NukeBuild {
	/// Support plugins are available for:
	///   - JetBrains ReSharper        https://nuke.build/resharper
	///   - JetBrains Rider            https://nuke.build/rider
	///   - Microsoft VisualStudio     https://nuke.build/visualstudio
	///   - Microsoft VSCode           https://nuke.build/vscode

	public static int Main() => Execute<Build>(x => x.Compile);

	[Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
	readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

	Target Clean => _ => _
		.Before(Restore)
		.Executes(() => {
			EnsureCleanDirectory(OutputDirectory);
		});

	Target Restore => _ => _
		.Executes(() => {
			OurMSBuild(s => s.SetRestore(true).SetTargets("restore"));
		});
	protected override void OnBuildInitialized() {

		base.OnBuildInitialized();
		ProcessTasks.DefaultLogInvocation = true;
		ProcessTasks.DefaultLogOutput = true;
		Serilog.Log.Information("IsLocalBuild           : {0}", IsLocalBuild.ToString());

		Serilog.Log.Information("Informational   Version: {0}", InformationalVersion);
		Serilog.Log.Information("SemVer          Version: {0}", SemVer);
		Serilog.Log.Information("AssemblySemVer  Version: {0}", AssemblySemVer);

	}
	const string MasterBranch = "master";
	const string ReleaseBranchPrefix = "tags";
	[GitRepository] GitRepository GitRepository;
	[GitVersion(NoFetch = true, NoCache = true)] readonly GitVersion GitVersion;
	string AssemblySemVer => GitVersion?.AssemblySemVer ?? "1.0.0";
	string SemVer => GitVersion?.SemVer ?? "1.0.0";
	string InformationalVersion => GitVersion?.InformationalVersion ?? "1.0.0";

	[CI] readonly GitHubActions GitHubActions;
	[Solution(GenerateProjects = true)] readonly Solution Solution = null!;
	AbsolutePath OutputDirectory => RootDirectory / "final";
	private IReadOnlyCollection<Output> OurMSBuild(Func<MSBuildSettings, MSBuildSettings> action, Project ScopeToSpecificProject = null) {
		var toolsPath = MSBuildToolPathResolver.Resolve(MSBuildVersion.VS2022, MSBuildPlatform.x64);

		MSBuildSettings s = new MSBuildSettings();
		if (ScopeToSpecificProject != null)
			s = s.SetProjectFile(ScopeToSpecificProject);
		else
			s = s.SetSolutionFile(Solution);
		s = s.SetProcessToolPath(toolsPath)
		.SetConfiguration(Configuration.ToString())
		.SetMSBuildPlatform(MSBuildPlatform.x64)
		.SetVerbosity(MSBuildVerbosity.Normal)
		;

		s = action(s);
		return MSBuild(s);
	}
	Target Compile => _ => _
		.DependsOn(Restore)
		.Executes(() => {
			EnsureCleanDirectory(OutputDirectory);

			var toBuild = new[] { Solution.ProductionStackTrace, Solution.ProductionStackTraceStd, Solution.ProductionStackTrace_Analyze, Solution.ProductionStackTrace_Analyze_Console };
			foreach (var proj in toBuild) {
				var OutDir = proj == Solution.ProductionStackTrace_Analyze_Console ? OutputDirectory / "console" : OutputDirectory / "test";
				var context = Serilog.Log.ForContext("Project", $"Building {proj.Name}");
				context.Information($"Starting build of: {proj.Name}");
				OurMSBuild(s => s
				.SetTargets("Build")
				.SetAssemblyVersion(AssemblySemVer)
				.SetOutDir(OutDir)
				.SetInformationalVersion(InformationalVersion), proj
				);
			}

		});
	Target Pack => _ => _
	.DependsOn(Compile)
	.Produces(OutputDirectory)
		.Executes(() => {


		DotNetPack(_ => _
				.SetProject(Solution.ProductionStackTraceStd)
				.SetOutputDirectory(OutputDirectory / "nuget")
				.SetVersion(SemVer)
				

				);

		  



		}
		);
}
