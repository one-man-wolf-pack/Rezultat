using AnalyzerLibrary;
using Rezultat;

namespace AnalyzerConsumer;

public static class OrderServiceExtensions
{
    extension(OrderService orders)
    {
        public Result<int, string> Resave(int order) => orders.Save(order);
    }
}

public static class ModernCaller
{
    public static void Run(OrderService orders) => /*RZT0001*/orders.Resave(1);
}
