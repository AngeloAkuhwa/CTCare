namespace CTCare.Shared.Settings;
public class RedisSetting
{
    public string ConnectionString { get; set; }
    public string ConnectionStringLocalDEv { get; set; }
    public string Password { get; set; }
    public int ConnectRetry { get; set; }
    public int ConnectTimeout { get; set; }
    public bool AbortOnConnectFail { get; set; }
    public int SyncTimeOut { get; set; }
    public TimeSpan SlidingExpiration { get; set; }
    public TimeSpan AbsoluteExpiration { get; set; }
}
