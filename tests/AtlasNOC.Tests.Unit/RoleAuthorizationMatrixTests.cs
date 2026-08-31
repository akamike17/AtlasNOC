using AtlasNOC.Web.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace AtlasNOC.Tests.Unit;

/// <summary>
/// Matriz de autorización por roles (§3): verifica estáticamente que los
/// controllers de escritura exigen Administrator/NocOperator y que la lectura
/// permite ReadOnly. ReadOnly no puede crear sitio/dispositivo, reconocer o
/// resolver alertas, resolver incidentes, ni iniciar discovery.
/// </summary>
public class RoleAuthorizationMatrixTests
{
    private const string AdminNoc = "Administrator,NocOperator";
    private const string AdminNocRead = "Administrator,NocOperator,ReadOnly";

    private static AuthorizeAttribute? ClassRoles(Type controller)
        => controller.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>().FirstOrDefault();

    private static List<AuthorizeAttribute> MethodRoles(Type controller, string methodName)
        => controller.GetMethods()
            .Where(m => m.Name == methodName)
            .SelectMany(m => m.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true))
            .Cast<AuthorizeAttribute>()
            .ToList();

    private static bool AllowsRole(AuthorizeAttribute? attr, string role)
        => attr?.Roles?.Contains(role, StringComparison.Ordinal) == true;

    [Theory]
    [InlineData(typeof(SitesController), "Create")]
    [InlineData(typeof(DevicesController), "Create")]
    [InlineData(typeof(AlertsController), "Acknowledge")]
    [InlineData(typeof(AlertsController), "Resolve")]
    [InlineData(typeof(IncidentsController), "Resolve")]
    public void Write_actions_reject_readonly(Type controller, string action)
    {
        // La acción (o la clase) debe restringir a Admin+Noc (sin ReadOnly).
        var methodAttrs = MethodRoles(controller, action);
        var effectiveRoles = methodAttrs.Count > 0
            ? string.Join(",", methodAttrs.Select(a => a.Roles ?? "")).Replace(" ", "")
            : ClassRoles(controller)?.Roles?.Replace(" ", "") ?? "";

        Assert.DoesNotContain("ReadOnly", effectiveRoles);
        Assert.Contains("NocOperator", effectiveRoles);
    }

    [Fact]
    public void ReadOnly_can_view_sites_devices_alerts_incidents()
    {
        foreach (var type in new[]
        {
            typeof(SitesController), typeof(DevicesController),
            typeof(AlertsController), typeof(IncidentsController),
        })
        {
            var cls = ClassRoles(type);
            Assert.NotNull(cls);
            Assert.Contains("ReadOnly", cls!.Roles ?? "");
        }
    }

    [Fact]
    public void Discovery_credentials_alertrules_require_operator_or_admin()
    {
        foreach (var type in new[]
        {
            typeof(DiscoveryController), typeof(CredentialsController), typeof(AlertRulesController),
        })
        {
            var cls = ClassRoles(type);
            Assert.NotNull(cls);
            // Estos controllers exigen operator o admin; ReadOnly NO puede
            // iniciar discovery, crear credenciales ni crear reglas.
            Assert.DoesNotContain("ReadOnly", cls!.Roles ?? "");
            Assert.Contains("NocOperator", cls.Roles ?? "");
        }
    }
}