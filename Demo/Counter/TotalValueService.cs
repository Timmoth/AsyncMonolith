namespace Demo.Counter;

public class TotalValueService
{
    private static double TotalValue { get; set; }

    public void Set(double totalValue)
    {
        TotalValue = totalValue;
    }

    public double Get()
    {
        return TotalValue;
    }
}