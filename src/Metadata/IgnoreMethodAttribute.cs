using System;
using System.Collections.Generic;
using System.Text;

namespace Hangfire.Dashboard.Management.v2.Metadata
{

	[AttributeUsage(AttributeTargets.Method)]
	public class IgnoreMethodAttribute : Attribute
	{
		public IgnoreMethodAttribute()
		{

		}
	}
}
