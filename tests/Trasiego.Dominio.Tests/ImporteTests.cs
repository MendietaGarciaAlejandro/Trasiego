using Trasiego.Dominio.Valores;

namespace Trasiego.Dominio.Tests;

public class ImporteTests
{
    [Fact]
    public void Se_guarda_con_cuatro_decimales()
    {
        Assert.Equal(3.3333m, Importe.De(3.33333333m).Valor);
    }

    [Fact]
    public void El_redondeo_es_comercial_y_no_bancario()
    {
        // Math.Round por defecto redondea al par mas cercano y daria 0,12.
        Assert.Equal(0.13m, Importe.De(0.125m).Visible);
        Assert.Equal(0.14m, Importe.De(0.135m).Visible);
    }

    [Fact]
    public void La_proporcion_de_una_parte_sale_redondeada()
    {
        Assert.Equal(3.3333m, Importe.De(10m).Proporcion(Cantidad.De(1), Cantidad.De(3)).Valor);
    }

    [Fact]
    public void Sacar_de_una_capa_unidad_a_unidad_no_pierde_ni_un_decimo()
    {
        // Tres unidades que costaron 10,00 €. Se sacan de una en una, y el valor que sale
        // tiene que sumar exactamente lo que costaron, ni un centimo mas ni uno menos.
        var quedanUnidades = Cantidad.De(3);
        var quedaValor = Importe.De(10m);
        var haSalido = Importe.Cero;

        while (!quedanUnidades.EsCero)
        {
            var una = Cantidad.De(1);
            var parte = quedaValor.Proporcion(una, quedanUnidades);

            haSalido += parte;
            quedaValor -= parte;        // el resto se resta, nunca se vuelve a calcular
            quedanUnidades -= una;
        }

        Assert.Equal(Importe.De(10m), haSalido);
        Assert.Equal(Importe.Cero, quedaValor);
    }

    [Fact]
    public void Repartir_en_partes_iguales_y_sumarlas_no_devuelve_el_total()
    {
        // Este test no prueba el codigo, prueba por que el codigo pide restar el resto:
        // tres tercios calculados por separado suman 9,9999.
        var total = Importe.De(10m);
        var unTercio = total.Proporcion(Cantidad.De(1), Cantidad.De(3));

        Assert.Equal(9.9999m, (unTercio + unTercio + unTercio).Valor);
        Assert.NotEqual(total, unTercio + unTercio + unTercio);
    }

    [Fact]
    public void No_se_reparte_sobre_una_cantidad_cero()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Importe.De(10m).Proporcion(Cantidad.De(1), Cantidad.Cero));
    }

    [Fact]
    public void La_parte_no_puede_ser_mayor_que_el_total()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Importe.De(10m).Proporcion(Cantidad.De(4), Cantidad.De(3)));
    }

    [Fact]
    public void El_coste_unitario_no_se_redondea()
    {
        var unitario = Importe.De(10m).PorUnidad(Cantidad.De(3));

        Assert.True(unitario > 3.3333m);
        Assert.True(unitario < 3.3334m);
    }

    [Fact]
    public void Un_importe_puede_ser_negativo()
    {
        Assert.Equal(-4m, (Importe.De(6m) - Importe.De(10m)).Valor);
    }
}
