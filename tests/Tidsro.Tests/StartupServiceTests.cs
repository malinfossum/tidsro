using Tidsro.Services;
using Xunit;

namespace Tidsro.Tests;

public class StartupServiceTests
{
    [Fact]
    public void Run_value_is_fully_quoted_to_survive_spaces_in_path()
    {
        var v = StartupService.RunValueFor(@"C:\Program Files\Tidsro\Tidsro.exe");
        Assert.Equal("\"C:\\Program Files\\Tidsro\\Tidsro.exe\"", v);
    }
}
