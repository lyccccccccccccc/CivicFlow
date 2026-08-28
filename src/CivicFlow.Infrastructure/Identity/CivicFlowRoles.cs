namespace CivicFlow.Infrastructure.Identity;

public static class CivicFlowRoles
{
    public const string Resident = "Resident";
    public const string CaseOfficer = "CaseOfficer";
    public const string TeamManager = "TeamManager";
    public const string SystemAdministrator = "SystemAdministrator";

    public static readonly string[] All =
    [
        Resident,
        CaseOfficer,
        TeamManager,
        SystemAdministrator
    ];
}
