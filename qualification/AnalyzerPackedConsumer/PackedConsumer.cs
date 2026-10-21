using AnalyzerLibrary;

namespace AnalyzerPackedConsumer;

public static class Downstream
{
    public static async Task Run(OrderService orders)
    {
        orders.Save(1);
        await orders.SaveAsync(2);
        /*CS4014*/
        orders.ValidateAsync(3);
    }
}
