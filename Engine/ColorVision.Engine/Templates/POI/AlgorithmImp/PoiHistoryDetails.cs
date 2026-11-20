using ColorVision.Database;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Results;
using log4net;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.POI.AlgorithmImp
{
    internal static class PoiHistoryDetails
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(PoiHistoryDetails));

        internal static IReadOnlyList<PoiPointResultModel>? Read(int resultId)
        {
            return PoiPointResultDao.Instance.TryGetAllByPid(resultId, out var rows) ? rows : null;
        }

        internal static bool TryLoad(ViewResultAlg result, Func<int, IReadOnlyList<PoiPointResultModel>?> read,
            Func<IReadOnlyList<PoiPointResultModel>, IEnumerable<IViewResult>>? project = null)
        {
            if (result.ViewResults != null) return true;

            try
            {
                IReadOnlyList<PoiPointResultModel>? rows;
                if (result.AlgResultMasterModel?.DatabaseEndpoint is { } endpoint)
                {
                    using var db = endpoint.CreateDbClient();
                    rows = db.Queryable<PoiPointResultModel>().Where(item => item.Pid == result.Id).ToList();
                }
                else
                {
                    rows = read(result.Id);
                }
                if (rows == null)
                {
                    log.Warn($"Historical point details could not be queried for result {result.Id} ({result.ResultType}).");
                    return false;
                }

                result.ViewResults = new ObservableCollection<IViewResult>(project != null ? project(rows) : rows);
                return true;
            }
            catch (Exception ex)
            {
                log.Warn($"Historical point details could not be loaded for result {result.Id} ({result.ResultType}).", ex);
                return false;
            }
        }

        internal static ObservableCollection<IViewResult>? GetForDisplay(ViewResultContext context, ViewResultAlg result)
        {
            if (result.ViewResults != null) return result.ViewResults;

            context.ListView.ItemsSource = null;
            context.LeftGridViewColumnVisibilitys.Clear();
            if (context.ListView.View is GridView gridView)
                gridView.Columns.Clear();
            context.SideTextBox.Text = UnavailableMessage(result.Id);
            context.SideTextBox.Visibility = Visibility.Visible;
            return null;
        }

        internal static ObservableCollection<IViewResult> RequireForExport(ViewResultAlg result)
        {
            return result.ViewResults ?? throw new InvalidOperationException(UnavailableMessage(result.Id));
        }

        private static string UnavailableMessage(int resultId)
        {
            return string.Format(CultureInfo.CurrentCulture, EngineLocalization.Get("PoiHistoryDetailsUnavailable"), resultId);
        }
    }
}
