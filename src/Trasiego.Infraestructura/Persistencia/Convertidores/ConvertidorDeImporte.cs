using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Trasiego.Dominio.Valores;

namespace Trasiego.Infraestructura.Persistencia.Convertidores;

public class ConvertidorDeImporte()
    : ValueConverter<Importe, decimal>(importe => importe.Valor, valor => Importe.De(valor));
