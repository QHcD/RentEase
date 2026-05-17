using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using PropertyLeasing.Reporting.Services;
using PropertyLeasing.Reporting.ViewModels;

namespace RentEase.Reporting.Tests;

public sealed class StubHttpMessageHandler : HttpMessageHandler
{
    public Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> Handler { get; set; } =
        (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        => Handler(request, cancellationToken);
}

public class ReportingApiJsonTests
{
    [Fact]
    public void Deserializes_occupancy_camelCase()
    {
        const string json = """[{"propertyName":"Tower A","totalUnits":10,"occupiedUnits":4,"availableUnits":6,"occupancyRate":40.5}]""";
        var list = JsonSerializer.Deserialize<List<OccupancyReportItem>>(json, ReportingJsonDefaults.SerializerOptions)!;
        Assert.Single(list);
        Assert.Equal("Tower A", list[0].PropertyName);
        Assert.Equal(10, list[0].TotalUnits);
        Assert.Equal(4, list[0].OccupiedUnits);
        Assert.Equal(6, list[0].AvailableUnits);
        Assert.Equal(40.5, list[0].OccupancyRate);
    }

    [Fact]
    public void Deserializes_maintenance_camelCase()
    {
        const string json = """{"totalRequests":8,"pendingRequests":2,"inProgressRequests":1,"resolvedRequests":5,"avgResolutionHours":12.25}""";
        var item = JsonSerializer.Deserialize<MaintenanceReportItem>(json, ReportingJsonDefaults.SerializerOptions)!;
        Assert.Equal(8, item.TotalRequests);
        Assert.Equal(5, item.ResolvedRequests);
        Assert.Equal(12.25, item.AvgResolutionHours);
    }

    [Fact]
    public void Deserializes_payment_camelCase()
    {
        const string json = """{"totalDue":1000,"totalPaid":750,"totalOverdue":100,"overdueCount":2}""";
        var item = JsonSerializer.Deserialize<PaymentReportItem>(json, ReportingJsonDefaults.SerializerOptions)!;
        Assert.Equal(1000m, item.TotalDue);
        Assert.Equal(750m, item.TotalPaid);
        Assert.Equal(2, item.OverdueCount);
    }

    [Fact]
    public void Deserializes_application_camelCase()
    {
        const string json = """[{"applicationId":1,"tenantName":"Jane","unitNumber":"S101","propertyName":"Seef","status":"Pending","createdAt":"2026-05-01T10:00:00Z"}]""";
        var list = JsonSerializer.Deserialize<List<ApplicationReportItem>>(json, ReportingJsonDefaults.SerializerOptions)!;
        Assert.Single(list);
        Assert.Equal("Jane", list[0].TenantName);
        Assert.Equal("Pending", list[0].Status);
    }

    [Fact]
    public void Deserializes_lease_camelCase()
    {
        const string json = """[{"leaseId":9,"applicationId":3,"tenantName":"Bob","unitNumber":"A1","propertyName":"X","leaseStartDate":"2026-01-01T00:00:00Z","leaseEndDate":"2027-01-01T00:00:00Z","monthlyRent":220,"status":"Active","createdAt":"2025-12-01T00:00:00Z"}]""";
        var list = JsonSerializer.Deserialize<List<LeaseReportItem>>(json, ReportingJsonDefaults.SerializerOptions)!;
        Assert.Single(list);
        Assert.Equal(220m, list[0].MonthlyRent);
        Assert.Equal("Active", list[0].Status);
    }
}

public class ApiClientTests
{
    [Fact]
    public async Task GetOccupancyReportAsync_returns_rows_when_api_returns_200()
    {
        var stub = new StubHttpMessageHandler();
        stub.Handler = static (req, _) =>
        {
            Assert.Equal("/api/reports/occupancy", req.RequestUri!.AbsolutePath);
            Assert.NotNull(req.Headers.Authorization);
            Assert.Equal("Bearer", req.Headers.Authorization!.Scheme);
            var json = """[{"propertyName":"P","totalUnits":3,"occupiedUnits":1,"availableUnits":2,"occupancyRate":33.33}]""";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            });
        };

        var http = new HttpClient(stub) { BaseAddress = new Uri("https://localhost/") };
        var api = new ApiClient(http, NullLogger<ApiClient>.Instance);
        api.SetToken("test-token");

        var rows = await api.GetOccupancyReportAsync();
        Assert.Single(rows);
        Assert.Equal("P", rows[0].PropertyName);
        Assert.Equal(3, rows[0].TotalUnits);
    }

    [Fact]
    public async Task GetOccupancyReportAsync_throws_ReportApiUnauthorized_on_401()
    {
        var stub = new StubHttpMessageHandler();
        stub.Handler = static (_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized));

        var http = new HttpClient(stub) { BaseAddress = new Uri("https://localhost/") };
        var api = new ApiClient(http, NullLogger<ApiClient>.Instance);
        api.SetToken("expired");

        await Assert.ThrowsAsync<ReportApiUnauthorizedException>(() => api.GetOccupancyReportAsync());
    }

    [Fact]
    public async Task GetPaymentReportAsync_throws_on_403()
    {
        var stub = new StubHttpMessageHandler();
        stub.Handler = static (_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden));

        var http = new HttpClient(stub) { BaseAddress = new Uri("https://localhost/") };
        var api = new ApiClient(http, NullLogger<ApiClient>.Instance);
        api.SetToken("x");

        await Assert.ThrowsAsync<ReportApiUnauthorizedException>(() => api.GetPaymentReportAsync());
    }
}
