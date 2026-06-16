using Tidsro.Services;
using Xunit;

namespace Tidsro.Tests;

public class StartupServiceTests
{
    [Fact]
    public void Run_value_quotes_the_path_and_passes_the_startup_flag()
    {
        var v = StartupService.RunValueFor(@"C:\Program Files\Tidsro\Tidsro.exe");
        Assert.Equal("\"C:\\Program Files\\Tidsro\\Tidsro.exe\" --startup", v);
    }
}
