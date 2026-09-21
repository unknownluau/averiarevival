namespace Averia.RccServiceArbiter.Rendering;

public sealed class RenderValidationException(string message) : Exception(message);
public sealed class RenderCapacityException(string message) : Exception(message);
public sealed class RenderExecutionException(string message, Exception? inner = null) : Exception(message, inner);

