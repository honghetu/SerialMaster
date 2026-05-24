using SerialMaster.Core.Models;

namespace SerialMaster.Core.Services;

public interface ISettingsService
{
    AppSettings Load();
    void Save(AppSettings settings);
    string GetSettingsDirectory();
}
