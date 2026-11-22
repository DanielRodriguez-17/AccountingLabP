namespace AccountingLab.Domain.Model;

/// <summary>Reglas de precisión monetaria compartidas por los patrones: DECIMAL con hasta cuatro decimales.</summary>
public static class MoneyPrecision
{
    public const int MaxDecimals = 4;

    public static bool HasValidScale(decimal value) => ((decimal.GetBits(value)[3] >> 16) & 0xff) <= MaxDecimals;

    /// <summary>Lanza <see cref="ArgumentException"/> si el importe excede la precisión o el rango permitido.</summary>
    public static void Require(decimal value, string paramName, bool allowNegative = false)
    {
        if (!HasValidScale(value))
            throw new ArgumentException("El importe admite como máximo cuatro decimales.", paramName);
        if (!allowNegative && value < 0)
            throw new ArgumentException("El importe no puede ser negativo.", paramName);
        if (Math.Abs(value) > MoneyRules.Maximum)
            throw new ArgumentException("El importe excede el máximo admitido por DECIMAL(19,4).", paramName);
    }

    /// <summary>Redondeo monetario: cuatro posiciones y punto medio alejado de cero.</summary>
    public static decimal Round(decimal value) => Math.Round(value, MaxDecimals, MidpointRounding.AwayFromZero);
}
