using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using EastFive;
using EastFive.Extensions;
using EastFive.Linq;
using EastFive.Reflection;
using EastFive.Serialization;
using EastFive.Api.Resources;

namespace EastFive.Api.Meta.Flows
{
    public interface IDefineWorkflowVariable
    {
        (string, string) GetNameAndValue(Resources.Response response, Method method);
    }

    public class WorkflowVariableResourceResponseAttribute : System.Attribute, IDefineWorkflowScriptResponse
    {
        public string[] GetInitializationLines(Response response, Method method)
        {
            return new string[] { };
        }

        public string[] GetScriptLines(Response response, Method method)
        {
            var resourceType = response.ParamInfo.ParameterType.TryGetAttributeInterface(out IProvideResponseType responseTypeProvider) ?
                responseTypeProvider.GetResponseType(response.ParamInfo)
                :
                method.Route.Type;

            if (response.IsMultipart)
            {
                var parseLine = "let resourceList = pm.response.json();\r";
                var loopBegin = "for(const resourceIndex in resourceList) {\r";
                var varResource = "\tlet resource = resourceList[resourceIndex];\r";

                var resourceAssignments = GetResourceAssignments(out string discard1, out string discard2);

                var loopEnd = "\t}\r";

                return new string[][]
                        {
                            new string []
                            {
                                parseLine,
                                loopBegin,
                                varResource,
                            },
                            resourceAssignments,
                            new string []
                            {
                                loopEnd,
                            },
                        }
                    .SelectMany()
                    .ToArray();
            }

            {
                var parseLine = "let resource = pm.response.json();\r";
                var resourceAssignments = GetResourceAssignments(out string resourceTypeName, out string resourceNameName);
                var lastResourceAssignment = resourceNameName.HasBlackSpace() ?
                    $"pm.environment.set(\"{resourceTypeName}\", resourceId);\r"
                    :
                    string.Empty;

                var finalProperties = response.ParamInfo
                    .GetAttributesInterface<IDefineWorkflowVariable>()
                    .Select(extraVariableDefinition => extraVariableDefinition.GetNameAndValue(response, method))
                    .Select(
                        tpl => $"pm.environment.set(\"{tpl.Item1}\", resource.{tpl.Item2});\r")
                    .ToArray();

                return new string[][]
                        {
                            new string []
                            {
                                parseLine,
                            },
                            resourceAssignments,
                            finalProperties,
                            new string []
                            {
                                lastResourceAssignment,
                            },
                        }
                    .SelectMany()
                    .ToArray();
            }

            string[] GetResourceAssignments(out string resourceTypeName,
                out string resourceNameName)
            {
                var resourceName = method.Route.Name;
                resourceTypeName = $"{resourceName}";

                var idProperties = method.Route.Properties.Where(prop => prop.IsIdentfier).ToArray();
                if (!idProperties.Any())
                {
                    resourceNameName = string.Empty;
                    return new string[] { };
                }
                var idProperty = idProperties.First().Name;

                resourceNameName = resourceType
                    .GetPropertyAndFieldsWithAttributesInterface<ITitleResource>()
                    .First(
                        (tpl, next) =>
                        {
                            return tpl.Item1.TryGetAttributeInterface(out IProvideApiValue apiValueProvider) ?
                                apiValueProvider.GetPropertyName(tpl.Item1)
                                :
                                tpl.Item1.Name;
                        },
                        () => idProperty);

                var varResourceId = $"\t\tlet resourceId = resource.{idProperty};\r";

                var varResourceName = $"\t\tlet resourceName = resource.{resourceNameName};\r";
                var varNameVariable = "\t\tlet resourceNameVariable = resourceName.replace(/[^A-Z0-9]/ig, \"_\");\r";

                var varNameVariableName = $"\t\tlet resourceNameVariableName = \"{resourceTypeName}_\" + resourceNameVariable;\r";
                var setEnvVariable = "\t\tpm.environment.set(resourceNameVariableName, resourceId);\r";

                return new string[]
                {
                    varResourceId, varResourceName, varNameVariable, varNameVariableName, setEnvVariable
                };
            }

        }
    }
}
