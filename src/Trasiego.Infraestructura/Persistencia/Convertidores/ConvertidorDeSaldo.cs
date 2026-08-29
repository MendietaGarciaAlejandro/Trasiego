using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Trasiego.Dominio.Valores;

namespace Trasiego.Infraestructura.Persistencia.Convertidores;

public class ConvertidorDeSaldo()
    : ValueConverter<Saldo, decimal>(saldo => saldo.Valor, valor => Saldo.De(valor));
