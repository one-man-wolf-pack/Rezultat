using AnalyzerLibrary;

namespace AnalyzerConsumer;

public static class GeneratedCaller
{
    public static void Run(OrderService orders) => orders.Save(1);
}
