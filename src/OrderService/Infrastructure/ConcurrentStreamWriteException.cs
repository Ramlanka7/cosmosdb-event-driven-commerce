namespace OrderService.Infrastructure;

internal sealed class ConcurrentStreamWriteException(string message, Exception? innerException = null) : Exception(message, innerException);