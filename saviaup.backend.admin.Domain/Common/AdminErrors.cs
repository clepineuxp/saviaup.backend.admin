namespace SaviaUp.Admin.Domain.Common;

public static class AdminErrors
{
    public static readonly Error Validation = new("ADMIN_VALIDATION", "Los datos enviados no son válidos.", 400);
    public static readonly Error InvalidCredentials = new("ADMIN_INVALID_CREDENTIALS", "El correo o la contraseña no son válidos.", 401);
    public static readonly Error Unauthenticated = new("ADMIN_UNAUTHENTICATED", "La sesión administrativa no es válida.", 401);
    public static readonly Error Forbidden = new("ADMIN_FORBIDDEN", "No tienes permisos administrativos para realizar esta acción.", 403);
    public static readonly Error AdminUserNotFound = new("ADMIN_USER_NOT_FOUND", "El usuario administrador no existe.", 404);
    public static readonly Error AdminEmailExists = new("ADMIN_EMAIL_EXISTS", "Ya existe un administrador con este correo.", 409);
    public static readonly Error PlatformUserNotFound = new("PLATFORM_USER_NOT_FOUND", "El usuario de plataforma no existe.", 404);
    public static readonly Error OrganizationNotFound = new("ADMIN_ORGANIZATION_NOT_FOUND", "La organización no existe.", 404);
    public static readonly Error MembershipNotFound = new("ADMIN_MEMBERSHIP_NOT_FOUND", "La membresía no existe.", 404);
    public static readonly Error OwnerMustBeActiveMember = new("ADMIN_OWNER_MUST_BE_ACTIVE_MEMBER", "El nuevo owner debe ser un miembro activo de la organización.", 422);
    public static readonly Error OwnerCannotBeDisabled = new("ADMIN_OWNER_CANNOT_BE_DISABLED", "El owner no puede deshabilitarse. Transfiere primero la propiedad.", 409);
    public static readonly Error OwnerCannotBeReassigned = new("ADMIN_OWNER_CANNOT_BE_REASSIGNED", "El owner no puede reasignarse. Transfiere primero la propiedad.", 409);
    public static readonly Error OwnerFallbackRoleMissing = new("ADMIN_OWNER_FALLBACK_ROLE_MISSING", "La organización no tiene otro rol activo para el owner anterior.", 409);
    public static readonly Error MembershipAlreadyExists = new("ADMIN_MEMBERSHIP_ALREADY_EXISTS", "El usuario ya pertenece a la organización destino.", 409);
    public static readonly Error PlanNotFound = new("ADMIN_PLAN_NOT_FOUND", "El plan no existe.", 404);
    public static readonly Error PlanCodeExists = new("ADMIN_PLAN_CODE_EXISTS", "Ya existe un plan con este código.", 409);
    public static readonly Error PlanInUse = new("ADMIN_PLAN_IN_USE", "El plan está asignado y no puede archivarse o eliminarse con esta operación.", 409);
    public static readonly Error PlanNotActive = new("ADMIN_PLAN_NOT_ACTIVE", "Solo se pueden asignar planes activos.", 422);
    public static readonly Error PermissionNotFound = new("ADMIN_PERMISSION_NOT_FOUND", "Uno o más permisos no existen en el catálogo operativo.", 422);
    public static readonly Error AssignmentNotFound = new("ADMIN_PLAN_ASSIGNMENT_NOT_FOUND", "La organización no tiene un plan asignado.", 404);
    public static readonly Error PermissionSyncFailed = new("ADMIN_PERMISSION_SYNC_FAILED", "La configuración fue guardada, pero no pudo sincronizarse con la plataforma operacional.", 503);
    public static readonly Error OperationalUnavailable = new("ADMIN_OPERATIONAL_UNAVAILABLE", "No fue posible consultar la plataforma operacional.", 503);
}
