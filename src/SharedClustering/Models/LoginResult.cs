namespace SharedClustering.Models
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
