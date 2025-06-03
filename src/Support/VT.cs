using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

using Hangfire.Dashboard.Management.v2.Metadata;

namespace Hangfire.Dashboard.Management.v2.Support
{
	public static class VT
	{
		public static Dictionary<Type, HashSet<Type>> Implementations { get; private set; } = new Dictionary<Type, HashSet<Type>>();

		internal static void SetAllImplementations(Assembly assembly)
		{
			// this gets impls from param interfaces, but not from nested interfaces
			//JobsHelper.Metadata.ForEach(job => job.MethodInfo.GetParameters().ToList().ForEach(param => {
			//	assembly.GetTypes()
			//		.Where(t => t.IsClass && !t.IsAbstract && t.GetInterfaces().Contains(param.ParameterType))
			//		.ToList()
			//		.ForEach(implType => {
			//			if (!Implementations.ContainsKey(param.ParameterType))
			//			{
			//				Implementations[param.ParameterType] = new HashSet<Type>();
			//			}
			//			Implementations[param.ParameterType].Add(implType);
			//		});
			//}));

			JobsHelper.Metadata.ForEach(job => job.MethodInfo.GetParameters().ToList().ForEach(param => RegisterInterfaceImpls(assembly, param.ParameterType)));
		}
		private static void RegisterInterfaceImpls(Assembly assembly, Type interfaceType)
		{
			// this is only needed for interfaces
			if (!interfaceType.IsInterface) return;

			// init collection if null
			if (!Implementations.ContainsKey(interfaceType))
			{
				Implementations[interfaceType] = new HashSet<Type>();
			}

			RegisterImplementations(assembly, interfaceType);
		}

		// recursive
		private static void RegisterImplementations(Assembly assembly, Type interfaceType)
		{
			var dictList = Implementations[interfaceType];

			// get impls
			var implementations = GetInterfaceImplementations(assembly, interfaceType).ToList();

			foreach (var impl in implementations)
			{
				// register impl
				if (!dictList.Contains(impl)) dictList.Add(impl);

				//// register interface aswell
				//if (!dictList.Contains(interfaceType)) dictList.Add(interfaceType); // this fixes only a symptom, not the root problem

				// get nested interfaces
				var nestedInterfaces = GetInterfacePropsFromType(impl)
					.Where(i => i != interfaceType) // avoids circular refferences
					.ToList();

				// register nested interfaces
				foreach (var nestedInterface in nestedInterfaces) RegisterInterfaceImpls(assembly, nestedInterface);
			}
		}

		// gets all properties which are interfaces inside a type
		private static IEnumerable<Type> GetInterfacePropsFromType(Type classType)
		{
			var types = classType.GetProperties().Select(p => p.PropertyType).ToList();

			return types.Where(t => t.IsInterface);
		}

		// gets all concrete impls of given interface
		private static IEnumerable<Type> GetInterfaceImplementations(Assembly assembly, Type interfaceType) => assembly.GetTypes().Where(t => interfaceType.IsAssignableFrom(t) && !t.IsInterface);
	}
}
