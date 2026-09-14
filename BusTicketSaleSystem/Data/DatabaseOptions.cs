using BusTicketSaleSystem.Services;

namespace BusTicketSaleSystem.Data;

public static class DatabaseOptions
{
    public static string ConnectionString => AppConfig.ConnectionString;
    public static string MasterConnectionString => AppConfig.MasterConnectionString;
}
