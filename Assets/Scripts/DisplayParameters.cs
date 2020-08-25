struct Ratio
{
    public readonly int Numerator;
    public readonly int Denominator;

    public Ratio(int numerator, int denominator)
    {
        Numerator = numerator;
        Denominator = denominator;
    }
}

struct DisplayParameters
{
    public readonly int Width;
    public readonly int Height;
    public readonly Ratio RefreshRate;

    public DisplayParameters(int width, int height, Ratio refreshRate)
    {
        Width = width;
        Height = height;
        RefreshRate = refreshRate;
    }

    public override bool Equals(object obj)
    {
        if (obj is DisplayParameters other)
        {
            return this == other;
        }

        return false;
    }

    public override int GetHashCode()
    {
        return Width.GetHashCode() * 13 + Height.GetHashCode() * 17 + RefreshRate.Numerator.GetHashCode() * 19 + RefreshRate.Denominator.GetHashCode() * 23;
    }

    public static bool operator==(DisplayParameters left, DisplayParameters right)
    {
        return left.Width == right.Width && left.Height == right.Height &&
            left.RefreshRate.Numerator == right.RefreshRate.Numerator && left.RefreshRate.Denominator == right.RefreshRate.Denominator;
    }

    public static bool operator!=(DisplayParameters left, DisplayParameters right)
    {
        return !(left == right);
    }
}