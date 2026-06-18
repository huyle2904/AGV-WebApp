namespace NewAGV.Worker.Services;

public sealed class SeerRobotOptions
{
    public const string SectionName = "SeerRobot";

    public string Host { get; set; } = "192.168.5.102";
    public int StatusPort { get; set; } = 19204;
    public int ControlPort { get; set; } = 19205;
    public int NavigationPort { get; set; } = 19206;
    public int OtherPort { get; set; } = 19210;
    public byte ProtocolVersion { get; set; } = 1;
    public int RequestTimeoutSeconds { get; set; } = 3;
    public int PollIntervalSeconds { get; set; } = 3;
    public int MapSyncIntervalSeconds { get; set; } = 60;
    public string RobotIdFallback { get; set; } = "AGV-01";
    public string RobotNameFallback { get; set; } = "SEER AGV";
    public string ApiBaseUrl { get; set; } = "http://localhost:5222";
    public string HomeStationId { get; set; } = "HOME-01";
    public bool EnablePush { get; set; }
    public int PushIntervalMs { get; set; } = 1000;
}
