"""KittyKam API (Azure Functions, Python v2 model, consumption plan).

POST /api/photo?visitId=..&seq=..   body: image/jpeg  -> blob photos/{visitId}/{seq}.jpg
POST /api/visit                     body: JSON        -> table Visits (PartitionKey=day, RowKey=visitId)
GET  /api/visits?days=7                               -> recent visits as JSON
"""
import json
import os
from datetime import datetime, timedelta, timezone

import azure.functions as func
from azure.data.tables import TableServiceClient
from azure.storage.blob import BlobServiceClient

app = func.FunctionApp(http_auth_level=func.AuthLevel.FUNCTION)

_conn = os.environ["AzureWebJobsStorage"]
_blobs = BlobServiceClient.from_connection_string(_conn)
_tables = TableServiceClient.from_connection_string(_conn)


def _table():
    return _tables.create_table_if_not_exists("Visits")


@app.route(route="photo", methods=["POST"])
def photo(req: func.HttpRequest) -> func.HttpResponse:
    visit_id, seq = req.params.get("visitId"), req.params.get("seq")
    if not visit_id or not seq or not visit_id.isdigit() or not seq.isdigit():
        return func.HttpResponse("visitId and seq required", status_code=400)
    container = _blobs.get_container_client("photos")
    if not container.exists():
        container.create_container()
    container.upload_blob(f"{visit_id}/{seq}.jpg", req.get_body(), overwrite=True)
    return func.HttpResponse(status_code=204)


@app.route(route="visit", methods=["POST"])
def visit(req: func.HttpRequest) -> func.HttpResponse:
    v = req.get_json()
    started = datetime.fromtimestamp(int(v["startedAt"]), tz=timezone.utc)
    before, after = float(v["weightBeforeG"]), float(v["weightAfterG"])
    _table().upsert_entity({
        "PartitionKey": started.strftime("%Y-%m-%d"),
        "RowKey": str(v["visitId"]),
        "StartedAt": started.isoformat(),
        "DurationS": int(v["durationS"]),
        "WeightBeforeG": before,
        "WeightAfterG": after,
        "EatenG": round(before - after, 1),
        "Photos": int(v["photos"]),
        "Cat": "",  # filled in later by the identifier / manual labeling
    })
    return func.HttpResponse(status_code=204)


@app.route(route="visits", methods=["GET"])
def visits(req: func.HttpRequest) -> func.HttpResponse:
    days = int(req.params.get("days", "7"))
    since = (datetime.now(timezone.utc) - timedelta(days=days)).strftime("%Y-%m-%d")
    rows = [dict(e) for e in _table().query_entities(f"PartitionKey ge '{since}'")]
    return func.HttpResponse(json.dumps(rows, default=str), mimetype="application/json")
