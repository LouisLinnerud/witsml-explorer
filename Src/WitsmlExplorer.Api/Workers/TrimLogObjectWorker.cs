using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Serilog;
using Witsml;
using Witsml.Data;
using Witsml.Data.Curves;
using Witsml.Extensions;
using Witsml.ServiceReference;
using WitsmlExplorer.Api.Extensions;
using WitsmlExplorer.Api.Jobs;
using WitsmlExplorer.Api.Models;
using WitsmlExplorer.Api.Query;
using WitsmlExplorer.Api.Services;

namespace WitsmlExplorer.Api.Workers
{
    public class TrimLogObjectWorker : BaseWorker<TrimLogDataJob>, IWorker
    {
        private readonly IWitsmlClient witsmlClient;
        public JobType JobType => JobType.TrimLogObject;

        public TrimLogObjectWorker(IWitsmlClientProvider witsmlClientProvider)
        {
            witsmlClient = witsmlClientProvider.GetClient();
        }

        public override async Task<(WorkerResult, RefreshAction)> Execute(TrimLogDataJob job)
        {
            var witsmlLogQuery = LogQueries.GetWitsmlLogById(job.LogObject.WellUid, job.LogObject.WellboreUid, job.LogObject.LogUid);
            var witsmlLogs = await witsmlClient.GetFromStoreAsync(witsmlLogQuery, new OptionsIn(ReturnElements.HeaderOnly));
            var witsmlLog = witsmlLogs.Logs.First();

            var currentStartIndex = Index.Start(witsmlLog);
            var newStartIndex = Index.Start(witsmlLog, job.StartIndex);
            var currentEndIndex = Index.End(witsmlLog);
            var newEndIndex = Index.End(witsmlLog, job.EndIndex);

            var trimmedStartOfLog = false;
            if (currentStartIndex < newStartIndex && newStartIndex < currentEndIndex)
            {
                var trimLogObjectStartQuery = CreateRequest(
                    job.LogObject.WellUid,
                    job.LogObject.WellboreUid,
                    job.LogObject.LogUid,
                    witsmlLog.IndexType,
                    deleteTo: newStartIndex);

                var result = await witsmlClient.DeleteFromStoreAsync(trimLogObjectStartQuery);
                if (result.IsSuccessful)
                {
                    trimmedStartOfLog = true;
                }
                else
                {
                    Log.Error("Job failed. An error occurred when trimming log object start: {Job}", job.PrintProperties());
                    return (new WorkerResult(witsmlClient.GetServerHostname(), false, "Failed to update start of log", result.Reason, witsmlLog.GetDescription()), null);
                }
            }

            var trimmedEndOfLog = false;
            if (currentEndIndex > newEndIndex && newEndIndex > currentStartIndex)
            {
                var trimLogObjectEndQuery = CreateRequest(
                    job.LogObject.WellUid,
                    job.LogObject.WellboreUid,
                    job.LogObject.LogUid,
                    witsmlLog.IndexType,
                    deleteFrom: newEndIndex);

                var result = await witsmlClient.DeleteFromStoreAsync(trimLogObjectEndQuery);
                if (result.IsSuccessful)
                {
                    trimmedEndOfLog = true;
                }
                else
                {
                    Log.Error("Job failed. An error occurred when trimming log object end: {Job}", job.PrintProperties());
                    return (new WorkerResult(witsmlClient.GetServerHostname(), false, "Failed to update end of log", result.Reason, witsmlLog.GetDescription()), null);
                }
            }

            var refreshAction = new RefreshLogObject(witsmlClient.GetServerHostname(), job.LogObject.WellUid, job.LogObject.WellboreUid, job.LogObject.LogUid, RefreshType.Update);
            if (trimmedStartOfLog && trimmedEndOfLog)
            {
                return (new WorkerResult(witsmlClient.GetServerHostname(), true, $"Updated start/end of log [{job.LogObject.LogUid}]"), refreshAction);
            }
            if (trimmedStartOfLog)
            {
                return (new WorkerResult(witsmlClient.GetServerHostname(), true, $"Updated start of log [{job.LogObject.LogUid}]"), refreshAction);
            }
            if (trimmedEndOfLog)
            {
                return (new WorkerResult(witsmlClient.GetServerHostname(), true, $"Updated end of log [{job.LogObject.LogUid}]"), refreshAction);
            }

            return (new WorkerResult(witsmlClient.GetServerHostname(), false, $"Failed to update start/end of log [{job.LogObject.LogUid}]", "Invalid index range"), null);
        }

        private static WitsmlLogs CreateRequest(string wellUid, string wellboreUid, string logUid, string indexType, Index deleteTo = null, Index deleteFrom = null)
        {
            var witsmlLog = new WitsmlLog
            {
                UidWell = wellUid,
                UidWellbore = wellboreUid,
                Uid = logUid
            };

            switch (indexType)
            {
                case WitsmlLog.WITSML_INDEX_TYPE_MD:
                    witsmlLog.StartIndex = deleteFrom != null ? new WitsmlIndex((DepthIndex) deleteFrom) : null;
                    witsmlLog.EndIndex = deleteTo != null ? new WitsmlIndex((DepthIndex) deleteTo) : null;
                    break;
                case WitsmlLog.WITSML_INDEX_TYPE_DATE_TIME:
                    witsmlLog.StartDateTimeIndex = deleteFrom?.GetValueAsString();
                    witsmlLog.EndDateTimeIndex = deleteTo?.GetValueAsString();
                    break;
            }

            return new WitsmlLogs
            {
                Logs = new List<WitsmlLog> { witsmlLog }
            };
        }
    }
}
