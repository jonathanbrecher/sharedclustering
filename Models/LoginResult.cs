namespace AncestryDnaClustering.Models
{
    public enum LoginResult
    {
        InternalError,
        Success,
        Unauthorized,
        InvalidCredentials,
        MultifactorAuthentication,
        Exception,
    }
}
