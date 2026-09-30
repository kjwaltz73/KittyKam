using System.Text.Json;
using Azure;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace KittyKam.Api;

// POST /api/photo?visitId=..&seq=..  body: image/jpeg -> blob photos/{visitId}/{seq}.jpg
// POST /api/visit                    body: JSON       -> table Visits (PartitionKey=day, RowKey=visitId)
// GET  /api/visits?days=7                             -> recent visits
public class Functions(BlobServiceClient blobs, TableServiceClient tables)
{
    public record VisitDto(long VisitId, long StartedAt, int DurationS, double WeightBeforeG, double WeightAfterG, int Photos);

    static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString };

    async Task<TableClient> VisitsTable()
    {
        var t = tables.GetTableClient("Visits");
        await t.CreateIfNotExistsAsync();
        return t;
    }

    [Function("photo")]
    public async Task<IActionResult> Photo(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req)
    {
        if (!long.TryParse(req.Query["visitId"], out var visitId) || !int.TryParse(req.Query["seq"], out var seq))
            return new BadRequestObjectResult("visitId and seq required");

        var container = blobs.GetBlobContainerClient("photos");
        await container.CreateIfNotExistsAsync();
        await container.GetBlobClient($"{visitId}/{seq}.jpg").UploadAsync(req.Body, overwrite: true);
        return new NoContentResult();
    }

    [Function("visit")]
    public async Task<IActionResult> Visit(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req)
    {
        var v = await JsonSerializer.DeserializeAsync<VisitDto>(req.Body, Json);
        if (v is null) return new BadRequestObjectResult("invalid body");

        var started = DateTimeOffset.FromUnixTimeSeconds(v.StartedAt);
        var table = await VisitsTable();
        await table.UpsertEntityAsync(new TableEntity(started.ToString("yyyy-MM-dd"), v.VisitId.ToString())
        {
            ["StartedAt"] = started,
            ["DurationS"] = v.DurationS,
            ["WeightBeforeG"] = v.WeightBeforeG,
            ["WeightAfterG"] = v.WeightAfterG,
            ["EatenG"] = Math.Round(v.WeightBeforeG - v.WeightAfterG, 1),
            ["Photos"] = v.Photos,
            ["Cat"] = "", // filled in later by the identifier / manual labeling
        });
        return new NoContentResult();
    }

    [Function("visits")]
    public async Task<IActionResult> Visits(
        [HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequest req)
    {
        var days = int.TryParse(req.Query["days"], out var d) ? d : 7;
        var since = DateTime.UtcNow.AddDays(-days).ToString("yyyy-MM-dd");
        var table = await VisitsTable();

        var rows = new List<object>();
        await foreach (var e in table.QueryAsync<TableEntity>(x => x.PartitionKey.CompareTo(since) >= 0))
            rows.Add(new
            {
                visitId = e.RowKey,
                startedAt = e.GetDateTimeOffset("StartedAt"),
                durationS = e.GetInt32("DurationS"),
                weightBeforeG = e.GetDouble("WeightBeforeG"),
                weightAfterG = e.GetDouble("WeightAfterG"),
                eatenG = e.GetDouble("EatenG"),
                photos = e.GetInt32("Photos"),
                cat = e.GetString("Cat"),
            });
        return new OkObjectResult(rows);
    }
}
