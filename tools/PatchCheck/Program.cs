using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

// Verifies that every [HarmonyPatch(typeof(X), "Y")] in the built mod points at a method that
// actually exists in this build of the game. Catches typos in string-named targets, which the
// C# compiler cannot check and which otherwise only surface as a crash at game start.
class Program
{
	static int Main(string[] args)
	{
		string modDll = args[0];
		string managed = args[1];
		string bepinex = args[2];

		// The game ships its own Unity Mono corelib. Use that as the core assembly and keep the
		// host runtime's assemblies out entirely, or the two mscorlibs collide.
		List<string> assemblies = new List<string> { modDll };
		assemblies.AddRange(Directory.GetFiles(managed, "*.dll"));
		assemblies.AddRange(Directory.GetFiles(bepinex, "*.dll"));

		Dictionary<string, string> unique = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		foreach (string path in assemblies)
		{
			string name = Path.GetFileName(path);
			if (!unique.ContainsKey(name))
			{
				unique[name] = path;
			}
		}

		PathAssemblyResolver resolver = new PathAssemblyResolver(unique.Values.ToList());
		using MetadataLoadContext context = new MetadataLoadContext(resolver, "mscorlib");

		Assembly mod = context.LoadFromAssemblyPath(modDll);
		int failures = 0;
		int checkedCount = 0;

		Type[] modTypes;
		try { modTypes = mod.GetTypes(); }
		catch (ReflectionTypeLoadException e) { modTypes = Array.FindAll(e.Types, t => t != null); }

		foreach (Type type in modTypes)
		{
			List<CustomAttributeData> attributes = type.GetCustomAttributesData()
				.Where(a => a.AttributeType.Name == "HarmonyPatch" || a.AttributeType.Name == "HarmonyPatchAttribute")
				.ToList();
			if (attributes.Count == 0)
			{
				continue;
			}

			Type target = null;
			string methodName = null;

			foreach (CustomAttributeData attribute in attributes)
			{
				// Some mods pass argument-type arrays to disambiguate overloads, and those can fail to
				// resolve from outside their own assembly. Skip what cannot be read rather than dying.
				IList<CustomAttributeTypedArgument> arguments;
				try
				{
					arguments = attribute.ConstructorArguments;
				}
				catch
				{
					continue;
				}

				foreach (CustomAttributeTypedArgument argument in arguments)
				{
					if (argument.ArgumentType.FullName == "System.Type" && argument.Value is Type t)
					{
						target = t;
					}
					else if (argument.ArgumentType.FullName == "System.String" && argument.Value is string s)
					{
						methodName = s;
					}
				}
			}

			if (target == null || methodName == null)
			{
				continue;
			}

			checkedCount++;
			const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
			MethodInfo[] found = target.GetMethods(flags).Where(m => m.Name == methodName).ToArray();

			if (found.Length == 0)
			{
				Console.WriteLine($"MISSING  {target.Name}.{methodName}  (patch class {type.Name})");
				failures++;
				continue;
			}

			if (found.Length > 1)
			{
				Console.WriteLine($"AMBIGUOUS {target.Name}.{methodName} has {found.Length} overloads (patch class {type.Name})");
				failures++;
				continue;
			}

			MethodInfo method = found[0];
			string parameters = string.Join(", ", method.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name));
			Console.WriteLine($"ok       {target.Name}.{methodName}({parameters}) -> {method.ReturnType.Name}");

			// Harmony matches injected arguments by name, so a renamed game parameter silently breaks them.
			foreach (MethodInfo patchMethod in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly))
			{
				if (patchMethod.Name != "Prefix" && patchMethod.Name != "Postfix")
				{
					continue;
				}

				foreach (ParameterInfo parameter in patchMethod.GetParameters())
				{
					if (parameter.Name.StartsWith("__"))
					{
						continue;
					}

					if (!method.GetParameters().Any(p => p.Name == parameter.Name))
					{
						Console.WriteLine($"  ARG MISMATCH {type.Name}.{patchMethod.Name} wants '{parameter.Name}', target has ({parameters})");
						failures++;
					}
				}
			}
		}

		Console.WriteLine();
		Console.WriteLine($"{checkedCount} patch targets checked, {failures} problem(s).");
		return failures == 0 ? 0 : 1;
	}
}
