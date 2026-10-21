using AnalyzerLibrary;
using Rezultat;

namespace System.Threading.Tasks
{
    public sealed class Task<T>
    {
    }

    public readonly struct ValueTask<T>
    {
    }
}

namespace AnalyzerWrapperHomonyms
{
    public static class Caller
    {
        private static System.Threading.Tasks.Task<Result<int, string>> FakeSave() => new(); // CS0436

        private static System.Threading.Tasks.ValueTask<Validation<int, string>> FakeValidate() => default; // CS0436

        public static void Run(OrderService orders)
        {
            FakeSave();
            FakeValidate();
            /*RZT0001*/
            orders.SaveAsync(1);
            /*RZT0001*/
            orders.ValidateAsync(2);
        }
    }
}
