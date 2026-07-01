namespace SchoolMgmt.Domain.Common;

public class ConflictException : Exception
{
    public ConflictException(string message) : base(message) { }
}
