namespace Kargoyeri.Studio.Core.Infrastructure;

public static class StudioRoles
{
    public const string Admin = "StudioAdmin";
    public const string Operator = "StudioOperator";

    public const string WorkspaceCodeClaim = "studio:workspace-code";
    public const string WorkspaceNameClaim = "studio:workspace-name";
    public const string UsernameClaim      = "studio:username";
    public const string TenantRoleClaim    = "studio:tenant-role"; // ReadOnly / Operator / Manager

    // ── Authorization policy adlari ─────────────────────────────────────────────
    public const string CanWrite        = "Studio.CanWrite";        // Operator + Manager + Admin
    public const string CanManageTenant = "Studio.CanManageTenant"; // Manager + Admin
}
