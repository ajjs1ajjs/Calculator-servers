namespace AIResourceCalculator.Interfaces;

public record UpdateInfo(string Version, string DownloadUrl);

public interface IUpdateCheckService
{
    Task<UpdateInfo?> CheckForUpdateAsync();
}
