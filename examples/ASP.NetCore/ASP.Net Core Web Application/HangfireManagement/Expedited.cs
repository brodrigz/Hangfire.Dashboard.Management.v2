﻿using System;
using System.ComponentModel;
using Hangfire;
using Hangfire.Dashboard.Management.v2.Metadata;
using Hangfire.Dashboard.Management.v2.Support;
using Hangfire.Server;
using Newtonsoft.Json;

namespace ASP.Net_Core_Web_Application.HangfireManagement
{
	[ManagementPage(MenuName = "Expedited Jobs", Title = nameof(Expedited))]
	/*              A                            B                        */
	public class Expedited : IJob
	{
		private const string dateTimeOptions = @"
{
	""display"": {
		""buttons"": {
			""clear"": true,
			""close"": false,
			""today"": true
		}
	},
	""localization"": {
		""format"": ""L"",
		""clear"": ""This button clears the current value"",
		""hourCycle"": ""h12"",
		""dayViewHeaderFormat"": {
			""month"": ""long"",
			""year"": ""numeric""
		}
	},
	""restrictions"": {
		""minDate"": ""01/01/2020 0:00""
	}
}";

		[DisplayName("Job Number 1")] //C
		[Description("This is the description for Job Number 1")] //D
		[Queue("expedited")]
		[AllowMultiple]
		[ShowMetaData(true)]
		[AutomaticRetry]
		public void Job1(PerformContext context, IJobCancellationToken token,
			[DisplayData(
				Label = "String Input 1",
				Description = "This is the description text for the string input with a default value and the control is disabled",
				DefaultValue = "This is the Default Value",
				IsDisabled = true
			)] string strInput1,

			[DisplayData(
				Placeholder = "This is the placeholder text",
				Description = "This is the description text for the string input without a default value and the control is enabled",
				IsRequired = true
			)] string strInput2,

			[DisplayData(
				Label = "Multiline Input",
				IsMultiLine = true,
				Placeholder = "This is the multiline\nplaceholder text",
				Description = "This is the description text for the multiline input without a default value where the control is enabled and not required"
			)]
			string strInput3,

			[DisplayData(
				Label = "DateTime Input",
				Placeholder = "What is the date and time?",
				DefaultValue = "01/20/2020 1:02 AM",
				Description = "This is a date time input control"//,
				//ControlConfiguration = dateTimeOptions
			)] DateTime dtInput,

			[DisplayData(
				Label = "Boolean Input",
				DefaultValue = true,
				Description = "This is a boolean input"
			)] bool blInput,

			[DisplayData(
				Label = "Custom Class Input"
			)] TestClass customClass,

			[DisplayData(
				Label = "Select Input",
				DefaultValue = TestEnum.Test5,
				Description = "Based on an enum object"
			)] TestEnum enumTest
		)
		{
			//Do awesome things here
		}

		[DisplayName("Job Number 2")] //C
		[Description("This is the description for Job Number 1")] //D
		[Queue("default")]
		[AllowMultiple]
		[ShowMetaData(true)]
		[AutomaticRetry]
		public void Job2(PerformContext context, IJobCancellationToken token,
			[DisplayData(
				Label = "Choose your own implementation",
				Description = "This is an interface",
				DefaultValue = "ConcreteClassB"
			)]
			IInterfaceTest interfaceInput
		)
		{
			//Do awesome things here
			Console.WriteLine($"Common: {interfaceInput.InterfaceString}");
			Console.WriteLine($"Common: {interfaceInput.InterfaceMethod()}");

			switch (interfaceInput)
			{
				case ConcreteClassA a:
					Console.WriteLine($"A: {a.DataMemberA}");
					Console.WriteLine($"B: {a.InterfaceMethod()}");
					break;
				case ConcreteClassB b:
					Console.WriteLine($"B from I: {interfaceInput.InterfaceString}");
					Console.WriteLine($"B from I: {interfaceInput.InterfaceMethod()}");
					Console.WriteLine($"B: {b.DataMemberB}");

					if (b.NestedInterface != null)
					{
						Console.WriteLine($"B nest: {b.NestedInterface.InterfaceString}");
					}
					break;
				default:
					Console.WriteLine("Unknown implementation");
					break;
			}

		}

		public class ConcreteClassA : IInterfaceTest
		{

			[DisplayData(
				Label = "Data Member (data)",
				Description = "data member"
			)]
			public DateTime DataMemberA { get; set; }

			[DisplayData(
				Label = "String from Interface",
				Description = "Inherited data member"
			)]
			public string InterfaceString { get; set; }

			public string InterfaceMethod()
			{
				return $"Hello From A: {DataMemberA.Date}";
			}

		}

		public class ConcreteClassB : IInterfaceTest
		{

			[DisplayData(
				Label = "Data Member (integer)",
				Description = "Data member"
			)]
			public int DataMemberB { get; set; }

			[DisplayData(
				Label = "Choose your own concrete implementation",
				Description = "Circular references are not enabled. You can only pick concrete classes that are not circular."
			)]
			public IInterfaceTest NestedInterface { get; set; }

			[DisplayData(
				Label = "String from Interface",
				Description = "Inherited data member"
			)]
			public string InterfaceString { get; set; }

			public string InterfaceMethod()
			{
				return $"Hello From B: {DataMemberB}";
			}
		}


		public interface IInterfaceTest
		{
			string InterfaceString { get; set; }

			string InterfaceMethod()
			{
				return $"Interface Method: {InterfaceString}";
			}
		}

		public enum TestEnum
		{
			Test1,
			Test2,
			Test3,
			Test4 = 44,
			Test5
		}

		public class TestClass
		{
			[DisplayData(
				Label = "Class String",
				Description = "This is the description text",
				DefaultValue = "This is the Default Value"
			)]
			public string TestString { get; set; }

			[DisplayData(
				Label = "Nested Custom Class"
			)]
			public NestedClass Nested { get; set; }

			[DisplayData(
				Label = "Class Integer",
				Description = "This is the description text",
				DefaultValue = "2"
			)]
			public int TestInt { get; set; }
		}

		public class NestedClass
		{
			[DisplayData(
				Label = "Nested String",
				Description = "This is the description text",
				DefaultValue = "This is the Default Value"
			)]
			public string TestString { get; set; }

			[DisplayData(
				Label = "Nested Boolean Input",
				DefaultValue = true,
				Description = "This is a boolean input"
			)]
			public bool BlInput { get; set; }

			[DisplayData(
				Label = "Nested DateTime Input",
				Placeholder = "What is the date and time?",
				DefaultValue = "01/20/2020 1:02 AM",
				Description = "This is a date time input control"
			//ControlConfiguration = dateTimeOptions
			)]
			public DateTime DtInput { get; set; }
		}
	}
}
