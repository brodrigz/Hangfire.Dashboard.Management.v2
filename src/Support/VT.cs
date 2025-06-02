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
			JobsHelper.Metadata.ForEach(job => job.MethodInfo.GetParameters().ToList().ForEach(param => {
				assembly.GetTypes()
					.Where(t => t.IsClass && !t.IsAbstract && t.GetInterfaces().Contains(param.ParameterType))
					.ToList()
					.ForEach(implType => {
						if (!Implementations.ContainsKey(param.ParameterType))
						{
							Implementations[param.ParameterType] = new HashSet<Type>();
						}
						Implementations[param.ParameterType].Add(implType);
					});
			}));
		}
	}
}
