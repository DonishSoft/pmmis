using System.Security.Claims;

namespace PMMIS.Web.Services;

/// <summary>
/// Сервис проверки прав доступа к модулям (для использования в Views)
/// </summary>
public interface IPermissionService
{
    Task<bool> HasPermissionAsync(ClaimsPrincipal user, string menuKey, string permissionType);
}
