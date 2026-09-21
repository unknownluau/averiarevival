using System.Globalization;
using System.Xml.Linq;

namespace Averia.RccServiceArbiter.Rcc;

public static class RccSoapEnvelope
{
    private static readonly XNamespace SoapNamespace = "http://schemas.xmlsoap.org/soap/envelope/";
    private static readonly XNamespace XsiNamespace = "http://www.w3.org/2001/XMLSchema-instance";
    private static readonly XNamespace XsdNamespace = "http://www.w3.org/2001/XMLSchema";

    public static XDocument OpenJobEx(string serviceUrl, Job job, ScriptExecution script)
    {
        var serviceNamespace = ServiceNamespace(serviceUrl);
        return Wrap(new XElement(serviceNamespace + "OpenJobEx", JobElement(serviceNamespace, job), ScriptElement(serviceNamespace, script)));
    }

    public static XDocument BatchJobEx(string serviceUrl, Job job, ScriptExecution script)
    {
        var serviceNamespace = ServiceNamespace(serviceUrl);
        return Wrap(new XElement(serviceNamespace + "BatchJobEx", JobElement(serviceNamespace, job), ScriptElement(serviceNamespace, script)));
    }

    public static XDocument BatchJob(string serviceUrl, Job job, ScriptExecution script)
    {
        var serviceNamespace = ServiceNamespace(serviceUrl);
        return Wrap(new XElement(serviceNamespace + "BatchJob", JobElement(serviceNamespace, job), ScriptElement(serviceNamespace, script)));
    }

    public static XDocument ExecuteEx(string serviceUrl, string jobId, ScriptExecution script)
    {
        var serviceNamespace = ServiceNamespace(serviceUrl);
        return Wrap(new XElement(serviceNamespace + "ExecuteEx",
            new XElement(serviceNamespace + "jobID", jobId),
            ScriptElement(serviceNamespace, script)));
    }

    public static XDocument CloseJob(string serviceUrl, string jobId)
    {
        var serviceNamespace = ServiceNamespace(serviceUrl);
        return Wrap(new XElement(serviceNamespace + "CloseJob", new XElement(serviceNamespace + "jobID", jobId)));
    }

    public static XDocument GetAllJobs(string serviceUrl)
    {
        return Wrap(new XElement(ServiceNamespace(serviceUrl) + "GetAllJobs"));
    }

    public static string ToRequestBody(XDocument document)
    {
        return document.ToString(SaveOptions.DisableFormatting);
    }

    private static XDocument Wrap(XElement bodyContent)
    {
        return new XDocument(
            new XElement(SoapNamespace + "Envelope",
                new XAttribute(XNamespace.Xmlns + "xsi", XsiNamespace),
                new XAttribute(XNamespace.Xmlns + "xsd", XsdNamespace),
                new XAttribute(XNamespace.Xmlns + "soap", SoapNamespace),
                new XElement(SoapNamespace + "Body", bodyContent)));
    }

    public static string SoapAction(string serviceUrl, string action)
    {
        return $"{ServiceNamespace(serviceUrl).NamespaceName}{action}";
    }

    private static XNamespace ServiceNamespace(string serviceUrl)
    {
        var normalized = serviceUrl.Trim().TrimEnd('/');
        if (normalized.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return normalized + "/";
        }

        return $"http://{normalized}/";
    }

    private static XElement JobElement(XNamespace serviceNamespace, Job job)
    {
        return new XElement(serviceNamespace + "job",
            new XElement(serviceNamespace + "id", job.Id),
            new XElement(serviceNamespace + "expirationInSeconds", job.ExpirationInSeconds.ToString(CultureInfo.InvariantCulture)),
            new XElement(serviceNamespace + "category", job.Category.ToString(CultureInfo.InvariantCulture)),
            new XElement(serviceNamespace + "cores", job.Cores.ToString(CultureInfo.InvariantCulture)));
    }

    private static XElement ScriptElement(XNamespace serviceNamespace, ScriptExecution script)
    {
        return new XElement(serviceNamespace + "script",
            new XElement(serviceNamespace + "name", script.Name),
            new XElement(serviceNamespace + "script", new XCData(script.Script)),
            new XElement(serviceNamespace + "arguments", script.Arguments.Select(value => LuaValueElement(serviceNamespace, value))));
    }

    private static XElement LuaValueElement(XNamespace serviceNamespace, LuaValue value)
    {
        return new XElement(serviceNamespace + "LuaValue",
            new XElement(serviceNamespace + "type", value.Type.ToString()),
            new XElement(serviceNamespace + "value", value.Value),
            new XElement(serviceNamespace + "table", value.Table.Select(child => LuaValueElement(serviceNamespace, child))));
    }
}
