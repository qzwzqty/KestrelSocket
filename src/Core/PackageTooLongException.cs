namespace KestrelSocket.Core
{
    /// <summary>
    /// 异常
    /// </summary>
    /// <param name="message"></param>
    public class PackageTooLongException(string message = "Package too long")
        : Exception(message)
    {
    }
}
