using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Hangfire.Annotations;
using Hangfire.Common;
using Hangfire.Dashboard.Management.v2.Metadata;
using Hangfire.Server;
using Hangfire.Storage.Monitoring;
using Newtonsoft.Json;
using Hangfire.Dashboard.Management.v2.Support;

namespace Hangfire.Dashboard.Management.v2.Pages.Partials
{
	internal class JobPartial : RazorPage
	{
		public IEnumerable<Func<RazorPage, MenuItem>> Items { get; }
		public readonly string JobId;
		public readonly JobMetadata Job;
		public readonly HashSet<Type> NestedTypes = new HashSet<Type>();

		public JobPartial(string id, JobMetadata job)
		{
			if (id == null) throw new ArgumentNullException(nameof(id));
			if (job == null) throw new ArgumentNullException(nameof(job));
			JobId = id;
			Job = job;
		}

		public override void Execute()
		{
			var inputs = string.Empty;

			foreach (var parameterInfo in Job.MethodInfo.GetParameters())
			{
				if (parameterInfo.ParameterType == typeof(PerformContext) || parameterInfo.ParameterType == typeof(IJobCancellationToken))
				{
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

				var labelText = displayInfo?.Label ?? parameterInfo.Name;
				var placeholderText = displayInfo?.Placeholder ?? parameterInfo.Name;
				var myId = $"{JobId}_{parameterInfo.Name}";
				
				if(parameterInfo.ParameterType.IsInterface)
				{
					if (!VT.Implementations.ContainsKey(parameterInfo.ParameterType)) { inputs += $"<span>No concrete implementation of \"{parameterInfo.ParameterType}\" found in the current assembly.</span>"; continue; }

					var impls = VT.Implementations[parameterInfo.ParameterType];

					if (impls.Count == 1)
					{
						var implType = impls.First();
						NestedTypes.Add(implType);
						inputs += $"<div class=\"panel panel-default\"><div class=\"panel-heading\" role=\"button\" data-toggle=\"collapse\" href=\"#collapse_{myId}_{implType.Name}\" aria-expanded=\"false\" 	aria-controls=\"collapse_{myId}_{implType.Name}\"><h4 class=\"panel-title\">{implType.Name} {parameterInfo.Name}</h4></div><div id=\"collapse_{myId}_{implType.Name}\" class=\"panel-collapse collapse\"><div 	class=\"panel-body\">";
						inputs += InputNested($"{myId}_{implType.Name}", implType);
						NestedTypes.Remove(implType);
					}
					else
					{
						string defaultValue = displayInfo.DefaultValue?.ToString();

						//drop down menu for multiple implementations
						inputs += InputImplsList(myId, displayInfo.CssClasses, labelText, placeholderText, displayInfo.Description, impls, defaultValue, displayInfo.IsDisabled);

						//not showing implementations
						foreach (Type impl in impls)
						{
							NestedTypes.Add(impl);
							string dNone = defaultValue != null && impl.Name == defaultValue ? "" : "d-none";

							inputs += $"<div id=\"{myId}_{impl.Name}\" class=\"panel panel-default impl-panels-for-{myId} {dNone}\"><div class=\"panel-heading\" role=\"button\" data-toggle=\"collapse\" href=\"#collapse_{myId}_{impl.Name}\" aria-expanded=\"false\" 	aria-controls=\"collapse_{myId}_{impl.Name}\"><h4 class=\"panel-title\">{impl.Name} {parameterInfo.Name}</h4></div><div id=\"collapse_{myId}_{impl.Name}\" 	class=\"panel-collapse collapse\"><div 	class=\"panel-body\">";
							inputs += InputNested($"{myId}_{impl.Name}", impl);
							NestedTypes.Remove(impl);
						}
					}
				}
				else if (parameterInfo.ParameterType == typeof(string))
				{
					inputs += InputTextbox(myId, displayInfo.CssClasses, labelText, placeholderText, displayInfo.Description, displayInfo.DefaultValue, displayInfo.IsDisabled, displayInfo.IsRequired, displayInfo.IsMultiLine);
				}
				else if (parameterInfo.ParameterType == typeof(int))
				{
					inputs += InputNumberbox(myId, displayInfo.CssClasses, labelText, placeholderText, displayInfo.Description, displayInfo.DefaultValue, displayInfo.IsDisabled, displayInfo.IsRequired);
				}
				else if (parameterInfo.ParameterType == typeof(Uri))
				{
					inputs += Input(myId, displayInfo.CssClasses, labelText, placeholderText, displayInfo.Description, "url", displayInfo.DefaultValue, displayInfo.IsDisabled, displayInfo.IsRequired);
				}
				else if (parameterInfo.ParameterType == typeof(DateTime))
				{
					inputs += InputDatebox(myId, displayInfo.CssClasses, labelText, placeholderText, displayInfo.Description, displayInfo.DefaultValue, displayInfo.IsDisabled, displayInfo.IsRequired, displayInfo.ControlConfiguration);
				}
				else if (parameterInfo.ParameterType == typeof(bool))
				{
					inputs += "<br/>" + InputCheckbox(myId, displayInfo.CssClasses, labelText, placeholderText, displayInfo.Description, displayInfo.DefaultValue, displayInfo.IsDisabled);
				}
				else if (parameterInfo.ParameterType.IsEnum)
				{
					var data = new Dictionary<string, string>();
					foreach (int v in Enum.GetValues(parameterInfo.ParameterType))
					{
						data.Add(Enum.GetName(parameterInfo.ParameterType, v), v.ToString());
					}
					inputs += InputDataList(myId, displayInfo.CssClasses, labelText, placeholderText, displayInfo.Description, data, displayInfo.DefaultValue?.ToString(), displayInfo.IsDisabled);
				}
				else if (parameterInfo.ParameterType.IsClass)
				{
					inputs += $"<div class=\"panel panel-default\"><div class=\"panel-heading\" role=\"button\" data-toggle=\"collapse\" href=\"#collapse_{myId}\" aria-expanded=\"false\" aria-controls=\"collapse_{myId}\"><h4 class=\"panel-title\">{labelText}</h4></div><div id=\"collapse_{myId}\" class=\"panel-collapse collapse\"><div class=\"panel-body\">";
					NestedTypes.Add(parameterInfo.ParameterType);
					inputs += InputNested(myId, parameterInfo.ParameterType);
					NestedTypes.Remove(parameterInfo.ParameterType);
				}
				else
				{
					inputs += InputTextbox(myId, displayInfo.CssClasses, labelText, placeholderText, displayInfo.Description, displayInfo.DefaultValue, displayInfo.IsDisabled, displayInfo.IsRequired);
				}
			}

			if (string.IsNullOrWhiteSpace(inputs))
			{
				inputs = "<span>This job does not require inputs</span>";
			}

			WriteLiteral($@"
				<div class=""well"">
					{inputs}
				</div>
				<div id=""{JobId}_error""></div>
				<div id=""{JobId}_success""></div>
");
		}

		protected string Input(string id, string cssClasses, string labelText, string placeholderText, string descriptionText, string inputtype, object defaultValue = null, bool isDisabled = false, bool isRequired = false)
		{
			var control = $@"
<div class=""form-group {cssClasses} {(isRequired ? "required" : "")}"">
	<label for=""{id}"" class=""control-label"">{labelText}</label>
";

			if (inputtype == "textarea")
			{
				control += $@"
	<textarea rows=""10"" class=""hdm-job-input hdm-input-textarea form-control"" placeholder=""{placeholderText}"" id=""{id}"" {(isDisabled ? "disabled='disabled'" : "")} {(isRequired ? "required='required'" : "")}>{defaultValue}</textarea>
";
			}
			else
			{
				control += $@"
	<input class=""hdm-job-input hdm-input-{inputtype} form-control"" type=""{inputtype}"" placeholder=""{placeholderText}"" id=""{id}"" value=""{defaultValue}"" {(isDisabled ? "disabled='disabled'" : "")} {(isRequired ? "required='required'" : "")} />
";
			}

			if (!string.IsNullOrWhiteSpace(descriptionText))
			{
				control += $@"
	<small id=""{id}Help"" class=""form-text text-muted"">{descriptionText}</small>
";
			}
			control += $@"
</div>";
			return control;
		}

		protected string InputTextbox(string id, string cssClasses, string labelText, string placeholderText, string descriptionText, object defaultValue = null, bool isDisabled = false, bool isRequired = false, bool isMultiline = false)
		{
			return Input(id, cssClasses, labelText, placeholderText, descriptionText, isMultiline ? "textarea" : "text", defaultValue, isDisabled, isRequired);
		}

		protected string InputNumberbox(string id, string cssClasses, string labelText, string placeholderText, string descriptionText, object defaultValue = null, bool isDisabled = false, bool isRequired = false)
		{
			return Input(id, cssClasses, labelText, placeholderText, descriptionText, "number", defaultValue, isDisabled, isRequired);
		}

		protected string InputDatebox(string id, string cssClasses, string labelText, string placeholderText, string descriptionText, object defaultValue = null, bool isDisabled = false, bool isRequired = false, string controlConfig = "")
		{
			if (!string.IsNullOrWhiteSpace(controlConfig))
			{
				controlConfig = JsonConvert.SerializeObject(JsonConvert.DeserializeObject(controlConfig), Formatting.None);
			}
			return $@"
<div class=""form-group {cssClasses} {(isRequired ? "required" : "")}"">
	<label for=""{id}"" class=""control-label"">{labelText}</label>
	<div class='hdm-job-input-container hdm-input-date-container input-group date' id='{id}_datetimepicker' data-td_options='{controlConfig}' data-td_value='{defaultValue}'>
		<input type='text' class=""hdm-job-input hdm-input-date form-control"" placeholder=""{placeholderText}"" {(isDisabled ? "disabled='disabled'" : "")} {(isRequired ? "required='required'" : "")} />
		<span class=""input-group-addon"">
			<span class=""glyphicon glyphicon-calendar""></span>
		</span>
	</div>
		{(!string.IsNullOrWhiteSpace(descriptionText) ? $@"
		<small id=""{id}Help"" class=""form-text text-muted"">{descriptionText}</small>
" : "")}
</div>";
		}

		protected string InputCheckbox(string id, string cssClasses, string labelText, string placeholderText, string descriptionText, object defaultValue = null, bool isDisabled = false)
		{
			var bDefaultValue = (bool)(defaultValue ?? false);

			return $@"
<div class=""form-group {cssClasses}"">
	<div class=""form-check"">
		<input class=""hdm-job-input hdm-input-checkbox form-check-input"" type=""checkbox"" id=""{id}"" {(bDefaultValue ? "checked='checked'" : "")} {(isDisabled ? "disabled='disabled'" : "")} />
		<label class=""form-check-label"" for=""{id}"">{labelText}</label>
	</div>
		{(!string.IsNullOrWhiteSpace(descriptionText) ? $@"
		<small id=""{id}Help"" class=""form-text text-muted"">{descriptionText}</small>
" : "")}
</div>";
		}

		protected string InputDataList(string id, string cssClasses, string labelText, string placeholderText, string descriptionText, Dictionary<string, string> data, string defaultValue = null, bool isDisabled = false)
		{
			var initText = defaultValue != null ? defaultValue : !string.IsNullOrWhiteSpace(placeholderText) ? placeholderText : "Select a value";
			var initValue = defaultValue != null && data.ContainsKey(defaultValue) ? data[defaultValue].ToString() : "";
			var output = $@"
<div class=""{cssClasses}"">
	<label class=""control-label"">{labelText}</label>
	<div class=""dropdown"">
		<button id=""{id}"" class=""hdm-job-input hdm-input-datalist btn btn-default dropdown-toggle input-control-data-list"" type=""button"" data-selectedvalue=""{initValue}"" data-toggle=""dropdown"" aria-haspopup=""true"" aria-expanded=""false"" {(isDisabled ? "disabled='disabled'" : "")}>
			<span class=""{id} input-data-list-text pull-left"">{initText}</span>
			<span class=""caret""></span>
		</button>
		<ul class=""dropdown-menu data-list-options"" data-optionsid=""{id}"" aria-labelledby=""{id}"">";
			foreach (var item in data)
			{
				output += $@"
			<li><a href=""javascript:void(0)"" class=""option"" data-optiontext=""{item.Key}"" data-optionvalue=""{item.Value}"">{item.Key}</a></li>
";
			}

			output += $@"
		</ul>
	</div>
	{(!string.IsNullOrWhiteSpace(descriptionText) ? $@"
		<small id=""{id}Help"" class=""form-text text-muted"">{descriptionText}</small>
" : "")}
</div>";

			return output;
		}
		
        protected string InputImplsList(string id, string cssClasses, string labelText, string placeholderText, string descriptionText, HashSet<Type> impls, string defaultValue = null, bool isDisabled = false)
        {
			var initText = "Select a value";
			var initValue = "";

			if (defaultValue != null && impls.Any(i => i.Name == defaultValue))
			{
				var initTextImpl = impls.First(i => i.Name == defaultValue);
				initText = initTextImpl.Name;
				initValue = initTextImpl.FullName;
			}

            var output = $@"
            <div class= form-group ""{cssClasses}"">
                <label class=""control-label"">{labelText}</label>
                <div class=""dropdown"">
                    <button id=""{id}"" class=""hdm-impl-selector-button hdm-job-input hdm-input-datalist btn btn-default dropdown-toggle input-control-data-list"" type=""button"" data-selectedvalue=""{initValue}"" data-toggle=""dropdown"" aria-haspopup=""true"" aria-expanded=""false"" {(isDisabled ? "disabled='disabled'" : "")}>
                        <span class=""{id} input-data-list-text pull-left"">{initText}</span>
                        <span class=""caret""></span>
                    </button>
                    <ul class=""dropdown-menu data-list-options impl-selector-options"" data-optionsid=""{id}"" aria-labelledby=""{id}"">";
                        foreach (var impl in impls)
                        {
                            var targetPanelId = $"{id}_{impl.Name}";
                            output += $@"
                        <li><a class=""option"" data-optiontext=""{impl.Name}"" data-optionvalue=""{impl.FullName}"" data-target-panel-id=""{targetPanelId}"">{impl.Name}</a></li>";
                        }
            
                        output += $@"
                    </ul>
                </div>
                {(!string.IsNullOrWhiteSpace(descriptionText) ? $@"
                    <small id=""{id}Help"" class=""form-text text-muted"">{descriptionText}</small>
            " : "")}
            </div>";

            return output;
        }

		protected string InputNested(string parentId, Type parentType)
		{
			string input = string.Empty;

			foreach (var propertyInfo in parentType.GetProperties()
				.Where(prop => Attribute.IsDefined(prop, typeof(DisplayDataAttribute))))
			{
				var propId = $"{parentId}_{propertyInfo.Name}";
				var propDisplayInfo = propertyInfo.GetCustomAttribute<DisplayDataAttribute>() ?? new DisplayDataAttribute();

				var propLabelText = propDisplayInfo?.Label ?? propertyInfo.Name;
				var propPlaceholderText = propDisplayInfo?.Placeholder ?? propertyInfo.Name;

								if(propertyInfo.PropertyType.IsInterface)
				{
					if (!VT.Implementations.ContainsKey(propertyInfo.PropertyType)) { input += $"<span>No concrete implementation of \"{propertyInfo.PropertyType}\" found in the current assembly.</span>"; continue; }

					var impls = VT.Implementations[propertyInfo.PropertyType];

					if (impls.Count == 1)
					{
						var implType = impls.First();
						NestedTypes.Add(implType);
						input += $"<div class=\"panel panel-default\"><div class=\"panel-heading\" role=\"button\" data-toggle=\"collapse\" href=\"#collapse_{propId}_{implType.Name}\" aria-expanded=\"false\" 	aria-controls=\"collapse_{propId}_{implType.Name}\"><h4 class=\"panel-title\">{implType.Name}</h4></div><div id=\"collapse_{propId}_{implType.Name}\" class=\"panel-collapse collapse\"><div 	class=\"panel-body\">";
						input += InputNested($"{propId}_{implType.Name}", implType);
						NestedTypes.Remove(implType);
					}
					else
					{
						var filteredImpls = new HashSet<Type>(impls.Where(impl => !NestedTypes.Contains(impl)));
						string defaultValue = propDisplayInfo.DefaultValue?.ToString();

						//drop down menu
						input += InputImplsList(propId, propDisplayInfo.CssClasses, propDisplayInfo.Label, propPlaceholderText, propDisplayInfo.Description, filteredImpls, defaultValue, propDisplayInfo.IsDisabled);

						//implementations
						foreach (Type impl in impls)
						{
							string dNone = defaultValue != null && impl.Name == defaultValue ? "" : "d-none";

							if (!NestedTypes.Add(impl)) { input += null; continue; } //Circular reference, not allowed -> null
							input += $"<div id=\"{propId}_{impl.Name}\" class=\"panel panel-default impl-panels-for-{propId} {dNone}\"><div class=\"panel-heading\" role=\"button\" data-toggle=\"collapse\" href=\"#collapse_{propId}_{impl.Name}\" aria-expanded=\"false\" 	aria-controls=\"collapse_{propId}_{impl.Name}\"><h4 class=\"panel-title\">{impl.Name} {propertyInfo.Name}</h4></div><div id=\"collapse_{propId}_{impl.Name}\" 	class=\"panel-collapse collapse\"><div 	class=\"panel-body\">";
							input += InputNested($"{propId}_{impl.Name}", impl);
							NestedTypes.Remove(impl);
						}
					}
				}
				else if (propertyInfo.PropertyType == typeof(string))
				{
					input += InputTextbox(propId, propDisplayInfo.CssClasses, propLabelText, propPlaceholderText, propDisplayInfo.Description, propDisplayInfo.DefaultValue, propDisplayInfo.IsDisabled, propDisplayInfo.IsRequired, propDisplayInfo.IsMultiLine);
				}
				else if (propertyInfo.PropertyType == typeof(int))
				{
					input += InputNumberbox(propId, propDisplayInfo.CssClasses, propLabelText, propPlaceholderText, propDisplayInfo.Description, propDisplayInfo.DefaultValue, propDisplayInfo.IsDisabled, propDisplayInfo.IsRequired);
				}
				else if (propertyInfo.PropertyType == typeof(Uri))
				{
					input += Input(propId, propDisplayInfo.CssClasses, propLabelText, propPlaceholderText, propDisplayInfo.Description, "url", propDisplayInfo.DefaultValue, propDisplayInfo.IsDisabled, propDisplayInfo.IsRequired);
				}
				else if (propertyInfo.PropertyType == typeof(DateTime))
				{
					input += InputDatebox(propId, propDisplayInfo.CssClasses, propLabelText, propPlaceholderText, propDisplayInfo.Description, propDisplayInfo.DefaultValue, propDisplayInfo.IsDisabled, propDisplayInfo.IsRequired, propDisplayInfo.ControlConfiguration);
				}
				else if (propertyInfo.PropertyType == typeof(bool))
				{
					input += "<br/>" + InputCheckbox(propId, propDisplayInfo.CssClasses, propLabelText, propPlaceholderText, propDisplayInfo.Description, propDisplayInfo.DefaultValue, propDisplayInfo.IsDisabled);
				}
				else if (propertyInfo.PropertyType.IsEnum)
				{
					var data = new Dictionary<string, string>();
					foreach (int v in Enum.GetValues(propertyInfo.PropertyType))
					{
						data.Add(Enum.GetName(propertyInfo.PropertyType, v), v.ToString());
					}
					input += InputDataList(propId, propDisplayInfo.CssClasses, propLabelText, propPlaceholderText, propDisplayInfo.Description, data, propDisplayInfo.DefaultValue?.ToString(), propDisplayInfo.IsDisabled);
				}
				else if (propertyInfo.PropertyType.IsClass)
				{
					if (!NestedTypes.Add(propertyInfo.PropertyType)) { input += null;  continue; } //Circular reference, not allowed -> null
					input += $"<div class=\"panel panel-default\"><div class=\"panel-heading\" role=\"button\" data-toggle=\"collapse\" href=\"#collapse_{propId}\" aria-expanded=\"false\" aria-controls=\"collapse_{propId}\"><h4 class=\"panel-title\">{propLabelText}</h4></div><div id=\"collapse_{propId}\" class=\"panel-collapse collapse\"><div class=\"panel-body\">";
					input += InputNested(propId, propertyInfo.PropertyType);
					NestedTypes.Remove(propertyInfo.PropertyType);
				}
				else
				{
					input += InputTextbox(propId, propDisplayInfo.CssClasses, propLabelText, propPlaceholderText, propDisplayInfo.Description, propDisplayInfo.DefaultValue, propDisplayInfo.IsDisabled, propDisplayInfo.IsRequired);
				}
			}

			input += "</div></div></div>";
			return input;
		}

	}
}
