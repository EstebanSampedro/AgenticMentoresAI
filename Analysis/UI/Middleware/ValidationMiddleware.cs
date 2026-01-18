//using Academikus.AnalysisMentoresVerdes.Entity.OutPut;
//using Microsoft.AspNetCore.Mvc.Controllers;
//using System.ComponentModel.DataAnnotations;
//using System.Text.Json;

//namespace Academikus.AnalysisMentoresVerdes.WebApi.Middleware
//{
//    /// <summary>
//    /// Clase que se encarga de validar los modelos de entrada y salida de los controladores.
//    /// </summary>
//    public class ValidationMiddleware
//    {
//        private readonly RequestDelegate _next;
//        private string user = string.Empty;

//        /// <summary>
//        /// Constructor de la clase.
//        /// </summary>
//        /// <param name="next"></param>
//        public ValidationMiddleware(RequestDelegate next)
//        {
//            _next = next;
//        }

//        /// <summary>
//        /// Método que se encarga de validar los modelos de entrada y salida de los controladores por medio de los atributos de validación.
//        /// </summary>
//        /// <param name="context"></param>
//        /// <returns></returns>
//        public async Task InvokeAsync(HttpContext context)
//        {
//            var endpoint = context.GetEndpoint();
//            if (endpoint != null)
//            {
//                var actionDescriptor = endpoint.Metadata.GetMetadata<ControllerActionDescriptor>();
//                if (actionDescriptor != null)
//                {
//                    var parameters = actionDescriptor.Parameters;
//                    var errors = new List<string>();

//                    try
//                    {
//                        foreach (var parameter in parameters)
//                        {
//                            if (parameter.BindingInfo?.BindingSource == Microsoft.AspNetCore.Mvc.ModelBinding.BindingSource.Body)
//                            {
//                                context.Request.EnableBuffering();
//                                using (var reader = new StreamReader(context.Request.Body, leaveOpen: true))
//                                {
//                                    var body = await reader.ReadToEndAsync();
//                                    context.Request.Body.Position = 0;

//                                    var model = JsonSerializer.Deserialize(body, parameter.ParameterType, new JsonSerializerOptions
//                                    {
//                                        PropertyNameCaseInsensitive = true
//                                    });

//                                    if (model != null)
//                                    {
//                                        try
//                                        {
//                                            user = model.GetType().GetProperty("User")?.GetValue(model)?.ToString();
//                                        }
//                                        catch (Exception)
//                                        {
//                                        }

//                                        var validationResults = new List<ValidationResult>();
//                                        var validationContext = new ValidationContext(model);
//                                        if (!Validator.TryValidateObject(model, validationContext, validationResults, true))
//                                        {
//                                            errors.AddRange(validationResults.Select(vr => vr.ErrorMessage));
//                                        }
//                                    }
//                                }
//                            }
//                            else if (parameter.BindingInfo?.BindingSource == Microsoft.AspNetCore.Mvc.ModelBinding.BindingSource.Query)
//                            {
//                                var query = context.Request.Query;
//                                var model = Activator.CreateInstance(parameter.ParameterType);
//                                var queryValues = query.ToDictionary(q => q.Key, q => (object)q.Value.ToString());

//                                foreach (var prop in parameter.ParameterType.GetProperties())
//                                {
//                                    if (queryValues.ContainsKey(prop.Name))
//                                    {
//                                        prop.SetValue(model, Convert.ChangeType(queryValues[prop.Name], prop.PropertyType));
//                                    }
//                                }

//                                try
//                                {
//                                    var user = (queryValues.GetValueOrDefault("User")
//                                         ?? queryValues.GetValueOrDefault("user")) ?? string.Empty;

//                                }
//                                catch (Exception)
//                                {
//                                }

//                                var validationResults = new List<ValidationResult>();
//                                var validationContext = new ValidationContext(model);
//                                if (!Validator.TryValidateObject(model, validationContext, validationResults, true))
//                                {
//                                    errors.AddRange(validationResults.Select(vr => vr.ErrorMessage));
//                                }
//                            }
//                        }
//                    }
//                    catch (Exception ex)
//                    {
//                        context.Response.StatusCode = StatusCodes.Status200OK;
//                        WebApiResponseDTO webApiResponseModel = new WebApiResponseDTO();
//                        webApiResponseModel.ResponseCode = (ResponseTypeCodeDTO)Convert.ToInt32(ResponseTypeCodeDTO.Error);
//                        webApiResponseModel.ResponseMessage = ex.Message;
//                        webApiResponseModel.User = user;

//                        var capitalcaseJsonSerializerOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
//                        var capitalcaseJson = JsonSerializer.Serialize(webApiResponseModel, capitalcaseJsonSerializerOptions);

//                        await context.Response.WriteAsJsonAsync(webApiResponseModel, capitalcaseJsonSerializerOptions);

//                        return;
//                    }

//                    if (errors.Any())
//                    {
//                        context.Response.StatusCode = StatusCodes.Status200OK;
//                        WebApiResponseDTO webApiResponseModel = new WebApiResponseDTO();
//                        webApiResponseModel.ResponseCode = (ResponseTypeCodeDTO)Convert.ToInt32(ResponseTypeCodeDTO.Error);
//                        webApiResponseModel.ResponseMessage = string.Join("\n", errors);
//                        webApiResponseModel.User = user;

//                        var capitalcaseJsonSerializerOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
//                        var capitalcaseJson = JsonSerializer.Serialize(webApiResponseModel, capitalcaseJsonSerializerOptions);

//                        await context.Response.WriteAsJsonAsync(webApiResponseModel, capitalcaseJsonSerializerOptions);

//                        return;
//                    }
//                }
//            }

//            await _next(context);
//        }
//    }
//}
