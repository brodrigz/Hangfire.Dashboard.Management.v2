﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using Hangfire.Common;
using Hangfire.Dashboard;
using Hangfire.Dashboard.Management.v2.Metadata;
using Hangfire.Dashboard.Management.v2.Support;
using Hangfire.Dashboard.Pages;
using Hangfire.Server;
using Hangfire.States;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Hangfire.Dashboard.Management.v2.Pages
{
	partial class ManagementBasePage
	{
		public readonly string menuName;

		public readonly IEnumerable<JobMetadata> jobs;
		public readonly Dictionary<string, string> jobSections;


		protected internal ManagementBasePage(string menuName) : base()
		{

			//this.UrlHelper = new UrlHelper(this.Context);
			this.menuName = menuName;

			jobs = JobsHelper.Metadata.Where(j => j.MenuName.Contains(menuName)).OrderBy(x => x.SectionTitle).ThenBy(x => x.Name);
			jobSections = jobs.Select(j => j.SectionTitle).Distinct().ToDictionary(k => k, v => string.Empty);
		}


		public static void AddCommands(string menuName)
		{
			var jobs = JobsHelper.Metadata.Where(j => j.MenuName.Contains(menuName));

			foreach (var jobMetadata in jobs)
			{

				var route = $"{ManagementPage.UrlRoute}/{jobMetadata.JobId.ScrubURL()}";

				DashboardRoutes.Routes.Add(route, new CommandWithResponseDispatcher(context => {
					string errorMessage = null;
					string jobLink = null;
					var par = new List<object>();
					string GetFormVariable(string key)
					{
						return Task.Run(() => context.Request.GetFormValuesAsync(key)).Result.FirstOrDefault();
					}
					var id = GetFormVariable("id");
					var type = GetFormVariable("type");

					HashSet<Type> nestedTypes = new HashSet<Type>();

					foreach (var parameterInfo in jobMetadata.MethodInfo.GetParameters())
					{
						if (parameterInfo.ParameterType == typeof(PerformContext) || parameterInfo.ParameterType == typeof(IJobCancellationToken))
						{
							par.Add(null);
							continue;
						}

						DisplayDataAttribute displayInfo = null;
						if (parameterInfo.GetCustomAttributes(true).OfType<DisplayDataAttribute>().Any())
						{
							displayInfo = parameterInfo.GetCustomAttribute<DisplayDataAttribute>();
						}
						else
						{
							displayInfo = new DisplayDataAttribute();
						}

						var variable = $"{id}_{parameterInfo.Name}";
						if (parameterInfo.ParameterType == typeof(DateTime))
						{
							variable = $"{variable}_datetimepicker";
						}

						variable = variable.Trim('_');
						var formInput = GetFormVariable(variable);
						if (formInput == null) continue;

						object item = null;

						if (parameterInfo.ParameterType.IsInterface)
						{
							if (!VT.Implementations.ContainsKey(parameterInfo.ParameterType)) { errorMessage = $"{displayInfo.Label ?? parameterInfo.Name} is not a valid interface type or is not registered in VT."; break; }
							VT.Implementations.TryGetValue(parameterInfo.ParameterType, out HashSet<Type> impls);

							string implName = GetFormVariable($"{id}_{parameterInfo.Name}");
							var impl = impls.FirstOrDefault(concrete => concrete.FullName == implName);

							if (impl == null)
							{
								errorMessage = $"No valid concrete type of {parameterInfo.ParameterType} registered in VT.";
								//errorMessage = $"{impl.FullName} is not a valid concrete type of {parameterInfo.ParameterType} or is not registered in VT.";
								break;
							}

							nestedTypes.Add(impl);
							item = ProcessNestedParameters($"{variable}_{impl.Name}", impl, GetFormVariable, nestedTypes, out errorMessage);
							nestedTypes.Remove(impl);
						}
						else if (parameterInfo.ParameterType == typeof(string))
						{
							item = formInput;
							if (displayInfo.IsRequired && string.IsNullOrWhiteSpace((string)item))
							{
								errorMessage = $"{displayInfo.Label ?? parameterInfo.Name} is required.";
								break;
							}
						}
						else if (parameterInfo.ParameterType == typeof(int))
						{
							int intNumber;
							if (int.TryParse(formInput, out intNumber) == false)
							{
								errorMessage = $"{displayInfo.Label ?? parameterInfo.Name} was not in a correct format.";
								break;
							}
							item = intNumber;
						}
						else if (parameterInfo.ParameterType == typeof(DateTime))
						{
							item = formInput == null ? DateTime.MinValue : DateTime.Parse(formInput, null, DateTimeStyles.RoundtripKind);
							if (displayInfo.IsRequired && item.Equals(DateTime.MinValue))
							{
								errorMessage = $"{displayInfo.Label ?? parameterInfo.Name} is required.";
								break;
							}
						}
						else if (parameterInfo.ParameterType == typeof(bool))
						{
							item = formInput == "on";
						}
						else if (parameterInfo.ParameterType.IsClass)
						{
							nestedTypes.Add(parameterInfo.ParameterType);
							item = ProcessNestedParameters(variable, parameterInfo.ParameterType, GetFormVariable, nestedTypes, out errorMessage);
							nestedTypes.Remove(parameterInfo.ParameterType);
						}
						else if (!parameterInfo.ParameterType.IsValueType)
						{
							if (formInput == null || formInput.Length == 0)
							{
								item = null;
								if (displayInfo.IsRequired)
								{
									errorMessage = $"{displayInfo.Label ?? parameterInfo.Name} is required.";
									break;
								}
							}
							else
							{
								item = JsonConvert.DeserializeObject(formInput, parameterInfo.ParameterType);
							}
						}
						else
						{
							item = formInput;
						}

						par.Add(item);
					}

					if (errorMessage == null)
					{
						var job = new Job(jobMetadata.Type, jobMetadata.MethodInfo, par.ToArray());
						var client = new BackgroundJobClient(context.Storage);
						switch (type)
						{
							case "CronExpression":
								{
									var manager = new RecurringJobManager(context.Storage);
									var cron = GetFormVariable($"{id}_sys_cron");
									var name = GetFormVariable($"{id}_sys_name");

									if (string.IsNullOrWhiteSpace(cron))
									{
										errorMessage = "No Cron Expression Defined";
										break;
									}
									if (jobMetadata.AllowMultiple && string.IsNullOrWhiteSpace(name))
									{
										errorMessage = "No Job Name Defined";
										break;
									}

									try
									{
										var jobId = jobMetadata.AllowMultiple ? name : jobMetadata.JobId;
										manager.AddOrUpdate(jobId, job, cron, TimeZoneInfo.Local, jobMetadata.Queue);
										jobLink = new UrlHelper(context).To("/recurring");
									}
									catch (Exception e)
									{
										errorMessage = e.Message;
									}
									break;
								}
							case "ScheduleDateTime":
								{
									var datetime = GetFormVariable($"{id}_sys_datetime");

									if (string.IsNullOrWhiteSpace(datetime))
									{
										errorMessage = "No Schedule Defined";
										break;
									}

									if (!DateTime.TryParse(datetime, null, DateTimeStyles.RoundtripKind, out DateTime dt))
									{
										errorMessage = "Unable to parse Schedule";
										break;
									}
									try
									{
										var jobId = client.Create(job, new ScheduledState(dt.ToUniversalTime()));//Queue
										jobLink = new UrlHelper(context).JobDetails(jobId);
									}
									catch (Exception e)
									{
										errorMessage = e.Message;
									}
									break;
								}
							case "ScheduleTimeSpan":
								{
									var timeSpan = GetFormVariable($"{id}_sys_timespan");

									if (string.IsNullOrWhiteSpace(timeSpan))
									{
										errorMessage = $"No Delay Defined '{id}'";
										break;
									}

									if (!TimeSpan.TryParse(timeSpan, out TimeSpan dt))
									{
										errorMessage = "Unable to parse Delay";
										break;
									}

									try
									{
										var jobId = client.Create(job, new ScheduledState(dt));//Queue
										jobLink = new UrlHelper(context).JobDetails(jobId);
									}
									catch (Exception e)
									{
										errorMessage = e.Message;
									}
									break;
								}
							case "Enqueue":
							default:
								{
									try
									{
										var jobId = client.Create(job, new EnqueuedState(jobMetadata.Queue));
										jobLink = new UrlHelper(context).JobDetails(jobId);
									}
									catch (Exception e)
									{
										errorMessage = e.Message;
									}
									break;
								}
						}
					}

					context.Response.ContentType = "application/json";

					if (!string.IsNullOrEmpty(jobLink))
					{
						context.Response.StatusCode = (int)HttpStatusCode.OK;
						context.Response.WriteAsync(JsonConvert.SerializeObject(new { jobLink }));
						return true;
					}

					context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
					context.Response.WriteAsync(JsonConvert.SerializeObject(new { errorMessage }));
					return false;
				}));
			}
		}

		private static object ProcessNestedParameters(string parentId, Type parentType, Func<string, string> GetFormVariable, HashSet<Type> nestedTypes, out string errorMessage)
		{
			errorMessage = null;
			object instance;

			try
			{
				instance = Activator.CreateInstance(parentType);
			}
			catch
			{
				errorMessage = $"Unable to create instance of {parentType.Name}";
				return null;
			}

			foreach (var propertyInfo in parentType.GetProperties().Where(prop => Attribute.IsDefined(prop, typeof(DisplayDataAttribute))))
			{
				var propId = $"{parentId}_{propertyInfo.Name}";
				var propDisplayInfo = propertyInfo.GetCustomAttribute<DisplayDataAttribute>();
				var propLabel = propDisplayInfo.Label ?? propertyInfo.Name;

				if (propertyInfo.PropertyType == typeof(DateTime) || propertyInfo.PropertyType == typeof(Nullable<DateTime>))
				{
					propId = $"{propId}_datetimepicker";
				}

				var formInput = GetFormVariable(propId);
				if (formInput == null) continue;

				if (propertyInfo.PropertyType.IsInterface)
				{
					if (!VT.Implementations.ContainsKey(propertyInfo.PropertyType)) { errorMessage = $"{propDisplayInfo.Label ?? propertyInfo.Name} is not a valid interface type or is not registered in VT."; break; }
					VT.Implementations.TryGetValue(propertyInfo.PropertyType, out HashSet<Type> impls);
					var filteredImpls = new HashSet<Type>(impls.Where(impl => !nestedTypes.Contains(impl)));


					var test = GetFormVariable($"{propId}");
					var choosedImpl = impls.FirstOrDefault(concrete => concrete.FullName == test);

					if (choosedImpl == null)
					{
						errorMessage = $"cannot find a valid concrete type of {propertyInfo.PropertyType} or is not registered in VT.";
						break;
					}

					nestedTypes.Add(choosedImpl);
					var nestedInstance = ProcessNestedParameters($"{propId}_{choosedImpl.Name}", choosedImpl, GetFormVariable, nestedTypes, out errorMessage);
					nestedTypes.Remove(choosedImpl);

					propertyInfo.SetValue(instance, nestedInstance);
				}
				else if (propertyInfo.PropertyType == typeof(string))
				{
					propertyInfo.SetValue(instance, formInput);
					if (propDisplayInfo.IsRequired && string.IsNullOrWhiteSpace((string)formInput))
					{
						errorMessage = $"{propLabel} is required.";
						break;
					}
				}
				else if (propertyInfo.PropertyType == typeof(int) || propertyInfo.PropertyType == typeof(Nullable<int>))
				{
					if (int.TryParse(formInput, out int intValue))
					{
						propertyInfo.SetValue(instance, intValue);
					}
					else
					{
						errorMessage = $"{propLabel} was not in a correct format.";
						break;
					}
				}
				else if (propertyInfo.PropertyType == typeof(DateTime) || propertyInfo.PropertyType == typeof(Nullable<DateTime>))
				{
					var dateTimeValue = formInput == null ? DateTime.MinValue : DateTime.Parse(formInput, null, DateTimeStyles.RoundtripKind);
					propertyInfo.SetValue(instance, dateTimeValue);
					if (propDisplayInfo.IsRequired && dateTimeValue.Equals(DateTime.MinValue))
					{
						errorMessage = $"{propLabel} is required.";
						break;
					}
				}
				else if (propertyInfo.PropertyType == typeof(bool) || propertyInfo.PropertyType == typeof(Nullable<bool>))
				{
					propertyInfo.SetValue(instance, formInput == "on");
				}
				else if (propertyInfo.PropertyType.IsEnum)
				{
					try
					{
						var enumValue = Enum.Parse(propertyInfo.PropertyType, formInput);
						propertyInfo.SetValue(instance, enumValue);
					}
					catch (Exception e)
					{
						errorMessage = $"{propLabel} was not in a correct format: {e.Message}";
						break;
					}
				}
				else if (propertyInfo.PropertyType.IsClass)
				{
					if (!nestedTypes.Add(propertyInfo.PropertyType)) { continue; } //Circular reference, not allowed
					var nestedInstance = ProcessNestedParameters(propId, propertyInfo.PropertyType, GetFormVariable, nestedTypes, out errorMessage);
					nestedTypes.Remove(propertyInfo.PropertyType);

					propertyInfo.SetValue(instance, nestedInstance);
				}
				else if (!propertyInfo.PropertyType.IsValueType)
				{
					if (formInput == null || formInput.Length == 0)
					{
						propertyInfo.SetValue(instance, null);
						if (propDisplayInfo.IsRequired)
						{
							errorMessage = $"{propLabel} is required.";
							break;
						}
					}
					else
					{
						propertyInfo.SetValue(instance, JsonConvert.DeserializeObject(formInput, propertyInfo.PropertyType));
					}
				}
				else
				{
					propertyInfo.SetValue(instance, formInput);
				}
			}

			return instance;
		}
	}
}
