namespace {{cookiecutter.assembly_name}}.AppHost;

public class SwaggerUIAnnotation(string[] documentNames, string path, EndpointReference endpointReference) : IResourceAnnotation
{
    public string[] DocumentNames { get; } = documentNames;
    public string Path { get; } = path;
    public EndpointReference EndpointReference { get; } = endpointReference;
}