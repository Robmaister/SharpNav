using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Mono.Options;
using SharpNav;

namespace SharpNav.CLI
{
	class Program
	{
		private static readonly string SharpNavVersion = typeof(NavMesh).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;
		private static readonly string ThisVersion = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;

		static int Main(string[] args)
		{
			bool help = false;
			bool version = false;
			Verbosity verbosity = Verbosity.Normal;

			List<string> files = new List<string>();

			var set = new OptionSet()
				.Add("verbosity=|v=", "Changes verbosity level. silent, minimal, normal, verbose, and debug are the only valid choices.", opt => { verbosity = Verbosity.Debug; })
				.Add("version", "Displays version information", opt => version = (opt != null))
				.Add("help|h", "Displays usage information", opt => help = (opt != null));

			try
			{
				files = set.Parse(args);
			}
			catch (OptionException)
			{
				Console.WriteLine("Options:");
				set.WriteOptionDescriptions(Console.Out);
				return 1;
			}

			if (help)
			{
				Console.WriteLine("Options:");
				set.WriteOptionDescriptions(Console.Out);
				return 0;
			}

			if (version)
			{
				Console.WriteLine("SharpNav     " + SharpNavVersion);
				Console.WriteLine("SharpNav.CLI " + ThisVersion);
				return 0;
			}

			if (verbosity > Verbosity.Normal)
				Console.WriteLine("Verbosity enabled (not really)");

			foreach (var f in files)
			{
				Console.WriteLine(f);
			}

			return 0;
		}
	}
}
