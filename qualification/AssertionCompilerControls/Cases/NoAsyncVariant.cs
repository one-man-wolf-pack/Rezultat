using System.Threading.Tasks;
using Rezultat;
using Rezultat.Testing.Xunit;

internal static class Case
{
    internal static Task<int> Observe(Task<Result<int, string>> pending) => pending.ShouldBeOkAsync();
}
