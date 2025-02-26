using Blueboard.Core.Auth.Interfaces;

namespace Blueboard.Core.Auth.Permissions;

public static class ImportPermissions
{
    public class IndexImportKeys : IPermission
    {
        public string Name { get; } = "Import.IndexImportKeys";
        public string DisplayName { get; } = "Import kulcsok listázása";
        public string Description { get; } = "Az összes import kulcs lekérése és listázása";
        public bool Dangerous { get; } = false;
    }

    public class ViewImportKey : IPermission
    {
        public string Name { get; } = "Import.ViewImportKey";
        public string DisplayName { get; } = "Import kulcs megtekintése";
        public string Description { get; } = "Egy adott import kulcs lekérése és megtekintése id alapján";
        public bool Dangerous { get; } = true;
    }

    public class CreateImportKey : IPermission
    {
        public string Name { get; } = "Import.CreateImportKey";
        public string DisplayName { get; } = "Import kulcs létrehozása";
        public string Description { get; } = "Új import kulcs létrehozása";
        public bool Dangerous { get; } = true;
    }

    public class UpdateImportKey : IPermission
    {
        public string Name { get; } = "Import.UpdateImportKey";
        public string DisplayName { get; } = "Import kulcs módosítása";
        public string Description { get; } = "Egy adott import kulcs módosítása id alapján";
        public bool Dangerous { get; } = true;
    }

    public class DeleteImportKey : IPermission
    {
        public string Name { get; } = "Import.DeleteImportKey";
        public string DisplayName { get; } = "Import kulcs törlése";
        public string Description { get; } = "Egy adott import kulcs törlése id alapján";
        public bool Dangerous { get; } = true;
    }
}