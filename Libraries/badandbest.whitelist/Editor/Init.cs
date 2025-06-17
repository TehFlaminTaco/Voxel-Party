using Editor;
using System.IO;

namespace Sandbox;

public static class Init
{
	[Event( "refresh" )]
	[Event( "localaddons.changed" )]
	public static void InitAnalyzer()
	{
		var project = Project.Current;

		var csprojName = project.Config.GetMetaOrDefault( "CsProjName", project.Config.Ident );
		if ( string.IsNullOrEmpty( csprojName ) ) csprojName = project.Config.Ident;
		var csProjPath = Path.Combine( Project.Current.GetCodePath(), csprojName + ".csproj" );

		var csProj = File.ReadAllLines( csProjPath );
		for ( int i = 0; i < csProj.Length; i++ )
		{
			if ( !csProj[i].Contains( "whitelist.csproj" ) )
			{
				continue;
			}

			var line = csProj[i].Replace( "ProjectReference", "Analyzer" );
			csProj[i] = line.Replace( @"Code\whitelist.csproj", @"Editor\Whitelist.analyzer" );
		}

		File.WriteAllLines( csProjPath, csProj );
	}

	// [EditorEvent.Hotload]
	// public static void Hotload()
	// {
	// 	
	// }
}
