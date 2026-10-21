using Rezultat;

namespace AnalyzerCoreOnly;

public static class CoreOnly
{
    public static void Run()
    {
        Result<int, string>.Ok(1);
        Validation<int, string>.Invalid("issue");
    }
}
