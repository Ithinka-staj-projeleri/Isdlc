namespace Isdlc.Application.Common.Exceptions;

/// <summary>
/// The requested record does not exist. Returned to the client as 404.
/// </summary>
public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message)
    {
    }

    public NotFoundException(string entityName, object id)
        : base($"{entityName} bulunamadı (Id: {id}).")
    {
    }
}
