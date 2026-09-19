using System;
using BusTicketSaleSystem.Services;
using Microsoft.Data.SqlClient;

namespace BusTicketSaleSystem.ViewModels;

/// <summary>
/// Shared failure reporting for the admin screens: logs the exception and returns the
/// message the ViewModel shows to the operator.
/// </summary>
internal static class AdminFailure
{
    public static string Describe(Exception exception, string action)
    {
        LoggerService.LogError($"Admin console: {action} thất bại.", exception);
        return exception switch
        {
            UnauthorizedAccessException => "Phiên làm việc đã hết hạn quyền quản trị viên. Vui lòng đăng nhập lại.",
            SqlException => $"{action} không thành công do cơ sở dữ liệu từ chối yêu cầu.",
            _ => $"{action} thất bại: {exception.Message}"
        };
    }
}
