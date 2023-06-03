namespace AzureDDNS.Settings
{
    public enum AzureCredentialType
    {
        //ManagedIdentity,
        TokenCredential,
        DefaultCredential = TokenCredential,
        ServicePrincipal,
    }
}