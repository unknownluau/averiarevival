using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

public class SwaggerFileOperationFilter : IOperationFilter
{
    private const string FileUploadMime = "multipart/form-data";

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (operation.RequestBody?.Content == null)
            return;

        var formData = operation.RequestBody.Content
            .FirstOrDefault(content => content.Key.Equals(FileUploadMime, StringComparison.OrdinalIgnoreCase));
        if (formData.Value == null)
            return;

        var fileProperties = GetFilePropertyNames(context).ToArray();
        if (fileProperties.Length == 0)
            return;

        var schema = formData.Value.Schema as OpenApiSchema ?? new OpenApiSchema();
        schema.Type ??= JsonSchemaType.Object;
        schema.Properties ??= new Dictionary<string, IOpenApiSchema>();

        foreach (var propertyName in fileProperties)
        {
            schema.Properties[propertyName] = new OpenApiSchema
            {
                Type = JsonSchemaType.String,
                Format = "binary",
            };
        }

        formData.Value.Schema = schema;
    }

    private static IEnumerable<string> GetFilePropertyNames(OperationFilterContext context)
    {
        foreach (var parameter in context.MethodInfo.GetParameters())
        {
            if (parameter.ParameterType == typeof(IFormFile) && parameter.Name != null)
            {
                yield return parameter.Name;
                continue;
            }

            foreach (var property in parameter.ParameterType.GetProperties())
            {
                if (property.PropertyType == typeof(IFormFile))
                    yield return property.Name;
            }
        }
    }
}
