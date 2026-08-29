using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Trasiego.Dominio.Valores;

namespace Trasiego.Infraestructura.Persistencia.Convertidores;

public class ConvertidorDeCantidad()
    : ValueConverter<Cantidad, decimal>(cantidad => cantidad.Valor, valor => Cantidad.De(valor));
