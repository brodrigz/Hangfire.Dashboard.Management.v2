using System;
using System.Linq;
using System.Reflection;
using Hangfire.Dashboard.Management.v2.Metadata;

namespace Hangfire.Dashboard.Management.v2.Support
{
	public static class ExtensionMethods
	{
		public static string ScrubURL(this string seed)
		{
			var _validCharacters = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789/\\_-".ToCharArray();
			string result = "";
			foreach (var s in seed.ToCharArray())
			{
				if (_validCharacters.Contains(s))
				{
					result += s;
				}
			}
			return result;
		}

		public static string GetDisplayName(this ParameterInfo type)
		{
			var attr = type.GetCustomAttribute<DisplayDataAttribute>();
			return attr?.Label ?? type.Name;
		}
		public static string GetDisplayName(this MemberInfo type)
		{
			var attr = type.GetCustomAttribute<DisplayDataAttribute>();
			return attr?.Label ?? type.Name;
		}
	}
}
