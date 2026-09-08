using AtlasNOC.Application.Dtos;
using Xunit;

namespace AtlasNOC.Tests.Unit;

public class UserAdministrationContractTests
{
    [Fact]
    public void UserOutputDtos_NeverExposePasswordsTokensOrHashes()
    {
        foreach (var dto in new[] { typeof(UserLiteDto), typeof(UserDetailDto) })
        {
            var names = dto.GetProperties().Select(p => p.Name).ToList();
            Assert.DoesNotContain(names, name => name.Contains("Password", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(names, name => name.Contains("Hash", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(names, name => name.Contains("Token", StringComparison.OrdinalIgnoreCase));
        }
    }
}
