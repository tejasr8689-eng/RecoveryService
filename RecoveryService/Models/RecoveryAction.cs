namespace RecoveryService.Models
{
    public enum RecoveryAction
    {
        RestartAppPool = 0,
        //StartWebsite = 1,
        RestartWebsite = 1,
        FullRecovery =2
    }
}
